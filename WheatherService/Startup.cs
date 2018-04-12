using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace WheatherService {
    public class Startup {

        private static DateTime _lastRequest;

        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public static int _busySpan;
        public static int _delay;
        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services) {
            _busySpan = Convert.ToInt32(Configuration["BusySpan"]);
            _delay = Convert.ToInt32(Configuration["Delay"]);
            Console.WriteLine("Busy: " + _busySpan);
            Console.WriteLine("Delay: " + _delay);
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }
            else {
                app.UseHsts();
            }

            app.Use(async (context, next) => {
                if (IsBusy()) {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    return;
                }
                await Task.Delay(_delay);
                await next();
            });

            app.UseMvc();
        }

        private static bool IsBusy() {
            var now = DateTime.Now;
            if ((now - _lastRequest) < TimeSpan.FromMilliseconds(_busySpan)) {
                return true;
            }
            _lastRequest = now;
            return false;
        }
    }
}
