using Domain.ValueObjects;

namespace Protocol.Contracts
{
    public interface IProtocolMessage
    {
        string MessageType { get; }

        string MessageId { get; }

        NodeId SourcePeerId { get; }

        NodeId? TargetPeerId { get; }

        DateTimeOffset Timestamp { get; }

        string ProtocolVersion { get; }

        string? CorrelationId { get; }

        byte Priority { get; }

        bool IsValid();

        int GetSize();
    }

    public interface IEncryptableMessage : IProtocolMessage
    {
        bool RequireEncryption { get; }

        byte[] GetPayload();

        void SetEncryptedPayload(byte[] encryptedPayload);
    }

    public interface IAcknowledgableMessage : IProtocolMessage
    {
        bool RequireAcknowledgment { get; }

        TimeSpan AcknowledgmentTimeout { get; }

        int MaxRetries { get; }
    }

    public interface IRequestMessage : IProtocolMessage
    {
        TimeSpan RequestTimeout { get; }

        string ExpectedResponseType { get; }
    }

    public interface IResponseMessage : IProtocolMessage
    {
        bool IsSuccess { get; }

        int? ErrorCode { get; }

        string? ErrorMessage { get; }
    }

    public interface IBroadcastMessage : IProtocolMessage
    {
        int TimeToLive { get; }

        string BroadcastScope { get; }
    }

    public interface IStreamingMessage : IProtocolMessage
    {
        long SequenceNumber { get; }

        long TotalMessages { get; }

        string StreamId { get; }

        bool IsLastMessage { get; }
    }
}
