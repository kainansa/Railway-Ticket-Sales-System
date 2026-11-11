using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RailwayTicketSales.Data;
using RailwayTicketSales.Models.Entities;
using RailwayTicketSales.Models.Enums;
using RailwayTicketSales.Models.ViewModels;
using Route = RailwayTicketSales.Models.Entities.Route;

namespace RailwayTicketSales.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // Admin Dashboard
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalTrains = await _context.Trains.CountAsync();
            ViewBag.TotalStations = await _context.Stations.CountAsync();
            ViewBag.TotalRoutes = await _context.Routes.CountAsync();
            ViewBag.ActiveSchedules = await _context.Schedules.Where(s => s.Status == TripStatus.BookingOpen).CountAsync();
            ViewBag.TotalUsers = await _userManager.Users.CountAsync();

            var completedOrders = await _context.BookingOrders.Where(b => b.OrderStatus == OrderStatus.Completed).ToListAsync();
            ViewBag.TotalRevenue = completedOrders.Sum(b => b.TotalAmount);
            ViewBag.TotalTickets = await _context.Tickets.CountAsync();

            var now = DateTime.UtcNow;
            ViewBag.ActiveHolds = await _context.BookingOrders
                .Where(b => (b.OrderStatus == OrderStatus.Pending || b.OrderStatus == OrderStatus.AwaitingPayment)
                            && b.HoldExpiresAt > now)
                .CountAsync();

            var recentBookings = await _context.BookingOrders
                .Include(b => b.Tickets)
                .OrderByDescending(b => b.CreatedDateTime)
                .Take(8)
                .ToListAsync();

            return View(recentBookings);
        }

        #region Train Management
        public async Task<IActionResult> Trains()
        {
            var trains = await _context.Trains
                .Include(t => t.Coaches)
                .Include(t => t.Schedules)
                .ToListAsync();
            return View(trains);
        }

        [HttpGet]
        public IActionResult CreateTrain() => View(new Train());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTrain(Train train)
        {
            if (!ModelState.IsValid) return View(train);

            if (await _context.Trains.AnyAsync(t => t.TrainNumber == train.TrainNumber))
            {
                ModelState.AddModelError("TrainNumber", "A train with this number already exists.");
                return View(train);
            }

            _context.Trains.Add(train);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Train {train.TrainNumber} added successfully.";
            return RedirectToAction(nameof(Trains));
        }

        [HttpGet]
        public async Task<IActionResult> EditTrain(int id)
        {
            var train = await _context.Trains.FindAsync(id);
            if (train == null) return NotFound();
            return View(train);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTrain(Train train)
        {
            if (!ModelState.IsValid) return View(train);

            _context.Trains.Update(train);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Train {train.TrainNumber} updated successfully.";
            return RedirectToAction(nameof(Trains));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTrain(int id)
        {
            var train = await _context.Trains
                .Include(t => t.Schedules)
                .FirstOrDefaultAsync(t => t.TrainId == id);

            if (train == null) return NotFound();

            if (train.Schedules.Any())
            {
                TempData["ErrorMessage"] = "Cannot delete train because it is assigned to existing schedules.";
                return RedirectToAction(nameof(Trains));
            }

            _context.Trains.Remove(train);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Train removed successfully.";
            return RedirectToAction(nameof(Trains));
        }
        #endregion

        #region Station Management
        public async Task<IActionResult> Stations()
        {
            var stations = await _context.Stations
                .Include(s => s.DepartureRoutes)
                .Include(s => s.ArrivalRoutes)
                .ToListAsync();
            return View(stations);
        }

        [HttpGet]
        public IActionResult CreateStation() => View(new Station());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStation(Station station)
        {
            if (!ModelState.IsValid) return View(station);

            if (await _context.Stations.AnyAsync(s => s.StationCode == station.StationCode))
            {
                ModelState.AddModelError("StationCode", "Station code already exists.");
                return View(station);
            }

            _context.Stations.Add(station);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Station {station.StationName} created successfully.";
            return RedirectToAction(nameof(Stations));
        }

        [HttpGet]
        public async Task<IActionResult> EditStation(int id)
        {
            var station = await _context.Stations.FindAsync(id);
            if (station == null) return NotFound();
            return View(station);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStation(Station station)
        {
            if (!ModelState.IsValid) return View(station);

            _context.Stations.Update(station);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Station {station.StationName} updated successfully.";
            return RedirectToAction(nameof(Stations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStation(int id)
        {
            var station = await _context.Stations
                .Include(s => s.DepartureRoutes)
                .Include(s => s.ArrivalRoutes)
                .FirstOrDefaultAsync(s => s.StationId == id);

            if (station == null) return NotFound();

            if (station.DepartureRoutes.Any() || station.ArrivalRoutes.Any())
            {
                TempData["ErrorMessage"] = "Cannot delete station because it is part of existing routes.";
                return RedirectToAction(nameof(Stations));
            }

            _context.Stations.Remove(station);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Station removed successfully.";
            return RedirectToAction(nameof(Stations));
        }
        #endregion

        #region Route Management
        public async Task<IActionResult> Routes()
        {
            var routes = await _context.Routes
                .Include(r => r.DepartureStation)
                .Include(r => r.ArrivalStation)
                .Include(r => r.Schedules)
                .ToListAsync();
            return View(routes);
        }

        [HttpGet]
        public async Task<IActionResult> CreateRoute()
        {
            ViewBag.Stations = await _context.Stations.OrderBy(s => s.StationName).ToListAsync();
            return View(new Route());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRoute(Route route)
        {
            if (route.DepartureStationId == route.ArrivalStationId)
            {
                ModelState.AddModelError("ArrivalStationId", "Departure and arrival stations cannot be the same.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Stations = await _context.Stations.OrderBy(s => s.StationName).ToListAsync();
                return View(route);
            }

            _context.Routes.Add(route);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Route {route.RouteName} created successfully.";
            return RedirectToAction(nameof(Routes));
        }

        [HttpGet]
        public async Task<IActionResult> EditRoute(int id)
        {
            var route = await _context.Routes.FindAsync(id);
            if (route == null) return NotFound();
            ViewBag.Stations = await _context.Stations.OrderBy(s => s.StationName).ToListAsync();
            return View(route);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoute(Route route)
        {
            if (route.DepartureStationId == route.ArrivalStationId)
            {
                ModelState.AddModelError("ArrivalStationId", "Departure and arrival stations cannot be the same.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Stations = await _context.Stations.OrderBy(s => s.StationName).ToListAsync();
                return View(route);
            }

            _context.Routes.Update(route);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Route {route.RouteName} updated successfully.";
            return RedirectToAction(nameof(Routes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoute(int id)
        {
            var route = await _context.Routes.Include(r => r.Schedules).FirstOrDefaultAsync(r => r.RouteId == id);
            if (route == null) return NotFound();

            if (route.Schedules.Any())
            {
                TempData["ErrorMessage"] = "Cannot delete route with assigned schedules.";
                return RedirectToAction(nameof(Routes));
            }

            _context.Routes.Remove(route);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Route deleted successfully.";
            return RedirectToAction(nameof(Routes));
        }
        #endregion

        #region Schedule / Trip Management
        public async Task<IActionResult> Schedules()
        {
            var schedules = await _context.Schedules
                .Include(s => s.Train)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.DepartureStation)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.ArrivalStation)
                .OrderByDescending(s => s.DepartureTime)
                .ToListAsync();
            return View(schedules);
        }

        [HttpGet]
        public async Task<IActionResult> CreateSchedule()
        {
            ViewBag.Trains = await _context.Trains.Where(t => t.Status == "Active").ToListAsync();
            ViewBag.Routes = await _context.Routes.Include(r => r.DepartureStation).Include(r => r.ArrivalStation).ToListAsync();
            return View(new Schedule { DepartureTime = DateTime.Today.AddDays(1).AddHours(8), ArrivalTime = DateTime.Today.AddDays(1).AddHours(16), BasePrice = 500000 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSchedule(Schedule schedule)
        {
            if (schedule.ArrivalTime <= schedule.DepartureTime)
            {
                ModelState.AddModelError("ArrivalTime", "Arrival time must be after departure time.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Trains = await _context.Trains.Where(t => t.Status == "Active").ToListAsync();
                ViewBag.Routes = await _context.Routes.Include(r => r.DepartureStation).Include(r => r.ArrivalStation).ToListAsync();
                return View(schedule);
            }

            _context.Schedules.Add(schedule);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Trip {schedule.TripCode} scheduled successfully.";
            return RedirectToAction(nameof(Schedules));
        }

        [HttpGet]
        public async Task<IActionResult> EditSchedule(int id)
        {
            var schedule = await _context.Schedules.FindAsync(id);
            if (schedule == null) return NotFound();

            ViewBag.Trains = await _context.Trains.Where(t => t.Status == "Active").ToListAsync();
            ViewBag.Routes = await _context.Routes.Include(r => r.DepartureStation).Include(r => r.ArrivalStation).ToListAsync();
            return View(schedule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSchedule(Schedule schedule)
        {
            if (schedule.ArrivalTime <= schedule.DepartureTime)
            {
                ModelState.AddModelError("ArrivalTime", "Arrival time must be after departure time.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Trains = await _context.Trains.Where(t => t.Status == "Active").ToListAsync();
                ViewBag.Routes = await _context.Routes.Include(r => r.DepartureStation).Include(r => r.ArrivalStation).ToListAsync();
                return View(schedule);
            }

            _context.Schedules.Update(schedule);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Trip {schedule.TripCode} updated successfully.";
            return RedirectToAction(nameof(Schedules));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeScheduleStatus(int scheduleId, TripStatus status)
        {
            var schedule = await _context.Schedules.FindAsync(scheduleId);
            if (schedule == null) return NotFound();

            schedule.Status = status;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Trip {schedule.TripCode} status changed to {status}.";
            return RedirectToAction(nameof(Schedules));
        }
        #endregion

        #region Coach & Seat Management
        public async Task<IActionResult> Coaches(int? trainId)
        {
            var query = _context.Coaches
                .Include(c => c.Train)
                .Include(c => c.Seats)
                .AsQueryable();

            if (trainId.HasValue)
            {
                query = query.Where(c => c.TrainId == trainId.Value);
                ViewBag.SelectedTrain = await _context.Trains.FindAsync(trainId.Value);
            }

            ViewBag.Trains = await _context.Trains.ToListAsync();
            var coaches = await query.OrderBy(c => c.Train!.TrainNumber).ThenBy(c => c.CoachNumber).ToListAsync();
            return View(coaches);
        }

        [HttpGet]
        public async Task<IActionResult> CreateCoach(int? trainId)
        {
            ViewBag.Trains = await _context.Trains.ToListAsync();
            return View(new Coach { TrainId = trainId ?? 0, CoachNumber = 1, TotalSeats = 28, CoachType = "SoftSeat" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCoach(Coach coach)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Trains = await _context.Trains.ToListAsync();
                return View(coach);
            }

            _context.Coaches.Add(coach);
            await _context.SaveChangesAsync();

            // Auto-generate seats for this coach
            SeatType seatType = coach.CoachType switch
            {
                "HardSeat" => SeatType.HardSeat,
                "SoftBerth" => SeatType.SoftBerth,
                "HardBerth" => SeatType.HardBerth,
                _ => SeatType.SoftSeat
            };

            var seats = new List<Seat>();
            for (int i = 1; i <= coach.TotalSeats; i++)
            {
                seats.Add(new Seat
                {
                    CoachId = coach.CoachId,
                    SeatNumber = $"{coach.CoachNumber}-{i:D2}",
                    SeatType = seatType,
                    Floor = (coach.FloorCount > 1 && i % 2 == 0) ? 2 : 1,
                    CurrentStatus = SeatStatus.Available
                });
            }
            await _context.Seats.AddRangeAsync(seats);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Coach {coach.CoachNumber} and {coach.TotalSeats} seats created.";
            return RedirectToAction(nameof(Coaches), new { trainId = coach.TrainId });
        }

        public async Task<IActionResult> Seats(int coachId)
        {
            var coach = await _context.Coaches
                .Include(c => c.Train)
                .Include(c => c.Seats)
                .FirstOrDefaultAsync(c => c.CoachId == coachId);

            if (coach == null) return NotFound();

            return View(coach);
        }
        #endregion

        #region Fare Management
        public async Task<IActionResult> Fares()
        {
            var fares = await _context.Fares
                .Include(f => f.Route)
                .ToListAsync();
            return View(fares);
        }

        [HttpGet]
        public async Task<IActionResult> CreateFare()
        {
            ViewBag.Routes = await _context.Routes.ToListAsync();
            return View(new Fare());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFare(Fare fare)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Routes = await _context.Routes.ToListAsync();
                return View(fare);
            }

            _context.Fares.Add(fare);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Fare configuration created successfully.";
            return RedirectToAction(nameof(Fares));
        }

        [HttpGet]
        public async Task<IActionResult> EditFare(int id)
        {
            var fare = await _context.Fares.FindAsync(id);
            if (fare == null) return NotFound();
            ViewBag.Routes = await _context.Routes.ToListAsync();
            return View(fare);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFare(Fare fare)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Routes = await _context.Routes.ToListAsync();
                return View(fare);
            }

            _context.Fares.Update(fare);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Fare configuration updated.";
            return RedirectToAction(nameof(Fares));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFare(int id)
        {
            var fare = await _context.Fares.FindAsync(id);
            if (fare == null) return NotFound();

            _context.Fares.Remove(fare);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Fare configuration deleted.";
            return RedirectToAction(nameof(Fares));
        }
        #endregion

        #region User Management
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();
            var userRoles = new Dictionary<string, string>();

            foreach (var u in users)
            {
                var r = await _userManager.GetRolesAsync(u);
                userRoles[u.Id] = r.FirstOrDefault() ?? "Passenger";
            }

            ViewBag.UserRoles = userRoles;
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = $"User {user.Email} status updated to {(user.IsActive ? "Active" : "Locked")}.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public IActionResult CreateStaff() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(RegisterViewModel model, string staffRole)
        {
            if (string.IsNullOrEmpty(staffRole)) staffRole = "TicketClerk";

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                IdCard = model.IdCard,
                PhoneNumber = model.PhoneNumber,
                Address = model.Address,
                UserType = staffRole,
                IsActive = true
            };

            var res = await _userManager.CreateAsync(user, model.Password);
            if (res.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, staffRole);
                TempData["SuccessMessage"] = $"Staff member {user.FullName} ({staffRole}) created successfully.";
                return RedirectToAction(nameof(Users));
            }

            foreach (var err in res.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }
            return View(model);
        }
        #endregion

        #region Reports & System Monitoring
        public async Task<IActionResult> BookingReports(DateTime? from, DateTime? to)
        {
            var fromDate = from ?? DateTime.Today.AddDays(-30);
            var toDate = (to ?? DateTime.Today.AddDays(7)).Date.AddDays(1);

            var orders = await _context.BookingOrders
                .Include(b => b.Tickets)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Schedule)
                        .ThenInclude(s => s!.Train)
                .Where(b => b.CreatedDateTime >= fromDate && b.CreatedDateTime < toDate)
                .OrderByDescending(b => b.CreatedDateTime)
                .ToListAsync();

            var tickets = await _context.Tickets
                .Where(t => t.IssuedDate >= fromDate && t.IssuedDate < toDate)
                .ToListAsync();

            var refunds = await _context.Refunds
                .Where(r => r.RequestDate >= fromDate && r.RequestDate < toDate && r.RefundStatus == RefundStatus.Completed)
                .ToListAsync();

            var model = new BookingReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate.AddDays(-1),
                TotalBookings = orders.Count,
                CompletedBookings = orders.Count(o => o.OrderStatus == OrderStatus.Completed),
                PendingBookings = orders.Count(o => o.OrderStatus == OrderStatus.Pending || o.OrderStatus == OrderStatus.AwaitingPayment),
                ExpiredBookings = orders.Count(o => o.OrderStatus == OrderStatus.Expired),
                TotalTicketsIssued = tickets.Count,
                TotalTicketsCancelled = tickets.Count(t => t.TicketStatus == TicketStatus.Cancelled),
                TotalTicketsRefunded = tickets.Count(t => t.TicketStatus == TicketStatus.Refunded),
                TotalRevenue = orders.Where(o => o.OrderStatus == OrderStatus.Completed).Sum(o => o.TotalAmount),
                TotalRefundedAmount = refunds.Sum(r => r.RefundAmount),
                RecentBookings = orders.Take(25).ToList()
            };

            return View(model);
        }

        public async Task<IActionResult> RevenueReports(DateTime? from, DateTime? to)
        {
            var fromDate = from ?? DateTime.Today.AddDays(-30);
            var toDate = (to ?? DateTime.Today.AddDays(7)).Date.AddDays(1);

            var tickets = await _context.Tickets
                .Include(t => t.Schedule)
                    .ThenInclude(s => s!.Route)
                .Include(t => t.Schedule)
                    .ThenInclude(s => s!.Train)
                .Include(t => t.Refunds)
                .Where(t => t.IssuedDate >= fromDate && t.IssuedDate < toDate)
                .ToListAsync();

            var routeGroups = tickets
                .GroupBy(t => new { Route = t.Schedule?.Route?.RouteName ?? "Unknown Route", Train = t.Schedule?.Train?.TrainNumber ?? "Unknown Train" })
                .Select(g => new RevenueReportItem
                {
                    RouteName = g.Key.Route,
                    TrainNumber = g.Key.Train,
                    TicketsSold = g.Count(t => t.TicketStatus == TicketStatus.ActivePaid || t.TicketStatus == TicketStatus.Refunded),
                    GrossRevenue = g.Sum(t => t.TicketPrice + t.InsuranceFee),
                    Refunds = g.SelectMany(t => t.Refunds).Where(r => r.RefundStatus == RefundStatus.Completed).Sum(r => r.RefundAmount)
                })
                .ToList();

            var model = new RevenueReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate.AddDays(-1),
                TotalGrossRevenue = routeGroups.Sum(rg => rg.GrossRevenue),
                TotalRefunds = routeGroups.Sum(rg => rg.Refunds),
                RouteRevenues = routeGroups
            };

            return View(model);
        }

        public async Task<IActionResult> SystemMonitor()
        {
            var now = DateTime.UtcNow;

            ViewBag.ActiveHolds = await _context.BookingOrders
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Schedule)
                .Where(b => (b.OrderStatus == OrderStatus.Pending || b.OrderStatus == OrderStatus.AwaitingPayment)
                            && b.HoldExpiresAt > now)
                .ToListAsync();

            ViewBag.ExpiredHolds = await _context.BookingOrders
                .Where(b => b.OrderStatus == OrderStatus.Expired)
                .OrderByDescending(b => b.CreatedDateTime)
                .Take(10)
                .ToListAsync();

            ViewBag.RecentLogs = await _context.NotificationLogs
                .OrderByDescending(l => l.SentDate)
                .Take(25)
                .ToListAsync();

            ViewBag.RecentTransactions = await _context.PaymentTransactions
                .Include(p => p.BookingOrder)
                .OrderByDescending(p => p.TransactionDate)
                .Take(25)
                .ToListAsync();

            return View();
        }
        #endregion
    }
}
