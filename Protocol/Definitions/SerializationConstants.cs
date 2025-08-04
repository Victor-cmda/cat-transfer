namespace Protocol.Definitions
{
    public static class SerializationFormats
    {
        public const string Json = "json";
        public const string Protobuf = "protobuf";
        public const string Binary = "binary";
        public const string Default = Json;
        public static string[] GetAllFormats()
        {
            return new[] { Json, Protobuf, Binary };
        }

        public static bool IsValidFormat(string format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            return format.Equals(Json, StringComparison.OrdinalIgnoreCase) ||
                   format.Equals(Protobuf, StringComparison.OrdinalIgnoreCase) ||
                   format.Equals(Binary, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class SerializationConstants
    {
        public const uint EnvelopeMagicNumber = 0x43415446;

        public const ushort ProtocolVersion = 1;
        public const int MaxMessageSize = 16 * 1024 * 1024;
        public const int MaxPayloadSize = 15 * 1024 * 1024;
        public const int EnvelopeHeaderSize = 256;
        public const int CompressionThreshold = 1024;
        public const int MessageTimeoutMs = 30000;
        public const int HeartbeatIntervalMs = 10000;
        public const int MaxRetryAttempts = 3;
        public const int RetryDelayMs = 1000;
    }

    public static class CompressionAlgorithms
    {
        public const string None = "none";
        public const string Gzip = "gzip";
        public const string Brotli = "brotli";
        public const string Lz4 = "lz4";
        public const string Default = Gzip;
        public static string[] GetAllAlgorithms()
        {
            return new[] { None, Gzip, Brotli, Lz4 };
        }

        public static bool IsValidAlgorithm(string algorithm)
        {
            if (string.IsNullOrEmpty(algorithm))
                return false;

            return algorithm.Equals(None, StringComparison.OrdinalIgnoreCase) ||
                   algorithm.Equals(Gzip, StringComparison.OrdinalIgnoreCase) ||
                   algorithm.Equals(Brotli, StringComparison.OrdinalIgnoreCase) ||
                   algorithm.Equals(Lz4, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class EncodingTypes
    {
        public const string Utf8 = "utf-8";
        public const string Ascii = "ascii";
        public const string Base64 = "base64";
        public const string Hex = "hex";
        public const string Default = Utf8;
    }
}
