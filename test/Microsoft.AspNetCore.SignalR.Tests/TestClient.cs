// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Channels;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.Sockets;
using Microsoft.AspNetCore.Sockets.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.SignalR.Tests
{
    public class TestClient : IDisposable
    {
        private static int _id;
        private IHubProtocol _protocol;
        private CancellationTokenSource _cts;
        private TestBinder _binder;

        public Connection Connection;
        public IChannelConnection<Message> Application { get; }
        public Task Connected => Connection.Metadata.Get<TaskCompletionSource<bool>>("ConnectedTask").Task;

        public TestClient(IServiceProvider serviceProvider, string format = "json")
        {
            var transportToApplication = Channel.CreateUnbounded<Message>();
            var applicationToTransport = Channel.CreateUnbounded<Message>();

            Application = ChannelConnection.Create<Message>(input: applicationToTransport, output: transportToApplication);
            var transport = ChannelConnection.Create<Message>(input: transportToApplication, output: applicationToTransport);

            Connection = new Connection(Guid.NewGuid().ToString(), transport);
            Connection.Metadata["formatType"] = format;
            Connection.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, Interlocked.Increment(ref _id).ToString()) }));
            Connection.Metadata["ConnectedTask"] = new TaskCompletionSource<bool>();

            _protocol = serviceProvider.GetService<HubProtocolRegistry>().GetProtocol(format);

            _binder = new TestBinder();

            _cts = new CancellationTokenSource();
        }

        public async Task<ResultMessage> InvokeAndWaitForResult(string methodName, params object[] args)
        {
            await Invoke(methodName, args);

            var result = await Read<ResultMessage>();

            await Read<CompletionMessage>();

            return result;
        }

        public async Task Invoke(string methodName, params object[] args)
        {
            Message message;
            using (var stream = new MemoryStream())
            {
                _protocol.WriteMessage(new InvocationMessage(invocationId: 0, target: methodName, arguments: args), stream);
                stream.Flush();

                message = new Message(stream.ToArray(), MessageType.Binary, endOfMessage: true);
            }

            await Application.Output.WriteAsync(message);
        }

        public async Task<T> Read<T>() where T : HubMessage
        {
            while (await Application.Input.WaitToReadAsync(_cts.Token))
            {
                var value = TryRead<T>();

                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        public T TryRead<T>() where T : HubMessage
        {
            Message message;
            if (Application.Input.TryRead(out message))
            {
                HubMessage hubMessage;
                using (var stream = new MemoryStream(message.Payload))
                {
                    hubMessage = _protocol.ParseMessage(stream, _binder);
                }

                if(hubMessage is T t)
                {
                    return t;
                }
                else
                {
                    throw new InvalidOperationException($"Received unexpected message: {hubMessage}");
                }
            }

            return null;
        }

        public void Dispose()
        {
            _cts.Cancel();
            Connection.Dispose();
        }

        private class TestBinder : IInvocationBinder
        {
            public Type[] GetParameterTypes(string methodName)
            {
                // TODO: Possibly support actual client methods
                return new[] { typeof(object) };
            }

            public Type GetReturnType(long invocationId)
            {
                return typeof(object);
            }
        }
    }
}
