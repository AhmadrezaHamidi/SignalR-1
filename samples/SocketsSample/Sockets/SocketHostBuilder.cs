using System;
using Microsoft.AspNetCore.Sockets;

namespace SocketsSample.Sockets
{
    public class SocketHostBuilder
    {
        private ISocketListener _socketListener;
        private ISocketBuilder _socketBuilder;

        public SocketHostBuilder(IServiceProvider services)
        {
            _socketBuilder = new SocketBuilder(services);
        }

        public SocketHostBuilder UseListener(ISocketListener socketListener)
        {
            _socketListener = socketListener;
            return this;
        }

        public SocketHostBuilder Configure(Action<ISocketBuilder> configure)
        {
            configure(_socketBuilder);
            return this;
        }

        public SocketHost Build() => new SocketHost(_socketListener, _socketBuilder.ApplicationServices, _socketBuilder.Build());
    }
}
