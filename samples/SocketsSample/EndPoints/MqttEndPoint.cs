// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Channels;
using DotNetty.Codecs.Mqtt;
using DotNetty.Codecs.Mqtt.Packets;
using Microsoft.AspNetCore.Sockets;

namespace SocketsSample
{
    public class MqttEndPoint : EndPoint
    {
        const int PacketIdLength = 2;
        const int StringSizeLength = 2;
        const int MaxVariableLength = 4;
        
        public ParseState State { get; set; }

        public override async Task OnConnectedAsync(Connection connection)
        {
            while (true)
            {
                // Wait for data
                var result = await connection.Channel.Input.ReadAsync();
                var input = result.Buffer;

                try
                {
                    if (input.IsEmpty && result.IsCompleted)
                    {
                        // No more data
                        break;
                    }

                    Packet packet;
                    if (TryDecodePacket(ref input, out packet))
                    {
                        if (packet.PacketType == PacketType.CONNECT)
                        {
                            var message = new ConnAckPacket();
                            message.ReturnCode = ConnectReturnCode.Accepted;

                            var buffer = connection.Channel.Output.Alloc();
                            buffer.WriteBigEndian<byte>((byte)CalculateFirstByteOfFixedHeader(message));
                            buffer.WriteBigEndian<byte>(2); // remaining length
                            if (message.SessionPresent)
                            {
                                buffer.WriteBigEndian<byte>(1); // 7 reserved 0-bits and SP = 1
                            }
                            else
                            {
                                buffer.WriteBigEndian<byte>(0); // 7 reserved 0-bits and SP = 0
                            }
                            buffer.WriteBigEndian((byte)message.ReturnCode);
                            await buffer.FlushAsync();
                        }
                        else if (packet.PacketType == PacketType.SUBSCRIBE)
                        {
                            var message = SubAckPacket.InResponseTo((SubscribePacket)packet, QualityOfService.AtLeastOnce);
                            int payloadBufferSize = message.ReturnCodes.Count;
                            int variablePartSize = PacketIdLength + payloadBufferSize;
                            int fixedHeaderBufferSize = 1 + MaxVariableLength;
                            var buf = connection.Channel.Output.Alloc(fixedHeaderBufferSize + variablePartSize);
                            buf.WriteBigEndian((byte)CalculateFirstByteOfFixedHeader(message));
                            WriteVariableLengthInt(buf, variablePartSize);
                            buf.WriteBigEndian((short)message.PacketId);
                            foreach (QualityOfService qos in message.ReturnCodes)
                            {
                                buf.WriteBigEndian((byte)qos);
                            }

                            await buf.FlushAsync();
                        }
                    }

                    if (!input.IsEmpty && result.IsCompleted)
                    {
                        // Didn't get the whole frame and the connection ended
                        throw new EndOfStreamException();
                    }
                }
                finally
                {
                    // Consume the input
                    connection.Channel.Input.Advance(input.Start, input.End);
                }
            }
        }

        static void WriteVariableLengthInt(WritableBuffer buffer, int value)
        {
            do
            {
                int digit = value % 128;
                value /= 128;
                if (value > 0)
                {
                    digit |= 0x80;
                }
                buffer.WriteBigEndian((byte)digit);
            }
            while (value > 0);
        }

        static int CalculateFirstByteOfFixedHeader(Packet packet)
        {
            int ret = 0;
            ret |= (int)packet.PacketType << 4;
            if (packet.Duplicate)
            {
                ret |= 0x08;
            }
            ret |= (int)packet.QualityOfService << 1;
            if (packet.RetainRequested)
            {
                ret |= 0x01;
            }
            return ret;
        }

