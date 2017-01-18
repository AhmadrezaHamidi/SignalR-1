using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace SocketsSample.Hubs
{
    public class GameLogic
    {
        private readonly HashSet<string> _connections = new HashSet<string>();
        private bool _gameRunning;
        private readonly IHubContext<Game> _hubContext;
        private Task _gameTask;

        public GameLogic(IHubContext<Game> hubContext)
        {
            _hubContext = hubContext;
        }

        public void RemoveConnection(string connectionId)
        {
            lock (_connections)
            {
                _connections.Remove(connectionId);
            }
        }

        public void AddConnection(string connectionId)
        {
            lock (_connections)
            {
                _connections.Add(connectionId);

                if (_connections.Count == 2 && !_gameRunning)
                {
                    _gameTask = RunGame();
                }
            }
        }

        private async Task RunGame()
        {
            _gameRunning = true;

            // Start the game on all clients
            var winner = await Task.WhenAny(InvokeOnAll("Ask"));

            var result = await winner;

            var ignore = _hubContext.Clients.Client(result.ConnectionId).InvokeAsync("Send", "You win :)");

            foreach (var item in _connections)
            {
                if (item == result.ConnectionId)
                {
                    continue;
                }

                ignore = _hubContext.Clients.Client(item).InvokeAsync("Send", "You lose :(");
            }
        }

        private Task<Result>[] InvokeOnAll(string method, params object[] args)
        {
            Task<Result>[] tasks;
            lock (_connections)
            {
                tasks = new Task<Result>[_connections.Count];

                int i = 0;
                foreach (var id in _connections)
                {
                    tasks[i++] = Invoke(id, method, args);
                }
            }

            return tasks;
        }

        private async Task<Result> Invoke(string id, string method, object[] args)
        {

            return new Result
            {
                Value = await _hubContext.Clients.Client(id).InvokeAsync(method, args),
                ConnectionId = id
            };
        }

        private class Result
        {
            public object Value { get; set; }
            public string ConnectionId { get; set; }
        }
    }

    public class Game : Hub
    {
        private readonly GameLogic _gameLogic;

        public Game(GameLogic gameLogic)
        {
            _gameLogic = gameLogic;
        }

        public override Task OnConnectedAsync()
        {
            _gameLogic.AddConnection(Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            _gameLogic.RemoveConnection(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
