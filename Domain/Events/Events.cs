using Domain.Interfaces;
using Domain.ValueObjects;

namespace Domain.Events
{
    public sealed record ChunkReceived(ChunkId chunkId, DateTimeOffset at) : IDomainEvent;
    public sealed record FileTransferRequested(FileId fileId, NodeId sender, ByteSize size) : IDomainEvent;
    public sealed record FileTransferCompleted(FileId fileId, DateTimeOffset completedAt) : IDomainEvent;
    public sealed record FileTransferFailed(FileId fileId, string reason) : IDomainEvent;
    public sealed record FileTransferProgress(FileId fileId, ByteSize transferred, ByteSize total) : IDomainEvent;
    public sealed record FileTransferStarted(FileId fileId, NodeId sender, ByteSize size) : IDomainEvent;
    public sealed record FileTransferCancelled(FileId fileId, NodeId sender) : IDomainEvent;
    public sealed record FileTransferResumed(FileId fileId, NodeId sender, ByteSize size) : IDomainEvent;
    public sealed record FileTransferPaused(FileId fileId, NodeId sender) : IDomainEvent;
}
