// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.SignalR
{
    class DynamicHubClients
    {
        private IHubClients _inner;

        public DynamicHubClients(IHubClients clients)
        {
            _inner = clients;
        }

        dynamic All => new DynamicClientProxy(_inner.All);
        dynamic User(string userId) => new DynamicClientProxy(_inner.User(userId));
        dynamic Group(string group) => new DynamicClientProxy(_inner.Group(group));
        dynamic Client(string connectionId) => new DynamicClientProxy(_inner.Client(connectionId));
    }
}
