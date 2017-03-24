// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.SignalR
{
    public class DefaultHubLifetimeManager<THub> : HubLifetimeManager<THub>
    {
        private readonly ConnectionList _connections = new ConnectionList();
        private readonly HubProtocolRegistry _registry;

        public DefaultHubLifetimeManager(HubProtocolRegistry registry)
        {
            _registry = registry;
        }

        public override Task AddGroupAsync(Connection connection, string groupName)
        {
            var groups = connection.Metadata.GetOrAdd("groups", _ => new HashSet<string>());

            lock (groups)
            {
                groups.Add(groupName);
            }

            return TaskCache.CompletedTask;
        }

        public override Task RemoveGroupAsync(Connection connection, string groupName)
        {
            var groups = connection.Metadata.Get<HashSet<string>>("groups");

            if (groups == null)
            {
                return TaskCache.CompletedTask;
            }

            lock (groups)
            {
                groups.Remove(groupName);
            }

            return TaskCache.CompletedTask;
        }

        public override Task InvokeAllAsync(string methodName, object[] args)
        {
            return InvokeAllWhere(methodName, args, c => true);
        }

        private Task InvokeAllWhere(string methodName, object[] args, Func<Connection, bool> include)
        {
            var tasks = new List<Task>(_connections.Count);
            var message = new InvocationMessage(invocationId: 0, target: methodName, arguments: args);

            // TODO: serialize once per format by providing a different stream?
            foreach (var connection in _connections)
            {
                if (!include(connection))
                {
                    continue;
                }

                var protocol = _registry.GetProtocol(connection.Metadata.Get<string>("formatType"));

                tasks.Add(WriteAsync(connection, protocol, message));
            }

            return Task.WhenAll(tasks);
        }

        public override Task InvokeConnectionAsync(string connectionId, string methodName, object[] args)
        {
            var connection = _connections[connectionId];

            var invocationAdapter = _registry.GetProtocol(connection.Metadata.Get<string>("formatType"));

            var message = new InvocationMessage(invocationId: 0, target: methodName, arguments: args);

            return WriteAsync(connection, invocationAdapter, message);
        }

        public override Task InvokeGroupAsync(string groupName, string methodName, object[] args)
        {
            return InvokeAllWhere(methodName, args, connection =>
            {
                var groups = connection.Metadata.Get<HashSet<string>>("groups");
                return groups?.Contains(groupName) == true;
            });
        }

        public override Task InvokeUserAsync(string userId, string methodName, object[] args)
        {
            return InvokeAllWhere(methodName, args, connection =>
            {
                return string.Equals(connection.User.Identity.Name, userId, StringComparison.Ordinal);
            });
        }

        public override Task OnConnectedAsync(Connection connection)
        {
            _connections.Add(connection);
            return TaskCache.CompletedTask;
        }

        public override Task OnDisconnectedAsync(Connection connection)
        {
            _connections.Remove(connection);
            return TaskCache.CompletedTask;
        }

        private static async Task WriteAsync(Connection connection, IHubProtocol protocol, InvocationMessage invocation)
        {
            Message message;
            using (var stream = new MemoryStream())
            {
                protocol.WriteMessage(invocation, stream);
                stream.Flush();

                message = new Message(stream.ToArray(), MessageType.Text, endOfMessage: true);
            }

            while (await connection.Transport.Output.WaitToWriteAsync())
            {
                if (connection.Transport.Output.TryWrite(message))
                {
                    break;
                }
            }
        }
    }
}
