// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Sockets.Internal.Transports
{
    public class WebSocketsTransport : IHttpTransport
    {
        private readonly WebSocketOptions _options;
        private readonly ILogger _logger;
        private readonly Channel<byte[]> _application;
        private readonly string _connectionId;

        public WebSocketsTransport(WebSocketOptions options, Channel<byte[]> application, string connectionId, ILoggerFactory loggerFactory)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _options = options;
            _application = application;
            _connectionId = connectionId;
            _logger = loggerFactory.CreateLogger<WebSocketsTransport>();
        }

        public async Task ProcessRequestAsync(HttpContext context, CancellationToken token)
        {
            Debug.Assert(context.WebSockets.IsWebSocketRequest, "Not a websocket request");

            using (var ws = await context.WebSockets.AcceptWebSocketAsync())
            {
                _logger.SocketOpened(_connectionId);

                await ProcessSocketAsync(ws);
            }
            _logger.SocketClosed(_connectionId);
        }

        public async Task ProcessSocketAsync(WebSocket socket)
        {
            using (var cts = new CancellationTokenSource())
            {
                // Begin sending and receiving. Receiving must be started first because ExecuteAsync enables SendAsync.
                var transportTask = StartReceiving(socket);
                var applicationTask = StartSending(socket, cts.Token);

                // Wait for something to shut complete
                var trigger = await Task.WhenAny(
                    transportTask,
                    applicationTask);

                var failed = trigger.IsCanceled || trigger.IsFaulted;
                var task = Task.CompletedTask;
                if (trigger == transportTask)
                {
                    cts.Cancel();
                    task = applicationTask;
                    _logger.WaitingForApplication(_connectionId);
                }
                else
                {
                    task = transportTask;
                    _logger.WaitingForClose(_connectionId);
                }

                // Don't send the close frame if we're already closed
                if (!IsWebSocketClosed(socket))
                {
                    await socket.CloseOutputAsync(failed ? WebSocketCloseStatus.InternalServerError : WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }

                var resultTask = await Task.WhenAny(task, Task.Delay(_options.CloseTimeout));

                if (resultTask != task)
                {
                    _logger.CloseTimedOut(_connectionId);
                    socket.Abort();
                }
                else
                {
                    // Observe any exceptions from second completed task
                    await task;
                }

                // Observe any exceptions from original completed task
                await trigger;
            }
        }

        private async Task StartReceiving(WebSocket socket)
        {
            try
            {
                // REVIEW: This code was copied from the client, it's highly unoptimized at the moment (especially
                // for server logic)
                var incomingMessage = new List<ArraySegment<byte>>();
                while (true)
                {
                    const int bufferSize = 4096;
                    var totalBytes = 0;
                    WebSocketReceiveResult receiveResult;
                    do
                    {
                        var buffer = new ArraySegment<byte>(new byte[bufferSize]);
                        // Exceptions are handled above where the send and receive tasks are being run.
                        receiveResult = await socket.ReceiveAsync(buffer, CancellationToken.None);

                        _logger.MessageReceived(_connectionId, receiveResult.MessageType, receiveResult.Count, receiveResult.EndOfMessage);

                        if (receiveResult.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }

                        var truncBuffer = new ArraySegment<byte>(buffer.Array, 0, receiveResult.Count);
                        incomingMessage.Add(truncBuffer);
                        totalBytes += receiveResult.Count;
                    } while (!receiveResult.EndOfMessage);

                    byte[] messageBuffer = null;

                    if (incomingMessage.Count > 1)
                    {
                        messageBuffer = new byte[totalBytes];
                        var offset = 0;
                        for (var i = 0; i < incomingMessage.Count; i++)
                        {
                            Buffer.BlockCopy(incomingMessage[i].Array, 0, messageBuffer, offset, incomingMessage[i].Count);
                            offset += incomingMessage[i].Count;
                        }
                    }
                    else
                    {
                        messageBuffer = new byte[incomingMessage[0].Count];
                        Buffer.BlockCopy(incomingMessage[0].Array, incomingMessage[0].Offset, messageBuffer, 0, incomingMessage[0].Count);
                    }

                    _logger.MessageToApplication(_connectionId, messageBuffer.Length);

                    while (await _application.Out.WaitToWriteAsync())
                    {
                        if (_application.Out.TryWrite(messageBuffer))
                        {
                            incomingMessage.Clear();
                            break;
                        }
                    }
                }
            }
            finally
            {
                // We're done writing to the websocket
                _application.Out.TryComplete();
            }
        }

        private async Task StartSending(WebSocket ws, CancellationToken cancellationToken)
        {
            try
            {
                while (await _application.In.WaitToReadAsync(cancellationToken))
                {
                    // Get a frame from the application
                    while (_application.In.TryRead(out var buffer))
                    {
                        if (buffer.Length > 0)
                        {
                            try
                            {
                                _logger.SendPayload(_connectionId, buffer.Length);

                                if (!IsWebSocketClosed(ws))
                                {
                                    await ws.SendAsync(new ArraySegment<byte>(buffer), _options.WebSocketMessageType, endOfMessage: true, cancellationToken: CancellationToken.None);
                                }
                            }
                            catch (WebSocketException socketException) when (IsWebSocketClosed(ws))
                            {
                                // this can happen when we send the CloseFrame to the client and try to write afterwards
                                _logger.SendFailed(_connectionId, socketException);
                                break;
                            }
                            catch (Exception ex)
                            {
                                _logger.ErrorWritingFrame(_connectionId, ex);
                                break;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // We cancelled the application because the transport ended
            }
        }

        private static bool IsWebSocketClosed(WebSocket ws)
        {
            return ws.State == WebSocketState.Aborted ||
                   ws.State == WebSocketState.Closed ||
                   ws.State == WebSocketState.CloseSent;
        }
    }
}
