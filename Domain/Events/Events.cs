using Domain.Aggregates.FileTransfer;
using Domain.Enumerations;
using Domain.Events;
using Domain.ValueObjects;

namespace Domain.Events
{
    public sealed record ChunkReceived(ChunkId chunkId, DateTimeOffset at, NodeId? sourceNode = null) : IDomainEvent;
    public sealed record FileTransferRequested(FileId fileId, NodeId sender, ByteSize size) : IDomainEvent;
    public sealed record FileTransferCompleted(FileId fileId, DateTimeOffset completedAt) : IDomainEvent;
    public sealed record FileTransferFailed(FileId fileId, string reason) : IDomainEvent;
    public sealed record FileTransferProgress(FileId fileId, ByteSize transferred, ByteSize total) : IDomainEvent;
    public sealed record FileTransferStarted(FileId fileId, NodeId sender, ByteSize size) : IDomainEvent;
    public sealed record FileTransferCancelled(FileId fileId, NodeId sender) : IDomainEvent;
    public sealed record FileTransferResumed(FileId fileId, NodeId sender, ByteSize size) : IDomainEvent;
    public sealed record FileTransferPaused(FileId fileId, NodeId sender) : IDomainEvent;
    public sealed record FileTransferSourceAdded(FileId fileId, NodeId sourceNode) : IDomainEvent;
    public sealed record FileTransferSourceRemoved(FileId fileId, NodeId sourceNode) : IDomainEvent;

    public sealed record ChunkRequested(ChunkId chunkId, NodeId fromPeer) : IDomainEvent;
    public sealed record ChunkRequestFailed(ChunkId chunkId, string reason, int retryCount) : IDomainEvent;
    public sealed record ChunkSourceDiscovered(ChunkId chunkId, NodeId sourceNode) : IDomainEvent;
    public sealed record ChunkSourceLost(ChunkId chunkId, NodeId sourceNode) : IDomainEvent;
    public sealed record ChunkPriorityChanged(ChunkId chunkId, Priority newPriority) : IDomainEvent;

    public sealed record PeerDiscovered(NodeId peerId, PeerAddress address) : IDomainEvent;
    public sealed record PeerConnectionStarted(NodeId peerId, PeerAddress address) : IDomainEvent;
    public sealed record PeerConnected(NodeId peerId, PeerAddress address) : IDomainEvent;
    public sealed record PeerDisconnected(NodeId peerId, string reason) : IDomainEvent;
    public sealed record PeerConnectionFailed(NodeId peerId, string reason, int attemptCount) : IDomainEvent;
    public sealed record PeerAddressUpdated(NodeId peerId, PeerAddress newAddress) : IDomainEvent;
    public sealed record PeerTopologyUpdated(NodeId peerId, int knownPeerCount) : IDomainEvent;

    public sealed record PeerAuthenticationStarted(NodeId peerId) : IDomainEvent;
    public sealed record HandshakeCompleted(NodeId peerId, EncryptionKey key) : IDomainEvent;
    public sealed record HandshakeFailed(NodeId peerId, string reason) : IDomainEvent;
    public sealed record EncryptionKeyRotated(NodeId peerId, EncryptionKey newKey) : IDomainEvent;
    public sealed record KeyExchangeInitiated(NodeId peerId, int publicKeySize) : IDomainEvent;
    public sealed record KeyExchangeCompleted(NodeId peerId, EncryptionKey sharedKey) : IDomainEvent;
    public sealed record KeyExchangeFailed(NodeId peerId, string reason) : IDomainEvent;
    public sealed record KeyExchangeResponded(NodeId peerId, EncryptionKey sharedKey) : IDomainEvent;

    public sealed record NetworkTopologyChanged(NetworkTopology newTopology) : IDomainEvent;
    public sealed record PeerListReceived(NodeId fromPeer, IReadOnlyList<NodeId> peerList) : IDomainEvent;
    public sealed record FileAdvertisementReceived(NodeId fromPeer, FileId fileId, FileMeta meta) : IDomainEvent;
    public sealed record FileAdvertisementSent(NodeId toPeer, FileId fileId) : IDomainEvent;
    public sealed record LocalAddressUpdated(NodeId nodeId, PeerAddress newAddress) : IDomainEvent;
    public sealed record PeerRemoved(NodeId peerId) : IDomainEvent;
    public sealed record FileAvailabilityUpdated(FileId fileId, NodeId peerId, bool available) : IDomainEvent;
    public sealed record PotentialPeerDiscovered(NodeId peerId, NodeId discoveredBy) : IDomainEvent;
    public sealed record BroadcastRequested(string eventType, IReadOnlyList<NodeId> targetPeers) : IDomainEvent;
    public sealed record PeerDiscoveryError(string message) : IDomainEvent;
    public sealed record PresenceAnnounced(NodeId nodeId, PeerAddress address) : IDomainEvent;
}
