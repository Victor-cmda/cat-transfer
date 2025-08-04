namespace Protocol.Definitions
{
    public static class MessageTypes
    {
        public const string PeerAnnouncement = "peer.announcement";
        public const string PeerDiscovery = "peer.discovery";
        public const string PeerDiscoveryResponse = "peer.discovery.response";
        public const string PeerLeave = "peer.leave";
        public const string HandshakeRequest = "handshake.request";
        public const string HandshakeResponse = "handshake.response";
        public const string HandshakeAck = "handshake.ack";
        public const string HandshakeFailure = "handshake.failure";
        public const string KeyExchangeInit = "key.exchange.init";
        public const string KeyExchangeResponse = "key.exchange.response";
        public const string KeyExchangeComplete = "key.exchange.complete";
        public const string KeyRotation = "key.rotation";
        public const string TransferRequest = "transfer.request";
        public const string TransferResponse = "transfer.response";
        public const string FileMetadata = "file.metadata";
        public const string FileChunk = "file.chunk";
        public const string ChunkAck = "chunk.ack";
        public const string ChunkResendRequest = "chunk.resend.request";
        public const string TransferProgress = "transfer.progress";
        public const string TransferComplete = "transfer.complete";
        public const string TransferCancel = "transfer.cancel";
        public const string TransferFailure = "transfer.failure";
        public const string Heartbeat = "control.heartbeat";
        public const string Error = "control.error";
        public const string Ack = "control.ack";
        public const string Disconnect = "control.disconnect";
        public const string ChecksumRequest = "checksum.request";
        public const string ChecksumResponse = "checksum.response";
        public const string ChunkChecksum = "checksum.chunk";
        public static readonly HashSet<string> AllTypes = new()
        {
            PeerAnnouncement, PeerDiscovery, PeerDiscoveryResponse, PeerLeave,
            HandshakeRequest, HandshakeResponse, HandshakeAck, HandshakeFailure,
            KeyExchangeInit, KeyExchangeResponse, KeyExchangeComplete, KeyRotation,
            TransferRequest, TransferResponse, FileMetadata, FileChunk,
            ChunkAck, ChunkResendRequest, TransferProgress, TransferComplete,
            TransferCancel, TransferFailure,
            Heartbeat, Error, Ack, Disconnect,
            ChecksumRequest, ChecksumResponse, ChunkChecksum
        };
        public static bool IsValid(string messageType)
        {
            return AllTypes.Contains(messageType);
        }
        public static string GetCategory(string messageType)
        {
            if (string.IsNullOrEmpty(messageType))
                return "unknown";

            var parts = messageType.Split('.');
            return parts.Length > 0 ? parts[0] : "unknown";
        }
        public static bool IsControlMessage(string messageType)
        {
            return GetCategory(messageType) == "control";
        }
        public static bool IsTransferMessage(string messageType)
        {
            var category = GetCategory(messageType);
            return category == "transfer" || category == "file" || category == "chunk";
        }
    }
}
