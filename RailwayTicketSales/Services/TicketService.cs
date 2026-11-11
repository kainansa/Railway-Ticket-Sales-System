using Microsoft.EntityFrameworkCore;
using QRCoder;
using RailwayTicketSales.Data;
using RailwayTicketSales.Models.Entities;
using RailwayTicketSales.Models.Enums;
using RailwayTicketSales.Models.ViewModels;
using RailwayTicketSales.Services.Interfaces;

namespace RailwayTicketSales.Services
{
    public class TicketService : ITicketService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPaymentGateway _paymentGateway;
        private readonly INotificationService _notificationService;
        private readonly ITrainService _trainService;
        private readonly ILogger<TicketService> _logger;

        public TicketService(
            ApplicationDbContext context,
            IPaymentGateway paymentGateway,
            INotificationService notificationService,
            ITrainService trainService,
            ILogger<TicketService> logger)
        {
            _context = context;
            _paymentGateway = paymentGateway;
            _notificationService = notificationService;
            _trainService = trainService;
            _logger = logger;
        }

        public async Task<Ticket?> GetTicketByCodeAsync(string ticketCode)
        {
            return await _context.Tickets
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
                .FirstOrDefaultAsync(t => t.TicketCode == ticketCode);
        }

        public async Task<Ticket?> GetTicketByIdAsync(int ticketId)
        {
            return await _context.Tickets
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
                .FirstOrDefaultAsync(t => t.TicketId == ticketId);
        }

        public async Task<List<Ticket>> GetUserTicketsAsync(string userId)
        {
            return await _context.Tickets
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
                .Where(t => t.BookingOrder != null && t.BookingOrder.UserId == userId)
                .OrderByDescending(t => t.IssuedDate)
                .ToListAsync();
        }

