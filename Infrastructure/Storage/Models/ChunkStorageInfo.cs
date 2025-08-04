using Domain.ValueObjects;

namespace Infrastructure.Storage.Models
{
    public record ChunkStorageInfo(
        ChunkId ChunkId,
        FileId FileId,
        int ChunkIndex,
        long ChunkSizeBytes,
        string ChecksumValue,
        string Status,
        DateTime CreatedAt,
        DateTime? CompletedAt,
        string StoragePath,
        bool IsCompressed,
        long CompressedSizeBytes,
        int RetryCount,
        DateTime? LastAccessedAt
    );
}
