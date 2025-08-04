using Domain.ValueObjects;

namespace Protocol.Contracts
{

    public interface IMessageSerializer
    {

        byte[] Serialize<T>(T message) where T : IProtocolMessage;
        T Deserialize<T>(byte[] data) where T : IProtocolMessage;
        IProtocolMessage Deserialize(byte[] data, string messageType);
        IEnumerable<string> GetSupportedMessageTypes();
        bool IsMessageTypeSupported(string messageType);
    }
    public record MessageHeader(
        string MessageId,
        string MessageType,
        string SourceNodeId,
        string? DestinationNodeId,
        string? CorrelationId,
        DateTimeOffset Timestamp,
        int PayloadSize,
        string Format
    );
    public interface IMessageCodec
    {
        byte[] Encode(IProtocolMessage message, byte[]? encryptionKey = null);
        IProtocolMessage Decode(byte[] data, byte[]? decryptionKey = null);
        MessageHeader PeekHeader(byte[] data);
        bool ValidateFormat(byte[] data);
    }
    public interface INetworkTransport : IDisposable
    {
        NodeId LocalPeerId { get; }
        string LocalEndpoint { get; }
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
        Task SendAsync(NodeId targetPeerId, IProtocolMessage message, CancellationToken cancellationToken = default);
        Task SendMessageAsync(NodeId targetPeerId, IProtocolMessage message, CancellationToken cancellationToken = default);
        Task BroadcastAsync(IBroadcastMessage message, CancellationToken cancellationToken = default);
        Task BroadcastMessageAsync(IProtocolMessage message, CancellationToken cancellationToken = default);
        Task ConnectAsync(string endpoint, CancellationToken cancellationToken = default);
        Task<bool> ConnectToPeerAsync(string endpoint, CancellationToken cancellationToken = default);
        Task DisconnectAsync(string endpoint, CancellationToken cancellationToken = default);
        Task DisconnectPeerAsync(NodeId peerId, CancellationToken cancellationToken = default);
        bool IsConnectedToPeer(NodeId peerId);
        event EventHandler<MessageReceivedEventArgs> MessageReceived;
        event EventHandler<PeerConnectedEventArgs> PeerConnected;
        event EventHandler<PeerDisconnectedEventArgs> PeerDisconnected;
        event EventHandler<ErrorEventArgs> TransportError;
        IEnumerable<NodeId> GetConnectedPeers();
        bool IsPeerConnected(NodeId peerId);
    }
    public interface IPeerDiscovery : IDisposable
    {
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
        Task AnnounceAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<PeerInfo>> DiscoverPeersAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
        event EventHandler<PeerDiscoveredEventArgs> PeerDiscovered;
        event EventHandler<PeerLeftEventArgs> PeerLeft;
    }
    public record PeerInfo(
        NodeId PeerId,
        string Endpoint,
        string ProtocolVersion,
        DateTimeOffset LastSeen,
        IDictionary<string, string> Metadata
    );
    public class MessageReceivedEventArgs : EventArgs
    {
        public MessageReceivedEventArgs(IProtocolMessage message, NodeId sourcePeerId)
        {
            Message = message;
            SourcePeerId = sourcePeerId;
        }

        public IProtocolMessage Message { get; }
        public NodeId SourcePeerId { get; }
    }
    public class PeerConnectedEventArgs : EventArgs
    {
        public PeerConnectedEventArgs(NodeId peerId, string endpoint)
        {
            PeerId = peerId;
            Endpoint = endpoint;
        }

        public NodeId PeerId { get; }
        public string Endpoint { get; }
    }
    public class PeerDisconnectedEventArgs : EventArgs
    {
        public PeerDisconnectedEventArgs(NodeId peerId, string? reason = null)
        {
            PeerId = peerId;
            Reason = reason;
        }

        public NodeId PeerId { get; }
        public string? Reason { get; }
    }
    public class PeerDiscoveredEventArgs : EventArgs
    {
        public PeerDiscoveredEventArgs(PeerInfo peerInfo)
        {
            PeerInfo = peerInfo;
        }

        public PeerInfo PeerInfo { get; }
    }
    public class PeerLeftEventArgs : EventArgs
    {
        public PeerLeftEventArgs(NodeId peerId)
        {
            PeerId = peerId;
        }

        public NodeId PeerId { get; }
    }
    public class ErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string? Context { get; }

        public ErrorEventArgs(Exception exception, string? context = null)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Context = context;
        }
    }
    public class MessageProcessedEventArgs : EventArgs
    {
        public MessageProcessedEventArgs(IProtocolMessage message, TimeSpan processingTime, NodeId sourceNodeId)
        {
            Message = message;
            ProcessingTime = processingTime;
            SourceNodeId = sourceNodeId;
            Timestamp = DateTime.UtcNow;
        }
        public IProtocolMessage Message { get; }
        public TimeSpan ProcessingTime { get; }
        public NodeId SourceNodeId { get; }
        public DateTime Timestamp { get; }
    }
    public class MessageHandlerErrorEventArgs : EventArgs
    {
        public MessageHandlerErrorEventArgs(Exception error, IProtocolMessage? message, NodeId sourceNodeId)
        {
            Error = error;
            Message = message;
            SourceNodeId = sourceNodeId;
            Timestamp = DateTime.UtcNow;
        }
        public Exception Error { get; }
        public IProtocolMessage? Message { get; }
        public NodeId SourceNodeId { get; }
        public DateTime Timestamp { get; }
    }
    public interface IMessageProcessor
    {
        Task<IProtocolMessage?> ProcessAsync(MessageProcessingContext context);
    }
    public class MessageProcessingContext
    {
        public IProtocolMessage Message { get; set; } = null!;

        public NodeId SenderId { get; set; }

        public NodeId LocalNodeId { get; set; }

        public string ConnectionId { get; set; } = string.Empty;

        public IDictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }

    public interface IProtocolService : IDisposable
    {
        Task StartAsync(CancellationToken cancellationToken = default);

        Task StopAsync(CancellationToken cancellationToken = default);

        Task SendMessageAsync(NodeId peerId, IProtocolMessage message, CancellationToken cancellationToken = default);

        Task BroadcastMessageAsync(IProtocolMessage message, CancellationToken cancellationToken = default);

        Task ConnectToPeerAsync(string address, int port, CancellationToken cancellationToken = default);

        Task DisconnectPeerAsync(NodeId peerId, CancellationToken cancellationToken = default);

        IEnumerable<NodeId> GetConnectedPeers();

        bool IsPeerConnected(NodeId peerId);

        event EventHandler<MessageReceivedEventArgs> MessageReceived;

        event EventHandler<PeerConnectedEventArgs> PeerConnected;

        event EventHandler<PeerDisconnectedEventArgs> PeerDisconnected;

        event EventHandler<ErrorEventArgs> Error;
    }

    public interface IMessageHandler
    {
        Task HandleMessageAsync(IProtocolMessage message, NodeId sourceNodeId, string connectionId, CancellationToken cancellationToken = default);

        bool CanHandle(Type messageType);

        event EventHandler<MessageProcessedEventArgs> MessageProcessed;

        event EventHandler<MessageHandlerErrorEventArgs> HandlerError;
    }
}
