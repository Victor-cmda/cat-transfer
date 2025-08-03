using Domain.Enumerations;
using Domain.Events;
using Domain.ValueObjects;
using System.Drawing;

namespace Domain.Aggregates.FileTransfer
{
    public sealed class FileTransfer
    {
        private readonly List<ChunkState> _chunks = new();
        public FileId Id { get; }
        public FileMeta Meta { get; }
        public TransferStatus Status { get; private set; } = TransferStatus.Pending;

        public IReadOnlyList<ChunkState> Chunks => _chunks.AsReadOnly();

        public FileTransfer(FileId id, FileMeta meta, IEnumerable<ChunkState> chunks)
        {
            Id = id;
            Meta = meta;
            _chunks.AddRange(chunks);
        }

        public void Start()
        {
            Status = TransferStatus.InProgress;
        }

        public void Pause(NodeId sender)
        {
            if (Status == TransferStatus.InProgress)
            {
                Status = TransferStatus.Paused;
            }
            DomainEvents.Raise(new FileTransferPaused(Id, sender));
        }

        public void Resume(NodeId sender)
        {
            if (Status == TransferStatus.Paused)
            {
                Status = TransferStatus.InProgress;
            }
            DomainEvents.Raise(new FileTransferResumed(Id, sender, Meta.Size));
        }

        public void Complete()
        {
            if (Status == TransferStatus.InProgress)
            {
                Status = TransferStatus.Completed;
            }
            DomainEvents.Raise(new FileTransferCompleted(Id, DateTimeOffset.UtcNow));
        }

        public void Fail(string reason)
        {
            if (Status != TransferStatus.Completed)
            {
                Status = TransferStatus.Failed;
            }
            DomainEvents.Raise(new FileTransferFailed(Id, reason));
        }

        public void MarkChunckReceived(ChunkId id)
        {
            var chunk = _chunks.Find(x => x.Id.Equals(id)) ?? throw new InvalidOperationException($"Chunk with ID {id} not found in transfer {Id}.");

            chunk.MarkAsReceived();
            if (AllChuksReceived())
            {
                Complete();
            }
        }


        private bool AllChuksReceived()
        {
            return _chunks.TrueForAll(x => x.Received);
        }
    }
}