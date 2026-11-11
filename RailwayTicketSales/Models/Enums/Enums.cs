namespace RailwayTicketSales.Models.Enums
{
    public enum TicketStatus
    {
        WaitingForPayment = 0,
        ReservingSeat = 1,
        VerifyingPayment = 2,
        ActivePaid = 3,
        Cancelled = 4,
        Refunded = 5
    }

    public enum OrderStatus
    {
        Pending = 0,
        AwaitingPayment = 1,
        AuthorizingTransaction = 2,
        Completed = 3,
        Expired = 4,
        Refunded = 5
    }

    public enum TripStatus
    {
        Scheduled = 0,
        BookingOpen = 1,
        SoldOut = 2,
        BookingClosed = 3,
        InTransit = 4,
        Completed = 5
    }

    public enum SeatType
    {
        HardSeat = 0,
        SoftSeat = 1,
        HardBerth = 2,
        SoftBerth = 3,
        VIP = 4
    }

    public enum SeatStatus
    {
        Available = 0,
        Held = 1,
        Booked = 2,
        Blocked = 3
    }

    public enum PaymentStatus
    {
        Pending = 0,
        Success = 1,
        Rejected = 2,
        Timeout = 3
    }

    public enum PaymentMethod
    {
        OnlineBanking = 0,
        VNPay_Mock = 1,
        CreditCard = 2,
        CashCounter = 3
    }

    public enum RefundStatus
    {
        Pending = 0,
        Approved = 1,
        Completed = 2,
        Rejected = 3
    }
}
