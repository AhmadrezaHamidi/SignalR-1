using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;

namespace Microsoft.AspNetCore.SignalR
{
    public interface IClientProxy
    {
        Task SendAsync(Message message, CancellationToken cancellationToken);
    }
}
