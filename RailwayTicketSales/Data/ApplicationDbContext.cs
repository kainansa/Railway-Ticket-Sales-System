using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RailwayTicketSales.Models.Entities;
using Route = RailwayTicketSales.Models.Entities.Route;

namespace RailwayTicketSales.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Station> Stations => Set<Station>();
        public DbSet<Route> Routes => Set<Route>();
        public DbSet<RouteStation> RouteStations => Set<RouteStation>();
        public DbSet<Train> Trains => Set<Train>();
        public DbSet<Coach> Coaches => Set<Coach>();
        public DbSet<Seat> Seats => Set<Seat>();
        public DbSet<Schedule> Schedules => Set<Schedule>();
        public DbSet<Fare> Fares => Set<Fare>();
        public DbSet<BookingOrder> BookingOrders => Set<BookingOrder>();
        public DbSet<BookingDetail> BookingDetails => Set<BookingDetail>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
        public DbSet<RefundPolicy> RefundPolicies => Set<RefundPolicy>();
        public DbSet<Refund> Refunds => Set<Refund>();
        public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Route -> Station relationships
            builder.Entity<Route>()
                .HasOne(r => r.DepartureStation)
                .WithMany(s => s.DepartureRoutes)
                .HasForeignKey(r => r.DepartureStationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Route>()
                .HasOne(r => r.ArrivalStation)
                .WithMany(s => s.ArrivalRoutes)
                .HasForeignKey(r => r.ArrivalStationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure BookingOrder relationships
            builder.Entity<BookingOrder>()
                .HasOne(b => b.User)
                .WithMany(u => u.BookingOrders)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BookingOrder>()
                .HasOne(b => b.Clerk)
                .WithMany()
                .HasForeignKey(b => b.ClerkId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Ticket relationships
            builder.Entity<Ticket>()
                .HasOne(t => t.BookingDetail)
                .WithOne(d => d.Ticket)
                .HasForeignKey<Ticket>(t => t.BookingDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Ticket>()
                .HasOne(t => t.Schedule)
                .WithMany(s => s.Tickets)
                .HasForeignKey(t => t.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Ticket>()
                .HasOne(t => t.Seat)
                .WithMany(s => s.Tickets)
                .HasForeignKey(t => t.SeatId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure BookingDetail relationships
            builder.Entity<BookingDetail>()
                .HasOne(d => d.Schedule)
                .WithMany(s => s.BookingDetails)
                .HasForeignKey(d => d.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BookingDetail>()
                .HasOne(d => d.Seat)
                .WithMany(s => s.BookingDetails)
                .HasForeignKey(d => d.SeatId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Refund relationships
            builder.Entity<Refund>()
                .HasOne(r => r.Ticket)
                .WithMany(t => t.Refunds)
                .HasForeignKey(r => r.TicketId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Refund>()
                .HasOne(r => r.ProcessedByClerk)
                .WithMany()
                .HasForeignKey(r => r.ProcessedByClerkId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index for faster search and unique constraints
            builder.Entity<Station>().HasIndex(s => s.StationCode).IsUnique();
            builder.Entity<Route>().HasIndex(r => r.RouteCode).IsUnique();
            builder.Entity<Train>().HasIndex(t => t.TrainNumber).IsUnique();
            builder.Entity<Schedule>().HasIndex(s => s.TripCode).IsUnique();
            builder.Entity<BookingOrder>().HasIndex(b => b.BookingCode).IsUnique();
            builder.Entity<Ticket>().HasIndex(t => t.TicketCode).IsUnique();
            builder.Entity<Refund>().HasIndex(r => r.RefundCode).IsUnique();
        }
    }
}
