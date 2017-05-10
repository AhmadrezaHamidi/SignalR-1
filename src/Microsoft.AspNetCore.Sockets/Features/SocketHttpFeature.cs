using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Sockets.Features
{
    public class SocketHttpFeature : ISocketHttpFeature
    {
        public HttpContext HttpContext { get; }
        public TransportType Transport { get; }

        public SocketHttpFeature(HttpContext httpContext, TransportType transport)
        {
            HttpContext = httpContext;
            Transport = transport;
        }
    }
}
