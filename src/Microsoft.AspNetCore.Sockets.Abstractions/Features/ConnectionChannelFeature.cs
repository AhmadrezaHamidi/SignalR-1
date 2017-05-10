namespace Microsoft.AspNetCore.Sockets.Features
{
    public class ConnectionChannelFeature : IConnectionChannelFeature
    {
        public IChannelConnection<Message> Channel { get; }

        public ConnectionChannelFeature(IChannelConnection<Message> channel)
        {
            Channel = channel;
        }
    }
}
