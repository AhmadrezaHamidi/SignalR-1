// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Sockets.Signals;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class SignalsDependencyInjectionExtensions
    {
        public static IServiceCollection AddSignals(this IServiceCollection services)
        {
            services.TryAddSingleton(typeof(SignalLifetimeManager<>), typeof(DefaultSignalLifetimeManager<>));
            services.AddEndPoint<SignalEndPoint>();
            return services;
        }
    }
}
