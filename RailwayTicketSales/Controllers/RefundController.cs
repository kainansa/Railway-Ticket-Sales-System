using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RailwayTicketSales.Models.Entities;
using RailwayTicketSales.Services.Interfaces;

namespace RailwayTicketSales.Controllers
{
    [Authorize]
    public class RefundController : Controller
    {
        private readonly IRefundService _refundService;
        private readonly ITicketService _ticketService;
        private readonly UserManager<ApplicationUser> _userManager;

        public RefundController(
            IRefundService refundService,
            ITicketService ticketService,
            UserManager<ApplicationUser> userManager)
        {
            _refundService = refundService;
            _ticketService = ticketService;
            _userManager = userManager;
        }

        // View Refund History
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var isStaff = User.IsInRole("Administrator") || User.IsInRole("TicketClerk");

            var refunds = await _refundService.GetRefundHistoryAsync(isStaff ? null : user?.Id);
            return View(refunds);
        }

        // Request Refund for an eligible cancelled ticket
        [HttpGet]
        public async Task<IActionResult> RequestRefund(int ticketId)
        {
            var ticket = await _ticketService.GetTicketByIdAsync(ticketId);
            if (ticket == null) return NotFound("Ticket not found.");

            var user = await _userManager.GetUserAsync(User);
            var isStaff = User.IsInRole("Administrator") || User.IsInRole("TicketClerk");
            if (!isStaff && ticket.BookingOrder?.UserId != user?.Id)
            {
                return Forbid();
            }

            var calc = await _refundService.CheckEligibilityAndCalculateAsync(ticketId);
            ViewBag.Ticket = ticket;

            return View(calc);
        }

        // Process Refund submission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessRefund(int ticketId, string reason = "Passenger cancellation request", string simulatedOutcome = "Success")
        {
            var ticket = await _ticketService.GetTicketByIdAsync(ticketId);
            if (ticket == null) return NotFound("Ticket not found.");

            var user = await _userManager.GetUserAsync(User);
            var isStaff = User.IsInRole("Administrator") || User.IsInRole("TicketClerk");
            if (!isStaff && ticket.BookingOrder?.UserId != user?.Id)
            {
                return Forbid();
            }

            var clerkId = isStaff ? user?.Id : null;
            var result = await _refundService.ProcessRefundAsync(ticketId, reason, clerkId, simulatedOutcome);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("Index");
            }

            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction("RequestRefund", new { ticketId });
        }
    }
}
