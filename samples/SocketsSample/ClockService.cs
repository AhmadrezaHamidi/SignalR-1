using System;
using Microsoft.AspNetCore.Hosting;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using SocketsSample.Hubs;

namespace SocketsSample
{
    public class ClockService : IHostedService
    {
        private Timer _timer;
        private readonly IHubContext<Clock> _context;

        public ClockService(IHubContext<Clock> context)
        {
            _context = context;
        }

        public void Start()
        {
            _timer = new Timer(Tick, this, 0, 1000);
        }

        private void Tick(object state)
        {
            _context.Clients.All.InvokeAsync("Tick", DateTime.Now.Ticks);
        }

        public void Stop()
        {
            _timer.Dispose();
        }
    }
}