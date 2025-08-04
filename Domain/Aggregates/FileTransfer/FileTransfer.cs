using Domain.Enumerations;
using Domain.Events;
using Domain.ValueObjects;

namespace Domain.Aggregates.FileTransfer
{
    public sealed class FileTransfer
    {
        private readonly List<ChunkState> _chunks = new();
        private readonly HashSet<NodeId> _sources = new();

        public FileId Id { get; }
        public FileMeta Meta { get; }
        public TransferStatus Status { get; private set; } = TransferStatus.Pending;
        public NodeId? Initiator { get; private set; }
        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset? StartedAt { get; private set; }
        public DateTimeOffset? CompletedAt { get; private set; }

        public IReadOnlyList<ChunkState> Chunks => _chunks.AsReadOnly();
        public IReadOnlySet<NodeId> Sources => _sources.ToHashSet();

        public FileTransfer(FileId id, FileMeta meta, IEnumerable<ChunkState> chunks, NodeId? initiator = null)
        {
            Id = id;
            Meta = meta;
            Initiator = initiator;
            CreatedAt = DateTimeOffset.UtcNow;
            _chunks.AddRange(chunks);
        }

        public void AddSource(NodeId sourceNode)
        {
            if (_sources.Add(sourceNode))
            {
                foreach (var chunk in _chunks.Where(c => !c.Received))
                {
                    chunk.AddAvailableSource(sourceNode);
                }
                DomainEvents.Raise(new FileTransferSourceAdded(Id, sourceNode));
            }
        }

        public async Task AddSourceAsync(NodeId sourceNode, CancellationToken cancellationToken = default)
        {
            if (_sources.Add(sourceNode))
            {
                foreach (var chunk in _chunks.Where(c => !c.Received))
                {
                    chunk.AddAvailableSource(sourceNode);
                }
                await AsyncDomainEvents.RaiseAsync(new FileTransferSourceAdded(Id, sourceNode), cancellationToken);
            }
        }

        public void RemoveSource(NodeId sourceNode)
        {
            if (_sources.Remove(sourceNode))
            {
                foreach (var chunk in _chunks)
                {
                    chunk.RemoveAvailableSource(sourceNode);
                }
                DomainEvents.Raise(new FileTransferSourceRemoved(Id, sourceNode));
            }
        }

        public async Task RemoveSourceAsync(NodeId sourceNode, CancellationToken cancellationToken = default)
        {
            if (_sources.Remove(sourceNode))
            {
                foreach (var chunk in _chunks)
                {
                    chunk.RemoveAvailableSource(sourceNode);
                }
                await AsyncDomainEvents.RaiseAsync(new FileTransferSourceRemoved(Id, sourceNode), cancellationToken);
            }
        }

        public void Start(NodeId? initiator = null)
        {
            if (Status == TransferStatus.Pending)
            {
                Status = TransferStatus.InProgress;
                StartedAt = DateTimeOffset.UtcNow;
                Initiator ??= initiator;
                DomainEvents.Raise(new FileTransferStarted(Id, Initiator ?? NodeId.NewGuid(), Meta.Size));
            }
        }

        public async Task StartAsync(NodeId? initiator = null, CancellationToken cancellationToken = default)
        {
            if (Status == TransferStatus.Pending)
            {
                Status = TransferStatus.InProgress;
                StartedAt = DateTimeOffset.UtcNow;
                Initiator ??= initiator;
                await AsyncDomainEvents.RaiseAsync(new FileTransferStarted(Id, Initiator ?? NodeId.NewGuid(), Meta.Size), cancellationToken);
            }
        }

        public void Pause(NodeId sender)
        {
            if (Status == TransferStatus.InProgress)
            {
                Status = TransferStatus.Paused;
                DomainEvents.Raise(new FileTransferPaused(Id, sender));
            }
        }

        public async Task PauseAsync(NodeId sender, CancellationToken cancellationToken = default)
        {
            if (Status == TransferStatus.InProgress)
            {
                Status = TransferStatus.Paused;
                await AsyncDomainEvents.RaiseAsync(new FileTransferPaused(Id, sender), cancellationToken);
            }
        }

        public void Resume(NodeId sender)
        {
            if (Status == TransferStatus.Paused)
            {
                Status = TransferStatus.InProgress;
                DomainEvents.Raise(new FileTransferResumed(Id, sender, Meta.Size));
            }
        }

        public void Complete()
        {
            if (Status == TransferStatus.InProgress && AllChunksReceived())
            {
                Status = TransferStatus.Completed;
                CompletedAt = DateTimeOffset.UtcNow;
                DomainEvents.Raise(new FileTransferCompleted(Id, CompletedAt.Value));
            }
        }

        public void Fail(string reason)
        {
            if (Status != TransferStatus.Completed)
            {
                Status = TransferStatus.Failed;
                DomainEvents.Raise(new FileTransferFailed(Id, reason));
            }
        }

        public void Cancel(NodeId sender)
        {
            if (Status == TransferStatus.InProgress || Status == TransferStatus.Paused)
            {
                Status = TransferStatus.Failed;
                DomainEvents.Raise(new FileTransferCancelled(Id, sender));
            }
        }

        public void MarkChunkReceived(ChunkId chunkId, NodeId? sourceNode = null)
        {
            var chunk = _chunks.Find(x => x.Id.Equals(chunkId)) 
                ?? throw new InvalidOperationException($"Chunk with ID {chunkId} not found in transfer {Id}.");

            chunk.MarkAsReceived(sourceNode);
            
            var completedChunks = _chunks.Count(c => c.Received);
            var totalChunks = _chunks.Count;
            var transferredBytes = new ByteSize(completedChunks * Meta.ChunkSize);
            
            DomainEvents.Raise(new FileTransferProgress(Id, transferredBytes, Meta.Size));

            if (AllChunksReceived())
            {
                Complete();
            }
        }

        public bool RequestNextChunk(NodeId requestingNode)
        {
            var availableChunk = GetNextChunkToRequest(requestingNode);
            if (availableChunk == null)
                return false;

            return availableChunk.RequestFromSource(requestingNode);
        }

        public void SetChunkPriority(ChunkId chunkId, Priority priority)
        {
            var chunk = _chunks.Find(x => x.Id.Equals(chunkId));
            chunk?.SetPriority(priority);
        }

        public void HandleChunkRequestFailed(ChunkId chunkId, string reason)
        {
            var chunk = _chunks.Find(x => x.Id.Equals(chunkId));
            chunk?.MarkRequestFailed(reason);
        }

        private ChunkState? GetNextChunkToRequest(NodeId requestingNode)
        {
            return _chunks
                .Where(c => !c.Received && c.AvailableFrom.Contains(requestingNode) && c.CurrentSource == null)
                .OrderByDescending(c => c.Priority)
                .ThenBy(c => c.RetryCount) 
                .FirstOrDefault();
        }

        private bool AllChunksReceived() => _chunks.TrueForAll(x => x.Received);

        public double CompletionPercentage
        {
            get
            {
                if (_chunks.Count == 0) return 0;
                return (_chunks.Count(c => c.Received) / (double)_chunks.Count) * 100;
            }
        }

        public ByteSize TransferredBytes
        {
            get
            {
                var completedChunks = _chunks.Count(c => c.Received);
                return new ByteSize(completedChunks * Meta.ChunkSize);
            }
        }

        public TimeSpan? TransferDuration
        {
            get
            {
                if (StartedAt == null) return null;
                var endTime = CompletedAt ?? DateTimeOffset.UtcNow;
                return endTime - StartedAt.Value;
            }
        }

        public bool HasAvailableSources => _sources.Count > 0;
    }
}