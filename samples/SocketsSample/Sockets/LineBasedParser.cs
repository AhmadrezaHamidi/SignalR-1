using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Channels;
using Microsoft.AspNetCore.Sockets;
using Microsoft.AspNetCore.Sockets.Features;

namespace SocketsSample.Sockets
{
    public static class LineBasedParser
    {
        public static ISocketBuilder UseLineBasedMessageParsing(this ISocketBuilder socket)
        {
            return socket.Use(async (connection, next) =>
            {
                // Get, and remove, the Pipe feature from the connection
                var pipeFeature = connection.Features.Get<IConnectionPipeFeature>();
                if (pipeFeature == null)
                {
                    throw new InvalidOperationException("Unable to access connection Pipe.");
                }
                connection.Features.Set<IConnectionPipeFeature>(null);

                // Create a channel pair
                var (left, right) = ChannelPair.Create<Message>();

                // Add the channel to the connection
                connection.Features.Set<IConnectionChannelFeature>(new ConnectionChannelFeature(right));

                // Set up reading/writing
                var localCts = new CancellationTokenSource();
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(localCts.Token, connection.ConnectionAborted);
                var reader = Reading(pipeFeature.Pipe.Input, left.Output, linkedCts.Token);
                var writer = Writing(pipeFeature.Pipe.Output, left.Input, linkedCts.Token);

                // Run the rest of the pipeline, now that we've changed the features around.
                await next();

                left.Output.Complete();
                right.Output.Complete();
                localCts.Cancel();

                // Wait for the reader and writer to stop
                await Task.WhenAll(reader, writer);
            });
        }

        private static async Task Reading(IPipeReader input, WritableChannel<Message> output, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await input.ReadAsync();
                var buffer = result.Buffer;
                if ((buffer.IsEmpty && result.IsCompleted) || result.IsCancelled)
                {
                    break;
                }

                // Seek to a '\n' in the buffer
                if (buffer.TrySliceTo((byte)'\n', out var slice, out var cursor))
                {
                    // Mark the message as consumed
                    cursor = buffer.Move(cursor, 1);
                    buffer = buffer.Slice(cursor);
                    input.Advance(buffer.Start);

                    var messageContent = slice.ToArray();

                    // Remove windows '\r' extra newline char
                    if (messageContent.Length > 0 && messageContent[messageContent.Length - 1] == '\r')
                    {
                        var newContent = new byte[messageContent.Length - 1];
                        Buffer.BlockCopy(messageContent, 0, newContent, 0, newContent.Length);
                        messageContent = newContent;
                    }

                    // Write the message to the channel
                    var message = new Message(messageContent, MessageType.Text);
                    while (!output.TryWrite(message))
                    {
                        if (!await output.WaitToWriteAsync(cancellationToken))
                        {
                            // Output closed
                            return;
                        }
                    }
                }
                else
                {
                    // No line found, mark what we've read as examined and go back to the start
                    input.Advance(buffer.Start, buffer.End);
                    if (result.IsCompleted)
                    {
                        return;
                    }
                    break;
                }
            }
        }

        private static async Task Writing(IPipeWriter output, ReadableChannel<Message> input, CancellationToken cancellationToken)
        {
            while (await input.WaitToReadAsync(cancellationToken))
            {
                while (!input.Completion.IsCompleted && !cancellationToken.IsCancellationRequested && input.TryRead(out var message))
                {
                    var alloc = output.Alloc(message.Payload.Length + 1);
                    var buffer = alloc.Buffer.Slice(0, message.Payload.Length + 1);
                    if (buffer.Length < message.Payload.Length + 1)
                    {
                        throw new OutOfMemoryException("Unable to allocate enough memory");
                    }
                    message.Payload.CopyTo(buffer.Span);
                    buffer.Span[message.Payload.Length] = (byte)'\n';

                    alloc.Advance(message.Payload.Length + 1);
                    alloc.Commit();
                    await alloc.FlushAsync();
                }
            }
        }
    }
}
