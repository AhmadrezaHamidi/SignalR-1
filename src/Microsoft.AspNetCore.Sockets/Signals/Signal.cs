using System.Threading.Tasks;
using System.Threading.Tasks.Channels;

namespace Microsoft.AspNetCore.Sockets.Signals
{
    public abstract class Signal
    {
        public virtual Task OnConnectedAsync(Connection connection) => Task.CompletedTask;
    }
}
