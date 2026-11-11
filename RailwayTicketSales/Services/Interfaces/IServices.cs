using RailwayTicketSales.Models.Entities;
using RailwayTicketSales.Models.Enums;
using RailwayTicketSales.Models.ViewModels;

namespace RailwayTicketSales.Services.Interfaces
{
    public interface IPaymentGateway
    {
        Task<PaymentResult> ProcessPaymentAsync(string bookingCode, decimal amount, PaymentMethod method, string simulatedOutcome = "Success");
        Task<PaymentResult> ProcessRefundAsync(string refundCode, decimal amount, string transactionRef);
    }

    public interface INotificationService
    {
        Task<bool> SendBookingConfirmationAsync(BookingOrder booking);
        Task<bool> SendETicketAsync(Ticket ticket);
        Task<bool> SendTicketChangeNotificationAsync(Ticket oldTicket, Ticket newTicket);
        Task<bool> SendCancellationNotificationAsync(Ticket ticket, decimal refundAmount);
        Task<bool> SendRefundNotificationAsync(Refund refund);
    }

    public interface ITrainService
    {
        Task<List<Station>> GetAllStationsAsync();
        Task<List<ScheduleSearchResult>> SearchSchedulesAsync(int departureStationId, int arrivalStationId, DateTime travelDate);
        Task<ScheduleDetailsViewModel?> GetScheduleDetailsAsync(int scheduleId);
        Task<decimal> CalculateFareAsync(int routeId, int coachId, SeatType seatType, decimal basePrice);
    }

    public interface IBookingService
    {
        Task<BookingOrder?> CreateTemporaryBookingAsync(CreateBookingRequest request, string? userId, bool isCounterBooking, string? clerkId);
        Task<bool> IsSeatAvailableAsync(int scheduleId, int seatId);
        Task<List<int>> GetUnavailableSeatIdsAsync(int scheduleId);
        Task<BookingOrder?> GetBookingByCodeAsync(string bookingCode);
        Task<BookingOrder?> GetBookingByIdAsync(int bookingOrderId);
        Task<bool> ValidateAndExpireHoldAsync(int bookingOrderId);
        Task<PaymentResult> ConfirmPaymentAndIssueTicketsAsync(int bookingOrderId, PaymentMethod paymentMethod, string simulatedOutcome = "Success");
        Task CancelExpiredHoldsAsync();
    }

    public interface ITicketService
    {
        Task<Ticket?> GetTicketByCodeAsync(string ticketCode);
        Task<Ticket?> GetTicketByIdAsync(int ticketId);
        Task<List<Ticket>> GetUserTicketsAsync(string userId);
        Task<bool> CanChangeTicketAsync(int ticketId);
        Task<ChangeTicketCalculation> CalculateChangeFeeAsync(int ticketId, int newScheduleId, int newSeatId);
        Task<TicketChangeResult> ProcessTicketChangeAsync(int ticketId, int newScheduleId, int newSeatId, PaymentMethod paymentMethod, string simulatedOutcome = "Success");
        Task<CancellationCalculation> CalculateCancellationAsync(int ticketId);
        Task<bool> CancelTicketAsync(int ticketId, string? clerkId = null);
        Task<string> GenerateQrCodeBase64Async(string qrContent);
    }

    public interface IRefundService
    {
        Task<RefundCalculationResult> CheckEligibilityAndCalculateAsync(int ticketId);
        Task<RefundResult> ProcessRefundAsync(int ticketId, string reason, string? clerkId = null, string simulatedOutcome = "Success");
        Task<List<Refund>> GetRefundHistoryAsync(string? userId = null);
    }
}
