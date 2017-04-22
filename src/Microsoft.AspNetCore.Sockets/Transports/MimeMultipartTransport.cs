using System;
using System.Buffers;
using System.IO.Pipelines.Text.Primitives;
using System.Text;
using System.Text.Formatting;
using System.Threading.Tasks.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Sockets.Transports
{
    // https://www.w3.org/Protocols/rfc1341/7_2_Multipart.html
    public class MimeMultipartTransport : HttpStreamingTransportBase
    {
        private static readonly byte[] _contentTypeText = Encoding.UTF8.GetBytes("Content-Type: text/plain\r\n");
        private static readonly byte[] _contentTypeBinary = Encoding.UTF8.GetBytes("Content-Type: application/octet-stream\r\n");
        private static readonly byte[] _contentTypeError = Encoding.UTF8.GetBytes("Content-Type: application/vnd.microsoft.aspnetcore.endpoint.error\r\n");
        private static readonly byte[] _contentTypeClose = Encoding.UTF8.GetBytes("Content-Type: application/vnd.microsoft.aspnetcore.endpoint.close\r\n");
        private static readonly byte[] _contentLengthPrefix = Encoding.UTF8.GetBytes("Content-Length: ");
        private static readonly byte[] _newline = Encoding.UTF8.GetBytes("\r\n");

        private byte[] _boundary;

        public MimeMultipartTransport(ReadableChannel<Message> application, ILoggerFactory loggerFactory) : base(application, loggerFactory)
        {
        }

        protected override void PrepareResponse(HttpContext context)
        {
            // Generate a random boundary from a GUID
            var boundaryString = Guid.NewGuid().ToString("N");

            context.Response.ContentType = $"multipart/mixed; boundary={boundaryString}";

            var size = Encoding.UTF8.GetByteCount(boundaryString);
            _boundary = new byte[size + 2];
            _boundary[0] = (byte)'-';
            _boundary[1] = (byte)'-';
            Encoding.UTF8.GetBytes(boundaryString, 0, boundaryString.Length, _boundary, 2);
            _boundary[_boundary.Length - 2] = (byte)'\r';
            _boundary[_boundary.Length - 1] = (byte)'\n';
        }

        protected override bool TryWriteMessage(Message message, PipelineTextOutput output)
        {
            if (!output.TryWrite(_boundary))
            {
                return false;
            }

            var contentType = GetContentType(message);
            if (!output.TryWrite(contentType))
            {
                return false;
            }

            if(!output.TryWrite(_contentLengthPrefix))
            {
                return false;
            }

            output.Append(message.Payload.Length);

            if(!output.TryWrite(_newline))
            {
                return false;
            }

            if(!output.TryWrite(_newline))
            {
                return false;
            }

            if(!output.TryWrite(message.Payload))
            {
                return false;
            }

            if(!output.TryWrite(_newline))
            {
                return false;
            }

            return true;
        }

        private static byte[] GetContentType(Message message)
        {
            switch (message.Type)
            {
                case MessageType.Text: return _contentTypeText;
                case MessageType.Binary: return _contentTypeBinary;
                case MessageType.Error: return _contentTypeError;
                case MessageType.Close: return _contentTypeClose;
                default: throw new FormatException($"Unknown Message Type: {message.Type}");
            }
        }
    }
}
