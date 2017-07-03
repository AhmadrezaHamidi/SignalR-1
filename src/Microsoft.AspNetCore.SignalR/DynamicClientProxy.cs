// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Dynamic;

namespace Microsoft.AspNetCore.SignalR
{
    class DynamicClientProxy : DynamicObject
    {
        private IClientProxy _clientProxy;
        public DynamicClientProxy(IClientProxy clientProxy)
        {
            _clientProxy = clientProxy;
        }
    }
}
