using ResilientApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ResilientApp.Services {
    public interface IWeatherService {
        Task<List<WeatherForecast>> GetData();
    }
}
