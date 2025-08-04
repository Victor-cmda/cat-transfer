using Google.Protobuf;
using Protocol.Contracts;
using Protocol.Definitions;
using Protocol.Exceptions;
using Protocol.Messages;
using System.Collections.Concurrent;

namespace Protocol.Serialization.Protobuf
{
    public class ProtobufMessageSerializer : IMessageSerializer
    {
        private readonly ConcurrentDictionary<string, bool> _supportedTypes;
        private readonly object _initLock = new();
        private bool _initialized = false;

        public ProtobufMessageSerializer()
        {
            _supportedTypes = new ConcurrentDictionary<string, bool>();
        }

        public byte[] Serialize<T>(T message) where T : IProtocolMessage
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            EnsureInitialized();

            var jsonSerializer = new Json.JsonMessageSerializer();
            return jsonSerializer.Serialize(message);
        }

        public T Deserialize<T>(byte[] data) where T : IProtocolMessage
        {
            if (data == null || data.Length == 0)
                throw new ArgumentNullException(nameof(data));

            EnsureInitialized();

            var jsonSerializer = new Json.JsonMessageSerializer();
            return jsonSerializer.Deserialize<T>(data);
        }

        public IProtocolMessage Deserialize(byte[] data, string messageType)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentNullException(nameof(data));

            if (string.IsNullOrEmpty(messageType))
                throw new ArgumentNullException(nameof(messageType));

            EnsureInitialized();

            var jsonSerializer = new Json.JsonMessageSerializer();
            return jsonSerializer.Deserialize(data, messageType);
        }

        public IEnumerable<string> GetSupportedMessageTypes()
        {
            EnsureInitialized();
            return _supportedTypes.Keys.ToList();
        }

        public bool IsMessageTypeSupported(string messageType)
        {
            if (string.IsNullOrEmpty(messageType))
                return false;

            EnsureInitialized();
            return _supportedTypes.ContainsKey(messageType);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            lock (_initLock)
            {
                if (_initialized)
                    return;

                RegisterSupportedTypes();
                _initialized = true;
            }
        }

        private void RegisterSupportedTypes()
        {
            _supportedTypes.TryAdd(MessageTypes.PeerAnnouncement, true);
            _supportedTypes.TryAdd(MessageTypes.PeerDiscovery, true);
            _supportedTypes.TryAdd(MessageTypes.PeerDiscoveryResponse, true);
            _supportedTypes.TryAdd(MessageTypes.PeerLeave, true);

            _supportedTypes.TryAdd(MessageTypes.HandshakeRequest, true);
            _supportedTypes.TryAdd(MessageTypes.HandshakeResponse, true);
            _supportedTypes.TryAdd(MessageTypes.HandshakeAck, true);
            _supportedTypes.TryAdd(MessageTypes.HandshakeFailure, true);

            _supportedTypes.TryAdd(MessageTypes.KeyExchangeInit, true);
            _supportedTypes.TryAdd(MessageTypes.KeyExchangeResponse, true);
            _supportedTypes.TryAdd(MessageTypes.KeyExchangeComplete, true);
            _supportedTypes.TryAdd(MessageTypes.KeyRotation, true);

            _supportedTypes.TryAdd(MessageTypes.TransferRequest, true);
            _supportedTypes.TryAdd(MessageTypes.TransferResponse, true);
            _supportedTypes.TryAdd(MessageTypes.FileMetadata, true);
            _supportedTypes.TryAdd(MessageTypes.FileChunk, true);
            _supportedTypes.TryAdd(MessageTypes.ChunkAck, true);
            _supportedTypes.TryAdd(MessageTypes.ChunkResendRequest, true);
            _supportedTypes.TryAdd(MessageTypes.TransferProgress, true);
            _supportedTypes.TryAdd(MessageTypes.TransferComplete, true);
            _supportedTypes.TryAdd(MessageTypes.TransferCancel, true);

            _supportedTypes.TryAdd(MessageTypes.Heartbeat, true);
            _supportedTypes.TryAdd(MessageTypes.Error, true);
            _supportedTypes.TryAdd(MessageTypes.Ack, true);
            _supportedTypes.TryAdd(MessageTypes.Disconnect, true);

            _supportedTypes.TryAdd(MessageTypes.ChecksumRequest, true);
            _supportedTypes.TryAdd(MessageTypes.ChecksumResponse, true);
            _supportedTypes.TryAdd(MessageTypes.ChunkChecksum, true);
        }
    }
}
