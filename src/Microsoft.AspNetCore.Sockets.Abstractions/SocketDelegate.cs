using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Sockets
{
    public delegate Task SocketDelegate(ConnectionContext connection);
}
