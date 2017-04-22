// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO.Pipelines.Text.Primitives;
using System.Threading.Tasks.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Sockets.Internal.Formatters;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Sockets.Transports
{
    public class ServerSentEventsTransport : HttpStreamingTransportBase
    {
        public ServerSentEventsTransport(ReadableChannel<Message> application, ILoggerFactory loggerFactory) : base(application, loggerFactory)
        {
        }

        protected override void PrepareResponse(HttpContext context)
        {
            context.Response.ContentType = "text/event-stream";
        }

        protected override bool TryWriteMessage(Message message, PipelineTextOutput output) =>
            ServerSentEventsMessageFormatter.TryWriteMessage(message, output);
    }
}
