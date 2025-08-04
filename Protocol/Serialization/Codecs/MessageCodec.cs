using Protocol.Contracts;
using Protocol.Definitions;
using Protocol.Exceptions;
using System.Collections.Concurrent;

namespace Protocol.Serialization.Codecs
{
    public class MessageCodec : IMessageCodec
    {
        private readonly ConcurrentDictionary<string, IMessageSerializer> _serializers;
        private readonly IMessageSerializer _defaultSerializer;
        private readonly object _initLock = new();
        private bool _initialized = false;

        public MessageCodec(IMessageSerializer defaultSerializer)
        {
            _defaultSerializer = defaultSerializer ?? throw new ArgumentNullException(nameof(defaultSerializer));
            _serializers = new ConcurrentDictionary<string, IMessageSerializer>();
        }

        public MessageCodec() : this(new Json.JsonMessageSerializer())
        {
        }

        public byte[] Encode(IProtocolMessage message, byte[]? encryptionKey = null)
        {
            return EncodeInternal(message, null);
        }

        public IProtocolMessage Decode(byte[] data, byte[]? decryptionKey = null)
        {
            return DecodeInternal(data);
        }

        public MessageHeader PeekHeader(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentNullException(nameof(data));

            try
            {
                var envelope = DeserializeEnvelope(data);
                return new MessageHeader(
                    envelope.MessageId,
                    envelope.MessageType,
                    envelope.SourceNodeId,
                    envelope.DestinationNodeId,
                    envelope.CorrelationId,
                    envelope.Timestamp,
                    envelope.PayloadSize,
                    envelope.Format
                );
            }
            catch (Exception ex)
            {
                throw new MessageSerializationException("Unknown", "peek header", ex);
            }
        }

        public bool ValidateFormat(byte[] data)
        {
            if (data == null || data.Length < SerializationConstants.EnvelopeHeaderSize)
                return false;

            try
            {
                using var stream = new MemoryStream(data);
                using var reader = new BinaryReader(stream);

                var magicNumber = reader.ReadUInt32();
                return magicNumber == SerializationConstants.EnvelopeMagicNumber;
            }
            catch
            {
                return false;
            }
        }

        public byte[] EncodeInternal<T>(T message, string? format = null) where T : IProtocolMessage
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            EnsureInitialized();

            var serializer = GetSerializer(format);
            
            try
            {
                var serializedData = serializer.Serialize(message);
                var envelope = CreateMessageEnvelope(message, serializedData, format ?? SerializationFormats.Default);
                
                return SerializeEnvelope(envelope);
            }
            catch (Exception ex) when (!(ex is MessageSerializationException))
            {
                throw new MessageSerializationException(
                    message.MessageType, 
                    "encode", 
                    ex
                );
            }
        }

        public IProtocolMessage DecodeInternal(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentNullException(nameof(data));

            EnsureInitialized();

            try
            {
                var envelope = DeserializeEnvelope(data);
                var serializer = GetSerializer(envelope.Format);
                
                return serializer.Deserialize(envelope.Payload, envelope.MessageType);
            }
            catch (Exception ex) when (!(ex is MessageSerializationException))
            {
                throw new MessageSerializationException("Unknown", "decode", ex);
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            lock (_initLock)
            {
                if (_initialized)
                    return;

                RegisterDefaultSerializers();
                _initialized = true;
            }
        }

        private void RegisterDefaultSerializers()
        {
            _serializers.TryAdd(SerializationFormats.Json, new Json.JsonMessageSerializer());
            _serializers.TryAdd(SerializationFormats.Protobuf, new Protobuf.ProtobufMessageSerializer());
        }

        private IMessageSerializer GetSerializer(string? format)
        {
            if (string.IsNullOrEmpty(format))
                return _defaultSerializer;

            var normalizedFormat = format.ToLowerInvariant();
            
            if (normalizedFormat == SerializationFormats.Default.ToLowerInvariant())
                return _defaultSerializer;

            if (_serializers.TryGetValue(normalizedFormat, out var serializer))
                return serializer;

            throw new ArgumentException($"Unsupported serialization format: {format}");
        }

        private MessageEnvelope CreateMessageEnvelope(IProtocolMessage message, byte[] payload, string format)
        {
            return new MessageEnvelope
            {
                MessageId = message.MessageId,
                MessageType = message.MessageType,
                SourceNodeId = message.SourcePeerId.ToString(),
                DestinationNodeId = message.TargetPeerId?.ToString(),
                CorrelationId = message.CorrelationId,
                Timestamp = message.Timestamp.DateTime,
                Format = format,
                PayloadSize = payload.Length,
                Payload = payload,
                Version = SerializationConstants.ProtocolVersion
            };
        }

        private byte[] SerializeEnvelope(MessageEnvelope envelope)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(SerializationConstants.EnvelopeMagicNumber);
            writer.Write(envelope.Version);
            writer.Write(envelope.MessageId);
            WriteString(writer, envelope.MessageType);
            WriteString(writer, envelope.SourceNodeId);
            WriteString(writer, envelope.DestinationNodeId ?? string.Empty);
            WriteString(writer, envelope.CorrelationId ?? string.Empty);
            writer.Write(envelope.Timestamp.ToBinary());
            WriteString(writer, envelope.Format);
            writer.Write(envelope.PayloadSize);
            writer.Write(envelope.Payload);

            return stream.ToArray();
        }

        private MessageEnvelope DeserializeEnvelope(byte[] data)
        {
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            var magicNumber = reader.ReadUInt32();
            if (magicNumber != SerializationConstants.EnvelopeMagicNumber)
            {
                throw new InvalidOperationException("Invalid envelope format - magic number mismatch");
            }

            var version = reader.ReadUInt16();
            var messageId = reader.ReadString();
            var messageType = ReadString(reader);
            var sourceNodeId = ReadString(reader);
            var destinationNodeId = ReadString(reader);
            var correlationId = ReadString(reader);
            var timestamp = DateTime.FromBinary(reader.ReadInt64());
            var format = ReadString(reader);
            var payloadSize = reader.ReadInt32();
            var payload = reader.ReadBytes(payloadSize);

            return new MessageEnvelope
            {
                Version = version,
                MessageId = messageId,
                MessageType = messageType,
                SourceNodeId = sourceNodeId,
                DestinationNodeId = string.IsNullOrEmpty(destinationNodeId) ? null : destinationNodeId,
                CorrelationId = string.IsNullOrEmpty(correlationId) ? null : correlationId,
                Timestamp = timestamp,
                Format = format,
                PayloadSize = payloadSize,
                Payload = payload
            };
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            writer.Write(value ?? string.Empty);
        }

        private static string ReadString(BinaryReader reader)
        {
            return reader.ReadString();
        }

        internal class MessageEnvelope
        {
            public required ushort Version { get; init; }
            public required string MessageId { get; init; }
            public required string MessageType { get; init; }
            public required string SourceNodeId { get; init; }
            public string? DestinationNodeId { get; init; }
            public string? CorrelationId { get; init; }
            public required DateTime Timestamp { get; init; }
            public required string Format { get; init; }
            public required int PayloadSize { get; init; }
            public required byte[] Payload { get; init; }
        }
    }
}
