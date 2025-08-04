using Domain.ValueObjects;
using Infrastructure.Storage.Models;

namespace Infrastructure.Storage.Interfaces
{
    public interface IMetadataStore
    {
        Task<FileStorageMetadata?> GetFileMetadataAsync(FileId fileId, CancellationToken cancellationToken = default);
        Task SaveFileMetadataAsync(FileStorageMetadata metadata, CancellationToken cancellationToken = default);
        Task DeleteFileMetadataAsync(FileId fileId, CancellationToken cancellationToken = default);
        Task<ChunkStorageInfo?> GetChunkInfoAsync(ChunkId chunkId, CancellationToken cancellationToken = default);
        Task SaveChunkInfoAsync(ChunkStorageInfo chunkInfo, CancellationToken cancellationToken = default);
        Task DeleteChunkInfoAsync(ChunkId chunkId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ChunkStorageInfo>> GetChunkInfosForFileAsync(FileId fileId, CancellationToken cancellationToken = default);
        Task<StorageStatistics> GetStorageStatisticsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<FileStorageMetadata>> GetMetadataByStatusAsync(string status, CancellationToken cancellationToken = default);
        Task UpdateChunkStatusAsync(ChunkId chunkId, string status, CancellationToken cancellationToken = default);
        Task<IEnumerable<ChunkStorageInfo>> GetOrphanedChunksAsync(CancellationToken cancellationToken = default);
    }

    public record StorageStatistics(
        long TotalFiles,
        long TotalChunks,
        long TotalSizeBytes,
        long CompressedSizeBytes,
        DateTime LastUpdated
    );
}
