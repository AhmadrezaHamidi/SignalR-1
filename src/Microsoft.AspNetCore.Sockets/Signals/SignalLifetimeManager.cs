// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Sockets.Signals
{
    public abstract class SignalLifetimeManager<TSignal> where TSignal : Signal
    {
        public abstract Task OnConnectedAsync(Connection connection);

        public abstract Task OnDisconnectedAsync(Connection connection);

        public abstract Task OnMessageAsync(Message message, CancellationToken cancellationToken);
    }
}
