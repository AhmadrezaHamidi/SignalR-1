// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.Sockets.Abstractions
{
    public interface ISocketBuilder
    {
        IServiceProvider ApplicationServices { get; }

        ISocketBuilder Use(Func<SocketDelegate, SocketDelegate> middleware);

        SocketDelegate Build();
    }
}
