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
    [Authorize]
    public class TicketController : Controller
    {
        private readonly ITicketService _ticketService;
        private readonly ITrainService _trainService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TicketController(
            ITicketService ticketService,
            ITrainService trainService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _ticketService = ticketService;
            _trainService = trainService;
            _context = context;
            _userManager = userManager;
        }

        // My Bookings & Tickets
        public async Task<IActionResult> MyBookings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var bookings = await _context.BookingOrders
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Schedule)
                        .ThenInclude(s => s!.Train)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Schedule)
                        .ThenInclude(s => s!.Route)
                            .ThenInclude(r => r!.DepartureStation)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Schedule)
                        .ThenInclude(s => s!.Route)
                            .ThenInclude(r => r!.ArrivalStation)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Seat)
                .Include(b => b.Tickets)
                .Where(b => b.UserId == user.Id)
                .OrderByDescending(b => b.CreatedDateTime)
                .ToListAsync();

            return View(bookings);
        }

        // View Electronic Ticket Details with QR Code
        public async Task<IActionResult> Details(int id)
        {
            var ticket = await _ticketService.GetTicketByIdAsync(id);
            if (ticket == null) return NotFound("Ticket not found.");

            // Ownership check
            var user = await _userManager.GetUserAsync(User);
            var isStaff = User.IsInRole("Administrator") || User.IsInRole("TicketClerk");
            if (!isStaff && ticket.BookingOrder?.UserId != user?.Id)
            {
                return Forbid();
            }

            // Generate QR Code image as base64 data URL
            var qrData = !string.IsNullOrEmpty(ticket.QrCodeData) ? ticket.QrCodeData : ticket.TicketCode;
            ViewBag.QrCodeImage = await _ticketService.GenerateQrCodeBase64Async(qrData);

            return View(ticket);
        }

        // Change Ticket: Step 1 - Select new schedule and seat
        [HttpGet]
        public async Task<IActionResult> Change(int id)
        {
            var ticket = await _ticketService.GetTicketByIdAsync(id);
            if (ticket == null) return NotFound("Ticket not found.");

            var user = await _userManager.GetUserAsync(User);
            var isStaff = User.IsInRole("Administrator") || User.IsInRole("TicketClerk");
            if (!isStaff && ticket.BookingOrder?.UserId != user?.Id)
            {
                return Forbid();
            }

            if (!await _ticketService.CanChangeTicketAsync(id))
            {
                TempData["ErrorMessage"] = "This ticket cannot be changed. Tickets can only be changed at least 4 hours before departure (BR-07).";
                return RedirectToAction("MyBookings");
            }

            // Available alternative schedules on the same route or other routes
            var departureStationId = ticket.Schedule?.Route?.DepartureStationId ?? 0;
            var arrivalStationId = ticket.Schedule?.Route?.ArrivalStationId ?? 0;

            var alternativeSchedules = await _context.Schedules
                .Include(s => s.Train)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.DepartureStation)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.ArrivalStation)
                .Where(s => s.Route!.DepartureStationId == departureStationId
                            && s.Route!.ArrivalStationId == arrivalStationId
                            && s.DepartureTime > DateTime.Now.AddHours(4)
                            && s.ScheduleId != ticket.ScheduleId)
                .OrderBy(s => s.DepartureTime)
                .ToListAsync();

            ViewBag.Ticket = ticket;
            ViewBag.AlternativeSchedules = alternativeSchedules;
            return View();
        }

        // Change Ticket: Step 2 - Review calculation and confirm change
        [HttpGet]
        public async Task<IActionResult> ReviewChange(int ticketId, int newScheduleId, int newSeatId)
        {
            var ticket = await _ticketService.GetTicketByIdAsync(ticketId);
            if (ticket == null) return NotFound();

            var calc = await _ticketService.CalculateChangeFeeAsync(ticketId, newScheduleId, newSeatId);
            if (!calc.IsEligible)
            {
                TempData["ErrorMessage"] = calc.Message;
                return RedirectToAction("Change", new { id = ticketId });
            }

            var newSchedule = await _context.Schedules
                .Include(s => s.Train)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.DepartureStation)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.ArrivalStation)
                .FirstOrDefaultAsync(s => s.ScheduleId == newScheduleId);

            var newSeat = await _context.Seats
                .Include(s => s.Coach)
                .FirstOrDefaultAsync(s => s.SeatId == newSeatId);

            ViewBag.Ticket = ticket;
            ViewBag.NewSchedule = newSchedule;
            ViewBag.NewSeat = newSeat;
            ViewBag.Calculation = calc;

            return View();
        }

        // Change Ticket: Step 3 - Submit change
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmChange(
            int ticketId,
            int newScheduleId,
            int newSeatId,
            PaymentMethod paymentMethod = PaymentMethod.OnlineBanking,
            string simulatedOutcome = "Success")
        {
            var result = await _ticketService.ProcessTicketChangeAsync(ticketId, newScheduleId, newSeatId, paymentMethod, simulatedOutcome);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("Details", new { id = result.NewTicketId });
            }

            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction("Change", new { id = ticketId });
        }

        // Cancel Ticket: Step 1 - Review cancellation details and penalty calculation
        [HttpGet]
        public async Task<IActionResult> Cancel(int id)
        {
            var ticket = await _ticketService.GetTicketByIdAsync(id);
            if (ticket == null) return NotFound("Ticket not found.");

            var user = await _userManager.GetUserAsync(User);
            var isStaff = User.IsInRole("Administrator") || User.IsInRole("TicketClerk");
            if (!isStaff && ticket.BookingOrder?.UserId != user?.Id)
            {
                return Forbid();
            }

            var calc = await _ticketService.CalculateCancellationAsync(id);
            ViewBag.Ticket = ticket;
            return View(calc);
        }

        // Cancel Ticket: Step 2 - Confirm cancellation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmCancel(int ticketId)
        {
            var ticket = await _ticketService.GetTicketByIdAsync(ticketId);
            if (ticket == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var isStaff = User.IsInRole("Administrator") || User.IsInRole("TicketClerk");
            if (!isStaff && ticket.BookingOrder?.UserId != user?.Id)
            {
                return Forbid();
            }

            var success = await _ticketService.CancelTicketAsync(ticketId);
            if (success)
            {
                TempData["SuccessMessage"] = "Ticket cancelled successfully. You may now request a refund for the refundable amount.";
                return RedirectToAction("RequestRefund", "Refund", new { ticketId });
            }

            TempData["ErrorMessage"] = "Could not cancel ticket. Please check departure deadline.";
            return RedirectToAction("MyBookings");
        }
    }
}
