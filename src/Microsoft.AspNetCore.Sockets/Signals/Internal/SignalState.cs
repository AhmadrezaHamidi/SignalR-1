namespace Microsoft.AspNetCore.Sockets.Signals
{
    public class SignalState
    {
        private readonly ConnectionList _connections = new ConnectionList();

        public string Name { get; }

        public SignalState(string name)
        {
            Name = name;
        }

        public void AddConnection(Connection connection)
        {
            _connections.Add(connection);
        }
    }
}
