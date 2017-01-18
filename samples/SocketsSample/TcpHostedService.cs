using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Sockets;
using Microsoft.AspNetCore.Sockets.Internal;
using Microsoft.Extensions.Logging;
using SocketsSample.EndPoints;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Channels;

namespace SocketsSample
{
    public class TcpHostedService<TEndPoint> : IHostedService where TEndPoint : Microsoft.AspNetCore.Sockets.EndPoint
    {
        private readonly TcpListener _listener = new TcpListener(IPAddress.Any, 5002);
        private Task _loop;
        private readonly ILogger<TcpHostedService<TEndPoint>> _logger;
        private readonly TEndPoint _endPoint;

        public TcpHostedService(ILogger<TcpHostedService<TEndPoint>> logger, TEndPoint endPoint)
        {
            _logger = logger;
            _endPoint = endPoint;
        }

        public void Start()
        {
            _listener.Start();

            _logger.LogDebug("Starting on {address}", _listener.LocalEndpoint);

            _loop = HandleAll();
        }

        private async Task HandleAll()
        {
            try
            {
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync();

                    _logger.LogDebug("Accepted connection {connection}", client.Client.LocalEndPoint);

                    // Dispatch
                    var ignore = Task.Run(() => ProcessConnection(client));
                }
            }
            catch (Exception)
            {

            }
        }

        private async Task ProcessConnection(TcpClient client)
        {
            var id = Guid.NewGuid().ToString();
            var transport = Channel.CreateUnbounded<Message>();
            var application = Channel.CreateUnbounded<Message>();

            var connection = new Connection(id, new ChannelConnection<Message>(transport, application));
            var applicationTask = _endPoint.OnConnectedAsync(connection);
            var ns = client.GetStream();

            var writing = WriteToClient(ns, application);
            var reading = WriteToTransport(ns, transport);

            var result = await Task.WhenAny(reading, writing, applicationTask);

            if (result != applicationTask)
            {
                client.Close();
            }

            connection.Dispose();

            await Task.WhenAll(reading, writing, applicationTask);
        }

        private async Task WriteToTransport(NetworkStream ns, IChannel<Message> transport)
        {
            try
            {
                while (true)
                {
                    var buffer = new byte[1024];
                    int read = await ns.ReadAsync(buffer, 0, buffer.Length);

                    _logger.LogDebug("Read {bytes} bytes from the TCP connection", read);

                    if (read == 0)
                    {
                        break;
                    }

                    var payload = ReadableBuffer.Create(buffer, 0, read).Preserve();
                    var message = new Message(payload, Format.Text, endOfMessage: true);

                    while (await transport.WaitToWriteAsync())
                    {
                        if (transport.TryWrite(message))
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, "Failed to write to TCP connection");
            }
        }

        private async Task WriteToClient(NetworkStream ns, IChannel<Message> application)
        {
            while (await application.WaitToReadAsync())
            {
                try
                {
                    Message message;
                    while (application.TryRead(out message))
                    {
                        var buffer = message.Payload.Buffer.ToArray();
                        await ns.WriteAsync(buffer, 0, buffer.Length);

                        _logger.LogDebug("Wrote {bytes} bytes to the TCP connection", buffer.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(0, ex, "Failed to write to TCP connection");
                    break;
                }
            }
        }

        public void Stop()
        {
            _listener.Stop();

            _logger.LogDebug("Stopped the TCP listener");

            _loop.Wait();

            _logger.LogDebug("Application unwound");
        }
    }
}
