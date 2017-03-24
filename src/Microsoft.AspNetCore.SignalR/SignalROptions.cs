// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.AspNetCore.SignalR
{
    public class SignalROptions
    {
        internal readonly Dictionary<string, Type> _invocationMappings = new Dictionary<string, Type>();

        public void RegisterHubProtocol<THubProtocol>(string format) where THubProtocol : IHubProtocol
        {
            _invocationMappings[format] = typeof(THubProtocol);
        }
    }
}
