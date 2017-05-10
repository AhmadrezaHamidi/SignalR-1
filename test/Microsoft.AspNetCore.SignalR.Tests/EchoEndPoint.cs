// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;

namespace Microsoft.AspNetCore.SignalR.Tests
{
    public class EchoEndPoint : EndPoint
    {
        public async override Task OnConnectedAsync(ConnectionContext connection)
        {
            if(!connection.TryGetChannel(out var channel))
            {
                throw new InvalidOperationException("Unable to get connection Channel.");
            }

            await channel.Output.WriteAsync(await channel.Input.ReadAsync());
        }
    }
}
