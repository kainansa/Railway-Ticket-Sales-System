using RailwayTicketSales.Models.Enums;
using RailwayTicketSales.Models.ViewModels;
using RailwayTicketSales.Services.Interfaces;

namespace RailwayTicketSales.Services
{
    public class MockPaymentGateway : IPaymentGateway
    {
        public Task<PaymentResult> ProcessPaymentAsync(string bookingCode, decimal amount, PaymentMethod method, string simulatedOutcome = "Success")
        {
            var result = new PaymentResult();

            switch (simulatedOutcome.ToLower())
            {
                case "rejected":
                    result.Success = false;
                    result.Status = PaymentStatus.Rejected;
                    result.TransactionCode = $"FAIL-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
                    result.Message = "Payment was rejected by issuing bank. Insufficient funds or card declined.";
                    break;

                case "timeout":
                    result.Success = false;
                    result.Status = PaymentStatus.Timeout;
                    result.TransactionCode = $"TIMEOUT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
                    result.Message = "Payment gateway response timed out. Transaction cancelled.";
                    break;

                case "success":
                default:
                    result.Success = true;
                    result.Status = PaymentStatus.Success;
                    result.TransactionCode = $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
                    result.Message = $"Payment of {amount:N0} VND via {method} was successful. Transaction approved.";
                    break;
            }

            return Task.FromResult(result);
        }

        public Task<PaymentResult> ProcessRefundAsync(string refundCode, decimal amount, string transactionRef)
        {
            var result = new PaymentResult
            {
                Success = true,
                Status = PaymentStatus.Success,
                TransactionCode = $"REF-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
                Message = $"Refund of {amount:N0} VND processed successfully back to the customer's payment account."
            };

            return Task.FromResult(result);
        }
    }
}
