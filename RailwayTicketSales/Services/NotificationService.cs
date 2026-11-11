using Microsoft.Extensions.Logging;
using RailwayTicketSales.Data;
using RailwayTicketSales.Models.Entities;
using RailwayTicketSales.Services.Interfaces;

namespace RailwayTicketSales.Services
{
    public class MockNotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MockNotificationService> _logger;

        public MockNotificationService(ApplicationDbContext context, ILogger<MockNotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> SendBookingConfirmationAsync(BookingOrder booking)
        {
            try
            {
                var body = $"Dear {booking.CustomerName},\n\nYour railway booking {booking.BookingCode} has been successfully completed.\nTotal Amount: {booking.TotalAmount:N0} VND.\nTickets issued: {booking.Tickets.Count}.\nThank you for choosing Vietnam Railways!";
                
                var log = new NotificationLog
                {
                    RecipientEmail = booking.CustomerEmail,
                    RecipientPhone = booking.CustomerPhone,
                    NotificationType = "BookingConfirmation",
                    Subject = $"Railway Booking Confirmation - {booking.BookingCode}",
                    MessageBody = body,
                    SentDate = DateTime.UtcNow,
                    DeliveryStatus = "Sent"
                };

                _context.NotificationLogs.Add(log);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Sent booking confirmation for {Code} to {Email}", booking.BookingCode, booking.CustomerEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send booking confirmation");
                return false;
            }
        }

        public async Task<bool> SendETicketAsync(Ticket ticket)
        {
            try
            {
                var body = $"Dear {ticket.PassengerName},\n\nYour electronic ticket {ticket.TicketCode} for train {ticket.Schedule?.Train?.TrainNumber} is ready.\nSeat: {ticket.Seat?.SeatNumber}\nDeparture: {ticket.Schedule?.DepartureTime:dd/MM/yyyy HH:mm}\nPlease present your QR Code at the ticket gate.";

                var email = ticket.BookingOrder?.CustomerEmail ?? "passenger@railway.com";
                var phone = ticket.BookingOrder?.CustomerPhone ?? "N/A";

                var log = new NotificationLog
                {
                    RecipientEmail = email,
                    RecipientPhone = phone,
                    NotificationType = "ETicket",
                    Subject = $"Electronic Ticket Issued - {ticket.TicketCode}",
                    MessageBody = body,
                    SentDate = DateTime.UtcNow,
                    DeliveryStatus = "Sent"
                };

                _context.NotificationLogs.Add(log);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send e-ticket");
                return false;
            }
        }

        public async Task<bool> SendTicketChangeNotificationAsync(Ticket oldTicket, Ticket newTicket)
        {
            try
            {
                var body = $"Dear {newTicket.PassengerName},\n\nYour ticket {oldTicket.TicketCode} has been successfully updated to {newTicket.TicketCode}.\nNew Trip: {newTicket.Schedule?.TripCode}\nNew Seat: {newTicket.Seat?.SeatNumber}\nNew Departure: {newTicket.Schedule?.DepartureTime:dd/MM/yyyy HH:mm}.";

                var log = new NotificationLog
                {
                    RecipientEmail = newTicket.BookingOrder?.CustomerEmail ?? "passenger@railway.com",
                    RecipientPhone = newTicket.BookingOrder?.CustomerPhone ?? "N/A",
                    NotificationType = "TicketChange",
                    Subject = $"Ticket Modification Notice - {newTicket.TicketCode}",
                    MessageBody = body,
                    SentDate = DateTime.UtcNow,
                    DeliveryStatus = "Sent"
                };

                _context.NotificationLogs.Add(log);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send ticket change notification");
                return false;
            }
        }

        public async Task<bool> SendCancellationNotificationAsync(Ticket ticket, decimal refundAmount)
        {
            try
            {
                var body = $"Dear {ticket.PassengerName},\n\nYour ticket {ticket.TicketCode} has been cancelled.\nRefundable amount: {refundAmount:N0} VND.\nThe seat has been released back to available inventory.";

                var log = new NotificationLog
                {
                    RecipientEmail = ticket.BookingOrder?.CustomerEmail ?? "passenger@railway.com",
                    RecipientPhone = ticket.BookingOrder?.CustomerPhone ?? "N/A",
                    NotificationType = "TicketCancellation",
                    Subject = $"Ticket Cancellation Notice - {ticket.TicketCode}",
                    MessageBody = body,
                    SentDate = DateTime.UtcNow,
                    DeliveryStatus = "Sent"
                };

                _context.NotificationLogs.Add(log);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send cancellation notification");
                return false;
            }
        }

        public async Task<bool> SendRefundNotificationAsync(Refund refund)
        {
            try
            {
                var email = refund.Ticket?.BookingOrder?.CustomerEmail ?? "passenger@railway.com";
                var body = $"Dear Customer,\n\nYour refund request {refund.RefundCode} for ticket {refund.Ticket?.TicketCode} has been processed.\nOriginal Fare: {refund.TicketPrice:N0} VND\nCancellation Fee: {refund.CancellationFee:N0} VND\nRefund Amount: {refund.RefundAmount:N0} VND.\nFunds will arrive in your account according to your bank's schedule.";

                var log = new NotificationLog
                {
                    RecipientEmail = email,
                    RecipientPhone = refund.Ticket?.BookingOrder?.CustomerPhone ?? "N/A",
                    NotificationType = "RefundConfirmation",
                    Subject = $"Refund Confirmation - {refund.RefundCode}",
                    MessageBody = body,
                    SentDate = DateTime.UtcNow,
                    DeliveryStatus = "Sent"
                };

                _context.NotificationLogs.Add(log);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send refund notification");
                return false;
            }
        }
    }
}
