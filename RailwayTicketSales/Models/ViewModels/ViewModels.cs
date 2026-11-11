using System.ComponentModel.DataAnnotations;
using RailwayTicketSales.Models.Entities;
using RailwayTicketSales.Models.Enums;

namespace RailwayTicketSales.Models.ViewModels
{
    // Search Models
    public class TrainSearchViewModel
    {
        [Required(ErrorMessage = "Please select a departure station.")]
        [Display(Name = "Departure Station")]
        public int DepartureStationId { get; set; }

        [Required(ErrorMessage = "Please select an arrival station.")]
        [Display(Name = "Arrival Station")]
        public int ArrivalStationId { get; set; }

        [Required(ErrorMessage = "Please choose a travel date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Travel Date")]
        public DateTime TravelDate { get; set; } = DateTime.Today;

        public List<Station>? Stations { get; set; }
        public List<ScheduleSearchResult>? SearchResults { get; set; }
        public bool HasSearched { get; set; } = false;
    }

    public class ScheduleSearchResult
    {
        public int ScheduleId { get; set; }
        public string TripCode { get; set; } = string.Empty;
        public string TrainNumber { get; set; } = string.Empty;
        public string TrainType { get; set; } = string.Empty;
        public string RouteName { get; set; } = string.Empty;
        public string DepartureStationName { get; set; } = string.Empty;
        public string ArrivalStationName { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public TimeSpan Duration => ArrivalTime - DepartureTime;
        public decimal BasePrice { get; set; }
        public TripStatus Status { get; set; }
        public int TotalSeats { get; set; }
        public int AvailableSeats { get; set; }
    }

    // Schedule details and Seat selection
    public class ScheduleDetailsViewModel
    {
        public Schedule Schedule { get; set; } = null!;
        public List<CoachSeatViewModel> Coaches { get; set; } = new();
        public List<int> UnavailableSeatIds { get; set; } = new();
    }

    public class CoachSeatViewModel
    {
        public Coach Coach { get; set; } = null!;
        public List<SeatDisplayItem> Seats { get; set; } = new();
    }

    public class SeatDisplayItem
    {
        public int SeatId { get; set; }
        public string SeatNumber { get; set; } = string.Empty;
        public SeatType SeatType { get; set; }
        public int Floor { get; set; }
        public bool IsAvailable { get; set; }
        public decimal Price { get; set; }
    }

    // Booking Request ViewModel
    public class CreateBookingRequest
    {
        public int ScheduleId { get; set; }

        [Required(ErrorMessage = "Please provide customer name.")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please provide customer email.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please provide phone number.")]
        public string CustomerPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please provide National ID / Passport.")]
        public string CustomerIdCard { get; set; } = string.Empty;

        public List<PassengerItemRequest> Passengers { get; set; } = new();
    }

    public class PassengerItemRequest
    {
        public int SeatId { get; set; }
        public string SeatNumber { get; set; } = string.Empty;
        public string CoachNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Passenger name is required (BR-01).")]
        public string PassengerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "National ID / Passport is required (BR-01).")]
        public string PassengerIdCard { get; set; } = string.Empty;

        public string? PassengerPhone { get; set; }

        public string PassengerType { get; set; } = "Adult"; // Adult, Child, Senior, Student
        public decimal Price { get; set; }
    }

    // Payment ViewModel
    public class PaymentViewModel
    {
        public int BookingOrderId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime? HoldExpiresAt { get; set; }
        public int RemainingSeconds => HoldExpiresAt.HasValue 
            ? Math.Max(0, (int)(HoldExpiresAt.Value - DateTime.UtcNow).TotalSeconds) 
            : 0;

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.OnlineBanking;
        public string SimulatedOutcome { get; set; } = "Success"; // Success, Rejected, Timeout
        public List<BookingDetail> Details { get; set; } = new();
        public Schedule Schedule { get; set; } = null!;
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string TransactionCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public PaymentStatus Status { get; set; }
    }

    // Ticket & Cancellation / Refund ViewModels
    public class CancellationCalculation
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public decimal OriginalPrice { get; set; }
        public decimal FeePercentage { get; set; }
        public decimal CancellationFee { get; set; }
        public decimal RefundableAmount { get; set; }
        public double HoursUntilDeparture { get; set; }
        public bool IsEligible { get; set; }
        public string PolicyDescription { get; set; } = string.Empty;
    }

    public class ChangeTicketCalculation
    {
        public int TicketId { get; set; }
        public decimal OldTicketPrice { get; set; }
        public decimal NewTicketPrice { get; set; }
        public decimal FareDifference { get; set; }
        public decimal ChangeFee { get; set; }
        public decimal TotalPayable { get; set; }
        public bool IsEligible { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class TicketChangeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? NewTicketId { get; set; }
        public string? NewTicketCode { get; set; }
    }

    public class RefundCalculationResult
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public decimal TicketPrice { get; set; }
        public decimal FeePercentage { get; set; }
        public decimal CancellationFee { get; set; }
        public decimal RefundableAmount { get; set; }
        public bool IsEligible { get; set; }
        public string PolicyDescription { get; set; } = string.Empty;
    }

    public class RefundResult
    {
        public bool Success { get; set; }
        public string RefundCode { get; set; } = string.Empty;
        public decimal RefundAmount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    // Reports ViewModels
    public class BookingReportViewModel
    {
        public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-30);
        public DateTime ToDate { get; set; } = DateTime.Today.AddDays(7);
        public int TotalBookings { get; set; }
        public int CompletedBookings { get; set; }
        public int PendingBookings { get; set; }
        public int ExpiredBookings { get; set; }
        public int TotalTicketsIssued { get; set; }
        public int TotalTicketsCancelled { get; set; }
        public int TotalTicketsRefunded { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalRefundedAmount { get; set; }
        public decimal NetRevenue => TotalRevenue - TotalRefundedAmount;
        public List<BookingOrder> RecentBookings { get; set; } = new();
    }

    public class RevenueReportItem
    {
        public string RouteName { get; set; } = string.Empty;
        public string TrainNumber { get; set; } = string.Empty;
        public int TicketsSold { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal Refunds { get; set; }
        public decimal NetRevenue => GrossRevenue - Refunds;
    }

    public class RevenueReportViewModel
    {
        public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-30);
        public DateTime ToDate { get; set; } = DateTime.Today.AddDays(7);
        public decimal TotalGrossRevenue { get; set; }
        public decimal TotalRefunds { get; set; }
        public decimal TotalNetRevenue => TotalGrossRevenue - TotalRefunds;
        public List<RevenueReportItem> RouteRevenues { get; set; } = new();
    }

    // Auth ViewModels
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full Name is required.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "National ID / Passport is required.")]
        [Display(Name = "National ID / Passport")]
        public string IdCard { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required.")]
        [Phone(ErrorMessage = "Invalid phone number.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class UserProfileViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string IdCard { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Address { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
