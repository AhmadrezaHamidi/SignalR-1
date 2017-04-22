using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.SignalR
{
    public class SignalRContext
    {
        public ConnectionLifetimeManager LifetimeManager { get; }

        public ClientProxy All { get; }

        public SignalRContext(ConnectionLifetimeManager lifetimeManager)
        {
            LifetimeManager = lifetimeManager;

            All = new ClientProxy(lifetimeManager.SendAllAsync);
        }

        public ClientProxy User(string userId) => new ClientProxy((message, token) => LifetimeManager.SendUserAsync(userId, message, token));
        public ClientProxy Group(string groupName) => new ClientProxy((message, token) => LifetimeManager.SendGroupAsync(groupName, message, token));
        public ClientProxy Connection(string connectionId) => new ClientProxy((message, token) => LifetimeManager.SendConnectionAsync(connectionId, message, token));
    }
}