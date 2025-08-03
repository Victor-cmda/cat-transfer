using Domain.Enumerations;
using Domain.Events;
using Domain.ValueObjects;

namespace Domain.Aggregates.FileTransfer
{
    public sealed class ChunkState
    {
        public ChunkId Id { get; }
        public bool Received { get; private set; }
        public DateTimeOffset? ReceivedAt { get; private set; }
        public HashSet<NodeId> AvailableFrom { get; } = new();
        public NodeId? CurrentSource { get; private set; }
        public int RetryCount { get; private set; }
        public Priority Priority { get; private set; } = Priority.Normal;
        public DateTimeOffset? LastRequestedAt { get; private set; }

        public ChunkState(ChunkId id, Priority priority = Priority.Normal)
        {
            Id = id;
            Priority = priority;
        }

        public void AddAvailableSource(NodeId nodeId)
        {
            if (AvailableFrom.Add(nodeId))
            {
                DomainEvents.Raise(new ChunkSourceDiscovered(Id, nodeId));
            }
        }

        public void RemoveAvailableSource(NodeId nodeId)
        {
            if (AvailableFrom.Remove(nodeId))
            {
                if (CurrentSource?.Equals(nodeId) == true)
                {
                    CurrentSource = null;
                }
                DomainEvents.Raise(new ChunkSourceLost(Id, nodeId));
            }
        }

        public bool RequestFromSource(NodeId sourceNode)
        {
            if (!AvailableFrom.Contains(sourceNode) || Received)
                return false;

            CurrentSource = sourceNode;
            LastRequestedAt = DateTimeOffset.UtcNow;
            DomainEvents.Raise(new ChunkRequested(Id, sourceNode));
            return true;
        }

        public void MarkAsReceived(NodeId? sourceNode = null)
        {
            if (!Received)
            {
                Received = true;
                ReceivedAt = DateTimeOffset.UtcNow;
                CurrentSource = sourceNode ?? CurrentSource;
                DomainEvents.Raise(new ChunkReceived(Id, DateTimeOffset.UtcNow, sourceNode));
            }
        }

        public void MarkRequestFailed(string reason)
        {
            RetryCount++;
            CurrentSource = null;
            DomainEvents.Raise(new ChunkRequestFailed(Id, reason, RetryCount));
        }

        public void SetPriority(Priority newPriority)
        {
            if (Priority != newPriority)
            {
                Priority = newPriority;
                DomainEvents.Raise(new ChunkPriorityChanged(Id, newPriority));
            }
        }

        public bool CanRetry(int maxRetries) => RetryCount < maxRetries && !Received;

        public bool HasAvailableSources => AvailableFrom.Count > 0;

        public NodeId? GetBestAvailableSource()
        {
            return AvailableFrom.FirstOrDefault(node => !node.Equals(CurrentSource));
        }

        public bool IsRequestTimeout(TimeSpan timeout)
        {
            return LastRequestedAt.HasValue &&
                   DateTimeOffset.UtcNow - LastRequestedAt.Value > timeout;
        }
    }
}
