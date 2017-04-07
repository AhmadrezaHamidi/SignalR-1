using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace SocketsSample.Hubs
{
    public class StockPrices : Hub
    {
        public IObservable<string> SubscribeToStock(string symbol) =>
            Observable.Timer(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1))
                .Select((_, index) => $"{symbol}: ${Math.Pow(10, (index + 1)):0.00}/share")
                .TakeUntil(DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20));

        public IEnumerable<string> SubscribeToStock(string symbole, CancellationToken cancellatioNToken)
        {
            try
            {
                for (var i = 0; i < 10; i++)
                {
                    if (cancellationToken.IsCancelled)
                    {
                        yield break;
                    }
                    yield return "$0.00/share";
                    await Task.Delay(1000);
                }
            } finally
            {
                //
            }
        }

        public interface IAsyncEnumerator<out T>
        {
            // Yields true when there's an item, false when the enumerator is complete
            // Waits until an item is available
            Task<bool> MoveNext();
            T Current { get; }

            // Get the current item, returns the item, sets gotItem to true if an item was available
            // gotItem == false implies you need to call MoveNext again to see if there's more data
            T HardToUseTryNext(out bool gotItem);
        }

        private static bool TryNext<T>(this IAsyncEnumerator<T> self, out T item)
        {
            item = self.HardToUseTryNext(out var val);
            return val;
        }

        private Subject<string> _subject = new Subject<string>();
        public IObservable<string> Subscribe()
        {
            return _subject;
        }

        public void Send(string message)
        {
            Clients.All.InvokeAsync("ReceiveMessage", message);
            //_subject.OnNext(message);
        }
    }
}
