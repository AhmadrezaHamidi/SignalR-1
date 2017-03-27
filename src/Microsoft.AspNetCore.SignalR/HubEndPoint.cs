// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.SignalR
{
    public class HubEndPoint<THub> : HubEndPoint<THub, IClientProxy> where THub : Hub<IClientProxy>
    {
        public HubEndPoint(HubLifetimeManager<THub> lifetimeManager,
                           IHubContext<THub> hubContext,
                           HubProtocolRegistry registry,
                           IOptions<EndPointOptions<HubEndPoint<THub, IClientProxy>>> endPointOptions,
                           ILogger<HubEndPoint<THub>> logger,
                           IServiceScopeFactory serviceScopeFactory)
            : base(lifetimeManager, hubContext, registry, endPointOptions, logger, serviceScopeFactory)
        {
        }
    }

    public class HubEndPoint<THub, TClient> : EndPoint, IInvocationBinder where THub : Hub<TClient>
    {
        private readonly Dictionary<string, HubMethodDescriptor> _methods = new Dictionary<string, HubMethodDescriptor>(StringComparer.OrdinalIgnoreCase);

        private readonly HubLifetimeManager<THub> _lifetimeManager;
        private readonly IHubContext<THub, TClient> _hubContext;
        private readonly ILogger<HubEndPoint<THub, TClient>> _logger;
        private readonly HubProtocolRegistry _registry;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public HubEndPoint(HubLifetimeManager<THub> lifetimeManager,
                           IHubContext<THub, TClient> hubContext,
                           HubProtocolRegistry registry,
                           IOptions<EndPointOptions<HubEndPoint<THub, TClient>>> endPointOptions,
                           ILogger<HubEndPoint<THub, TClient>> logger,
                           IServiceScopeFactory serviceScopeFactory)
        {
            _lifetimeManager = lifetimeManager;
            _hubContext = hubContext;
            _registry = registry;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;

            DiscoverHubMethods();
        }

        public override async Task OnConnectedAsync(Connection connection)
        {
            try
            {
                await _lifetimeManager.OnConnectedAsync(connection);
                await RunHubAsync(connection);
            }
            finally
            {
                await _lifetimeManager.OnDisconnectedAsync(connection);
            }
        }

        private async Task RunHubAsync(Connection connection)
        {
            await HubOnConnectedAsync(connection);

            try
            {
                await DispatchMessagesAsync(connection);
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, "Error when processing requests.");
                await HubOnDisconnectedAsync(connection, ex);
                throw;
            }

            await HubOnDisconnectedAsync(connection, null);
        }

        private async Task HubOnConnectedAsync(Connection connection)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var hubActivator = scope.ServiceProvider.GetRequiredService<IHubActivator<THub, TClient>>();
                    var hub = hubActivator.Create();
                    try
                    {
                        InitializeHub(hub, connection);
                        await hub.OnConnectedAsync();
                    }
                    finally
                    {
                        hubActivator.Release(hub);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, "Error when invoking OnConnectedAsync on hub.");
                throw;
            }
        }

        private async Task HubOnDisconnectedAsync(Connection connection, Exception exception)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var hubActivator = scope.ServiceProvider.GetRequiredService<IHubActivator<THub, TClient>>();
                    var hub = hubActivator.Create();
                    try
                    {
                        InitializeHub(hub, connection);
                        await hub.OnDisconnectedAsync(exception);
                    }
                    finally
                    {
                        hubActivator.Release(hub);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, "Error when invoking OnDisconnectedAsync on hub.");
                throw;
            }
        }

        private async Task DispatchMessagesAsync(Connection connection)
        {
            var invocationAdapter = _registry.GetProtocol(connection.Metadata.Get<string>("formatType"));

            // We use these for error handling. Since we dispatch multiple hub invocations
            // in parallel, we need a way to communicate failure back to the main processing loop. The
            // cancellation token is used to stop reading from the channel, the tcs
            // is used to get the exception so we can bubble it up the stack
            var cts = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<object>();

            try
            {
                while (await connection.Transport.Input.WaitToReadAsync(cts.Token))
                {
                    while (connection.Transport.Input.TryRead(out var incomingMessage))
                    {
                        HubMessage message;
                        using (var inputStream = new MemoryStream(incomingMessage.Payload))
                        {
                            message = invocationAdapter.ParseMessage(inputStream, this);
                        }

                        // Is there a better way of detecting that a connection was closed?
                        if (message == null)
                        {
                            break;
                        }

                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("Received hub message: {message}", message);
                        }

                        switch (message)
                        {
                            case InvocationMessage invocation:
                                // Don't wait on the result of execution, continue processing other
                                // incoming messages on this connection.
                                var ignore = ProcessInvocation(connection, invocationAdapter, invocation, cts, tcs);
                                break;
                            default:
                                // Drop the message for now, and log it
                                _logger.LogWarning("TODO: Dropped unprocessed non-invocation message: {message}", message);
                                break;
                        }

                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Await the task so the exception bubbles up to the caller
                await tcs.Task;
            }
        }

        private async Task ProcessInvocation(Connection connection,
                                             IHubProtocol protocol,
                                             InvocationMessage invocation,
                                             CancellationTokenSource cts,
                                             TaskCompletionSource<object> tcs)
        {
            try
            {
                // If an unexpected exception occurs then we want to kill the entire connection
                // by ending the processing loop
                await Execute(connection, protocol, invocation);
            }
            catch (Exception ex)
            {
                // Set the exception on the task completion source
                tcs.TrySetException(ex);

                // Cancel reading operation
                cts.Cancel();
            }
        }

        private Task Execute(Connection connection, IHubProtocol protocol, InvocationMessage invocation)
        {
            if (!_methods.TryGetValue(invocation.Target, out var descriptor))
            {
                // If there's no method then return a failed response for this request
                var error = $"Unknown hub method '{invocation.Target}'";

                return HandleError(error, connection, protocol, invocation);
            }


            var methodExecutor = descriptor.MethodExecutor;

            var scope = _serviceScopeFactory.CreateScope();
            var hubActivator = scope.ServiceProvider.GetRequiredService<IHubActivator<THub, TClient>>();
            var hub = hubActivator.Create();

            InitializeHub(hub, connection);

            IObservable<object> result;
            try
            {
                result = methodExecutor.Execute(hub, invocation.Arguments);
            }
            catch (TargetInvocationException tex)
            {
                return HandleError(tex.InnerException.Message, connection, protocol, invocation);
            }

            var tcs = new TaskCompletionSource<object>();
            result.Subscribe(new HubObserver(this, hubActivator, hub, tcs, invocation.InvocationId, protocol, connection));
            return tcs.Task;
        }

        private async Task HandleError(string error, Connection connection, IHubProtocol protocol, InvocationMessage invocation)
        {
            _logger.LogError("Unknown hub method '{method}'", invocation.Target);
            await SendMessage(connection, protocol, new ResultMessage(invocation.InvocationId, error, payload: null));
            await SendMessage(connection, protocol, new CompletionMessage(invocation.InvocationId));
        }

        private void InitializeHub(THub hub, Connection connection)
        {
            hub.Clients = _hubContext.Clients;
            hub.Context = new HubCallerContext(connection);
            hub.Groups = new GroupManager<THub>(connection, _lifetimeManager);
        }

        private void DiscoverHubMethods()
        {
            var typeInfo = typeof(THub).GetTypeInfo();

            foreach (var methodInfo in typeInfo.DeclaredMethods.Where(m => IsHubMethod(m)))
            {
                var methodName = methodInfo.Name;

                if (_methods.ContainsKey(methodName))
                {
                    throw new NotSupportedException($"Duplicate definitions of '{methodInfo.Name}'. Overloading is not supported.");
                }

                var executor = ObjectMethodExecutor.Create(methodInfo, typeInfo);
                _methods[methodName] = new HubMethodDescriptor(executor);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Hub method '{methodName}' is bound", methodName);
                }
            };
        }

        private static bool IsHubMethod(MethodInfo methodInfo)
        {
            // TODO: Add more checks
            if (!methodInfo.IsPublic || methodInfo.IsSpecialName)
            {
                return false;
            }

            var baseDefinition = methodInfo.GetBaseDefinition().DeclaringType;
            var baseType = baseDefinition.GetTypeInfo().IsGenericType ? baseDefinition.GetGenericTypeDefinition() : baseDefinition;
            if (typeof(Hub<>) == baseType)
            {
                return false;
            }

            return true;
        }

        private static Message EncodeMessage(IHubProtocol protocol, HubMessage result)
        {
            // TODO: Pool memory
            Message outMessage;
            using (var outStream = new MemoryStream())
            {
                protocol.WriteMessage(result, outStream);
                outStream.Flush();

                outMessage = new Message(outStream.ToArray(), MessageType.Text, endOfMessage: true);
            }

            return outMessage;
        }

        private async Task SendMessage(Connection connection, IHubProtocol protocol, HubMessage hubMessage)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Sending hub message: {message}", hubMessage);
            }

            var message = EncodeMessage(protocol, hubMessage);
            while (await connection.Transport.Output.WaitToWriteAsync())
            {
                if (connection.Transport.Output.TryWrite(message))
                {
                    break;
                }
            }
        }

        Type IInvocationBinder.GetReturnType(long invocationId)
        {
            throw new NotSupportedException("Can't accept results from Clients yet");
        }

        Type[] IInvocationBinder.GetParameterTypes(string methodName)
        {
            HubMethodDescriptor descriptor;
            if (!_methods.TryGetValue(methodName, out descriptor))
            {
                return Type.EmptyTypes;
            }
            return descriptor.ParameterTypes;
        }

        // REVIEW: We can decide to move this out of here if we want pluggable hub discovery
        private class HubMethodDescriptor
        {
            public HubMethodDescriptor(ObjectMethodExecutor methodExecutor)
            {
                MethodExecutor = methodExecutor;
                ParameterTypes = methodExecutor.MethodParameters.Select(p => p.ParameterType).ToArray();
            }

            public ObjectMethodExecutor MethodExecutor { get; }

            public Type[] ParameterTypes { get; }
        }

        private class HubObserver : IObserver<object>, IDisposable
        {
            private TaskCompletionSource<object> _tcs;
            private long _invocationId;
            private IHubProtocol _protocol;
            private Connection _connection;
            private readonly HubEndPoint<THub, TClient> _ep;
            private readonly IHubActivator<THub, TClient> _hubActivator;
            private readonly THub _hub;
            private int _disposed = 0;

            public HubObserver(HubEndPoint<THub, TClient> ep, IHubActivator<THub, TClient> hubActivator, THub hub, TaskCompletionSource<object> tcs, long invocationId, IHubProtocol protocol, Connection connection)
            {
                _ep = ep;
                _tcs = tcs;
                _invocationId = invocationId;
                _protocol = protocol;
                _connection = connection;
                _hubActivator = hubActivator;
                _hub = hub;
            }

            public void OnCompleted()
            {
                Dispose();
                _tcs.TrySetResult(null);
            }

            public void OnError(Exception error)
            {
                Dispose();
                _tcs.TrySetResult(null);
                _ = _ep.SendMessage(_connection, _protocol, new ResultMessage(_invocationId, error: error.Message, payload: null));
            }

            public void OnNext(object value)
            {
                if (!_tcs.Task.IsCompleted)
                {
                    _ = _ep.SendMessage(_connection, _protocol, new ResultMessage(_invocationId, error: null, payload: value));
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    // It was previously undisposed. So do the clean-up
                    _hubActivator.Release(_hub);
                }
            }
        }
    }
}
