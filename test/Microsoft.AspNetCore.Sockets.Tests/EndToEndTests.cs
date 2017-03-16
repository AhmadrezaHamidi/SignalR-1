using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Sockets.Tests
{
    public class EndToEndTests
    {
        private ITestOutputHelper _output;

        public EndToEndTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task EchoEndpointEndToEnd()
        {
            var app = CreateTestApp();
        }

        private (IWebHost host, string baseUrl) CreateTestApp<TEndPoint>(string path) where TEndPoint : EndPoint
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddXUnit
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseUrls("http://localhost:*")
                .UseLoggerFactory(new LoggerFactory());
                .ConfigureServices(services =>
                {
                    services.AddEndPoint<TEndPoint>();
                })
                .Configure(app =>
                {
                    app.UseSockets(routes =>
                    {
                        routes.MapEndpoint<TEndPoint>(path);
                    });
                })
                .Build();
        }
    }
}
