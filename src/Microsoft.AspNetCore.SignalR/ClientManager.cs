using System.Collections.Concurrent;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.SignalR
{
    public class ClientManager
    {
        private ConcurrentDictionary<string, Connection> _connections = new ConcurrentDictionary<string, Connection>();
        private readonly ILogger<ClientManager> _logger;

        public IClientProxy All { get; }

        public ClientManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ClientManager>();

            All = new ClientProxy(() => _connections.Values, loggerFactory.CreateLogger(typeof(ClientProxy).FullName + ":All"));
        }

        /// <summary>
        /// Try to register the connection in the client manager
        /// </summary>
        /// <param name="connection">The connection to register</param>
        /// <returns>true, if the connection was registered; false, if there is already a connection with this ID</returns>
        public bool TryRegisterConnection(Connection connection)
        {
            if(_connections.TryAdd(connection.ConnectionId, connection))
            {
                _logger.LogTrace("[Connection {connectionId}] Registered Successfully", connection.ConnectionId);
                return true;
            }
            else
            {
                _logger.LogWarning("[Connection {connectionId}] Cannot be registered because it already exists!", connection.ConnectionId);
                return false;
            }
        }

        public void UnregisterConnection(Connection connection)
        {
            // If it fails, it's because it's already gone somehow :)
            if(_connections.TryRemove(connection.ConnectionId, out _))
            {
                _logger.LogTrace("[Connection {connectionId}] Unregistered Successfully");
            }
            else
            {
                _logger.LogTrace("[Connection {connectionId}] Was already unregistered!");
            }
        }
    }
}