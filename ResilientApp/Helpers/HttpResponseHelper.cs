using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ResilientApp.Helpers {
    public static class HttpResponseHelper {
        public static HttpResponseMessage CreateEmptyResponse() => 
            new HttpResponseMessage { Content = new FakeContent("[]") };
    }

    public class FakeContent : StringContent {

        private string _content;

        public FakeContent(string content) : base(content) {
            _content = content;
            _memoryStream = new FakeMemoryStream(Encoding.UTF8.GetBytes(_content));
        }

        private FakeMemoryStream _memoryStream;

        protected override void Dispose(bool disposing) {
            _memoryStream = new FakeMemoryStream(Encoding.UTF8.GetBytes(_content));
        }

        protected override Task<Stream> CreateContentReadStreamAsync() {
            return Task.FromResult((Stream)_memoryStream);
        }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) {
            return Task.CompletedTask;
        }
    }

    public class FakeHttpResponseMessage : HttpResponseMessage {
        protected override void Dispose(bool disposing) {
            
        }
    }
}
