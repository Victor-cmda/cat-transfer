namespace Protocol.Definitions
{
    public static class ProtocolConstants
    {
        public static readonly Version ProtocolVersion = new Version(1, 0, 0);
        public const string DefaultMulticastAddress = "239.255.255.250";
        public const int DefaultMulticastPort = 1900;
        public const int DefaultTcpPort = 8080;
        public const int MaxConcurrentConnections = 100;
        public const int MaxMessageSize = 16 * 1024 * 1024;
        public const int DefaultBufferSize = 64 * 1024;



        public const int DefaultConnectionTimeoutMs = 30000;

        public const int DefaultHandshakeTimeoutMs = 10000;

        public const int DefaultHeartbeatIntervalMs = 30000;

        public const int DefaultMessageTimeoutMs = 5000;

        public const int PeerDiscoveryIntervalMs = 60000;

        public const int PeerDiscoveryTimeoutMs = 5000;

        public const int KeyRotationIntervalMs = 3600000;



        public const int DefaultChunkSize = 1024 * 1024;
        public const int MinChunkSize = 4 * 1024;
        public const int MaxChunkSize = 16 * 1024 * 1024;
        public const int MaxChunkRetries = 3;
        public const int MaxConcurrentChunks = 10;
        public const int ProgressReportingInterval = 100;
        public const long MaxFileSize = 1024L * 1024L * 1024L;



        public const string DefaultEncryptionAlgorithm = "AES-256-GCM";
        public const string DefaultKeyExchangeAlgorithm = "ECDH-P256";
        public const string DefaultChecksumAlgorithm = "SHAKE256";
        public const int AesGcmNonceSize = 12;
        public const int AesGcmTagSize = 16;
        public const int EcdhPublicKeySize = 65;
        public const int Shake256DigestSize = 32;



        public const int MaxPeerIdLength = 64;
        public const int MaxFileNameLength = 255;
        public const int MaxMetadataSize = 4096;
        public const int MaxErrorMessageLength = 512;
        public const int MaxNetworkPeers = 1000;



        public static readonly byte[] ProtocolSignature = { 0x43, 0x41, 0x54, 0x50 };

        public const int MessageHeaderSize = 32;
        public const int MinMessageSize = MessageHeaderSize;



        public const int ErrorProtocol = 1000;
        public const int ErrorVersionIncompatible = 1001;
        public const int ErrorHandshakeFailed = 1002;
        public const int ErrorKeyExchangeFailed = 1003;
        public const int ErrorTransferFailed = 1004;
        public const int ErrorChecksumMismatch = 1005;
        public const int ErrorTimeout = 1006;
        public const int ErrorInvalidMessage = 1007;
        public const int ErrorPeerNotFound = 1008;
        public const int ErrorFileNotFound = 1009;
        public const int ErrorInsufficientPermissions = 1010;



        public static bool IsValidChunkSize(int chunkSize)
        {
            return chunkSize >= MinChunkSize && chunkSize <= MaxChunkSize;
        }

        public static bool IsValidFileSize(long fileSize)
        {
            return fileSize > 0 && fileSize <= MaxFileSize;
        }

        public static string GetErrorMessage(int errorCode)
        {
            return errorCode switch
            {
                ErrorProtocol => "Protocol error",
                ErrorVersionIncompatible => "Version incompatible",
                ErrorHandshakeFailed => "Handshake failed",
                ErrorKeyExchangeFailed => "Key exchange failed",
                ErrorTransferFailed => "Transfer failed",
                ErrorChecksumMismatch => "Checksum mismatch",
                ErrorTimeout => "Network timeout",
                ErrorInvalidMessage => "Invalid message format",
                ErrorPeerNotFound => "Peer not found",
                ErrorFileNotFound => "File not found",
                ErrorInsufficientPermissions => "Insufficient permissions",
                _ => "Unknown error"
            };
        }

        public static int CalculateOptimalChunkSize(long fileSize)
        {
            if (fileSize <= 0)
                return DefaultChunkSize;


            if (fileSize < 10 * 1024 * 1024)
                return Math.Max(MinChunkSize, (int)(fileSize / 100));


            if (fileSize < 100 * 1024 * 1024)
                return DefaultChunkSize;


            return Math.Min(MaxChunkSize, DefaultChunkSize * 4);
        }
    }
}
