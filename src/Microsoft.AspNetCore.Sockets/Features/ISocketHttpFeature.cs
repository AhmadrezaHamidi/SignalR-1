using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Sockets.Features
{
    public interface ISocketHttpFeature
    {
        HttpContext HttpContext { get; }
        TransportType Transport { get; }
    }

    public static class SocketHttpFeatureConnectionContextExtensions
    {
        public static HttpContext GetHttpContext(this ConnectionContext connectionContext)
        {
            return connectionContext.Features.Get<ISocketHttpFeature>()?.HttpContext;
        }
    }
}
