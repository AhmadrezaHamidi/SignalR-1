// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class SignalRDependencyInjectionExtensions
    {
        public static ISignalRBuilder AddSignalR(this IServiceCollection services)
        {
            services.AddSockets();
            services.AddSingleton<SignalREndPoint>();
            services.AddSingleton<IConfigureOptions<SignalROptions>, SignalROptionsSetup>();
            services.AddSingleton<ConnectionLifetimeManager, DefaultConnectionLifetimeManager>();
            services.AddSingleton<JsonNetInvocationAdapter>();
            services.AddSingleton<InvocationAdapterRegistry>();
            services.AddScoped<SignalRContext>();
            services.AddRouting();

            return new SignalRBuilder(services);
        }

        public static ISignalRBuilder AddSignalR(this IServiceCollection services, Action<SignalROptions> setupAction)
        {
            return services.AddSignalR().AddSignalROptions(setupAction);
        }

        public static ISignalRBuilder AddSignalROptions(this ISignalRBuilder builder, Action<SignalROptions> setupAction)
        {
            builder.Services.Configure(setupAction);
            return builder;
        }
    }
}
