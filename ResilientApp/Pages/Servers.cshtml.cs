using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ResilientApp.Services;
using System.Collections.Generic;

namespace ResilientApp.Pages {
    public class ServersModel : PageModel {
        private readonly ServiceRouter _serviceRouter;

        [BindProperty]
        public string Server1 { get; set; }
        [BindProperty]
        public string Server2 { get; set; }

        public ServersModel(ServiceRouter serviceRouter) {
            _serviceRouter = serviceRouter;
        }

        public void OnGet() {
            Server1 = _serviceRouter.Servers[0];
            Server2 = _serviceRouter.Servers[1];
        }

        public void OnPost() {
            _serviceRouter.Servers[0] = Server1;
            _serviceRouter.Servers[1] = Server2;
        }
    }
}