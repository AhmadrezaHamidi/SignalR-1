// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;
using Microsoft.AspNetCore.Sockets.Features;
using Microsoft.Extensions.Logging;

namespace SocialWeather
{
    public class SocialWeatherEndPoint : EndPoint
    {
        private readonly PersistentConnectionLifeTimeManager _lifetimeManager;
        private readonly FormatterResolver _formatterResolver;
        private readonly ILogger<SocialWeatherEndPoint> _logger;

        public SocialWeatherEndPoint(PersistentConnectionLifeTimeManager lifetimeManager,
            FormatterResolver formatterResolver, ILogger<SocialWeatherEndPoint> logger)
        {
            _lifetimeManager = lifetimeManager;
            _formatterResolver = formatterResolver;
            _logger = logger;
        }

        public async override Task OnConnectedAsync(ConnectionContext connection)
        {
            if(!connection.TryGetChannel(out var channel))
            {
                throw new InvalidOperationException("Unable to access connection Channel.");
            }

            _lifetimeManager.OnConnectedAsync(connection);
            await ProcessRequests(connection, channel);
            _lifetimeManager.OnDisconnectedAsync(connection);
        }

        public async Task ProcessRequests(ConnectionContext connection, IChannelConnection<Message> channel)
        {
            var formatter = _formatterResolver.GetFormatter<WeatherReport>(
                (string)connection.Items[ConnectionMetadataNames.Format]);

            while (await channel.Input.WaitToReadAsync())
            {
                if (channel.Input.TryRead(out var message))
                {
                    var stream = new MemoryStream();
                    await stream.WriteAsync(message.Payload, 0, message.Payload.Length);
                    stream.Position = 0;
                    var weatherReport = await formatter.ReadAsync(stream);
                    await _lifetimeManager.SendToAllAsync(weatherReport);
                }
            }
        }
    }
}
