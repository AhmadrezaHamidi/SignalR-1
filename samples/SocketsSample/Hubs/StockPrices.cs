using System;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;

namespace SocketsSample.Hubs
{
    public class StockPrices : Hub
    {
        public IObservable<string> SubscribeToStock(string symbol) =>
            Observable.Timer(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1))
                .Select((_, index) => $"{symbol}: ${Math.Pow(10, (index + 1)):0.00}/share")
                .TakeUntil(DateTimeOffset.UtcNow + TimeSpan.FromSeconds(20));
    }
}
