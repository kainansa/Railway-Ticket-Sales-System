using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RailwayTicketSales.Data;
using RailwayTicketSales.Models;
using RailwayTicketSales.Models.ViewModels;
using RailwayTicketSales.Services.Interfaces;

namespace RailwayTicketSales.Controllers
{
    public class HomeController : Controller
    {
        private readonly ITrainService _trainService;
        private readonly ApplicationDbContext _context;

        public HomeController(ITrainService trainService, ApplicationDbContext context)
        {
            _trainService = trainService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var stations = await _trainService.GetAllStationsAsync();
            var model = new TrainSearchViewModel
            {
                Stations = stations,
                TravelDate = DateTime.Today.AddDays(1)
            };

            // Featured upcoming schedules
            ViewBag.FeaturedSchedules = await _context.Schedules
                .Include(s => s.Train)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.DepartureStation)
                .Include(s => s.Route)
                    .ThenInclude(r => r!.ArrivalStation)
                .Where(s => s.DepartureTime > DateTime.Now)
                .OrderBy(s => s.DepartureTime)
                .Take(6)
                .ToListAsync();

            return View(model);
        }

        public async Task<IActionResult> RefundPolicy()
        {
            var policies = await _context.RefundPolicies
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.HoursBeforeDeparture)
                .ToListAsync();
            return View(policies);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