        static bool TryDecodePacket(ref ReadableBuffer input, out Packet packet)
        {
            // Make a copy until we figure out where to do partial parsing 
            var buffer = input;
            if (buffer.Length < 2) // packet consists of at least 2 bytes
            {
                packet = null;
                return false;
            }

            int signature = buffer.Peek();
            buffer = buffer.Slice(1);

            int remainingLength;
            if (!TryDecodeRemainingLength(ref buffer, out remainingLength) || buffer.Length < remainingLength)
            {
                packet = null;
                return false;
            }

            buffer = buffer.Slice(0, remainingLength);
            packet = DecodePacketInternal(ref buffer, signature);

            if (buffer.Length > 0)
            {
                throw new Exception($"Declared remaining length is bigger than packet data size by {remainingLength}.");
            }

            input = buffer;

            return true;
        }

        static Packet DecodePacketInternal(ref ReadableBuffer buffer, int packetSignature)
        {
            if (Signatures.IsPublish(packetSignature))
            {
                var qualityOfService = (QualityOfService)((packetSignature >> 1) & 0x3); // take bits #1 and #2 ONLY and convert them into QoS value
                if (qualityOfService == QualityOfService.Reserved)
                {
                    throw new Exception($"Unexpected QoS value of {(int)qualityOfService} for {PacketType.PUBLISH} packet.");
                }

                bool duplicate = (packetSignature & 0x8) == 0x8; // test bit#3
                bool retain = (packetSignature & 0x1) != 0; // test bit#0
                var packet = new PublishPacket(qualityOfService, duplicate, retain);
                // DecodePublishPacket(ref buffer, packet);
                return packet;
            }

            switch (packetSignature) // strict match checks for valid message type + correct values in flags part
            {
                case Signatures.PubAck:
                    var pubAckPacket = new PubAckPacket();
                    DecodePacketIdVariableHeader(ref buffer, pubAckPacket);
                    return pubAckPacket;
                case Signatures.PubRec:
                    var pubRecPacket = new PubRecPacket();
                    DecodePacketIdVariableHeader(ref buffer, pubRecPacket);
                    return pubRecPacket;
                case Signatures.PubRel:
                    var pubRelPacket = new PubRelPacket();
                    DecodePacketIdVariableHeader(ref buffer, pubRelPacket);
                    return pubRelPacket;
                case Signatures.PubComp:
                    var pubCompPacket = new PubCompPacket();
                    DecodePacketIdVariableHeader(ref buffer, pubCompPacket);
                    return pubCompPacket;
                case Signatures.PingReq:
                    ValidateServerPacketExpected(packetSignature);
                    return PingReqPacket.Instance;
                case Signatures.Subscribe:
                    ValidateServerPacketExpected(packetSignature);
                    var subscribePacket = new SubscribePacket();
                    DecodePacketIdVariableHeader(ref buffer, subscribePacket);
                    DecodeSubscribePayload(ref buffer, subscribePacket);
                    return subscribePacket;
                case Signatures.Unsubscribe:
                    ValidateServerPacketExpected(packetSignature);
                    var unsubscribePacket = new UnsubscribePacket();
                    DecodePacketIdVariableHeader(ref buffer, unsubscribePacket);
                    // DecodeUnsubscribePayload(buffer, unsubscribePacket, ref remainingLength);
                    return unsubscribePacket;
                case Signatures.Connect:
                    ValidateServerPacketExpected(packetSignature);
                    var connectPacket = new ConnectPacket();
                    DecodeConnectPacket(ref buffer, connectPacket);
                    return connectPacket;
                case Signatures.Disconnect:
                    ValidateServerPacketExpected(packetSignature);
                    return DisconnectPacket.Instance;
                case Signatures.ConnAck:
                    ValidateClientPacketExpected(packetSignature);
                    var connAckPacket = new ConnAckPacket();
                    DecodeConnAckPacket(ref buffer, connAckPacket);
                    return connAckPacket;
                case Signatures.SubAck:
                    ValidateClientPacketExpected(packetSignature);
                    var subAckPacket = new SubAckPacket();
                    DecodePacketIdVariableHeader(ref buffer, subAckPacket);
                    // DecodeSubAckPayload(buffer, subAckPacket, ref remainingLength);
                    return subAckPacket;
                case Signatures.UnsubAck:
                    ValidateClientPacketExpected(packetSignature);
                    var unsubAckPacket = new UnsubAckPacket();
                    DecodePacketIdVariableHeader(ref buffer, unsubAckPacket);
                    return unsubAckPacket;
                case Signatures.PingResp:
                    ValidateClientPacketExpected(packetSignature);
                    return PingRespPacket.Instance;
                default:
                    throw new Exception($"First packet byte value of `{packetSignature}` is invalid.");
            }
        }

