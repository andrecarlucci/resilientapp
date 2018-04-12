using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Linq;
using WheatherService.Models;

namespace WheatherService.Controllers {
    public class ForecastsController : Controller {

        private static string[] _summaries = {
            "Praticamente Sibéria",
            "Congelando",
            "Muito Frio",
            "Frio",
            "Agradável",
            "Quente",
            "Muito Quente",
            "Fervendo",
            "Inferno na Terra"
        };

        public ForecastsController() {

        }

        [HttpGet("api/forecasts")]
        public IActionResult Get() {
            var rng = new Random();
            var result = rng.Next(1, 5);
            Console.WriteLine("-> " + result);
            Trace.WriteLine("-> " + result);
            if (result == 1) {
                throw new Exception("BAD BAD SERVICE!");
            }
            return Ok(Enumerable.Range(1, 5).Select(index => new WeatherForecast {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = _summaries[rng.Next(_summaries.Length)],
                Source = Request.Host.Host
            }));
        }
    }
}
