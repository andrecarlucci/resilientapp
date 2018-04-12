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
    public class CircuitBreakerMessageHandler : ServiceDiscoveryMessageHandler {
        
        public CircuitBreakerMessageHandler(ServiceRouter serviceRouter) 
            : base(serviceRouter) {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                                     CancellationToken cancellationToken) {

            return await CreateFallback().WrapAsync(CreateRetry())
                                         .WrapAsync(_circuitBreaker)
                                         .ExecuteAsync(() => base.SendAsync(request, cancellationToken));
        }

        private RetryPolicy<HttpResponseMessage> CreateRetry() {
            return Policy.HandleResult<HttpResponseMessage>(m => !m.IsSuccessStatusCode)
                         .Or<HttpRequestException>()
                         .WaitAndRetryAsync(3, c => TimeSpan.FromSeconds(c), onRetry: (e, t) => Log("retry: " + e.Exception?.Message ?? "no ex"));
        }
        
        private static CircuitBreakerPolicy CreateCircuiteBreaker() {
            return Policy.Handle<HttpRequestException>()
                         .CircuitBreakerAsync(3, TimeSpan.FromSeconds(10), (e, t) => Log("Circuit OPEN"), () => Log("Circuit CLOSED"));

        }

        private FallbackPolicy<HttpResponseMessage> CreateFallback() {
            return Policy<HttpResponseMessage>
                        .Handle<BrokenCircuitException>()
                        .FallbackAsync(HttpResponseHelper.CreateEmptyResponse(), onFallbackAsync: async e => Log("FALLBACK"));
        }

        private static CircuitBreakerPolicy _circuitBreaker = CreateCircuiteBreaker();

        private static void Log(string msg) {
            Trace.WriteLine("->>>>>> POLLY: " + msg);
        }
    }
}
