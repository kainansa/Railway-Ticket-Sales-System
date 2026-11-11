using Microsoft.EntityFrameworkCore;
using RailwayTicketSales.Data;
using RailwayTicketSales.Models.Entities;
using RailwayTicketSales.Models.Enums;
using RailwayTicketSales.Models.ViewModels;
using RailwayTicketSales.Services.Interfaces;

namespace RailwayTicketSales.Services
{
    public class RefundService : IRefundService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPaymentGateway _paymentGateway;
        private readonly INotificationService _notificationService;
        private readonly ILogger<RefundService> _logger;

        public RefundService(
            ApplicationDbContext context,
            IPaymentGateway paymentGateway,
            INotificationService notificationService,
            ILogger<RefundService> logger)
        {
            _context = context;
            _paymentGateway = paymentGateway;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<RefundCalculationResult> CheckEligibilityAndCalculateAsync(int ticketId)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Schedule)
                .Include(t => t.Refunds)
                .FirstOrDefaultAsync(t => t.TicketId == ticketId);

            if (ticket == null)
            {
                return new RefundCalculationResult { IsEligible = false, PolicyDescription = "Ticket not found." };
            }

            // Check if already refunded (prevent duplicate refunds)
            if (ticket.TicketStatus == TicketStatus.Refunded || ticket.Refunds.Any(r => r.RefundStatus == RefundStatus.Completed))
            {
                return new RefundCalculationResult
                {
                    TicketId = ticketId,
                    TicketCode = ticket.TicketCode,
                    IsEligible = false,
                    PolicyDescription = "This ticket has already been refunded. Duplicate refund is not permitted (BR-10)."
                };
            }

            // Only Cancelled or ActivePaid tickets can be refunded
            if (ticket.TicketStatus != TicketStatus.Cancelled && ticket.TicketStatus != TicketStatus.ActivePaid)
            {
                return new RefundCalculationResult
                {
                    TicketId = ticketId,
                    TicketCode = ticket.TicketCode,
                    IsEligible = false,
                    PolicyDescription = $"Ticket is in '{ticket.TicketStatus}' status and cannot be refunded."
                };
            }

            var departureTime = ticket.Schedule?.DepartureTime ?? DateTime.Now;
            var hoursUntil = (departureTime - DateTime.Now).TotalHours;

            // Load policies
            var policies = await _context.RefundPolicies
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.HoursBeforeDeparture)
                .ToListAsync();

            RefundPolicy? matchedPolicy = null;
            foreach (var p in policies)
            {
                if (hoursUntil >= p.HoursBeforeDeparture)
                {
                    matchedPolicy = p;
                    break;
                }
            }

            if (matchedPolicy == null)
            {
                matchedPolicy = policies.LastOrDefault() ?? new RefundPolicy { FeePercentage = 100, Description = "Departure passed. 100% cancellation penalty." };
            }

            decimal feePct = matchedPolicy.FeePercentage;
            decimal feeAmount = ticket.TicketPrice * (feePct / 100.0m);
            decimal refundable = Math.Max(0, ticket.TicketPrice - feeAmount);

            return new RefundCalculationResult
            {
                TicketId = ticketId,
                TicketCode = ticket.TicketCode,
                TicketPrice = ticket.TicketPrice,
                FeePercentage = feePct,
                CancellationFee = feeAmount,
                RefundableAmount = refundable,
                IsEligible = feePct < 100 && refundable > 0,
                PolicyDescription = matchedPolicy.Description
            };
        }

        public async Task<RefundResult> ProcessRefundAsync(int ticketId, string reason, string? clerkId = null, string simulatedOutcome = "Success")
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var calc = await CheckEligibilityAndCalculateAsync(ticketId);
                if (!calc.IsEligible)
                {
                    return new RefundResult { Success = false, Message = calc.PolicyDescription };
                }

                var ticket = await _context.Tickets
                    .Include(t => t.BookingOrder)
                    .Include(t => t.Schedule)
                    .FirstOrDefaultAsync(t => t.TicketId == ticketId);

                if (ticket == null)
                    return new RefundResult { Success = false, Message = "Ticket not found." };

                var now = DateTime.UtcNow;
                var refundCode = $"REF-{now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";

                // Call Payment Gateway
                var gwResult = await _paymentGateway.ProcessRefundAsync(refundCode, calc.RefundableAmount, ticket.BookingOrder?.BookingCode ?? "N/A");

                var refundStatus = gwResult.Success ? RefundStatus.Completed : RefundStatus.Pending;

                var refund = new Refund
                {
                    TicketId = ticketId,
                    RefundCode = refundCode,
                    RequestDate = now,
                    ProcessedDate = gwResult.Success ? now : null,
                    TicketPrice = calc.TicketPrice,
                    CancellationFee = calc.CancellationFee,
                    RefundAmount = calc.RefundableAmount,
                    RefundStatus = refundStatus,
                    Reason = reason,
                    ProcessedByClerkId = clerkId,
                    TransactionReference = gwResult.TransactionCode
                };

                _context.Refunds.Add(refund);

                if (gwResult.Success)
                {
                    ticket.TicketStatus = TicketStatus.Refunded;
                    ticket.IsActive = false;
                    ticket.QrCodeData = $"REFUNDED|{ticket.TicketCode}|{now:yyyyMMddHHmmss}";

                    // Check if all tickets in this order are cancelled or refunded
                    var allTickets = await _context.Tickets
                        .Where(t => t.BookingOrderId == ticket.BookingOrderId && t.TicketId != ticket.TicketId)
                        .ToListAsync();

                    if (allTickets.All(t => t.TicketStatus == TicketStatus.Refunded || t.TicketStatus == TicketStatus.Cancelled))
                    {
                        if (ticket.BookingOrder != null)
                        {
                            ticket.BookingOrder.OrderStatus = OrderStatus.Refunded;
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (gwResult.Success)
                {
                    try
                    {
                        refund.Ticket = ticket;
                        await _notificationService.SendRefundNotificationAsync(refund);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send refund notification");
                    }
                }

                return new RefundResult
                {
                    Success = gwResult.Success,
                    RefundCode = refundCode,
                    RefundAmount = calc.RefundableAmount,
                    Message = gwResult.Success 
                        ? $"Refund of {calc.RefundableAmount:N0} VND processed successfully." 
                        : "Refund marked as Pending due to gateway delay."
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process refund");
                return new RefundResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<List<Refund>> GetRefundHistoryAsync(string? userId = null)
        {
            var query = _context.Refunds
                .Include(r => r.Ticket)
                    .ThenInclude(t => t!.Schedule)
                        .ThenInclude(s => s!.Train)
                .Include(r => r.Ticket)
                    .ThenInclude(t => t!.BookingOrder)
                .Include(r => r.ProcessedByClerk)
                .AsQueryable();

            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(r => r.Ticket!.BookingOrder!.UserId == userId);
            }

            return await query.OrderByDescending(r => r.RequestDate).ToListAsync();
        }
    }
}
