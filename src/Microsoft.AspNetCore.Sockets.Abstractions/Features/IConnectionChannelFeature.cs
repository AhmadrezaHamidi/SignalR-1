namespace Microsoft.AspNetCore.Sockets.Features
{
    public interface IConnectionChannelFeature
    {
        IChannelConnection<Message> Channel { get; }
    }
}
