using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using RailwayTicketSales.Models.Enums;

namespace RailwayTicketSales.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? IdCard { get; set; }

        [MaxLength(200)]
        public string? Address { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(30)]
        public string UserType { get; set; } = "Passenger"; // Passenger, TicketClerk, Administrator

        public bool IsActive { get; set; } = true;

        public virtual ICollection<BookingOrder> BookingOrders { get; set; } = new List<BookingOrder>();
    }

    public class Station
    {
        [Key]
        public int StationId { get; set; }

        [Required, MaxLength(20)]
        public string StationCode { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string StationName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Address { get; set; }

        public virtual ICollection<Route> DepartureRoutes { get; set; } = new List<Route>();
        public virtual ICollection<Route> ArrivalRoutes { get; set; } = new List<Route>();
        public virtual ICollection<RouteStation> RouteStations { get; set; } = new List<RouteStation>();
    }

    public class Route
    {
        [Key]
        public int RouteId { get; set; }

        [Required, MaxLength(20)]
        public string RouteCode { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string RouteName { get; set; } = string.Empty;

        [ForeignKey("DepartureStation")]
        public int DepartureStationId { get; set; }
        public virtual Station? DepartureStation { get; set; }

        [ForeignKey("ArrivalStation")]
        public int ArrivalStationId { get; set; }
        public virtual Station? ArrivalStation { get; set; }

        [Column(TypeName = "decimal(8, 2)")]
        public decimal DistanceKm { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<RouteStation> RouteStations { get; set; } = new List<RouteStation>();
        public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
        public virtual ICollection<Fare> Fares { get; set; } = new List<Fare>();
    }

    public class RouteStation
    {
        [Key]
        public int RouteStationId { get; set; }

        [ForeignKey("Route")]
        public int RouteId { get; set; }
        public virtual Route? Route { get; set; }

        [ForeignKey("Station")]
        public int StationId { get; set; }
        public virtual Station? Station { get; set; }

        public int StopOrder { get; set; }

        [Column(TypeName = "decimal(8, 2)")]
        public decimal DistanceFromOriginKm { get; set; }

        public int StopDurationMinutes { get; set; }
    }

    public class Train
    {
        [Key]
        public int TrainId { get; set; }

        [Required, MaxLength(20)]
        public string TrainNumber { get; set; } = string.Empty; // e.g. SE1, SE2, TN1

        [Required, MaxLength(50)]
        public string TrainType { get; set; } = "Express"; // Express, Local, HighSpeed

        public int TotalCapacity { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = "Active";

        public virtual ICollection<Coach> Coaches { get; set; } = new List<Coach>();
        public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    }

    public class Coach
    {
        [Key]
        public int CoachId { get; set; }

        [ForeignKey("Train")]
        public int TrainId { get; set; }
        public virtual Train? Train { get; set; }

        public int CoachNumber { get; set; } // 1, 2, 3...

        [Required, MaxLength(50)]
        public string CoachType { get; set; } = "SoftSeat"; // HardSeat, SoftSeat, Sleeper

        public int TotalSeats { get; set; }

        public int FloorCount { get; set; } = 1;

        public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();
    }

    public class Seat
    {
        [Key]
        public int SeatId { get; set; }

        [ForeignKey("Coach")]
        public int CoachId { get; set; }
        public virtual Coach? Coach { get; set; }

        [Required, MaxLength(10)]
        public string SeatNumber { get; set; } = string.Empty; // e.g. 01, 02, A1, B2

        public SeatType SeatType { get; set; } = SeatType.SoftSeat;

        public int Floor { get; set; } = 1;

        public SeatStatus CurrentStatus { get; set; } = SeatStatus.Available;

        public virtual ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }

    public class Schedule
    {
        [Key]
        public int ScheduleId { get; set; }

        [Required, MaxLength(30)]
        public string TripCode { get; set; } = string.Empty; // e.g. SE1-20261112

        [ForeignKey("Train")]
        public int TrainId { get; set; }
        public virtual Train? Train { get; set; }

        [ForeignKey("Route")]
        public int RouteId { get; set; }
        public virtual Route? Route { get; set; }

        public DateTime DepartureTime { get; set; }

        public DateTime ArrivalTime { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal BasePrice { get; set; }

        public TripStatus Status { get; set; } = TripStatus.BookingOpen;

        public virtual ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }

    public class Fare
    {
        [Key]
        public int FareId { get; set; }

        [ForeignKey("Route")]
        public int RouteId { get; set; }
        public virtual Route? Route { get; set; }

        [MaxLength(50)]
        public string CoachType { get; set; } = "SoftSeat";

        public SeatType SeatType { get; set; } = SeatType.SoftSeat;

        [Column(TypeName = "decimal(12, 2)")]
        public decimal AdditionalPrice { get; set; } = 0;

        [Column(TypeName = "decimal(5, 2)")]
        public decimal Multiplier { get; set; } = 1.00m;

        public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    }

    public class BookingOrder
    {
        [Key]
        public int BookingOrderId { get; set; }

        [Required, MaxLength(30)]
        public string BookingCode { get; set; } = string.Empty; // e.g. BK-20261111-XXXX

        [ForeignKey("User")]
        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        [Required, MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required, MaxLength(100), EmailAddress]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string CustomerPhone { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? CustomerIdCard { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal TotalAmount { get; set; }

        public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;

        public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;

        public DateTime? HoldExpiresAt { get; set; } // BR-04 10-minute hold

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.OnlineBanking;

        public bool IsCounterBooking { get; set; } = false;

        [ForeignKey("Clerk")]
        public string? ClerkId { get; set; }
        public virtual ApplicationUser? Clerk { get; set; }

        public virtual ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
        public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
    }

    public class BookingDetail
    {
        [Key]
        public int BookingDetailId { get; set; }

        [ForeignKey("BookingOrder")]
        public int BookingOrderId { get; set; }
        public virtual BookingOrder? BookingOrder { get; set; }

        [ForeignKey("Schedule")]
        public int ScheduleId { get; set; }
        public virtual Schedule? Schedule { get; set; }

        [ForeignKey("Seat")]
        public int SeatId { get; set; }
        public virtual Seat? Seat { get; set; }

        [Required, MaxLength(100)]
        public string PassengerName { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string PassengerIdCard { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PassengerPhone { get; set; }

        [MaxLength(30)]
        public string PassengerType { get; set; } = "Adult"; // Adult, Child, Senior, Student

        [Column(TypeName = "decimal(12, 2)")]
        public decimal Price { get; set; }

        public virtual Ticket? Ticket { get; set; }
    }

    public class Ticket
    {
        [Key]
        public int TicketId { get; set; }

        [Required, MaxLength(30)]
        public string TicketCode { get; set; } = string.Empty; // e.g. TKT-20261111-XXXX

        [ForeignKey("BookingOrder")]
        public int BookingOrderId { get; set; }
        public virtual BookingOrder? BookingOrder { get; set; }

        [ForeignKey("BookingDetail")]
        public int BookingDetailId { get; set; }
        public virtual BookingDetail? BookingDetail { get; set; }

        [ForeignKey("Schedule")]
        public int ScheduleId { get; set; }
        public virtual Schedule? Schedule { get; set; }

        [ForeignKey("Seat")]
        public int SeatId { get; set; }
        public virtual Seat? Seat { get; set; }

        [Required, MaxLength(100)]
        public string PassengerName { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string PassengerIdCard { get; set; } = string.Empty;

        [MaxLength(30)]
        public string PassengerType { get; set; } = "Adult";

        [Column(TypeName = "decimal(12, 2)")]
        public decimal TicketPrice { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal InsuranceFee { get; set; } = 5000;

        [MaxLength(500)]
        public string? QrCodeData { get; set; }

        public TicketStatus TicketStatus { get; set; } = TicketStatus.ActivePaid;

        public DateTime IssuedDate { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();
    }

    public class PaymentTransaction
    {
        [Key]
        public int TransactionId { get; set; }

        [ForeignKey("BookingOrder")]
        public int BookingOrderId { get; set; }
        public virtual BookingOrder? BookingOrder { get; set; }

        [Required, MaxLength(50)]
        public string TransactionCode { get; set; } = string.Empty;

        [Column(TypeName = "decimal(12, 2)")]
        public decimal Amount { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        public PaymentStatus Status { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? PaymentGatewayResponse { get; set; }

        [MaxLength(300)]
        public string? ErrorMessage { get; set; }
    }

    public class RefundPolicy
    {
        [Key]
        public int RefundPolicyId { get; set; }

        public int HoursBeforeDeparture { get; set; } // e.g. 24 hours, 4 hours

        [Column(TypeName = "decimal(5, 2)")]
        public decimal FeePercentage { get; set; } // e.g. 10.00 (%)

        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }

    public class Refund
    {
        [Key]
        public int RefundId { get; set; }

        [ForeignKey("Ticket")]
        public int TicketId { get; set; }
        public virtual Ticket? Ticket { get; set; }

        [Required, MaxLength(30)]
        public string RefundCode { get; set; } = string.Empty;

        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedDate { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal TicketPrice { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal CancellationFee { get; set; }

        [Column(TypeName = "decimal(12, 2)")]
        public decimal RefundAmount { get; set; }

        public RefundStatus RefundStatus { get; set; } = RefundStatus.Pending;

        [MaxLength(300)]
        public string? Reason { get; set; }

        [ForeignKey("ProcessedByClerk")]
        public string? ProcessedByClerkId { get; set; }
        public virtual ApplicationUser? ProcessedByClerk { get; set; }

        [MaxLength(100)]
        public string? TransactionReference { get; set; }
    }

    public class NotificationLog
    {
        [Key]
        public int NotificationLogId { get; set; }

        [MaxLength(100)]
        public string? RecipientEmail { get; set; }

        [MaxLength(20)]
        public string? RecipientPhone { get; set; }

        [Required, MaxLength(50)]
        public string NotificationType { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string MessageBody { get; set; } = string.Empty;

        public DateTime SentDate { get; set; } = DateTime.UtcNow;

        [Required, MaxLength(20)]
        public string DeliveryStatus { get; set; } = "Sent"; // Sent, Failed, Pending

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }
    }
}
