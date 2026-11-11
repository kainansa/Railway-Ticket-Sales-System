using Microsoft.EntityFrameworkCore;
using RailwayTicketSales.Data;
using RailwayTicketSales.Models.Entities;
using RailwayTicketSales.Models.Enums;
using RailwayTicketSales.Models.ViewModels;
using RailwayTicketSales.Services.Interfaces;

namespace RailwayTicketSales.Services
{
    public class BookingService : IBookingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPaymentGateway _paymentGateway;
        private readonly INotificationService _notificationService;
        private readonly ILogger<BookingService> _logger;

        public BookingService(
            ApplicationDbContext context,
            IPaymentGateway paymentGateway,
            INotificationService notificationService,
            ILogger<BookingService> logger)
        {
            _context = context;
            _paymentGateway = paymentGateway;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<bool> IsSeatAvailableAsync(int scheduleId, int seatId)
        {
            var now = DateTime.UtcNow;

            // Check if ticket is ActivePaid
            var hasActiveTicket = await _context.Tickets
                .AnyAsync(t => t.ScheduleId == scheduleId && t.SeatId == seatId && t.TicketStatus == TicketStatus.ActivePaid);

            if (hasActiveTicket) return false;

            // Check if seat is currently held under an unexpired pending order
            var isHeld = await _context.BookingDetails
                .AnyAsync(bd => bd.ScheduleId == scheduleId && bd.SeatId == seatId
                                && bd.BookingOrder != null
                                && (bd.BookingOrder.OrderStatus == OrderStatus.Pending || bd.BookingOrder.OrderStatus == OrderStatus.AwaitingPayment)
                                && bd.BookingOrder.HoldExpiresAt > now);

            return !isHeld;
        }

        public async Task<List<int>> GetUnavailableSeatIdsAsync(int scheduleId)
        {
            var now = DateTime.UtcNow;

            var booked = await _context.Tickets
                .Where(t => t.ScheduleId == scheduleId && t.TicketStatus == TicketStatus.ActivePaid)
                .Select(t => t.SeatId)
                .ToListAsync();

            var held = await _context.BookingDetails
                .Where(bd => bd.ScheduleId == scheduleId
                             && bd.BookingOrder != null
                             && (bd.BookingOrder.OrderStatus == OrderStatus.Pending || bd.BookingOrder.OrderStatus == OrderStatus.AwaitingPayment)
                             && bd.BookingOrder.HoldExpiresAt > now)
                .Select(bd => bd.SeatId)
                .ToListAsync();

            return booked.Union(held).Distinct().ToList();
        }

        public async Task<BookingOrder?> CreateTemporaryBookingAsync(CreateBookingRequest request, string? userId, bool isCounterBooking, string? clerkId)
        {
            if (request.Passengers == null || request.Passengers.Count == 0)
                throw new InvalidOperationException("No passengers or seats selected.");

            // Concurrency Protection: Database Transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var schedule = await _context.Schedules
                    .Include(s => s.Train)
                    .FirstOrDefaultAsync(s => s.ScheduleId == request.ScheduleId);

                if (schedule == null)
                    throw new InvalidOperationException("Train schedule not found.");

                if (schedule.Status == TripStatus.BookingClosed || schedule.Status == TripStatus.Completed)
                    throw new InvalidOperationException("Booking is closed for this trip.");

                var now = DateTime.UtcNow;

                // Validate each seat
                foreach (var p in request.Passengers)
                {
                    // BR-01: personal info validation
                    if (string.IsNullOrWhiteSpace(p.PassengerName) || string.IsNullOrWhiteSpace(p.PassengerIdCard))
                        throw new InvalidOperationException("Valid passenger information (Name and ID Card) is required for all seats (BR-01).");

                    // BR-02: Check concurrency / availability
                    var hasTicket = await _context.Tickets
                        .AnyAsync(t => t.ScheduleId == schedule.ScheduleId && t.SeatId == p.SeatId && t.TicketStatus == TicketStatus.ActivePaid);

                    if (hasTicket)
                        throw new InvalidOperationException($"Seat {p.SeatNumber} has already been booked by another passenger.");

                    var isHeld = await _context.BookingDetails
                        .AnyAsync(bd => bd.ScheduleId == schedule.ScheduleId && bd.SeatId == p.SeatId
                                        && bd.BookingOrder != null
                                        && (bd.BookingOrder.OrderStatus == OrderStatus.Pending || bd.BookingOrder.OrderStatus == OrderStatus.AwaitingPayment)
                                        && bd.BookingOrder.HoldExpiresAt > now);

                    if (isHeld)
                        throw new InvalidOperationException($"Seat {p.SeatNumber} is currently held by another passenger. Please select another seat.");
                }

                // BR-03 & BR-04: 10-minute hold
                var holdDuration = isCounterBooking ? TimeSpan.FromMinutes(20) : TimeSpan.FromMinutes(10);
                var holdExpiry = now.Add(holdDuration);

                decimal totalFare = request.Passengers.Sum(p => p.Price + 5000); // 5000 insurance fee per ticket

                var bookingCode = $"BK-{now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";

                var booking = new BookingOrder
                {
                    BookingCode = bookingCode,
                    UserId = userId,
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail,
                    CustomerPhone = request.CustomerPhone,
                    CustomerIdCard = request.CustomerIdCard,
                    TotalAmount = totalFare,
                    OrderStatus = OrderStatus.AwaitingPayment,
                    CreatedDateTime = now,
                    HoldExpiresAt = holdExpiry,
                    IsCounterBooking = isCounterBooking,
                    ClerkId = clerkId
                };

                _context.BookingOrders.Add(booking);
                await _context.SaveChangesAsync();

                foreach (var p in request.Passengers)
                {
                    var detail = new BookingDetail
                    {
                        BookingOrderId = booking.BookingOrderId,
                        ScheduleId = schedule.ScheduleId,
                        SeatId = p.SeatId,
                        PassengerName = p.PassengerName,
                        PassengerIdCard = p.PassengerIdCard,
                        PassengerPhone = p.PassengerPhone,
                        PassengerType = p.PassengerType,
                        Price = p.Price
                    };
                    _context.BookingDetails.Add(detail);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Temporary booking {Code} created with 10-minute hold until {Expiry}", bookingCode, holdExpiry);
                return booking;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create temporary booking");
                throw;
            }
        }

        public async Task<BookingOrder?> GetBookingByCodeAsync(string bookingCode)
        {
            return await _context.BookingOrders
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Seat)
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
                .Include(b => b.Tickets)
                .Include(b => b.PaymentTransactions)
                .FirstOrDefaultAsync(b => b.BookingCode == bookingCode);
        }

