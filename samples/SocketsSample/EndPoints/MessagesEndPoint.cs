// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;
using Microsoft.AspNetCore.Sockets.Features;

namespace SocketsSample.EndPoints
{
    public class MessagesEndPoint : EndPoint
    {
        public ConnectionList Connections { get; } = new ConnectionList();

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            if (!connection.TryGetChannel(out var channel))
            {
                throw new InvalidOperationException("Unable to access connection Channel.");
            }

            Connections.Add(connection);

            var transportName = connection.GetTransport()?.ToString() ?? "<unknown>";
            await Broadcast($"{connection.ConnectionId} connected ({transportName})");

            try
            {
                while (await channel.Input.WaitToReadAsync())
                {
                    Message message;
                    if (channel.Input.TryRead(out message))
                    {
                        // We can avoid the copy here but we'll deal with that later
                        var text = Encoding.UTF8.GetString(message.Payload);
                        text = $"{connection.ConnectionId}: {text}";
                        await Broadcast(Encoding.UTF8.GetBytes(text), message.Type, message.EndOfMessage);
                    }
                }
            }
            finally
            {
                Connections.Remove(connection);

                await Broadcast($"{connection.ConnectionId} disconnected ({transportName})");
            }
        }

        private Task Broadcast(string text)
        {
            return Broadcast(Encoding.UTF8.GetBytes(text), MessageType.Text, endOfMessage: true);
        }

        private Task Broadcast(byte[] payload, MessageType format, bool endOfMessage)
        {
            var tasks = new List<Task>(Connections.Count);

            foreach (var c in Connections)
            {
                if (!c.TryGetChannel(out var channel))
                {
                    throw new InvalidOperationException("Unable to access connection Channel.");
                }
                tasks.Add(channel.Output.WriteAsync(new Message(
                    payload,
                    format,
                    endOfMessage)));
            }

            return Task.WhenAll(tasks);
        }
    }
}
