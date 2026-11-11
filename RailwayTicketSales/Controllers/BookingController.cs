using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RailwayTicketSales.Data;
using RailwayTicketSales.Models.Entities;
using RailwayTicketSales.Models.Enums;
using RailwayTicketSales.Models.ViewModels;
using RailwayTicketSales.Services.Interfaces;

namespace RailwayTicketSales.Controllers
{
    public class BookingController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly ITrainService _trainService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BookingController(
            IBookingService bookingService,
            ITrainService trainService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _bookingService = bookingService;
            _trainService = trainService;
            _context = context;
            _userManager = userManager;
        }

        // Step 1: Select Seat
        [HttpGet]
        public async Task<IActionResult> SelectSeat(int scheduleId)
        {
            var details = await _trainService.GetScheduleDetailsAsync(scheduleId);
            if (details == null)
                return NotFound("Schedule not found.");

            return View(details);
        }

        // Step 2: Passenger Info Form
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> PassengerInfo(int scheduleId, string seatIds)
        {
            if (string.IsNullOrEmpty(seatIds))
            {
                TempData["ErrorMessage"] = "Please select at least one seat before proceeding.";
                return RedirectToAction("SelectSeat", new { scheduleId });
            }

            var schedule = await _context.Schedules
                .Include(s => s.Train)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.DepartureStation)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.ArrivalStation)
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);

            if (schedule == null) return NotFound("Schedule not found.");

            var idList = seatIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(int.Parse)
                                .ToList();

            var seats = await _context.Seats
                .Include(s => s.Coach)
                .Where(s => idList.Contains(s.SeatId))
                .ToListAsync();

            // Validate that all requested seats are still available
            var unavailable = await _bookingService.GetUnavailableSeatIdsAsync(scheduleId);
            foreach (var seat in seats)
            {
                if (unavailable.Contains(seat.SeatId))
                {
                    TempData["ErrorMessage"] = $"Seat {seat.SeatNumber} was just taken by another passenger. Please select another seat.";
                    return RedirectToAction("SelectSeat", new { scheduleId });
                }
            }

            var user = await _userManager.GetUserAsync(User);

            var model = new CreateBookingRequest
            {
                ScheduleId = scheduleId,
                CustomerName = user?.FullName ?? string.Empty,
                CustomerEmail = user?.Email ?? string.Empty,
                CustomerPhone = user?.PhoneNumber ?? string.Empty,
                CustomerIdCard = user?.IdCard ?? string.Empty,
                Passengers = new List<PassengerItemRequest>()
            };

            foreach (var seat in seats)
            {
                var fare = await _trainService.CalculateFareAsync(schedule.RouteId, seat.CoachId, seat.SeatType, schedule.BasePrice);
                model.Passengers.Add(new PassengerItemRequest
                {
                    SeatId = seat.SeatId,
                    SeatNumber = seat.SeatNumber,
                    CoachNumber = seat.Coach?.CoachNumber.ToString() ?? "1",
                    Price = fare,
                    PassengerType = "Adult",
                    PassengerName = (model.Passengers.Count == 0 && user != null) ? user.FullName : string.Empty,
                    PassengerIdCard = (model.Passengers.Count == 0 && user != null) ? (user.IdCard ?? string.Empty) : string.Empty,
                    PassengerPhone = (model.Passengers.Count == 0 && user != null) ? (user.PhoneNumber ?? string.Empty) : string.Empty
                });
            }

            ViewBag.Schedule = schedule;
            return View(model);
        }

        // Step 3: Create Temporary Booking with 10-minute hold
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateBookingRequest model)
        {
            if (!ModelState.IsValid)
            {
                var schedule = await _context.Schedules
                    .Include(s => s.Train)
                    .Include(s => s.Route)
                        .ThenInclude(r => r!.DepartureStation)
                    .Include(s => s.Route)
                        .ThenInclude(r => r!.ArrivalStation)
                    .FirstOrDefaultAsync(s => s.ScheduleId == model.ScheduleId);
                ViewBag.Schedule = schedule;
                return View("PassengerInfo", model);
            }

            var user = await _userManager.GetUserAsync(User);

            try
            {
                var booking = await _bookingService.CreateTemporaryBookingAsync(
                    model,
                    user?.Id,
                    isCounterBooking: false,
                    clerkId: null);

                if (booking == null)
                {
                    TempData["ErrorMessage"] = "Unable to reserve seat. Please try again.";
                    return RedirectToAction("SelectSeat", new { scheduleId = model.ScheduleId });
                }

                TempData["SuccessMessage"] = "Your seat has been temporarily held for 10 minutes. Please complete payment before the countdown expires.";
                return RedirectToAction("Payment", new { id = booking.BookingOrderId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("SelectSeat", new { scheduleId = model.ScheduleId });
            }
        }

        // Step 4: Payment Page
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Payment(int id)
        {
            var booking = await _bookingService.GetBookingByIdAsync(id);
            if (booking == null) return NotFound("Booking not found.");

            // Ownership check (unless Clerk/Admin)
            var user = await _userManager.GetUserAsync(User);
            var isStaff = User.IsInRole("Administrator") || User.IsInRole("TicketClerk");
            if (!isStaff && booking.UserId != user?.Id)
            {
                return Forbid();
            }

            // Check server-side 10-minute hold
            var isValidHold = await _bookingService.ValidateAndExpireHoldAsync(id);
            if (!isValidHold && booking.OrderStatus != OrderStatus.Completed)
            {
                TempData["ErrorMessage"] = "Your 10-minute reservation has expired. The reserved seat has been released (BR-04).";
                return RedirectToAction("Index", "Train");
            }

            var schedule = booking.BookingDetails.FirstOrDefault()?.Schedule;
            if (schedule == null) return NotFound("Associated schedule not found.");

            var model = new PaymentViewModel
            {
                BookingOrderId = booking.BookingOrderId,
                BookingCode = booking.BookingCode,
                CustomerName = booking.CustomerName,
                CustomerEmail = booking.CustomerEmail,
                CustomerPhone = booking.CustomerPhone,
                TotalAmount = booking.TotalAmount,
                HoldExpiresAt = booking.HoldExpiresAt,
                Details = booking.BookingDetails.ToList(),
                Schedule = schedule,
                PaymentMethod = PaymentMethod.OnlineBanking,
                SimulatedOutcome = "Success"
            };

            return View(model);
        }

        // Step 5: Process Payment
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int bookingOrderId, PaymentMethod paymentMethod, string simulatedOutcome = "Success")
        {
            var booking = await _bookingService.GetBookingByIdAsync(bookingOrderId);
            if (booking == null) return NotFound("Booking not found.");

            var user = await _userManager.GetUserAsync(User);
            var isStaff = User.IsInRole("Administrator") || User.IsInRole("TicketClerk");
            if (!isStaff && booking.UserId != user?.Id)
            {
                return Forbid();
            }

            var result = await _bookingService.ConfirmPaymentAndIssueTicketsAsync(bookingOrderId, paymentMethod, simulatedOutcome);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("Confirmation", new { id = bookingOrderId });
            }
            else
            {
                if (result.Status == PaymentStatus.Timeout)
                {
                    TempData["ErrorMessage"] = result.Message;
                    return RedirectToAction("Index", "Train");
                }

                // If rejected, allow retry while hold is still valid
                TempData["ErrorMessage"] = $"Payment Failed: {result.Message}. You may retry with another method.";
                return RedirectToAction("Payment", new { id = bookingOrderId });
            }
        }

        // Step 6: Confirmation View
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Confirmation(int id)
        {
            var booking = await _bookingService.GetBookingByIdAsync(id);
            if (booking == null) return NotFound("Booking not found.");

            var user = await _userManager.GetUserAsync(User);
            var isStaff = User.IsInRole("Administrator") || User.IsInRole("TicketClerk");
            if (!isStaff && booking.UserId != user?.Id)
            {
                return Forbid();
            }

            return View(booking);
        }
    }
}
