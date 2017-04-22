using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.SignalR
{
    public class DefaultConnectionLifetimeManager : ConnectionLifetimeManager
    {
        private readonly ConnectionList _connections = new ConnectionList();
        private readonly ILogger _logger;

        public DefaultConnectionLifetimeManager(ILogger<DefaultConnectionLifetimeManager> logger)
        {
            _logger = logger;
        }

        public override Task AddGroupAsync(Connection connection, string groupName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var groups = connection.Metadata.GetOrAdd("groups", _ => new HashSet<string>());

            lock (groups)
            {
                groups.Add(groupName);
            }
            _logger.LogTrace("[Connection {connectionId}] Added to group {group}", connection.ConnectionId, groupName);

            return Task.CompletedTask;
        }

        public override Task RemoveGroupAsync(Connection connection, string groupName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var groups = connection.Metadata.Get<HashSet<string>>("groups");

            if (groups == null)
            {
                return Task.CompletedTask;
            }

            lock (groups)
            {
                groups.Remove(groupName);
            }
            _logger.LogTrace("[Connection {connectionId}] Removed from group {group}", connection.ConnectionId, groupName);

            return Task.CompletedTask;
        }

        public override Task SendAllAsync(Message message, CancellationToken cancellationToken)
        {
            return SendAllWhere(message, c => true, cancellationToken);
        }

        public override Task SendConnectionAsync(string connectionId, Message message, CancellationToken cancellationToken)
        {
            var connection = _connections[connectionId];

            return WriteAsync(connection, message, cancellationToken);
        }

        public override Task SendGroupAsync(string groupName, Message message, CancellationToken cancellationToken)
        {
            return SendAllWhere(message, connection =>
            {
                var groups = connection.Metadata.Get<HashSet<string>>("groups");
                return groups?.Contains(groupName) == true;
            }, cancellationToken);
        }

        public override Task SendUserAsync(string userId, Message message, CancellationToken cancellationToken)
        {
            return SendAllWhere(message, connection =>
            {
                return string.Equals(connection.User.Identity.Name, userId, StringComparison.Ordinal);
            }, cancellationToken);
        }

        public override Task OnConnectedAsync(Connection connection)
        {
            _connections.Add(connection);
            _logger.LogInformation("[Connection {connectionId}] Connected");
            return Task.CompletedTask;
        }

        public override Task OnDisconnectedAsync(Connection connection)
        {
            _connections.Remove(connection);
            _logger.LogInformation("[Connection {connectionId}] Disconnected");
            return Task.CompletedTask;
        }

        private Task SendAllWhere(Message message, Func<Connection, bool> include, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tasks = new List<Task>(_connections.Count);

            // TODO: serialize once per format by providing a different stream?
            foreach (var connection in _connections)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!include(connection))
                {
                    continue;
                }

                tasks.Add(WriteAsync(connection, message, cancellationToken));
            }

            return Task.WhenAll(tasks);
        }

        private async Task WriteAsync(Connection connection, Message message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            while (await connection.Transport.Output.WaitToWriteAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (connection.Transport.Output.TryWrite(message))
                {
                    _logger.LogTrace("[Connection {connectionId}] Sent message ({type}, {length} bytes)", connection.ConnectionId, message.Type, message.Payload.Length);
                    break;
                }
            }
            _logger.LogTrace("[Connection {connectionId}] Failed to send message, connection was closed", connection.ConnectionId);
        }
    }
}
