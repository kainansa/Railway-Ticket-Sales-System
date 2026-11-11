using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RailwayTicketSales.Models.Entities;
using RailwayTicketSales.Models.Enums;
using Route = RailwayTicketSales.Models.Entities.Route;

namespace RailwayTicketSales.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Seed Roles
            string[] roles = ["Administrator", "TicketClerk", "Passenger"];
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Seed Users
            // Admin
            var adminEmail = "admin@railway.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "System Administrator",
                    IdCard = "001099000001",
                    PhoneNumber = "0901234567",
                    Address = "118 Le Duan, Hoan Kiem, Hanoi",
                    UserType = "Administrator",
                    IsActive = true
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrator");
                }
            }

            // Clerk
            var clerkEmail = "clerk@railway.com";
            var clerkUser = await userManager.FindByEmailAsync(clerkEmail);
            if (clerkUser == null)
            {
                clerkUser = new ApplicationUser
                {
                    UserName = clerkEmail,
                    Email = clerkEmail,
                    EmailConfirmed = true,
                    FullName = "Station Ticket Clerk",
                    IdCard = "001099000002",
                    PhoneNumber = "0912345678",
                    Address = "Hanoi Railway Station Counter 01",
                    UserType = "TicketClerk",
                    IsActive = true
                };
                var result = await userManager.CreateAsync(clerkUser, "Clerk@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(clerkUser, "TicketClerk");
                }
            }

            // Passenger
            var passengerEmail = "passenger@railway.com";
            var passengerUser = await userManager.FindByEmailAsync(passengerEmail);
            if (passengerUser == null)
            {
                passengerUser = new ApplicationUser
                {
                    UserName = passengerEmail,
                    Email = passengerEmail,
                    EmailConfirmed = true,
                    FullName = "Nguyen Van An",
                    IdCard = "001201000123",
                    PhoneNumber = "0987654321",
                    Address = "25 Trang Thi, Hoan Kiem, Hanoi",
                    UserType = "Passenger",
                    IsActive = true
                };
                var result = await userManager.CreateAsync(passengerUser, "Passenger@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(passengerUser, "Passenger");
                }
            }

            // 3. Seed Stations (at least 8 stations)
            if (!await context.Stations.AnyAsync())
            {
                var stations = new List<Station>
                {
                    new() { StationCode = "HAN", StationName = "Ha Noi Station", City = "Ha Noi", Address = "120 Le Duan, Dong Da, Ha Noi" },
                    new() { StationCode = "VIN", StationName = "Vinh Station", City = "Nghe An", Address = "1 Le Ninh, Quan Bau, Vinh" },
                    new() { StationCode = "DHO", StationName = "Dong Hoi Station", City = "Quang Binh", Address = "Thuan Ly, Dong Hoi" },
                    new() { StationCode = "HUE", StationName = "Hue Station", City = "Thua Thien Hue", Address = "02 Bui Thi Xuan, Hue" },
                    new() { StationCode = "DAN", StationName = "Da Nang Station", City = "Da Nang", Address = "200 Hai Phong, Thanh Khe, Da Nang" },
                    new() { StationCode = "NHA", StationName = "Nha Trang Station", City = "Khanh Hoa", Address = "17 Thai Nguyen, Phuoc Tan, Nha Trang" },
                    new() { StationCode = "PTH", StationName = "Phan Thiet Station", City = "Binh Thuan", Address = "1 Le Duan, Phu Tai, Phan Thiet" },
                    new() { StationCode = "SGO", StationName = "Sai Gon Station", City = "Ho Chi Minh City", Address = "01 Nguyen Thong, Ward 9, District 3, HCM" }
                };
                await context.Stations.AddRangeAsync(stations);
                await context.SaveChangesAsync();
            }

            // 4. Seed Routes
            if (!await context.Routes.AnyAsync())
            {
                var stHan = await context.Stations.FirstAsync(s => s.StationCode == "HAN");
                var stSgo = await context.Stations.FirstAsync(s => s.StationCode == "SGO");
                var stDan = await context.Stations.FirstAsync(s => s.StationCode == "DAN");

                var routes = new List<Route>
                {
                    new() { RouteCode = "HAN-SGO", RouteName = "Ha Noi - Sai Gon (North-South)", DepartureStationId = stHan.StationId, ArrivalStationId = stSgo.StationId, DistanceKm = 1726, IsActive = true },
                    new() { RouteCode = "SGO-HAN", RouteName = "Sai Gon - Ha Noi (South-North)", DepartureStationId = stSgo.StationId, ArrivalStationId = stHan.StationId, DistanceKm = 1726, IsActive = true },
                    new() { RouteCode = "HAN-DAN", RouteName = "Ha Noi - Da Nang Express", DepartureStationId = stHan.StationId, ArrivalStationId = stDan.StationId, DistanceKm = 791, IsActive = true },
                    new() { RouteCode = "DAN-SGO", RouteName = "Da Nang - Sai Gon Express", DepartureStationId = stDan.StationId, ArrivalStationId = stSgo.StationId, DistanceKm = 935, IsActive = true }
                };
                await context.Routes.AddRangeAsync(routes);
                await context.SaveChangesAsync();
            }

            // 5. Seed Trains (at least 5 trains)
            if (!await context.Trains.AnyAsync())
            {
                var trains = new List<Train>
                {
                    new() { TrainNumber = "SE1", TrainType = "Express", TotalCapacity = 80, Status = "Active" },
                    new() { TrainNumber = "SE2", TrainType = "Express", TotalCapacity = 80, Status = "Active" },
                    new() { TrainNumber = "SE3", TrainType = "Express", TotalCapacity = 80, Status = "Active" },
                    new() { TrainNumber = "SE4", TrainType = "Express", TotalCapacity = 80, Status = "Active" },
                    new() { TrainNumber = "TN1", TrainType = "Local", TotalCapacity = 80, Status = "Active" }
                };
                await context.Trains.AddRangeAsync(trains);
                await context.SaveChangesAsync();

                // 6. Seed Coaches and Seats for each train
                foreach (var train in trains)
                {
                    // 3 Coaches per train: Coach 1 = SoftSeat, Coach 2 = HardSeat, Coach 3 = SoftBerth (Sleeper)
                    var coaches = new List<Coach>
                    {
                        new() { TrainId = train.TrainId, CoachNumber = 1, CoachType = "SoftSeat", TotalSeats = 28, FloorCount = 1 },
                        new() { TrainId = train.TrainId, CoachNumber = 2, CoachType = "HardSeat", TotalSeats = 28, FloorCount = 1 },
                        new() { TrainId = train.TrainId, CoachNumber = 3, CoachType = "SoftBerth", TotalSeats = 24, FloorCount = 2 }
                    };
                    await context.Coaches.AddRangeAsync(coaches);
                    await context.SaveChangesAsync();

                    foreach (var coach in coaches)
                    {
                        var seats = new List<Seat>();
                        SeatType seatType = coach.CoachType switch
                        {
                            "HardSeat" => SeatType.HardSeat,
                            "SoftBerth" => SeatType.SoftBerth,
                            _ => SeatType.SoftSeat
                        };

                        for (int i = 1; i <= coach.TotalSeats; i++)
                        {
                            int floor = coach.FloorCount > 1 ? ((i % 2 == 1) ? 1 : 2) : 1;
                            seats.Add(new Seat
                            {
                                CoachId = coach.CoachId,
                                SeatNumber = $"{coach.CoachNumber}-{i:D2}",
                                SeatType = seatType,
                                Floor = floor,
                                CurrentStatus = SeatStatus.Available
                            });
                        }
                        await context.Seats.AddRangeAsync(seats);
                    }
                    await context.SaveChangesAsync();
                }
            }

            // 7. Seed Fares
            if (!await context.Fares.AnyAsync())
            {
                var routes = await context.Routes.ToListAsync();
                var fares = new List<Fare>();
                foreach (var r in routes)
                {
                    fares.Add(new Fare { RouteId = r.RouteId, CoachType = "HardSeat", SeatType = SeatType.HardSeat, AdditionalPrice = 0, Multiplier = 1.0m });
                    fares.Add(new Fare { RouteId = r.RouteId, CoachType = "SoftSeat", SeatType = SeatType.SoftSeat, AdditionalPrice = 50000, Multiplier = 1.25m });
                    fares.Add(new Fare { RouteId = r.RouteId, CoachType = "SoftBerth", SeatType = SeatType.SoftBerth, AdditionalPrice = 150000, Multiplier = 1.6m });
                }
                await context.Fares.AddRangeAsync(fares);
                await context.SaveChangesAsync();
            }

            // 8. Seed Schedules / Trips
            if (!await context.Schedules.AnyAsync())
            {
                var rHanSgo = await context.Routes.FirstAsync(r => r.RouteCode == "HAN-SGO");
                var rSgoHan = await context.Routes.FirstAsync(r => r.RouteCode == "SGO-HAN");
                var rHanDan = await context.Routes.FirstAsync(r => r.RouteCode == "HAN-DAN");
                var rDanSgo = await context.Routes.FirstAsync(r => r.RouteCode == "DAN-SGO");

                var tSE1 = await context.Trains.FirstAsync(t => t.TrainNumber == "SE1");
                var tSE2 = await context.Trains.FirstAsync(t => t.TrainNumber == "SE2");
                var tSE3 = await context.Trains.FirstAsync(t => t.TrainNumber == "SE3");
                var tSE4 = await context.Trains.FirstAsync(t => t.TrainNumber == "SE4");
                var tTN1 = await context.Trains.FirstAsync(t => t.TrainNumber == "TN1");

                var now = DateTime.Today;

                var schedules = new List<Schedule>();

                // Schedules for next 7 days
                for (int day = 0; day <= 7; day++)
                {
                    var date = now.AddDays(day);

                    // SE1: HAN-SGO departing 06:00
                    schedules.Add(new Schedule
                    {
                        TripCode = $"SE1-{date:yyyyMMdd}",
                        TrainId = tSE1.TrainId,
                        RouteId = rHanSgo.RouteId,
                        DepartureTime = date.AddHours(6),
                        ArrivalTime = date.AddDays(1).AddHours(14),
                        BasePrice = 650000,
                        Status = TripStatus.BookingOpen
                    });

                    // SE2: SGO-HAN departing 19:30
                    schedules.Add(new Schedule
                    {
                        TripCode = $"SE2-{date:yyyyMMdd}",
                        TrainId = tSE2.TrainId,
                        RouteId = rSgoHan.RouteId,
                        DepartureTime = date.AddHours(19).AddMinutes(30),
                        ArrivalTime = date.AddDays(2).AddHours(4),
                        BasePrice = 650000,
                        Status = TripStatus.BookingOpen
                    });

                    // SE3: HAN-DAN departing 08:15
                    schedules.Add(new Schedule
                    {
                        TripCode = $"SE3-{date:yyyyMMdd}",
                        TrainId = tSE3.TrainId,
                        RouteId = rHanDan.RouteId,
                        DepartureTime = date.AddHours(8).AddMinutes(15),
                        ArrivalTime = date.AddHours(22).AddMinutes(30),
                        BasePrice = 380000,
                        Status = TripStatus.BookingOpen
                    });

                    // SE4: DAN-SGO departing 14:00
                    schedules.Add(new Schedule
                    {
                        TripCode = $"SE4-{date:yyyyMMdd}",
                        TrainId = tSE4.TrainId,
                        RouteId = rDanSgo.RouteId,
                        DepartureTime = date.AddHours(14),
                        ArrivalTime = date.AddDays(1).AddHours(4).AddMinutes(30),
                        BasePrice = 420000,
                        Status = TripStatus.BookingOpen
                    });

                    // TN1: HAN-SGO departing 13:10
                    schedules.Add(new Schedule
                    {
                        TripCode = $"TN1-{date:yyyyMMdd}",
                        TrainId = tTN1.TrainId,
                        RouteId = rHanSgo.RouteId,
                        DepartureTime = date.AddHours(13).AddMinutes(10),
                        ArrivalTime = date.AddDays(1).AddHours(23),
                        BasePrice = 520000,
                        Status = TripStatus.BookingOpen
                    });
                }

                await context.Schedules.AddRangeAsync(schedules);
                await context.SaveChangesAsync();
            }

            // 9. Seed Refund Policies
            if (!await context.RefundPolicies.AnyAsync())
            {
                var policies = new List<RefundPolicy>
                {
                    new()
                    {
                        HoursBeforeDeparture = 24,
                        FeePercentage = 10.00m,
                        Description = "Cancellation more than 24 hours before departure: 10% fee (90% refundable).",
                        IsActive = true
                    },
                    new()
                    {
                        HoursBeforeDeparture = 4,
                        FeePercentage = 20.00m,
                        Description = "Cancellation between 4 and 24 hours before departure: 20% fee (80% refundable).",
                        IsActive = true
                    },
                    new()
                    {
                        HoursBeforeDeparture = 0,
                        FeePercentage = 100.00m,
                        Description = "Cancellation less than 4 hours before departure: 100% cancellation penalty (Non-refundable).",
                        IsActive = true
                    }
                };
                await context.RefundPolicies.AddRangeAsync(policies);
                await context.SaveChangesAsync();
            }

            // 10. Seed a completed sample booking for passenger demo account so "My Bookings" has immediate data to test Change/Cancel/Refund!
            if (!await context.BookingOrders.AnyAsync())
            {
                var pUser = await userManager.FindByEmailAsync("passenger@railway.com");
                var schedule = await context.Schedules
                    .Include(s => s.Train)
                    .ThenInclude(t => t!.Coaches)
                    .ThenInclude(c => c.Seats)
                    .OrderByDescending(s => s.DepartureTime)
                    .FirstOrDefaultAsync(s => s.DepartureTime > DateTime.Now.AddDays(2));

                if (pUser != null && schedule?.Train != null)
                {
                    var coach = schedule.Train.Coaches.First();
                    var seat1 = coach.Seats.First();
                    var seat2 = coach.Seats.Skip(1).First();

                    var booking = new BookingOrder
                    {
                        BookingCode = $"BK-{DateTime.UtcNow:yyyyMMdd}-0001",
                        UserId = pUser.Id,
                        CustomerName = pUser.FullName,
                        CustomerEmail = pUser.Email ?? "passenger@railway.com",
                        CustomerPhone = pUser.PhoneNumber ?? "0987654321",
                        CustomerIdCard = pUser.IdCard,
                        TotalAmount = (schedule.BasePrice + 5000) * 2,
                        OrderStatus = OrderStatus.Completed,
                        CreatedDateTime = DateTime.UtcNow.AddHours(-2),
                        HoldExpiresAt = null,
                        PaymentMethod = PaymentMethod.OnlineBanking,
                        IsCounterBooking = false
                    };
                    await context.BookingOrders.AddAsync(booking);
                    await context.SaveChangesAsync();

                    var detail1 = new BookingDetail
                    {
                        BookingOrderId = booking.BookingOrderId,
                        ScheduleId = schedule.ScheduleId,
                        SeatId = seat1.SeatId,
                        PassengerName = "Nguyen Van An",
                        PassengerIdCard = "001201000123",
                        PassengerPhone = "0987654321",
                        PassengerType = "Adult",
                        Price = schedule.BasePrice
                    };
                    var detail2 = new BookingDetail
                    {
                        BookingOrderId = booking.BookingOrderId,
                        ScheduleId = schedule.ScheduleId,
                        SeatId = seat2.SeatId,
                        PassengerName = "Tran Thi Mai",
                        PassengerIdCard = "001202000456",
                        PassengerPhone = "0987654322",
                        PassengerType = "Adult",
                        Price = schedule.BasePrice
                    };
                    await context.BookingDetails.AddRangeAsync(detail1, detail2);
                    await context.SaveChangesAsync();

                    var ticket1 = new Ticket
                    {
                        TicketCode = $"TKT-{DateTime.UtcNow:yyyyMMdd}-0001",
                        BookingOrderId = booking.BookingOrderId,
                        BookingDetailId = detail1.BookingDetailId,
                        ScheduleId = schedule.ScheduleId,
                        SeatId = seat1.SeatId,
                        PassengerName = detail1.PassengerName,
                        PassengerIdCard = detail1.PassengerIdCard,
                        PassengerType = detail1.PassengerType,
                        TicketPrice = detail1.Price,
                        InsuranceFee = 5000,
                        TicketStatus = TicketStatus.ActivePaid,
                        IssuedDate = DateTime.UtcNow.AddHours(-2),
                        IsActive = true,
                        QrCodeData = $"RAILWAY|TKT-{DateTime.UtcNow:yyyyMMdd}-0001|{schedule.TripCode}|Seat:{seat1.SeatNumber}|Nguyen Van An"
                    };

                    var ticket2 = new Ticket
                    {
                        TicketCode = $"TKT-{DateTime.UtcNow:yyyyMMdd}-0002",
                        BookingOrderId = booking.BookingOrderId,
                        BookingDetailId = detail2.BookingDetailId,
                        ScheduleId = schedule.ScheduleId,
                        SeatId = seat2.SeatId,
                        PassengerName = detail2.PassengerName,
                        PassengerIdCard = detail2.PassengerIdCard,
                        PassengerType = detail2.PassengerType,
                        TicketPrice = detail2.Price,
                        InsuranceFee = 5000,
                        TicketStatus = TicketStatus.ActivePaid,
                        IssuedDate = DateTime.UtcNow.AddHours(-2),
                        IsActive = true,
                        QrCodeData = $"RAILWAY|TKT-{DateTime.UtcNow:yyyyMMdd}-0002|{schedule.TripCode}|Seat:{seat2.SeatNumber}|Tran Thi Mai"
                    };
                    await context.Tickets.AddRangeAsync(ticket1, ticket2);

                    var tx = new PaymentTransaction
                    {
                        BookingOrderId = booking.BookingOrderId,
                        TransactionCode = $"TXN-{DateTime.UtcNow:yyyyMMdd}-0001",
                        Amount = booking.TotalAmount,
                        PaymentMethod = PaymentMethod.OnlineBanking,
                        Status = PaymentStatus.Success,
                        TransactionDate = DateTime.UtcNow.AddHours(-2),
                        PaymentGatewayResponse = "SUCCESS: Transaction Approved"
                    };
                    await context.PaymentTransactions.AddAsync(tx);

                    var log = new NotificationLog
                    {
                        RecipientEmail = pUser.Email,
                        RecipientPhone = pUser.PhoneNumber,
                        NotificationType = "BookingConfirmation",
                        Subject = "Booking Confirmation - " + booking.BookingCode,
                        MessageBody = $"Dear Nguyen Van An, your booking {booking.BookingCode} for trip {schedule.TripCode} has been confirmed. Issued tickets: {ticket1.TicketCode}, {ticket2.TicketCode}.",
                        DeliveryStatus = "Sent",
                        SentDate = DateTime.UtcNow.AddHours(-2)
                    };
                    await context.NotificationLogs.AddAsync(log);

                    await context.SaveChangesAsync();
                }
            }
        }
    }
}
