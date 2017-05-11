using System.IO.Pipelines;
using Microsoft.AspNetCore.Sockets.Features;

namespace SocketsSample.Sockets
{
    public class ConnectionPipeFeature : IConnectionPipeFeature
    {
        public IPipeConnection Pipe { get; }

        public ConnectionPipeFeature(IPipeConnection pipe)
        {
            Pipe = pipe;
        }
    }
}
