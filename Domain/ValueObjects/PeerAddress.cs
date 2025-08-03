namespace Domain.ValueObjects
{
    public readonly record struct PeerAddress(string host, int port)
    {
        public static PeerAddress Create(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host cannot be null or empty.", nameof(host));
            
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

            return new PeerAddress(host, port);
        }

        public override string ToString() => $"{host}:{port}";

        public static PeerAddress Parse(string address)
        {
            var parts = address.Split(':');
            if (parts.Length != 2)
                throw new FormatException("Address must be in format 'host:port'");

            if (!int.TryParse(parts[1], out var port))
                throw new FormatException("Invalid port number");

            return Create(parts[0], port);
        }
    }
}
