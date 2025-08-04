using Domain.ValueObjects;
using Domain.Aggregates.FileTransfer;
using Domain.Enumerations;

namespace Application.Messages
{
    public interface IApplicationResponse : IApplicationMessage { }

    public record FileTransferStarted(
        FileId FileId,
        NodeId InitiatorNode,
        DateTime StartedAt
    ) : IApplicationResponse;

    public record FileTransferPaused(
        FileId FileId,
        NodeId RequestingNode,
        DateTime PausedAt
    ) : IApplicationResponse;

    public record FileTransferResumed(
        FileId FileId,
        NodeId RequestingNode,
        DateTime ResumedAt
    ) : IApplicationResponse;

    public record FileTransferCompleted(
        FileId FileId,
        DateTime CompletedAt,
        ByteSize TotalSize
    ) : IApplicationResponse;

    public record FileTransferFailed(
        FileId FileId,
        string Reason,
        DateTime FailedAt
    ) : IApplicationResponse;

    public record FileTransferCancelled(
        FileId FileId,
        NodeId RequestingNode,
        DateTime CancelledAt
    ) : IApplicationResponse;

    public record ChunkStored(
        ChunkId ChunkId,
        NodeId? SourceNode,
        DateTime StoredAt
    ) : IApplicationResponse;

    public record ChunkRequested(
        ChunkId ChunkId,
        NodeId FromNode,
        NodeId RequestingNode,
        DateTime RequestedAt
    ) : IApplicationResponse;

    public record ChunkRequestFailed(
        ChunkId ChunkId,
        NodeId FromNode,
        string Reason,
        DateTime FailedAt
    ) : IApplicationResponse;

    public record FileTransferStatusResponse(
        FileId FileId,
        TransferStatus Status,
        double CompletionPercentage,
        ByteSize TransferredBytes,
        ByteSize TotalBytes,
        TimeSpan? Duration,
        IReadOnlyList<NodeId> Sources
    ) : IApplicationResponse;

    public record ActiveTransfersResponse(
        IReadOnlyList<FileTransferStatusResponse> ActiveTransfers,
        int TotalCount
    ) : IApplicationResponse;

    public record ChunkAvailabilityResponse(
        ChunkId ChunkId,
        IReadOnlyList<NodeId> AvailableFromNodes,
        bool IsLocallyCached
    ) : IApplicationResponse;

    public record PeerConnected(
        NodeId PeerId,
        string Endpoint,
        DateTime ConnectedAt
    ) : IApplicationResponse;

    public record PeerDisconnected(
        NodeId PeerId,
        string Reason,
        DateTime DisconnectedAt
    ) : IApplicationResponse;

    public record FileAvailabilityBroadcasted(
        FileId FileId,
        NodeId AdvertisingNode,
        int BroadcastCount,
        DateTime BroadcastedAt
    ) : IApplicationResponse;

    public record ApplicationError(
        string ErrorCode,
        string Message,
        Exception? Exception = null,
        DateTime OccurredAt = default
    ) : IApplicationResponse
    {
        public DateTime OccurredAt { get; init; } = OccurredAt == default ? DateTime.UtcNow : OccurredAt;
    }
}