        static void DecodePacketIdVariableHeader(ref ReadableBuffer buffer, PacketWithId packet)
        {
            int packetId = packet.PacketId = DecodeUnsignedShort(ref buffer);
            if (packetId == 0)
            {
                throw new Exception("[MQTT-2.3.1-1]");
            }
        }

        static void DecodeSubscribePayload(ref ReadableBuffer buffer, SubscribePacket packet)
        {
            var subscribeTopics = new List<SubscriptionRequest>();
            while (buffer.Length > 0)
            {
                string topicFilter = DecodeString(ref buffer);
                ValidateTopicFilter(topicFilter);

                int qos = buffer.Peek();
                if (qos >= (int)QualityOfService.Reserved)
                {
                    throw new Exception($"[MQTT-3.8.3-4]. Invalid QoS value: {qos}.");
                }
                buffer = buffer.Slice(1);
                subscribeTopics.Add(new SubscriptionRequest(topicFilter, (QualityOfService)qos));
            }

            if (subscribeTopics.Count == 0)
            {
                throw new Exception("[MQTT-3.8.3-3]");
            }

            packet.Requests = subscribeTopics;
        }

        static void ValidateTopicFilter(string topicFilter)
        {
            int length = topicFilter.Length;
            if (length == 0)
            {
                throw new Exception("[MQTT-4.7.3-1]");
            }

            for (int i = 0; i < length; i++)
            {
                char c = topicFilter[i];
                switch (c)
                {
                    case '+':
                        if ((i > 0 && topicFilter[i - 1] != '/') || (i < length - 1 && topicFilter[i + 1] != '/'))
                        {
                            throw new Exception($"[MQTT-4.7.1-3]. Invalid topic filter: {topicFilter}");
                        }
                        break;
                    case '#':
                        if (i < length - 1 || (i > 0 && topicFilter[i - 1] != '/'))
                        {
                            throw new Exception($"[MQTT-4.7.1-2]. Invalid topic filter: {topicFilter}");
                        }
                        break;
                }
            }
        }

        static void DecodeConnectPacket(ref ReadableBuffer buffer, ConnectPacket packet)
        {
            string protocolName = DecodeString(ref buffer);
            if (!Util.ProtocolName.Equals(protocolName, StringComparison.Ordinal))
            {
                throw new Exception($"Unexpected protocol name. Expected: {Util.ProtocolName}. Actual: {protocolName}");
            }
            packet.ProtocolName = Util.ProtocolName;

            packet.ProtocolLevel = buffer.ReadBigEndian<byte>();
            buffer = buffer.Slice(1);

            if (packet.ProtocolLevel != Util.ProtocolLevel)
            {
                var connAckPacket = new ConnAckPacket();
                connAckPacket.ReturnCode = ConnectReturnCode.RefusedUnacceptableProtocolVersion;
                // context.WriteAndFlushAsync(connAckPacket);
                throw new Exception($"Unexpected protocol level. Expected: {Util.ProtocolLevel}. Actual: {packet.ProtocolLevel}");
            }

            int connectFlags = buffer.ReadBigEndian<byte>();
            buffer = buffer.Slice(1);

            packet.CleanSession = (connectFlags & 0x02) == 0x02;

            bool hasWill = (connectFlags & 0x04) == 0x04;
            if (hasWill)
            {
                packet.HasWill = true;
                packet.WillRetain = (connectFlags & 0x20) == 0x20;
                packet.WillQualityOfService = (QualityOfService)((connectFlags & 0x18) >> 3);
                if (packet.WillQualityOfService == QualityOfService.Reserved)
                {
                    throw new Exception($"[MQTT-3.1.2-14] Unexpected Will QoS value of {(int)packet.WillQualityOfService}.");
                }
                packet.WillTopicName = string.Empty;
            }
            else if ((connectFlags & 0x38) != 0) // bits 3,4,5 [MQTT-3.1.2-11]
            {
                throw new Exception("[MQTT-3.1.2-11]");
            }

            packet.HasUsername = (connectFlags & 0x80) == 0x80;
            packet.HasPassword = (connectFlags & 0x40) == 0x40;
            if (packet.HasPassword && !packet.HasUsername)
            {
                throw new Exception("[MQTT-3.1.2-22]");
            }
            if ((connectFlags & 0x1) != 0) // [MQTT-3.1.2-3]
            {
                throw new Exception("[MQTT-3.1.2-3]");
            }

            packet.KeepAliveInSeconds = DecodeUnsignedShort(ref buffer);

            string clientId = DecodeString(ref buffer);
            Util.ValidateClientId(clientId);
            packet.ClientId = clientId;

            if (hasWill)
            {
                packet.WillTopicName = DecodeString(ref buffer);
                int willMessageLength = DecodeUnsignedShort(ref buffer);
                packet.WillMessage = buffer.Slice(0, willMessageLength);
                buffer = buffer.Slice(willMessageLength);
            }

            if (packet.HasUsername)
            {
                packet.Username = DecodeString(ref buffer);
            }

            if (packet.HasPassword)
            {
                packet.Password = DecodeString(ref buffer);
            }
        }

