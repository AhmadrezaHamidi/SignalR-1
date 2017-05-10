using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.Sockets
{
    public abstract class ConnectionContext
    {
        public abstract string ConnectionId { get; }

        public abstract IFeatureCollection Features { get; }

        public abstract ClaimsPrincipal User { get; set; }

        public abstract IDictionary<object, object> Items { get; set; }

        public abstract IServiceProvider ConnectionServices { get; set; }

        public abstract CancellationToken ConnectionAborted { get; set; }

        public abstract void Abort();

        public abstract bool TryGetPipe(out IPipeConnection pipe);

        public abstract bool TryGetChannel(out IChannelConnection<Message> channel);
    }
}
