using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;

namespace Microsoft.AspNetCore.SignalR
{
    public abstract class ConnectionLifetimeManager
    {
        public abstract Task OnConnectedAsync(Connection connection);

        public abstract Task OnDisconnectedAsync(Connection connection);

        public abstract Task SendAllAsync(Message message, CancellationToken cancellationToken);

        public abstract Task SendConnectionAsync(string connectionId, Message message, CancellationToken cancellationToken);

        public abstract Task SendGroupAsync(string groupName, Message message, CancellationToken cancellationToken);

        public abstract Task SendUserAsync(string userId, Message message, CancellationToken cancellationToken);

        public abstract Task AddGroupAsync(Connection connection, string groupName, CancellationToken cancellationToken);

        public abstract Task RemoveGroupAsync(Connection connection, string groupName, CancellationToken cancellationToken);
    }
}
