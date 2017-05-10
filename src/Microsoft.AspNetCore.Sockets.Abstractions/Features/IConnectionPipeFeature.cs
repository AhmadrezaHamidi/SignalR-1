using System.IO.Pipelines;

namespace Microsoft.AspNetCore.Sockets.Features
{
    public interface IConnectionPipeFeature
    {
        IPipeConnection Pipe { get; }
    }
}
