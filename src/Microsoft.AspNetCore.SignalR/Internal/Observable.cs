using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Microsoft.AspNetCore.SignalR.Internal
{
    internal static class Observable
    {
        public static IObservable<object> Completed => CompletedObservable.Instance;

        public static IObservable<object> FromAwaitable(Type returnType, object awaiter, Func<object, bool> isCompleted, Func<object, object> getResult, Action<object, Action> onCompleted)
        {
            return new AwaiterObservable(returnType, awaiter, isCompleted, getResult, onCompleted);
        }

        public static IObservable<object> FromError(Exception ex)
        {
            return new ErrorObservable(ex);
        }

        public static IObservable<object> FromInvocation(Func<object> invocation)
        {
            object returnValue;
            try
            {
                returnValue = invocation();
            }
            catch(Exception ex)
            {
                return FromError(ex);
            }
            return FromValue(returnValue);
        }

        public static IObservable<object> FromValue(object returnValue)
        {
            return new ValueObservable(returnValue);
        }

        private class CompletedObservable : IObservable<object>
        {
            public static readonly CompletedObservable Instance = new CompletedObservable();

            private CompletedObservable()
            {

            }

            public IDisposable Subscribe(IObserver<object> observer)
            {
                observer.OnCompleted();
                return Disposable.Null;
            }
        }

        private class ErrorObservable : IObservable<object>
        {
            private readonly Exception _error;

            public ErrorObservable(Exception error)
            {
                _error = error;
            }

            public IDisposable Subscribe(IObserver<object> observer)
            {
                observer.OnError(_error);
                // OnError is a terminator, no need to call OnComplete
                return Disposable.Null;
            }
        }

        private class ValueObservable : IObservable<object>
        {
            private readonly object _value;

            public ValueObservable(object value)
            {
                _value = value;
            }

            public IDisposable Subscribe(IObserver<object> observer)
            {
                observer.OnNext(_value);
                observer.OnCompleted();
                return Disposable.Null;
            }
        }

        private class AwaiterObservable : IObservable<object>
        {
            private readonly object _awaiter;
            private readonly Func<object, bool> _isCompleted;
            private readonly Func<object, object> _getResult;
            private readonly Action<object, Action> _onCompleted;
            private readonly Type _returnType;

            public AwaiterObservable(Type returnType, object awaiter, Func<object, bool> isCompleted, Func<object, object> getResult, Action<object, Action> onCompleted)
            {
                _returnType = returnType;
                _awaiter = awaiter;
                _isCompleted = isCompleted;
                _getResult = getResult;
                _onCompleted = onCompleted;
            }

            public IDisposable Subscribe(IObserver<object> observer)
            {
                if (_isCompleted(_awaiter))
                {
                    ManifestResult(observer);
                    return Disposable.Null;
                }
                else
                {
                    var cts = new CancellationTokenSource();
                    var token = cts.Token;
                    _onCompleted(_awaiter, () =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            ManifestResult(observer);
                        }
                    });
                    return Disposable.FromCancellationTokenSource(cts);
                }
            }

            private void ManifestResult(IObserver<object> observer)
            {
                object returnValue;
                try
                {
                    returnValue = _getResult(_awaiter);
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                    return;
                }
                if (_returnType != typeof(void))
                {
                    observer.OnNext(returnValue);
                }
                observer.OnCompleted();
            }
        }
    }
}
