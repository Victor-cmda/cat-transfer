using Domain.ValueObjects;
using Domain.Aggregates.FileTransfer;

namespace Application.Messages
{
    public interface IApplicationMessage { }

    public interface IFileTransferMessage : IApplicationMessage
    {
        FileId FileId { get; }
    }

    public interface IChunkMessage : IApplicationMessage
    {
        ChunkId ChunkId { get; }
    }

    public interface INetworkMessage : IApplicationMessage
    {
        NodeId NodeId { get; }
    }

    public record StartFileTransferCommand(
        FileId FileId,
        FileMeta Meta,
        NodeId? InitiatorNode = null
    ) : IFileTransferMessage;

    public record PauseFileTransferCommand(
        FileId FileId,
        NodeId RequestingNode
    ) : IFileTransferMessage;

    public record ResumeFileTransferCommand(
        FileId FileId,
        NodeId RequestingNode
    ) : IFileTransferMessage;

    public record CancelFileTransferCommand(
        FileId FileId,
        NodeId RequestingNode
    ) : IFileTransferMessage;

    public record RequestChunkCommand(
        ChunkId ChunkId,
        NodeId FromNode,
        NodeId RequestingNode
    ) : IChunkMessage;

    public record StoreChunkCommand(
        ChunkId ChunkId,
        byte[] Data,
        NodeId? SourceNode = null
    ) : IChunkMessage;

    public record GetFileTransferStatusQuery(
        FileId FileId
    ) : IFileTransferMessage;

    public record GetActiveTransfersQuery : IApplicationMessage;

    public record OutboundChunkSentNotice(
        FileId FileId,
        long BytesSentSoFar
    ) : IFileTransferMessage;

    public record GetChunkAvailabilityQuery(
        ChunkId ChunkId
    ) : IChunkMessage;

    public record ConnectToPeerCommand(
        NodeId PeerId,
        string Endpoint
    ) : INetworkMessage { NodeId INetworkMessage.NodeId => PeerId; }

    public record DisconnectFromPeerCommand(
        NodeId PeerId
    ) : INetworkMessage { NodeId INetworkMessage.NodeId => PeerId; }

    public record BroadcastFileAvailabilityCommand(
        FileId FileId,
        NodeId AdvertisingNode
    ) : INetworkMessage { NodeId INetworkMessage.NodeId => AdvertisingNode; }
}
