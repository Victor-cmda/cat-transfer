using Protocol.Contracts;
using Protocol.Definitions;
using Protocol.Exceptions;
using Protocol.Messages;
using Protocol.Messages.Discovery;
using Protocol.Messages.Handshake;
using Protocol.Messages.KeyExchange;
using Protocol.Messages.Transfer;
using Protocol.Messages.Control;
using Protocol.Messages.Checksum;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Protocol.Serialization.Json
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly ConcurrentDictionary<string, Type> _messageTypes;
        private readonly object _initLock = new();
        private bool _initialized = false;

        public JsonMessageSerializer()
        {
            _serializerOptions = CreateJsonOptions();
            _messageTypes = new ConcurrentDictionary<string, Type>();
        }

        public byte[] Serialize<T>(T message) where T : IProtocolMessage
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            EnsureInitialized();

            try
            {
                var json = JsonSerializer.Serialize(message, _serializerOptions);
                return System.Text.Encoding.UTF8.GetBytes(json);
            }
            catch (Exception ex)
            {
                throw new MessageSerializationException(message.MessageType, "serialize", ex);
            }
        }

        public T Deserialize<T>(byte[] data) where T : IProtocolMessage
        {
            if (data == null || data.Length == 0)
                throw new ArgumentNullException(nameof(data));

            EnsureInitialized();

            try
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                var message = JsonSerializer.Deserialize<T>(json, _serializerOptions);
                
                if (message == null)
                    throw new InvalidOperationException("Deserialization resulted in null message");

                return message;
            }
            catch (Exception ex) when (!(ex is MessageSerializationException))
            {
                throw new MessageSerializationException(typeof(T).Name, "deserialize", ex);
            }
        }

        public IProtocolMessage Deserialize(byte[] data, string messageType)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentNullException(nameof(data));

            if (string.IsNullOrEmpty(messageType))
                throw new ArgumentNullException(nameof(messageType));

            EnsureInitialized();

            try
            {
                if (!_messageTypes.TryGetValue(messageType, out var type))
                {
                    throw new MessageSerializationException(
                        messageType,
                        "deserialize",
                        new ArgumentException($"Unknown message type: {messageType}")
                    );
                }

                var json = System.Text.Encoding.UTF8.GetString(data);
                var message = JsonSerializer.Deserialize(json, type, _serializerOptions);
                
                if (message is IProtocolMessage protocolMessage)
                    return protocolMessage;

                throw new InvalidOperationException($"Deserialized object is not an IProtocolMessage: {message?.GetType()}");
            }
            catch (Exception ex) when (!(ex is MessageSerializationException))
            {
                throw new MessageSerializationException(messageType, "deserialize", ex);
            }
        }

        public IEnumerable<string> GetSupportedMessageTypes()
        {
            EnsureInitialized();
            return _messageTypes.Keys.ToList();
        }

        public bool IsMessageTypeSupported(string messageType)
        {
            if (string.IsNullOrEmpty(messageType))
                return false;

            EnsureInitialized();
            return _messageTypes.ContainsKey(messageType);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            lock (_initLock)
            {
                if (_initialized)
                    return;

                RegisterMessageTypes();
                _initialized = true;
            }
        }

        private void RegisterMessageTypes()
        {
            RegisterMessageType<PeerAnnouncementMessage>(MessageTypes.PeerAnnouncement);
            RegisterMessageType<PeerDiscoveryMessage>(MessageTypes.PeerDiscovery);
            RegisterMessageType<PeerDiscoveryResponseMessage>(MessageTypes.PeerDiscoveryResponse);
            RegisterMessageType<PeerLeaveMessage>(MessageTypes.PeerLeave);

            RegisterMessageType<HandshakeRequestMessage>(MessageTypes.HandshakeRequest);
            RegisterMessageType<HandshakeResponseMessage>(MessageTypes.HandshakeResponse);
            RegisterMessageType<HandshakeAckMessage>(MessageTypes.HandshakeAck);
            RegisterMessageType<HandshakeFailureMessage>(MessageTypes.HandshakeFailure);

            RegisterMessageType<KeyExchangeInitMessage>(MessageTypes.KeyExchangeInit);
            RegisterMessageType<KeyExchangeResponseMessage>(MessageTypes.KeyExchangeResponse);
            RegisterMessageType<KeyExchangeCompleteMessage>(MessageTypes.KeyExchangeComplete);
            RegisterMessageType<KeyRotationMessage>(MessageTypes.KeyRotation);

            RegisterMessageType<TransferRequestMessage>(MessageTypes.TransferRequest);
            RegisterMessageType<TransferResponseMessage>(MessageTypes.TransferResponse);
            RegisterMessageType<FileMetadataMessage>(MessageTypes.FileMetadata);
            RegisterMessageType<FileChunkMessage>(MessageTypes.FileChunk);
            RegisterMessageType<ChunkAckMessage>(MessageTypes.ChunkAck);
            RegisterMessageType<ChunkResendRequestMessage>(MessageTypes.ChunkResendRequest);
            RegisterMessageType<TransferProgressMessage>(MessageTypes.TransferProgress);
            RegisterMessageType<TransferCompleteMessage>(MessageTypes.TransferComplete);
            RegisterMessageType<TransferCancelMessage>(MessageTypes.TransferCancel);

            RegisterMessageType<HeartbeatMessage>(MessageTypes.Heartbeat);
            RegisterMessageType<ErrorMessage>(MessageTypes.Error);
            RegisterMessageType<AckMessage>(MessageTypes.Ack);
            RegisterMessageType<DisconnectMessage>(MessageTypes.Disconnect);

            RegisterMessageType<ChecksumRequestMessage>(MessageTypes.ChecksumRequest);
            RegisterMessageType<ChecksumResponseMessage>(MessageTypes.ChecksumResponse);
            RegisterMessageType<ChunkChecksumMessage>(MessageTypes.ChunkChecksum);
        }

        private void RegisterMessageType<T>(string messageType) where T : IProtocolMessage
        {
            _messageTypes.TryAdd(messageType, typeof(T));
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                IncludeFields = false,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new DateTimeJsonConverter());
            options.Converters.Add(new ByteArrayJsonConverter());
            options.Converters.Add(new NodeIdJsonConverter());
            options.Converters.Add(new FileIdJsonConverter());
            options.Converters.Add(new ChunkIdJsonConverter());

            return options;
        }
    }

    public class DateTimeJsonConverter : JsonConverter<DateTime>
    {
        private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
                return DateTime.MinValue;

            if (DateTime.TryParseExact(value, DateTimeFormat, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dateTime))
                return dateTime;

            if (DateTime.TryParse(value, out dateTime))
                return dateTime;

            throw new JsonException($"Unable to parse DateTime: {value}");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(DateTimeFormat));
        }
    }

    public class ByteArrayJsonConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
                return Array.Empty<byte>();

            try
            {
                return Convert.FromBase64String(value);
            }
            catch (FormatException ex)
            {
                throw new JsonException($"Unable to parse byte array from base64: {value}", ex);
            }
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            if (value == null || value.Length == 0)
            {
                writer.WriteStringValue(string.Empty);
                return;
            }

            writer.WriteStringValue(Convert.ToBase64String(value));
        }
    }

    public class NodeIdJsonConverter : JsonConverter<Domain.ValueObjects.NodeId>
    {
        public override Domain.ValueObjects.NodeId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
                throw new JsonException("NodeId cannot be null or empty");

            return new Domain.ValueObjects.NodeId(value);
        }

        public override void Write(Utf8JsonWriter writer, Domain.ValueObjects.NodeId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.value);
        }
    }

    public class FileIdJsonConverter : JsonConverter<Domain.ValueObjects.FileId>
    {
        public override Domain.ValueObjects.FileId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
                throw new JsonException("FileId cannot be null or empty");

            return new Domain.ValueObjects.FileId(value);
        }

        public override void Write(Utf8JsonWriter writer, Domain.ValueObjects.FileId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.value);
        }
    }

    public class ChunkIdJsonConverter : JsonConverter<Domain.ValueObjects.ChunkId>
    {
        public override Domain.ValueObjects.ChunkId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
                throw new JsonException("ChunkId cannot be null or empty");

            var parts = value.Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[1], out var offset))
                throw new JsonException($"Invalid ChunkId format: {value}");

            var fileId = new Domain.ValueObjects.FileId(parts[0]);
            return new Domain.ValueObjects.ChunkId(fileId, offset);
        }

        public override void Write(Utf8JsonWriter writer, Domain.ValueObjects.ChunkId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