        public async Task<BookingOrder?> GetBookingByIdAsync(int bookingOrderId)
        {
            return await _context.BookingOrders
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Seat)
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
                .Include(b => b.Tickets)
                .Include(b => b.PaymentTransactions)
                .FirstOrDefaultAsync(b => b.BookingOrderId == bookingOrderId);
        }

        public async Task<bool> ValidateAndExpireHoldAsync(int bookingOrderId)
        {
            var booking = await _context.BookingOrders.FindAsync(bookingOrderId);
            if (booking == null) return false;

            if (booking.OrderStatus == OrderStatus.Completed) return true;

            if (booking.HoldExpiresAt.HasValue && booking.HoldExpiresAt.Value <= DateTime.UtcNow)
            {
                booking.OrderStatus = OrderStatus.Expired;
                await _context.SaveChangesAsync();
                return false; // Hold has expired
            }

            return true;
        }

        public async Task<PaymentResult> ConfirmPaymentAndIssueTicketsAsync(int bookingOrderId, PaymentMethod paymentMethod, string simulatedOutcome = "Success")
        {
            var booking = await _context.BookingOrders
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Schedule)
                        .ThenInclude(s => s!.Train)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Seat)
                .FirstOrDefaultAsync(b => b.BookingOrderId == bookingOrderId);

            if (booking == null)
                return new PaymentResult { Success = false, Message = "Booking not found." };

            // Server-side hold check: BR-04
            if (booking.HoldExpiresAt.HasValue && booking.HoldExpiresAt.Value <= DateTime.UtcNow)
            {
                booking.OrderStatus = OrderStatus.Expired;
                await _context.SaveChangesAsync();
                return new PaymentResult
                {
                    Success = false,
                    Status = PaymentStatus.Timeout,
                    Message = "Your 10-minute seat reservation has expired. The seat has been released. Please make a new search."
                };
            }

            if (booking.OrderStatus == OrderStatus.Completed)
            {
                return new PaymentResult
                {
                    Success = true,
                    Status = PaymentStatus.Success,
                    Message = "This booking order has already been paid and completed."
                };
            }

            // Step 1: Process payment through Payment Gateway
            booking.OrderStatus = OrderStatus.AuthorizingTransaction;
            await _context.SaveChangesAsync();

            var paymentResult = await _paymentGateway.ProcessPaymentAsync(booking.BookingCode, booking.TotalAmount, paymentMethod, simulatedOutcome);

            // Record transaction
            var transaction = new PaymentTransaction
            {
                BookingOrderId = booking.BookingOrderId,
                TransactionCode = paymentResult.TransactionCode,
                Amount = booking.TotalAmount,
                PaymentMethod = paymentMethod,
                Status = paymentResult.Status,
                TransactionDate = DateTime.UtcNow,
                PaymentGatewayResponse = paymentResult.Message,
                ErrorMessage = paymentResult.Success ? null : paymentResult.Message
            };
            _context.PaymentTransactions.Add(transaction);

            if (!paymentResult.Success)
            {
                // Payment failed: revert order state back to AwaitingPayment if hold is still valid
                if (booking.HoldExpiresAt.HasValue && booking.HoldExpiresAt.Value > DateTime.UtcNow)
                {
                    booking.OrderStatus = OrderStatus.AwaitingPayment;
                }
                else
                {
                    booking.OrderStatus = OrderStatus.Expired;
                }
                await _context.SaveChangesAsync();
                return paymentResult;
            }

            // Step 2: Payment Succeeded -> Issue Tickets (BR-05)
            booking.OrderStatus = OrderStatus.Completed;
            booking.PaymentMethod = paymentMethod;

            var issuedTickets = new List<Ticket>();
            var now = DateTime.UtcNow;

            foreach (var detail in booking.BookingDetails)
            {
                var ticketCode = $"TKT-{now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
                var qrData = $"RAILWAY|{ticketCode}|Trip:{detail.Schedule?.TripCode}|Seat:{detail.Seat?.SeatNumber}|Pax:{detail.PassengerName}|ID:{detail.PassengerIdCard}";

                var ticket = new Ticket
                {
                    TicketCode = ticketCode,
                    BookingOrderId = booking.BookingOrderId,
                    BookingDetailId = detail.BookingDetailId,
                    ScheduleId = detail.ScheduleId,
                    SeatId = detail.SeatId,
                    PassengerName = detail.PassengerName,
                    PassengerIdCard = detail.PassengerIdCard,
                    PassengerType = detail.PassengerType,
                    TicketPrice = detail.Price,
                    InsuranceFee = 5000,
                    QrCodeData = qrData,
                    TicketStatus = TicketStatus.ActivePaid,
                    IssuedDate = now,
                    IsActive = true
                };

                _context.Tickets.Add(ticket);
                issuedTickets.Add(ticket);
            }

            await _context.SaveChangesAsync();

            // Step 3: Trigger Email/SMS notifications asynchronously
            try
            {
                await _notificationService.SendBookingConfirmationAsync(booking);
                foreach (var t in issuedTickets)
                {
                    await _notificationService.SendETicketAsync(t);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notifications, but ticket issuance succeeded (A7 flow).");
            }

            return paymentResult;
        }

        public async Task CancelExpiredHoldsAsync()
        {
            var now = DateTime.UtcNow;
            var expiredBookings = await _context.BookingOrders
                .Where(b => (b.OrderStatus == OrderStatus.Pending || b.OrderStatus == OrderStatus.AwaitingPayment)
                            && b.HoldExpiresAt.HasValue
                            && b.HoldExpiresAt.Value <= now)
                .ToListAsync();

            foreach (var b in expiredBookings)
            {
                b.OrderStatus = OrderStatus.Expired;
            }

            if (expiredBookings.Count > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cancelled {Count} expired booking holds automatically.", expiredBookings.Count);
            }
        }
    }
}