        static void DecodeConnAckPacket(ref ReadableBuffer buffer, ConnAckPacket packet)
        {
            int ackData = DecodeUnsignedShort(ref buffer);
            packet.SessionPresent = ((ackData >> 8) & 0x1) != 0;
            packet.ReturnCode = (ConnectReturnCode)(ackData & 0xFF);
        }

        static void ValidateServerPacketExpected(int signature)
        {
            // if (!this.isServer)
            // {
            //     throw new DecoderException($"Packet type determined through first packet byte `{signature}` is not supported by MQTT client.");
            // }
        }

        static void ValidateClientPacketExpected(int signature)
        {
            throw new Exception($"Packet type determined through first packet byte `{signature}` is not supported by MQTT server.");
        }

        static string DecodeString(ref ReadableBuffer buffer)
        {
            int size = DecodeUnsignedShort(ref buffer);

            if (size == 0)
            {
                return string.Empty;
            }


            var array = buffer.Slice(0, size).ToArray();
            string value = Encoding.UTF8.GetString(array);

            buffer = buffer.Slice(size);

            // todo: enforce string definition by MQTT spec
            // buffer.SetReaderIndex(buffer.ReaderIndex + size);
            return value;
        }

        static int DecodeUnsignedShort(ref ReadableBuffer buffer)
        {
            var value = buffer.ReadBigEndian<ushort>();
            buffer = buffer.Slice(2);
            return value;
        }

        static bool TryDecodeRemainingLength(ref ReadableBuffer buffer, out int value)
        {
            int readable = buffer.Length;

            int result = 0;
            int multiplier = 1;
            byte digit;
            int read = 0;
            do
            {
                if (readable < read + 1)
                {
                    value = default(int);
                    return false;
                }
                digit = (byte)buffer.Peek();
                buffer = buffer.Slice(1); // TODO: Fix, as this is inefficient
                result += (digit & 0x7f) * multiplier;
                multiplier <<= 7;
                read++;
            }
            while ((digit & 0x80) != 0 && read < 4);

            if (read == 4 && (digit & 0x80) != 0)
            {
                throw new Exception("Remaining length exceeds 4 bytes in length");
            }

            int completeMessageSize = result + 1 + read;
            if (completeMessageSize > 1024 * 50)
            {
                throw new Exception("Message is too big: " + completeMessageSize);
            }

            value = result;
            return true;
        }

        public enum ParseState
        {
            Ready,
            Failed
        }
    }
}
