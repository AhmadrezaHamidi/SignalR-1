// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    using Channels;

    public sealed class PublishPacket : PacketWithId
    {
        readonly QualityOfService qos;
        readonly bool duplicate;
        readonly bool retainRequested;

        private PreservedBuffer _preserved;

        public PublishPacket(QualityOfService qos, bool duplicate, bool retain)
        {
            this.qos = qos;
            this.duplicate = duplicate;
            this.retainRequested = retain;
        }

        public override PacketType PacketType => PacketType.PUBLISH;

        public override bool Duplicate => this.duplicate;

        public override QualityOfService QualityOfService => this.qos;

        public override bool RetainRequested => this.retainRequested;

        public string TopicName { get; set; }

        public ReadableBuffer Payload { get; set; }

        public PreservedBuffer Retain()
        {
            _preserved = Payload.Preserve();
            return _preserved;
        }

        public void Release() => _preserved.Dispose();

        /*public IByteBufferHolder Copy()
        {
            var result = new PublishPacket(this.qos, this.duplicate, this.retainRequested);
            result.TopicName = this.TopicName;
            result.Payload = this.Payload.Copy();
            return result;
        }

        IByteBufferHolder IByteBufferHolder.Duplicate()
        {
            var result = new PublishPacket(this.qos, this.duplicate, this.retainRequested);
            result.TopicName = this.TopicName;
            result.Payload = this.Payload.Duplicate();
            return result;
        }*/
    }
}