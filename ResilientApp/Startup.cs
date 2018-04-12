using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using ResilientApp.Services;
using System;
using System.Net.Http;

namespace ResilientApp {
    public class Startup {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services) {
            services.AddSingleton<ServiceRouter>();
            var uri = new Uri(Configuration["WeatherServiceUrl"]);

            //ConfigureHttpClient(services, uri);
            
            
            //ConfigureViaHttpClientFactory(services, uri);
            //ConfigureViaHandler(services, uri);
            ConfigurePolly(services, uri);

            services.AddMvc()
                    .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        private void ConfigureHttpClient(IServiceCollection services, Uri url) {
            services.AddSingleton<HttpClient>();
            services.AddSingleton<IWeatherService>(c => new WeatherService(c.GetService<HttpClient>(), url));
        }

        private void ConfigureViaHttpClientFactory(IServiceCollection services, Uri uri) {
            services.AddHttpClient("weather", client => client.BaseAddress = uri)
                    .AddTypedClient<IWeatherService>(c => new WeatherService(c));
        }

        private void ConfigureViaHandler(IServiceCollection services, Uri uri) {
            services.AddTransient<ServiceDiscoveryMessageHandler>();
            services.AddHttpClient("weather", client => client.BaseAddress = uri)
                    .AddHttpMessageHandler<ServiceDiscoveryMessageHandler>()
                    .AddTypedClient<IWeatherService>(c => new WeatherService(c));
        }

        private void ConfigurePolly(IServiceCollection services, Uri uri) {
            services.AddTransient<CircuitBreakerMessageHandler>();
            services.AddHttpClient("weather", client => client.BaseAddress = uri)
                    .AddHttpMessageHandler<CircuitBreakerMessageHandler>()
                    .AddTypedClient<IWeatherService>(c => new WeatherService(c));
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
            if (env.IsDevelopment()) {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
            }
            else {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseMvc();
        }
    }
}
