using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Sockets.Abstractions
{
    public delegate Task SocketDelegate(Connection connection);
}
