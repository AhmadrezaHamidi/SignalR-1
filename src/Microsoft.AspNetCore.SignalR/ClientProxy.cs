using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;

namespace Microsoft.AspNetCore.SignalR
{
    public struct ClientProxy
    {
        private Func<Message, CancellationToken, Task> _implementation;

        public ClientProxy(Func<Message, CancellationToken, Task> implementation)
        {
            _implementation = implementation;
        }

        public Task SendAsync(Message message, CancellationToken cancellationToken) => _implementation(message, cancellationToken);
    }
}