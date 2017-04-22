// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using Microsoft.AspNetCore.SignalR;

namespace Microsoft.AspNetCore.Builder
{
    public static class SignalRAppBuilderExtensions
    {
        public static readonly string DefaultPath = "/signalr";

        public static IApplicationBuilder UseSignalR(this IApplicationBuilder app) => UseSignalR(app, DefaultPath);

        public static IApplicationBuilder UseSignalR(this IApplicationBuilder app, string path)
        {
            app.UseSockets(routes =>
            {
                routes.MapEndpoint<SignalREndPoint>(path);
            });

            return app;
        }
    }
}
