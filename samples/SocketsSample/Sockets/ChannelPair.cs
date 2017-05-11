using System;
using System.Threading.Tasks.Channels;
using Microsoft.AspNetCore.Sockets;
using Microsoft.AspNetCore.Sockets.Internal;

namespace SocketsSample.Sockets
{
    internal class ChannelPair
    {
        internal static (IChannelConnection<T> left, IChannelConnection<T> right) Create<T>()
        {
            var leftToRight = Channel.CreateUnbounded<T>();
            var rightToLeft = Channel.CreateUnbounded<T>();

            return (
                left: new ChannelConnection<T>(rightToLeft, leftToRight),
                right: new ChannelConnection<T>(leftToRight, rightToLeft));
        }
    }
}
