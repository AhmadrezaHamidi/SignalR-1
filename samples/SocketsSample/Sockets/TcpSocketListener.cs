using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Sockets;
using Microsoft.AspNetCore.Sockets.Features;

namespace SocketsSample.Sockets
{
    public class TcpSocketListener : ISocketListener
    {
        private long _nextId;
        private readonly IPEndPoint _binding;
        private readonly ConcurrentDictionary<long, Task> _clientTasks = new ConcurrentDictionary<long, Task>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly PipeFactory _factory;

        public TcpSocketListener(IPEndPoint binding)
        {
            _factory = new PipeFactory(BufferPool.Default);
            _binding = binding;
        }

        public async Task RunAsync(IServiceProvider services, SocketDelegate socketApp)
        {
            var listener = new TcpListener(_binding);

            listener.Start();

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync();
                var id = Interlocked.Increment(ref _nextId);
                if (!_clientTasks.TryAdd(id, RunClient(id, services, socketApp, client, _cancellationTokenSource.Token)))
                {
                    throw new InvalidOperationException("Duplicate connection ID!");
                }
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
        }

        private async Task RunClient(long id, IServiceProvider services, SocketDelegate socketApp, TcpClient client, CancellationToken token)
        {
            try
            {
                var connection = new DefaultConnectionContext();

                var localEP = (IPEndPoint)client.Client.LocalEndPoint;
                var remoteEP = (IPEndPoint)client.Client.RemoteEndPoint;

                connection.ConnectionServices = services;
                connection.Features.Set<IHttpConnectionFeature>(new HttpConnectionFeature()
                {
                    ConnectionId = $"tcp:{id}",
                    LocalIpAddress = localEP.Address,
                    LocalPort = localEP.Port,
                    RemoteIpAddress = remoteEP.Address,
                    RemotePort = remoteEP.Port
                });
                connection.Features.Set<IConnectionPipeFeature>(new ConnectionPipeFeature(_factory.CreateConnection(client.GetStream())));
                connection.Features.Set<IHttpRequestLifetimeFeature>(new HttpRequestLifetimeFeature()
                {
                     RequestAborted = token
                });

                // Run the app!
                await socketApp(connection);
            }
            finally
            {
                // Remove this task from the bag
                _clientTasks.TryRemove(id, out _);
            }
        }
    }
}
