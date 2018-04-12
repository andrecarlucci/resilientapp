using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Retry;
using ResilientApp.Helpers;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientApp.Services {
    public class RetryMessageHandler : ServiceDiscoveryMessageHandler {
        
        public RetryMessageHandler(ServiceRouter serviceRouter) 
            : base(serviceRouter) {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                                     CancellationToken cancellationToken) {

            return await Policy.HandleResult<HttpResponseMessage>(m => !m.IsSuccessStatusCode)
                  .Or<HttpRequestException>()
                  .WaitAndRetryAsync(3, c => TimeSpan.FromSeconds(c), onRetry: (e, t) => Log("retry: " + e.Exception?.Message ?? "no ex"))
                  .ExecuteAsync(() => base.SendAsync(request, cancellationToken));                             
        }
        
        private static void Log(string msg) {
            Trace.WriteLine("->>>>>> POLLY: " + msg);
        }
    }
}
