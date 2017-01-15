// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;

namespace SocketsSample.EndPoints
{
    public class MessagesEndPoint : EndPoint
    {
        public ConnectionList Connections { get; } = new ConnectionList();

        public override async Task OnConnectedAsync(Connection connection)
        {
            Connections.Add(connection);

            await Broadcast($"{connection.ConnectionId} connected ({connection.Metadata["transport"]})");

            try
            {
                while (await connection.Transport.Input.WaitToReadAsync())
                {
                    Message message;
                    while (connection.Transport.Input.TryRead(out message))
                    {
                        using (message)
                        {
                            // We can avoid the copy here but we'll deal with that later
                            await Broadcast(message.Payload.Buffer, message.MessageFormat, message.EndOfMessage);
                        }
                    }
                }
            }
            finally
            {
                Connections.Remove(connection);

                await Broadcast($"{connection.ConnectionId} disconnected ({connection.Metadata["transport"]})");
            }
        }

        private Task Broadcast(string text)
        {
            return Broadcast(ReadableBuffer.Create(Encoding.UTF8.GetBytes(text)), Format.Text, endOfMessage: true);
        }

        private Task Broadcast(ReadableBuffer payload, Format format, bool endOfMessage)
        {
            var tasks = new List<Task>(Connections.Count);
            var message = new Message(payload.Preserve(), format, endOfMessage);

            foreach (var c in Connections)
            {
                tasks.Add(WriteAsync(message, format, endOfMessage, c));
            }

            return Task.WhenAll(tasks);
        }

        private static async Task WriteAsync(Message message, Format format, bool endOfMessage, Connection c)
        {
            while (await c.Transport.Output.WaitToWriteAsync())
            {
                if (c.Transport.Output.TryWrite(message))
                {
                    break;
                }
            }
        }
    }
}
