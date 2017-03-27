using System;
using System.Threading;

namespace Microsoft.AspNetCore.SignalR.Internal
{
    internal static class Disposable
    {
        public static IDisposable Null => NullDisposable.Instance;

        public static IDisposable FromCancellationTokenSource(CancellationTokenSource cancellationTokenSource)
        {
            return Create(cts => cts.Cancel(), cancellationTokenSource);
        }

        public static IDisposable Create<T>(Action<T> disposeAction, T state)
        {
            return new ActionDisposable<T>(disposeAction, state);
        }

        private class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new NullDisposable();

            private NullDisposable()
            {
            }

            public void Dispose()
            {
            }
        }

        private class ActionDisposable<T> : IDisposable
        {
            private readonly Action<T> _disposeAction;
            private readonly T _state;

            public ActionDisposable(Action<T> disposeAction, T state)
            {
                _disposeAction = disposeAction;
                _state = state;
            }

            public void Dispose()
            {
                _disposeAction(_state);
            }
        }
    }
}
