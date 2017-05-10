using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Sockets
{
    public static class SocketBuilderExtensions
    {
        public static ISocketBuilder Use(this ISocketBuilder socketBuilder, Func<ConnectionContext, Func<Task>, Task> middleware)
        {
            return socketBuilder.Use(next =>
            {
                return context =>
                {
                    Func<Task> simpleNext = () => next(context);
                    return middleware(context, simpleNext);
                };
            });
        }

        public static ISocketBuilder UseEndPoint<TEndPoint>(this ISocketBuilder socketBuilder) where TEndPoint : EndPoint
        {
            // This is a terminal middleware, so there's no need to use the 'next' parameter
            return socketBuilder.Use((connection, _) =>
            {
                var endpoint = socketBuilder.ApplicationServices.GetRequiredService<TEndPoint>();
                return endpoint.OnConnectedAsync(connection);
            });
        }
    }
}
