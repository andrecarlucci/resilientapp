using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResilientApp.Models;
using ResilientApp.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ResilientApp.Pages {
    public class ForecastsModel : PageModel {
        public List<WeatherForecast> Forecasts { get; set; } = new List<WeatherForecast>();

        public async Task OnGet([FromServices] IWeatherService weatherService) {
            Forecasts = await weatherService.GetData();
        }
    }
}