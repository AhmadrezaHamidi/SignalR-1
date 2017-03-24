// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.AspNetCore.Sockets;

namespace Microsoft.AspNetCore.SignalR.Protocol
{
    public interface IHubProtocol
    {
        MessageType MessageType { get; }

        HubMessage ParseMessage(Stream input, IInvocationBinder binder);
        void WriteMessage(HubMessage message, Stream output);
    }
}
