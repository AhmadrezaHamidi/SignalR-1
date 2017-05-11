using System;
using System.IO.Pipelines;

namespace SocketsSample.Sockets
{
    public class PipeConnection : IPipeConnection
    {
        public IPipeReader Input { get; }
        public IPipeWriter Output { get; }

        public PipeConnection(IPipeReader input, IPipeWriter output)
        {
            Input = input;
            Output = output;
        }

        public void Dispose()
        {
        }
    }

    public static class PipeFactoryCreationExtensions
    {
        public static (IPipeConnection left, IPipeConnection right) CreatePipelinePair(this PipeFactory factory)
        {
            // Create a set of pipes
            var leftToRight = factory.Create();
            var rightToLeft = factory.Create();

            return (
                left: new PipeConnection(rightToLeft.Reader, leftToRight.Writer),
                right: new PipeConnection(leftToRight.Reader, rightToLeft.Writer));
        }
    }
}
