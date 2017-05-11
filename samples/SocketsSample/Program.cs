// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocketsSample.EndPoints;
using SocketsSample.Sockets;

namespace SocketsSample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            var host = new WebHostBuilder()
                .UseConfiguration(config)
                .ConfigureLogging(factory =>
                {
                    factory.AddConsole();
                })
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            var tcpHost = new SocketHostBuilder(host.Services)
                .UseListener(new TcpSocketListener(new IPEndPoint(IPAddress.Any, 5030)))
                .Configure(socket =>
                {
                    socket.UseLineBasedMessageParsing();
                    socket.UseEndPoint<MessagesEndPoint>();
                })
                .Build();
            var tcp = tcpHost.RunAsync();

            host.Run();

            tcpHost.Stop();

            tcp.Wait();
        }
    }
}
