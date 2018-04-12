using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientApp.Services {
    public class ServiceDiscoveryMessageHandler : DelegatingHandler {
        private readonly ServiceRouter _serviceRouter;

        public ServiceDiscoveryMessageHandler(ServiceRouter serviceRouter) {
            _serviceRouter = serviceRouter;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, 
                                                                     CancellationToken cancellationToken) {
            var url = request.RequestUri.ToString();
            url = _serviceRouter.Route(url);
            request.RequestUri = new Uri(url);

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
