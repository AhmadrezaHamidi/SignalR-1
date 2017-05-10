// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;
using Microsoft.AspNetCore.Sockets.Features;

namespace SocialWeather
{
    public class PersistentConnectionLifeTimeManager
    {
        private readonly FormatterResolver _formatterResolver;
        private readonly ConnectionList _connectionList = new ConnectionList();

        public PersistentConnectionLifeTimeManager(FormatterResolver formatterResolver)
        {
            _formatterResolver = formatterResolver;
        }

        public void OnConnectedAsync(ConnectionContext connection)
        {
            connection.Items[ConnectionMetadataNames.Format] = "json";
            _connectionList.Add(connection);
        }

        public void OnDisconnectedAsync(ConnectionContext connection)
        {
            _connectionList.Remove(connection);
        }

        public async Task SendToAllAsync<T>(T data)
        {
            foreach (var connection in _connectionList)
            {
                var context = connection.GetHttpContext();
                var format =
                    string.Equals(context.Request.Query["format"], "binary", StringComparison.OrdinalIgnoreCase)
                        ? MessageType.Binary
                        : MessageType.Text;

                var formatter = _formatterResolver.GetFormatter<T>((string)connection.Items[ConnectionMetadataNames.Format]);
                var ms = new MemoryStream();
                await formatter.WriteAsync(data, ms);

                if (!connection.TryGetChannel(out var channel))
                {
                    throw new InvalidOperationException("Cannot send message, unable to access connection Channel.");
                }

                channel.Output.TryWrite(new Message(ms.ToArray(), format, endOfMessage: true));
            }
        }

        public Task InvokeConnectionAsync(string connectionId, object data)
        {
            throw new NotImplementedException();
        }

        public Task InvokeGroupAsync(string groupName, object data)
        {
            throw new NotImplementedException();
        }

        public Task InvokeUserAsync(string userId, object data)
        {
            throw new NotImplementedException();
        }

        public void AddGroupAsync(ConnectionContext connection, string groupName)
        {
            connection.AddGroup(groupName);
        }

        public void RemoveGroupAsync(ConnectionContext connection, string groupName)
        {
            connection.RemoveGroup(groupName);
        }
    }
}
