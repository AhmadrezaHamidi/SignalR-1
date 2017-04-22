using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.SignalR
{
    public class ClientProxy : IClientProxy
    {
        private Func<IEnumerable<Connection>> _getConnections;
        private readonly ILogger _logger;

        public ClientProxy(Func<IEnumerable<Connection>> getConnections, ILogger logger)
        {
            _getConnections = getConnections;
            _logger = logger;
        }

        public async Task SendAsync(Message message, CancellationToken cancellationToken)
        {
            var connections = _getConnections();
            foreach (var connection in connections)
            {
                _logger.LogTrace("[Connection {connectionId}] Sending {length} byte {type} message", connection.ConnectionId, message.Payload.Length, message.Type);
                if(!await SendToAsync(connection, message, cancellationToken))
                {
                    _logger.LogTrace("[Connection {connectionId}] Unable to send message, connection has terminated");
                }
                else
                {
                    _logger.LogTrace("[Connection {connectionId}] Message sent");
                }
            }
        }

        private async Task<bool> SendToAsync(Connection connection, Message message, CancellationToken cancellationToken)
        {
            while (!connection.Transport.Output.TryWrite(message))
            {
                if (!await connection.Transport.Output.WaitToWriteAsync(cancellationToken))
                {
                    // Connection has closed
                    return false;
                }
            }
            return true;
        }
    }
}