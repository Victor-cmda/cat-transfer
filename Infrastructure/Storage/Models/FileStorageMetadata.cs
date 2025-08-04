using Domain.ValueObjects;

namespace Infrastructure.Storage.Models
{
    public record FileStorageMetadata(
        FileId FileId,
        string FileName,
        long FileSizeBytes,
        string ChecksumAlgorithm,
        string ChecksumValue,
        string Status,
        DateTime CreatedAt,
        DateTime? CompletedAt,
        string StoragePath,
        bool IsCompressed,
        long CompressedSizeBytes,
        NodeId? InitiatorNodeId,
        int TotalChunks,
        int CompletedChunks
    );
}
