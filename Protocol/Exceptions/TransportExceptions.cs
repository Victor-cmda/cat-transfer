namespace Protocol.Exceptions
{
    public class NetworkTransportException : Exception
    {
        public NetworkTransportException() : base() { }

        public NetworkTransportException(string message) : base(message) { }

        public NetworkTransportException(string message, Exception innerException) 
            : base(message, innerException) { }
    }

    public class PeerException : Exception
    {
        public string? PeerId { get; }

        public PeerException(string? peerId = null) : base() 
        {
            PeerId = peerId;
        }

        public PeerException(string message, string? peerId = null) : base(message) 
        {
            PeerId = peerId;
        }

        public PeerException(string message, Exception innerException, string? peerId = null) 
            : base(message, innerException) 
        {
            PeerId = peerId;
        }
    }
}
