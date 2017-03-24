// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.Sockets;
using Microsoft.AspNetCore.Sockets.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.AspNetCore.SignalR.Client
{
    public class HubConnection
    {
        private readonly ILogger _logger;
        private readonly IConnection _connection;
        private readonly IHubProtocol _protocol;
        private readonly HubBinder _binder;

        private readonly CancellationTokenSource _connectionActive = new CancellationTokenSource();

        // We need to ensure pending calls added after a connection failure don't hang. Right now the easiest thing to do is lock.
        private readonly object _pendingCallsLock = new object();
        private readonly Dictionary<long, InvocationRequest> _pendingCalls = new Dictionary<long, InvocationRequest>();

        private readonly ConcurrentDictionary<string, InvocationHandler> _handlers = new ConcurrentDictionary<string, InvocationHandler>();

        private long _nextId = 0;

        public event Action Connected
        {
            add { _connection.Connected += value; }
            remove { _connection.Connected -= value; }
        }

        public event Action<Exception> Closed
        {
            add { _connection.Closed += value; }
            remove { _connection.Closed -= value; }
        }

        public HubConnection(Uri url)
            : this(new Connection(url), new JsonHubProtocol(), null)
        { }

        public HubConnection(Uri url, ILoggerFactory loggerFactory)
            : this(new Connection(url), new JsonHubProtocol(), loggerFactory)
        { }

        public HubConnection(Uri url, IHubProtocol protocol, ILoggerFactory loggerFactory)
            : this(new Connection(url, loggerFactory), protocol, loggerFactory)
        { }

        public HubConnection(IConnection connection, IHubProtocol protocol, ILoggerFactory loggerFactory)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            _connection = connection;
            _binder = new HubBinder(this);
            _protocol = protocol;
            _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<HubConnection>();
            _connection.Received += OnDataReceived;
            _connection.Closed += Shutdown;
        }

        public Task StartAsync() => StartAsync(null, null);
        public Task StartAsync(HttpClient httpClient) => StartAsync(null, httpClient);
        public Task StartAsync(ITransport transport) => StartAsync(transport, null);

        public async Task StartAsync(ITransport transport, HttpClient httpClient)
        {
            await _connection.StartAsync(transport, httpClient);
        }

        public async Task DisposeAsync()
        {
            await _connection.DisposeAsync();
        }

        // TODO: Client return values/tasks?
        // TODO: Overloads for void hub methods
        // TODO: Overloads that use type parameters (like On<T1>, On<T1, T2>, etc.)
        public void On(string methodName, Type[] parameterTypes, Action<object[]> handler)
        {
            var invocationHandler = new InvocationHandler(parameterTypes, handler);
            _handlers.AddOrUpdate(methodName, invocationHandler, (_, __) => invocationHandler);
        }

        public Task<T> Invoke<T>(string methodName, params object[] args) => Invoke<T>(methodName, CancellationToken.None, args);
        public async Task<T> Invoke<T>(string methodName, CancellationToken cancellationToken, params object[] args) => ((T)(await Invoke(methodName, typeof(T), cancellationToken, args)));

        public Task<object> Invoke(string methodName, Type returnType, params object[] args) => Invoke(methodName, returnType, CancellationToken.None, args);
        public async Task<object> Invoke(string methodName, Type returnType, CancellationToken cancellationToken, params object[] args)
        {
            _logger.LogTrace("Preparing invocation of '{0}', with return type '{1}' and {2} args", methodName, returnType.AssemblyQualifiedName, args.Length);

            // Create an invocation request.
            var request = new InvocationMessage(GetNextId(), methodName, args);

            // I just want an excuse to use 'irq' as a variable name...
            _logger.LogDebug("Registering Invocation ID '{0}' for tracking", request.InvocationId);
            var irq = new InvocationRequest(cancellationToken, returnType);

            lock (_pendingCallsLock)
            {
                if (_connectionActive.IsCancellationRequested)
                {
                    throw new InvalidOperationException("Connection has been terminated.");
                }
                _pendingCalls.Add(request.InvocationId, irq);
            }

            // Trace the invocation, but only if that logging level is enabled (because building the args list is a bit slow)
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                var argsList = string.Join(", ", args.Select(a => a.GetType().FullName));
                _logger.LogTrace("Invocation #{0}: {1} {2}({3})", request.InvocationId, returnType.FullName, methodName, argsList);
            }

            try
            {
                byte[] payload;
                using (var stream = new MemoryStream())
                {
                    _protocol.WriteMessage(request, stream);
                    stream.Flush();
                    payload = stream.ToArray();
                }

                _logger.LogInformation("Sending Invocation #{0}", request.InvocationId);

                await _connection.SendAsync(payload, _protocol.MessageType, cancellationToken);
                _logger.LogInformation("Sending Invocation #{0} complete", request.InvocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, "Sending Invocation #{0} failed", request.InvocationId);
                irq.Subject.OnError(ex);
                lock (_pendingCallsLock)
                {
                    _pendingCalls.Remove(request.InvocationId);
                }
            }

            // Return the completion task. It will be completed by ReceiveMessages when the response is received.
            return await AdaptObservable(returnType, irq.Subject);
        }

        private static readonly MethodInfo CastMethod = typeof(Observable).GetRuntimeMethods().FirstOrDefault(m => m.Name == nameof(Observable.Cast));
        private Task<object> AdaptObservable(Type returnType, Subject<object> subject)
        {
            var typeInfo = returnType.GetTypeInfo();
            if (typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(IObservable<>))
            {
                var targetType = typeInfo.GenericTypeArguments[0];
                var method = CastMethod.MakeGenericMethod(targetType);

                // Return the observable "immediately"
                return Task.FromResult(method.Invoke(null, new[] { subject }));
            }
            else
            {
                // Materialize the first (and only) item, when it arrives.
                return (Task<object>)subject.SingleAsync();
            }
        }

        private void OnDataReceived(byte[] data, MessageType messageType)
        {
            HubMessage message;
            using (var stream = new MemoryStream(data))
            {
                message = _protocol.ParseMessage(new MemoryStream(data), _binder);
            }

            InvocationRequest irq;
            switch (message)
            {
                case InvocationMessage invocation:
                    DispatchInvocation(invocation, _connectionActive.Token);
                    break;
                case ResultMessage result:
                    lock (_pendingCallsLock)
                    {
                        _connectionActive.Token.ThrowIfCancellationRequested();
                        irq = _pendingCalls[result.InvocationId];
                        DispatchInvocationResult(result, irq, _connectionActive.Token);
                    }
                    break;
                case CompletionMessage completion:
                    lock (_pendingCallsLock)
                    {
                        _connectionActive.Token.ThrowIfCancellationRequested();
                        irq = _pendingCalls[completion.InvocationId];
                        _pendingCalls.Remove(completion.InvocationId);
                    }
                    irq.Subject.OnCompleted();
                    break;
            }
        }

        private void Shutdown(Exception ex = null)
        {
            _logger.LogTrace("Shutting down connection");
            if (ex != null)
            {
                _logger.LogError("Connection is shutting down due to an error: {0}", ex);
            }

            lock (_pendingCallsLock)
            {
                _connectionActive.Cancel();
                foreach (var call in _pendingCalls.Values)
                {
                    if (ex != null)
                    {
                        call.Subject.OnError(ex);
                    }
                    else
                    {
                        call.Subject.OnError(new OperationCanceledException());
                    }
                    call.Subject.OnCompleted();
                }
                _pendingCalls.Clear();
            }
        }

        private void DispatchInvocation(InvocationMessage invocationDescriptor, CancellationToken cancellationToken)
        {
            // Find the handler
            if (!_handlers.TryGetValue(invocationDescriptor.Target, out InvocationHandler handler))
            {
                _logger.LogWarning("Failed to find handler for '{0}' method", invocationDescriptor.Target);
            }

            // TODO: Return values
            // TODO: Dispatch to a sync context to ensure we aren't blocking this loop.
            handler.Handler(invocationDescriptor.Arguments);
        }

        private void DispatchInvocationResult(ResultMessage result, InvocationRequest irq, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Received Result for Invocation #{0}", result.InvocationId);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // If the invocation hasn't been cancelled, dispatch the result
            if (!irq.CancellationToken.IsCancellationRequested)
            {
                irq.Registration.Dispose();

                // Complete the request based on the result
                // TODO: the TrySetXYZ methods will cause continuations attached to the Task to run, so we should dispatch to a sync context or thread pool.
                if (!string.IsNullOrEmpty(result.Error))
                {
                    _logger.LogInformation("Completing Invocation #{0} with error: {1}", result.InvocationId, result.Error);
                    irq.Subject.OnError(new Exception(result.Error));
                }
                else
                {
                    if (irq.HasResult)
                    {
                        irq.Subject.OnError(new InvalidCastException("Received multiple results, but expected only 1 result"));
                    }
                    else
                    {
                        _logger.LogInformation("Received result of type {1} for Invocation #{0}", result.InvocationId, result.Payload?.GetType()?.FullName ?? "<<void>>");
                        irq.Subject.OnNext(result.Payload);
                    }
                }
            }
        }

        private long GetNextId() => Interlocked.Increment(ref _nextId);

        private class HubBinder : IInvocationBinder
        {
            private HubConnection _connection;

            public HubBinder(HubConnection connection)
            {
                _connection = connection;
            }

            public Type GetReturnType(long invocationId)
            {
                if (!_connection._pendingCalls.TryGetValue(invocationId, out InvocationRequest irq))
                {
                    _connection._logger.LogError("Unsolicited response received for invocation '{0}'", invocationId);
                    return null;
                }

                var typeInfo = irq.ResultType.GetTypeInfo();
                if (typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(IObservable<>))
                {
                    return typeInfo.GenericTypeArguments[0];
                }
                else
                {
                    return irq.ResultType;
                }
            }

            public Type[] GetParameterTypes(string methodName)
            {
                if (!_connection._handlers.TryGetValue(methodName, out InvocationHandler handler))
                {
                    _connection._logger.LogWarning("Failed to find handler for '{0}' method", methodName);
                    return Type.EmptyTypes;
                }
                return handler.ParameterTypes;
            }
        }

        private struct InvocationHandler
        {
            public Action<object[]> Handler { get; }
            public Type[] ParameterTypes { get; }

            public InvocationHandler(Type[] parameterTypes, Action<object[]> handler)
            {
                Handler = handler;
                ParameterTypes = parameterTypes;
            }
        }

        private class InvocationRequest
        {
            public Type ResultType { get; }
            public CancellationToken CancellationToken { get; }
            public CancellationTokenRegistration Registration { get; }
            public Subject<object> Subject { get; set; }
            public bool HasResult { get; set; }

            public InvocationRequest(CancellationToken cancellationToken, Type resultType)
            {
                var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                CancellationToken = cancellationToken;
                Registration = cancellationToken.Register(() => tcs.TrySetCanceled());
                HasResult = false;
                ResultType = resultType;

                Subject = new Subject<object>();
            }
        }
    }
}
