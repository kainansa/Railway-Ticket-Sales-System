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
    [Authorize(Roles = "TicketClerk,Administrator")]
    public class ClerkController : Controller
    {
        private readonly IBookingService _bookingService;
        private readonly ITicketService _ticketService;
        private readonly ITrainService _trainService;
        private readonly IRefundService _refundService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClerkController(
            IBookingService bookingService,
            ITicketService ticketService,
            ITrainService trainService,
            IRefundService refundService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _bookingService = bookingService;
            _ticketService = ticketService;
            _trainService = trainService;
            _refundService = refundService;
            _context = context;
            _userManager = userManager;
        }

        // Clerk Dashboard
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            ViewBag.TodayBookings = await _context.BookingOrders
                .Where(b => b.CreatedDateTime >= today)
                .CountAsync();

            ViewBag.TodayTicketsIssued = await _context.Tickets
                .Where(t => t.IssuedDate >= today && t.TicketStatus == TicketStatus.ActivePaid)
                .CountAsync();

            ViewBag.TodayCounterRevenue = await _context.BookingOrders
                .Where(b => b.CreatedDateTime >= today && b.OrderStatus == OrderStatus.Completed && b.IsCounterBooking)
                .SumAsync(b => b.TotalAmount);

            ViewBag.PendingRefunds = await _context.Refunds
                .Where(r => r.RefundStatus == RefundStatus.Pending)
                .CountAsync();

            var recentOrders = await _context.BookingOrders
                .Include(b => b.BookingDetails)
                .Include(b => b.Tickets)
                .OrderByDescending(b => b.CreatedDateTime)
                .Take(10)
                .ToListAsync();

            return View(recentOrders);
        }

        // Search Trains at Counter
        public async Task<IActionResult> SearchTrains(int? departureStationId, int? arrivalStationId, DateTime? travelDate)
        {
            var stations = await _trainService.GetAllStationsAsync();
            var model = new TrainSearchViewModel
            {
                Stations = stations,
                TravelDate = travelDate ?? DateTime.Today
            };

            if (departureStationId.HasValue && arrivalStationId.HasValue)
            {
                model.DepartureStationId = departureStationId.Value;
                model.ArrivalStationId = arrivalStationId.Value;
                model.SearchResults = await _trainService.SearchSchedulesAsync(
                    departureStationId.Value,
                    arrivalStationId.Value,
                    model.TravelDate);
                model.HasSearched = true;
            }

            return View(model);
        }

        // Counter Booking: Step 1 - Select Seats
        [HttpGet]
        public async Task<IActionResult> CounterBooking(int scheduleId)
        {
            var details = await _trainService.GetScheduleDetailsAsync(scheduleId);
            if (details == null) return NotFound("Schedule not found.");

            return View(details);
        }

        // Counter Booking: Step 2 - Passenger Info
        [HttpGet]
        public async Task<IActionResult> CounterPassengerInfo(int scheduleId, string seatIds)
        {
            if (string.IsNullOrEmpty(seatIds))
            {
                TempData["ErrorMessage"] = "Please select at least one seat.";
                return RedirectToAction("CounterBooking", new { scheduleId });
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

            var model = new CreateBookingRequest
            {
                ScheduleId = scheduleId,
                CustomerName = "Counter Customer",
                CustomerEmail = "customer@station.railway.vn",
                CustomerPhone = "0900000000",
                CustomerIdCard = "000000000000",
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
                    PassengerType = "Adult"
                });
            }

            ViewBag.Schedule = schedule;
            return View(model);
        }

        // Counter Booking: Step 3 - Create Counter Order
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCounterOrder(CreateBookingRequest model)
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
                return View("CounterPassengerInfo", model);
            }

            var clerk = await _userManager.GetUserAsync(User);

            try
            {
                var booking = await _bookingService.CreateTemporaryBookingAsync(
                    model,
                    userId: null,
                    isCounterBooking: true,
                    clerkId: clerk?.Id);

                if (booking == null)
                {
                    TempData["ErrorMessage"] = "Unable to create booking at this moment.";
                    return RedirectToAction("CounterBooking", new { scheduleId = model.ScheduleId });
                }

                return RedirectToAction("ConfirmCounterPayment", new { bookingOrderId = booking.BookingOrderId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("CounterBooking", new { scheduleId = model.ScheduleId });
            }
        }

        // Counter Payment Confirmation View
        [HttpGet]
        public async Task<IActionResult> ConfirmCounterPayment(int bookingOrderId)
        {
            var booking = await _bookingService.GetBookingByIdAsync(bookingOrderId);
            if (booking == null) return NotFound("Booking not found.");

            return View(booking);
        }

        // Counter Payment Confirmation Action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessCounterPayment(int bookingOrderId, PaymentMethod paymentMethod = PaymentMethod.CashCounter)
        {
            var result = await _bookingService.ConfirmPaymentAndIssueTicketsAsync(bookingOrderId, paymentMethod, "Success");

            if (result.Success)
            {
                TempData["SuccessMessage"] = "Counter payment confirmed and tickets issued successfully!";
                return RedirectToAction("PrintCounterTickets", new { bookingOrderId });
            }

            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction("ConfirmCounterPayment", new { bookingOrderId });
        }

        // Print Counter Tickets
        [HttpGet]
        public async Task<IActionResult> PrintCounterTickets(int bookingOrderId)
        {
            var booking = await _bookingService.GetBookingByIdAsync(bookingOrderId);
            if (booking == null) return NotFound("Booking not found.");

            // Generate QR codes for all tickets
            var qrImages = new Dictionary<int, string>();
            foreach (var ticket in booking.Tickets)
            {
                var qrData = !string.IsNullOrEmpty(ticket.QrCodeData) ? ticket.QrCodeData : ticket.TicketCode;
                qrImages[ticket.TicketId] = await _ticketService.GenerateQrCodeBase64Async(qrData);
            }
            ViewBag.QrImages = qrImages;

            return View(booking);
        }

        // Search and Manage Bookings
        public async Task<IActionResult> ManageBookings(string? searchKeyword)
        {
            var query = _context.BookingOrders
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Schedule)
                        .ThenInclude(s => s!.Train)
                .Include(b => b.Tickets)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var term = searchKeyword.Trim().ToLower();
                query = query.Where(b => b.BookingCode.ToLower().Contains(term)
                                      || b.CustomerName.ToLower().Contains(term)
                                      || b.CustomerPhone.Contains(term)
                                      || (b.CustomerIdCard != null && b.CustomerIdCard.Contains(term)));
            }

            var bookings = await query.OrderByDescending(b => b.CreatedDateTime).Take(50).ToListAsync();
            ViewBag.SearchKeyword = searchKeyword;
            return View(bookings);
        }

        // Search and Manage Tickets
        public async Task<IActionResult> ManageTickets(string? searchKeyword)
        {
            var query = _context.Tickets
                .Include(t => t.Schedule)
                    .ThenInclude(s => s!.Train)
                .Include(t => t.Schedule)
                    .ThenInclude(s => s!.Route)
                        .ThenInclude(r => r!.DepartureStation)
                .Include(t => t.Schedule)
                    .ThenInclude(s => s!.Route)
                        .ThenInclude(r => r!.ArrivalStation)
                .Include(t => t.Seat)
                    .ThenInclude(st => st!.Coach)
                .Include(t => t.BookingOrder)
                .Include(t => t.Refunds)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var term = searchKeyword.Trim().ToLower();
                query = query.Where(t => t.TicketCode.ToLower().Contains(term)
                                      || t.PassengerName.ToLower().Contains(term)
                                      || t.PassengerIdCard.Contains(term));
            }

            var tickets = await query.OrderByDescending(t => t.IssuedDate).Take(50).ToListAsync();
            ViewBag.SearchKeyword = searchKeyword;
            return View(tickets);
        }
    }
}
