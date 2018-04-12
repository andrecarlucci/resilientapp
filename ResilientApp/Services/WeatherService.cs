using Newtonsoft.Json;
using ResilientApp.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace ResilientApp.Services {
    public class WeatherService : IWeatherService {

        private readonly Uri _uri;
        private readonly HttpClient _httpClient;

        public WeatherService(HttpClient httpClient) : this(httpClient, null) {

        }

        public WeatherService(HttpClient httpClient, Uri uri) {
            _httpClient = httpClient;
            _uri = uri;
        }

        public virtual async Task<List<WeatherForecast>> GetData() {
            var json = await _httpClient.GetStringAsync(_uri);
            return JsonConvert.DeserializeObject<List<WeatherForecast>>(json);
        }
    }
}
