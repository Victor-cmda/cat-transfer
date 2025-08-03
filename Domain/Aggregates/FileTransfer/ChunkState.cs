using Domain.ValueObjects;

namespace Domain.Aggregates.FileTransfer
{
    public sealed class ChunkState
    {
        public ChunkId Id { get; set; }
        public bool Received { get; set; }
        public DateTimeOffset? ReceivedAt { get; set; }

        public ChunkState(ChunkId id)
        {
            Id = id;
        }

        public void MarkAsReceived()
        {
            Received = true;
            ReceivedAt = DateTimeOffset.UtcNow;
        }
    }
}
