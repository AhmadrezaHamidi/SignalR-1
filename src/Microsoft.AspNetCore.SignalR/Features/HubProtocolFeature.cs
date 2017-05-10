using Microsoft.AspNetCore.SignalR.Internal.Protocol;

namespace Microsoft.AspNetCore.SignalR.Features
{
    public class HubProtocolFeature : IHubProtocolFeature
    {
        public IHubProtocol HubProtocol { get; set;  }
    }
}
