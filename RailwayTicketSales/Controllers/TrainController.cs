using Microsoft.AspNetCore.Mvc;
using RailwayTicketSales.Models.ViewModels;
using RailwayTicketSales.Services.Interfaces;

namespace RailwayTicketSales.Controllers
{
    public class TrainController : Controller
    {
        private readonly ITrainService _trainService;

        public TrainController(ITrainService trainService)
        {
            _trainService = trainService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? departureStationId, int? arrivalStationId, DateTime? travelDate)
        {
            var stations = await _trainService.GetAllStationsAsync();
            var model = new TrainSearchViewModel
            {
                Stations = stations,
                TravelDate = travelDate ?? DateTime.Today.AddDays(1)
            };

            if (departureStationId.HasValue && arrivalStationId.HasValue)
            {
                model.DepartureStationId = departureStationId.Value;
                model.ArrivalStationId = arrivalStationId.Value;
                model.SearchResults = await _trainService.SearchSchedulesAsync(
                    departureStationId.Value,
                    arrivalStationId.Value,
                    model.TravelDate);
                model.HasSearched = true;
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var details = await _trainService.GetScheduleDetailsAsync(id);
            if (details == null)
            {
                return NotFound();
            }

            return View(details);
        }
    }
}
