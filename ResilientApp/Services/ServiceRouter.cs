using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

namespace ResilientApp.Services {
    public class ServiceRouter {

        //Original: weatherservice.local.com
        //Para:     sx.weatherservice.local.com
        private static int _next = 0;
        private static Regex _regex = new Regex("(s[0-9]?)?\\.?weatherservice");

        public List<string> Servers { get; private set; } = new List<string> {
            "s1", "s2"
        };

        public string Route(string url) {
            var server = (Interlocked.Increment(ref _next) % 2);
            url = _regex.Replace(url, Servers[server] + "." + "weatherservice");
            return url;
        }
    }
}
