using System;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Sockets.Client;
using Microsoft.Extensions.Logging;

namespace ClientSample
{
    public class StreamSample
    {
        public static async Task MainAsync(string[] args)
        {
            if(args.Contains("--debug"))
            {
                args = args.Where(a => a != "--debug").ToArray();
                Console.WriteLine($"Waiting for debugger. Process ID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
                Console.WriteLine("Press ENTER to continue...");
                Console.ReadLine();
            }

            var baseUrl = "http://localhost:5000/stockPrices";
            if (args.Length > 0)
            {
                baseUrl = args[0];
            }

            var loggerFactory = new LoggerFactory();
            //loggerFactory.AddConsole(LogLevel.Debug);
            var logger = loggerFactory.CreateLogger<Program>();

            using (var httpClient = new HttpClient(new LoggingMessageHandler(loggerFactory, new HttpClientHandler())))
            {
                logger.LogInformation("Connecting to {0}", baseUrl);
                var transport = new LongPollingTransport(httpClient, loggerFactory);
                var connection = new HubConnection(new Uri(baseUrl), new JsonHubProtocol(), loggerFactory);
                try
                {
                    await connection.StartAsync(transport, httpClient);
                    logger.LogInformation("Connected to {0}", baseUrl);

                    var cts = new CancellationTokenSource();
                    Console.CancelKeyPress += (sender, a) =>
                    {
                        a.Cancel = true;
                        logger.LogInformation("Stopping loops...");
                        cts.Cancel();
                    };

                    // Call the method on the server
                    var result = await connection.Invoke<IObservable<string>>("SubscribeToStock", "MSFT");

                    foreach(var item in )

                    await result.ForEachAsync((s) => Console.WriteLine(s), cts.Token);
                }
                finally
                {
                    await connection.DisposeAsync();
                }
            }
        }

        private class TestObserver : IObserver<string>
        {
            private TaskCompletionSource<object> _tcs;

            public TestObserver(TaskCompletionSource<object> tcs)
            {
                _tcs = tcs;
            }

            public void OnCompleted()
            {
                _tcs.TrySetResult(null);
            }

            public void OnError(Exception error)
            {
                _tcs.TrySetException(error);
            }

            public void OnNext(string value)
            {
                Console.WriteLine("Received: " + value);
            }
        }
    }
}
