using Domain.Aggregates.FileTransfer;
using Domain.Enumerations;
using Domain.ValueObjects;
using System.Text.Json.Serialization;

namespace Infrastructure.Storage.Models
{
    public class FileTransferDto
    {
        public FileId Id { get; set; }
        public FileMeta Meta { get; set; }
        public TransferStatus Status { get; set; } = TransferStatus.Pending;
        public NodeId? Initiator { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public List<ChunkStateDto> Chunks { get; set; } = new();
        public List<NodeId> Sources { get; set; } = new();

        [JsonConstructor]
        public FileTransferDto() 
        {
            Id = new FileId(Guid.NewGuid().ToString());
            Meta = new FileMeta("", new ByteSize(0), 1024, new Checksum());
        }
        
        public static FileTransferDto FromDomain(FileTransfer fileTransfer)
        {
            return new FileTransferDto
            {
                Id = fileTransfer.Id,
                Meta = fileTransfer.Meta,
                Status = fileTransfer.Status,
                Initiator = fileTransfer.Initiator,
                CreatedAt = fileTransfer.CreatedAt,
                StartedAt = fileTransfer.StartedAt,
                CompletedAt = fileTransfer.CompletedAt,
                Chunks = fileTransfer.Chunks.Select(ChunkStateDto.FromDomain).ToList(),
                Sources = fileTransfer.Sources.ToList()
            };
        }
        
        public FileTransfer ToDomain()
        {
            var chunks = Chunks.Select(c => c.ToDomain());
            var fileTransfer = new FileTransfer(Id, Meta, chunks, Initiator);
            
            return fileTransfer;
        }
    }
    
    public class ChunkStateDto
    {
        public ChunkId Id { get; set; }
        public bool Received { get; set; }
        public DateTimeOffset? ReceivedAt { get; set; }
        public List<NodeId> AvailableFrom { get; set; } = new();
        public NodeId? CurrentSource { get; set; }
        public int RetryCount { get; set; }
        public Priority Priority { get; set; } = Priority.Normal;
        public DateTimeOffset? LastRequestedAt { get; set; }

        [JsonConstructor]
        public ChunkStateDto()
        {
            Id = new ChunkId(new FileId(Guid.NewGuid().ToString()), 0);
        }
        
        public static ChunkStateDto FromDomain(ChunkState chunkState)
        {
            return new ChunkStateDto
            {
                Id = chunkState.Id,
                Received = chunkState.Received,
                ReceivedAt = chunkState.ReceivedAt,
                AvailableFrom = chunkState.AvailableFrom.ToList(),
                CurrentSource = chunkState.CurrentSource,
                RetryCount = chunkState.RetryCount,
                Priority = chunkState.Priority,
                LastRequestedAt = chunkState.LastRequestedAt
            };
        }
        
        public ChunkState ToDomain()
        {
            var chunkState = new ChunkState(Id, Priority);
            
            foreach (var nodeId in AvailableFrom)
            {
                chunkState.AddAvailableSource(nodeId);
            }
            
            // Note: This is simplified - you might need to expose more methods in ChunkState
            // or create a factory method to restore full state
            
            return chunkState;
        }
    }
}
