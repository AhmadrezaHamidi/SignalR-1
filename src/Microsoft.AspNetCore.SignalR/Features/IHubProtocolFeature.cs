using Microsoft.AspNetCore.SignalR.Internal.Protocol;

namespace Microsoft.AspNetCore.SignalR.Features
{
    public interface IHubProtocolFeature
    {
        IHubProtocol HubProtocol { get; set; }
    }
}
