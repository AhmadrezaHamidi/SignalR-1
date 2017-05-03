// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Sockets.Signals
{
    public class SignalEndPoint<TSignal> : EndPoint where TSignal : Signal
    {
        private static readonly byte[] ErrorMessagePayload = Encoding.UTF8.GetBytes("An error occurred");

        private CancellationTokenSource _connectionActive = new CancellationTokenSource();
        private readonly SignalLifetimeManager<TSignal> _lifetimeManager;
        private readonly ILogger<SignalEndPoint<TSignal>> _logger;

        public SignalEndPoint(SignalLifetimeManager<TSignal> lifetimeManager, ILogger<SignalEndPoint<TSignal>> logger)
        {
            _lifetimeManager = lifetimeManager;
            _logger = logger;
        }

        public override async Task OnConnectedAsync(Connection connection)
        {
            _logger.LogTrace("[Connection {connectionId}] Connection established", connection.ConnectionId);

            await _lifetimeManager.OnConnectedAsync(connection);
            try
            {
                // Run receive loop
                // Sending happens from the lifetime manager itself
                // TODO: How does the lifetime manager shut down the connection from the server?
                await ReceiveMessagesAsync(connection, _connectionActive.Token);
            }
            catch(OperationCanceledException)
            {
                // We were shut down by the server shutting down
            }
            finally
            {
                await _lifetimeManager.OnDisconnectedAsync(connection);
            }

            _logger.LogTrace("[Connection {connectionId}] Connection closed", connection.ConnectionId);
        }

        private async Task ReceiveMessagesAsync(Connection connection, CancellationToken cancellationToken)
        {
            while (await connection.Transport.Input.WaitToReadAsync(cancellationToken))
            {
                while (connection.Transport.Input.TryRead(out var message))
                {
                    // Dispatch to the signal
                    _logger.LogTrace("[Connection {connectionId}] Dispatching message", connection.ConnectionId);
                    await _lifetimeManager.OnMessageAsync(message, cancellationToken);
                    _logger.LogTrace("[Connection {connectionId}] Dispatched message", connection.ConnectionId);
                }
            }
        }
    }
}
