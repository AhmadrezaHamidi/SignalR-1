using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;

namespace SocketsSample.Sockets
{
    public interface ISocketListener
    {
        Task RunAsync(IServiceProvider services, SocketDelegate socketApp);
        void Stop();
    }
}
