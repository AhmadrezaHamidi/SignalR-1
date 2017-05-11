using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;

namespace SocketsSample.Sockets
{
    public class SocketHost
    {
        private ISocketListener _socketListener;
        private SocketDelegate _socketDelegate;
        private readonly IServiceProvider _services;

        public SocketHost(ISocketListener socketListener, IServiceProvider services, SocketDelegate socketDelegate)
        {
            _socketListener = socketListener;
            _socketDelegate = socketDelegate;
            _services = services;
        }

        public Task RunAsync()
        {
            return _socketListener.RunAsync(_services, _socketDelegate);
        }

        internal void Stop()
        {
            _socketListener.Stop();
        }
    }
}
