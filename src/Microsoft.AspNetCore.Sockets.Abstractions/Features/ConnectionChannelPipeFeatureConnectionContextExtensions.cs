using System.IO.Pipelines;
using Microsoft.AspNetCore.Sockets.Features;

namespace Microsoft.AspNetCore.Sockets
{
    // Longest type name ever? It's extension methods though so I think we don't really care ;)
    public static class ConnectionChannelPipeFeatureConnectionContextExtensions
    {
        public static bool TryGetChannel(this ConnectionContext context, out IChannelConnection<Message> channel)
        {
            var feature = context.Features.Get<IConnectionChannelFeature>();
            if(feature != null)
            {
                channel = feature.Channel;
                return true;
            }
            else
            {
                channel = null;
                return false;
            }
        }

        public static bool TryGetPipe(this ConnectionContext context, out IPipeConnection pipe)
        {
            var feature = context.Features.Get<IConnectionPipeFeature>();
            if(feature != null)
            {
                pipe = feature.Pipe;
                return true;
            }
            else
            {
                pipe = null;
                return false;
            }
        }
    }
}
