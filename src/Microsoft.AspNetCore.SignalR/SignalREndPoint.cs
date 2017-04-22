using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.SignalR
{
    public class SignalREndPoint : EndPoint
    {
        private readonly ClientManager _clients;
        private readonly ILogger<SignalREndPoint> _logger;

        public SignalREndPoint(ClientManager clients, ILogger<SignalREndPoint> logger)
        {
            _clients = clients;
            _logger = logger;
        }

        public override async Task OnConnectedAsync(Connection connection)
        {
            // Register a new Client connection
            if (!_clients.TryRegisterConnection(connection))
            {
                _logger.LogWarning("Duplicate connection received: {connectionId}", connection.ConnectionId);

                // Just drop the connection
                return;
            }

            // Now we wait for the client to disconnect
            await connection.Transport.Input.Completion;

            // Unregister the connection
            _clients.UnregisterConnection(connection);
        }
    }
}