        public async Task<bool> CanChangeTicketAsync(int ticketId)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Schedule)
                .FirstOrDefaultAsync(t => t.TicketId == ticketId);

            if (ticket == null) return false;
            if (ticket.TicketStatus != TicketStatus.ActivePaid) return false;

            // Must be at least 4 hours before departure (BR-07)
            if (ticket.Schedule != null && ticket.Schedule.DepartureTime <= DateTime.Now.AddHours(4))
            {
                return false;
            }

            return true;
        }

        public async Task<ChangeTicketCalculation> CalculateChangeFeeAsync(int ticketId, int newScheduleId, int newSeatId)
        {
            var ticket = await GetTicketByIdAsync(ticketId);
            if (ticket == null || ticket.TicketStatus != TicketStatus.ActivePaid)
            {
                return new ChangeTicketCalculation { IsEligible = false, Message = "Ticket is not eligible for change (must be Active/Paid)." };
            }

            if (ticket.Schedule != null && ticket.Schedule.DepartureTime <= DateTime.Now.AddHours(4))
            {
                return new ChangeTicketCalculation { IsEligible = false, Message = "Ticket cannot be changed less than 4 hours before departure (BR-07)." };
            }

            var newSchedule = await _context.Schedules
                .Include(s => s.Route)
                .FirstOrDefaultAsync(s => s.ScheduleId == newScheduleId);

            var newSeat = await _context.Seats
                .Include(s => s.Coach)
                .FirstOrDefaultAsync(s => s.SeatId == newSeatId);

            if (newSchedule == null || newSeat == null)
            {
                return new ChangeTicketCalculation { IsEligible = false, Message = "New schedule or seat not found." };
            }

            // Calculate new fare
            decimal newFare = await _trainService.CalculateFareAsync(newSchedule.RouteId, newSeat.CoachId, newSeat.SeatType, newSchedule.BasePrice);
            decimal oldFare = ticket.TicketPrice;
            decimal diff = newFare - oldFare;
            decimal changeAdminFee = 20000; // Flat administrative fee for ticket exchange

            decimal totalPayable = diff > 0 ? (diff + changeAdminFee) : changeAdminFee;

            return new ChangeTicketCalculation
            {
                TicketId = ticketId,
                OldTicketPrice = oldFare,
                NewTicketPrice = newFare,
                FareDifference = diff,
                ChangeFee = changeAdminFee,
                TotalPayable = totalPayable,
                IsEligible = true,
                Message = "Eligible for change."
            };
        }

        public async Task<TicketChangeResult> ProcessTicketChangeAsync(
            int ticketId,
            int newScheduleId,
            int newSeatId,
            PaymentMethod paymentMethod,
            string simulatedOutcome = "Success")
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ticket = await GetTicketByIdAsync(ticketId);
                if (ticket == null || ticket.TicketStatus != TicketStatus.ActivePaid)
                {
                    return new TicketChangeResult { Success = false, Message = "Original ticket is not eligible for change." };
                }

                // Check eligibility
                var calc = await CalculateChangeFeeAsync(ticketId, newScheduleId, newSeatId);
                if (!calc.IsEligible)
                {
                    return new TicketChangeResult { Success = false, Message = calc.Message };
                }

                // Check seat availability on new schedule
                var now = DateTime.UtcNow;
                var seatTaken = await _context.Tickets
                    .AnyAsync(t => t.ScheduleId == newScheduleId && t.SeatId == newSeatId && t.TicketStatus == TicketStatus.ActivePaid);

                if (seatTaken)
                {
                    return new TicketChangeResult { Success = false, Message = "The selected new seat is already booked. Please pick another seat." };
                }

                // Process additional payment if required (A3 Alternative flow)
                if (calc.TotalPayable > 0)
                {
                    var payResult = await _paymentGateway.ProcessPaymentAsync($"CHG-{ticket.TicketCode}", calc.TotalPayable, paymentMethod, simulatedOutcome);
                    if (!payResult.Success)
                    {
                        await transaction.RollbackAsync();
                        return new TicketChangeResult
                        {
                            Success = false,
                            Message = $"Additional payment failed: {payResult.Message}. The original ticket remains unchanged."
                        };
                    }
                }

                // Update ticket details
                var newSchedule = await _context.Schedules.Include(s => s.Train).FirstOrDefaultAsync(s => s.ScheduleId == newScheduleId);
                var newSeat = await _context.Seats.FirstOrDefaultAsync(s => s.SeatId == newSeatId);

                var oldCode = ticket.TicketCode;
                var newCode = $"TKT-{now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
                var newQr = $"RAILWAY|{newCode}|Trip:{newSchedule?.TripCode}|Seat:{newSeat?.SeatNumber}|Passenger:{ticket.PassengerName}|Id:{ticket.PassengerIdCard}";

                ticket.ScheduleId = newScheduleId;
                ticket.SeatId = newSeatId;
                ticket.TicketPrice = calc.NewTicketPrice;
                ticket.TicketCode = newCode;
                ticket.QrCodeData = newQr;
                ticket.IssuedDate = now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Ticket {OldCode} changed to {NewCode} for new schedule {ScheduleId}", oldCode, newCode, newScheduleId);

                // Notification
                try
                {
                    var dummyOld = new Ticket { TicketCode = oldCode };
                    await _notificationService.SendTicketChangeNotificationAsync(dummyOld, ticket);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send change notification");
                }

                return new TicketChangeResult
                {
                    Success = true,
                    Message = "Ticket changed successfully!",
                    NewTicketId = ticket.TicketId,
                    NewTicketCode = newCode
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process ticket change");
                return new TicketChangeResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<CancellationCalculation> CalculateCancellationAsync(int ticketId)
        {
            var ticket = await GetTicketByIdAsync(ticketId);
            if (ticket == null || ticket.TicketStatus != TicketStatus.ActivePaid)
            {
                return new CancellationCalculation { IsEligible = false, PolicyDescription = "Ticket is not active/eligible for cancellation." };
            }

            var departureTime = ticket.Schedule?.DepartureTime ?? DateTime.Now;
            var hoursUntil = (departureTime - DateTime.Now).TotalHours;

            if (hoursUntil <= 0)
            {
                return new CancellationCalculation { IsEligible = false, PolicyDescription = "Train has already departed. Cancellation not permitted." };
            }

            // Find applicable RefundPolicy
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
                matchedPolicy = policies.LastOrDefault() ?? new RefundPolicy { FeePercentage = 100, Description = "Non-refundable" };
            }

            decimal feePct = matchedPolicy.FeePercentage;
            decimal feeAmount = ticket.TicketPrice * (feePct / 100.0m);
            decimal refundable = Math.Max(0, ticket.TicketPrice - feeAmount);

            return new CancellationCalculation
            {
                TicketId = ticketId,
                TicketCode = ticket.TicketCode,
                OriginalPrice = ticket.TicketPrice,
                FeePercentage = feePct,
                CancellationFee = feeAmount,
                RefundableAmount = refundable,
                HoursUntilDeparture = hoursUntil,
                IsEligible = feePct < 100,
                PolicyDescription = matchedPolicy.Description
            };
        }

        public async Task<bool> CancelTicketAsync(int ticketId, string? clerkId = null)
        {
            var ticket = await GetTicketByIdAsync(ticketId);
            if (ticket == null || ticket.TicketStatus != TicketStatus.ActivePaid)
                return false;

            var calc = await CalculateCancellationAsync(ticketId);
            if (!calc.IsEligible && calc.HoursUntilDeparture <= 0)
                return false;

            // Update ticket status to Cancelled and invalidate QR
            ticket.TicketStatus = TicketStatus.Cancelled;
            ticket.QrCodeData = $"CANCELLED|{ticket.TicketCode}|{DateTime.UtcNow:yyyyMMddHHmmss}";
            ticket.IsActive = false;

            await _context.SaveChangesAsync();

            // Send notification
            try
            {
                await _notificationService.SendCancellationNotificationAsync(ticket, calc.RefundableAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send cancellation notification");
            }

            return true;
        }

        public Task<string> GenerateQrCodeBase64Async(string qrContent)
        {
            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);
                byte[] qrCodeImage = qrCode.GetGraphic(20);
                string base64 = Convert.ToBase64String(qrCodeImage);
                return Task.FromResult($"data:image/png;base64,{base64}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating QR Code");
                return Task.FromResult(string.Empty);
            }
        }
    }
}
