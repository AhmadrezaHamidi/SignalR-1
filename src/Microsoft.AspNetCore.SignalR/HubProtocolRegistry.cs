// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.SignalR
{
    // REVIEW(anurse): This needs to be refactored a bit. Do we even want external users to be able to create new Hub Protocols?
    public class HubProtocolRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SignalROptions _options;

        public HubProtocolRegistry(IOptions<SignalROptions> options, IServiceProvider serviceProvider)
        {
            _options = options.Value;
            _serviceProvider = serviceProvider;
        }

        public IHubProtocol GetProtocol(string protocolName)
        {
            Type type;
            if (_options._invocationMappings.TryGetValue(protocolName, out type))
            {
                return _serviceProvider.GetRequiredService(type) as IHubProtocol;
            }

            return null;
        }
    }
}