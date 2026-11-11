using Microsoft.EntityFrameworkCore;
using RailwayTicketSales.Data;
using RailwayTicketSales.Models.Entities;
using RailwayTicketSales.Models.Enums;
using RailwayTicketSales.Models.ViewModels;
using RailwayTicketSales.Services.Interfaces;

namespace RailwayTicketSales.Services
{
    public class TrainService : ITrainService
    {
        private readonly ApplicationDbContext _context;

        public TrainService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Station>> GetAllStationsAsync()
        {
            return await _context.Stations.OrderBy(s => s.StationName).ToListAsync();
        }

        public async Task<List<ScheduleSearchResult>> SearchSchedulesAsync(int departureStationId, int arrivalStationId, DateTime travelDate)
        {
            var startOfDay = travelDate.Date;
            var endOfDay = startOfDay.AddDays(1);

            // Find matching routes where departureStation and arrivalStation match
            // Either direct route or via route stations
            var query = _context.Schedules
                .Include(s => s.Train)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.DepartureStation)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.ArrivalStation)
                .Include(s => s.Tickets)
                .Include(s => s.BookingDetails)
                    .ThenInclude(bd => bd.BookingOrder)
                .Where(s => s.DepartureTime >= startOfDay && s.DepartureTime < endOfDay
                            && s.Status != TripStatus.Completed
                            && s.Status != TripStatus.BookingClosed
                            && s.Route!.DepartureStationId == departureStationId
                            && s.Route!.ArrivalStationId == arrivalStationId);

            var schedules = await query.ToListAsync();

            var results = new List<ScheduleSearchResult>();
            var now = DateTime.UtcNow;

            foreach (var s in schedules)
            {
                // Calculate total seats from Train coaches
                int totalSeats = await _context.Seats
                    .Where(st => st.Coach!.TrainId == s.TrainId)
                    .CountAsync();

                // Booked tickets (ActivePaid)
                var bookedSeatIds = s.Tickets
                    .Where(t => t.TicketStatus == TicketStatus.ActivePaid)
                    .Select(t => t.SeatId)
                    .Distinct()
                    .ToList();

                // Held seats with valid hold
                var heldSeatIds = s.BookingDetails
                    .Where(bd => bd.BookingOrder != null
                                 && (bd.BookingOrder.OrderStatus == OrderStatus.Pending || bd.BookingOrder.OrderStatus == OrderStatus.AwaitingPayment)
                                 && bd.BookingOrder.HoldExpiresAt > now)
                    .Select(bd => bd.SeatId)
                    .Distinct()
                    .ToList();

                var unavailableCount = bookedSeatIds.Union(heldSeatIds).Count();
                var availableCount = Math.Max(0, totalSeats - unavailableCount);

                results.Add(new ScheduleSearchResult
                {
                    ScheduleId = s.ScheduleId,
                    TripCode = s.TripCode,
                    TrainNumber = s.Train?.TrainNumber ?? "N/A",
                    TrainType = s.Train?.TrainType ?? "Express",
                    RouteName = s.Route?.RouteName ?? "N/A",
                    DepartureStationName = s.Route?.DepartureStation?.StationName ?? "Departure",
                    ArrivalStationName = s.Route?.ArrivalStation?.StationName ?? "Arrival",
                    DepartureTime = s.DepartureTime,
                    ArrivalTime = s.ArrivalTime,
                    BasePrice = s.BasePrice,
                    Status = s.Status,
                    TotalSeats = totalSeats,
                    AvailableSeats = availableCount
                });
            }

            return results.OrderBy(r => r.DepartureTime).ToList();
        }

        public async Task<ScheduleDetailsViewModel?> GetScheduleDetailsAsync(int scheduleId)
        {
            var schedule = await _context.Schedules
                .Include(s => s.Train)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.DepartureStation)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.ArrivalStation)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.Fares)
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);

            if (schedule == null || schedule.Train == null)
                return null;

            var coaches = await _context.Coaches
                .Include(c => c.Seats)
                .Where(c => c.TrainId == schedule.TrainId)
                .OrderBy(c => c.CoachNumber)
                .ToListAsync();

            var now = DateTime.UtcNow;

            // Unavailable seats: Active tickets OR unexpired held orders
            var bookedSeatIds = await _context.Tickets
                .Where(t => t.ScheduleId == scheduleId && t.TicketStatus == TicketStatus.ActivePaid)
                .Select(t => t.SeatId)
                .ToListAsync();

            var heldSeatIds = await _context.BookingDetails
                .Where(bd => bd.ScheduleId == scheduleId
                             && bd.BookingOrder != null
                             && (bd.BookingOrder.OrderStatus == OrderStatus.Pending || bd.BookingOrder.OrderStatus == OrderStatus.AwaitingPayment)
                             && bd.BookingOrder.HoldExpiresAt > now)
                .Select(bd => bd.SeatId)
                .ToListAsync();

            var unavailableSeatIds = bookedSeatIds.Union(heldSeatIds).Distinct().ToList();

            var coachViewModels = new List<CoachSeatViewModel>();

            foreach (var coach in coaches)
            {
                var seatItems = new List<SeatDisplayItem>();
                foreach (var seat in coach.Seats.OrderBy(s => s.SeatNumber))
                {
                    var price = await CalculateFareAsync(schedule.RouteId, coach.CoachId, seat.SeatType, schedule.BasePrice);
                    seatItems.Add(new SeatDisplayItem
                    {
                        SeatId = seat.SeatId,
                        SeatNumber = seat.SeatNumber,
                        SeatType = seat.SeatType,
                        Floor = seat.Floor,
                        IsAvailable = !unavailableSeatIds.Contains(seat.SeatId),
                        Price = price
                    });
                }

                coachViewModels.Add(new CoachSeatViewModel
                {
                    Coach = coach,
                    Seats = seatItems
                });
            }

            return new ScheduleDetailsViewModel
            {
                Schedule = schedule,
                Coaches = coachViewModels,
                UnavailableSeatIds = unavailableSeatIds
            };
        }

        public async Task<decimal> CalculateFareAsync(int routeId, int coachId, SeatType seatType, decimal basePrice)
        {
            var coach = await _context.Coaches.FindAsync(coachId);
            var fare = await _context.Fares
                .FirstOrDefaultAsync(f => f.RouteId == routeId && f.CoachType == coach!.CoachType && f.SeatType == seatType);

            if (fare != null)
            {
                return (basePrice * fare.Multiplier) + fare.AdditionalPrice;
            }

            // Default calculation if specific fare rule not defined
            return seatType switch
            {
                SeatType.HardSeat => basePrice,
                SeatType.SoftSeat => basePrice * 1.25m,
                SeatType.HardBerth => basePrice * 1.45m,
                SeatType.SoftBerth => basePrice * 1.60m,
                SeatType.VIP => basePrice * 2.00m,
                _ => basePrice
            };
        }
    }
}
