// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO.Pipelines;
using System.IO.Pipelines.Text.Primitives;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Sockets.Transports
{
    public abstract class HttpStreamingTransportBase : IHttpTransport
    {
        protected ReadableChannel<Message> Application { get; }
        protected ILogger Logger { get; }

        protected HttpStreamingTransportBase(ReadableChannel<Message> application, ILoggerFactory loggerFactory)
        {
            Application = application;
            Logger = loggerFactory.CreateLogger<ServerSentEventsTransport>();
        }

        public async Task ProcessRequestAsync(HttpContext context, CancellationToken token)
        {
            context.Response.Headers["Cache-Control"] = "no-cache";

            // Make sure we disable all response buffering for SSE
            var bufferingFeature = context.Features.Get<IHttpBufferingFeature>();
            bufferingFeature?.DisableResponseBuffering();
            context.Response.Headers["Content-Encoding"] = "identity";

            PrepareResponse(context);

            await context.Response.Body.FlushAsync();

            var pipe = context.Response.Body.AsPipelineWriter();
            var output = new PipelineTextOutput(pipe, TextEncoder.Utf8); // We don't need the Encoder, but it's harmless to set.

            try
            {
                while (await Application.WaitToReadAsync(token))
                {
                    while (Application.TryRead(out var message))
                    {
                        if (!TryWriteMessage(message, output))
                        {
                            // We ran out of space to write, even after trying to enlarge.
                            // This should only happen in a significant lack-of-memory scenario.

                            // IOutput doesn't really have a way to write incremental

                            // Throwing InvalidOperationException here, but it's not quite an invalid operation...
                            throw new InvalidOperationException("Ran out of space to format messages!");
                        }

                        await output.FlushAsync();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Closed connection
            }
        }

        protected abstract void PrepareResponse(HttpContext context);
        protected abstract bool TryWriteMessage(Message message, PipelineTextOutput output);
    }
}
