using Domain.ValueObjects;
using Infrastructure.Storage.Interfaces;
using Infrastructure.Storage.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Storage.Implementations
{
    public class SimpleMetadataStore : IMetadataStore
    {
        private readonly ILogger<SimpleMetadataStore> _logger;

        public SimpleMetadataStore(ILogger<SimpleMetadataStore> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<FileStorageMetadata?> GetFileMetadataAsync(FileId fileId, CancellationToken cancellationToken = default)
        {

            _logger.LogWarning("GetFileMetadataAsync not implemented - returning null");
            return Task.FromResult<FileStorageMetadata?>(null);
        }

        public Task SaveFileMetadataAsync(FileStorageMetadata metadata, CancellationToken cancellationToken = default)
        {

            _logger.LogInformation("SaveFileMetadataAsync called for {FileId}", metadata.FileId);
            return Task.CompletedTask;
        }

        public Task DeleteFileMetadataAsync(FileId fileId, CancellationToken cancellationToken = default)
        {

            _logger.LogInformation("DeleteFileMetadataAsync called for {FileId}", fileId);
            return Task.CompletedTask;
        }

        public Task<ChunkStorageInfo?> GetChunkInfoAsync(ChunkId chunkId, CancellationToken cancellationToken = default)
        {

            _logger.LogWarning("GetChunkInfoAsync not implemented - returning null");
            return Task.FromResult<ChunkStorageInfo?>(null);
        }

        public Task SaveChunkInfoAsync(ChunkStorageInfo chunkInfo, CancellationToken cancellationToken = default)
        {

            _logger.LogInformation("SaveChunkInfoAsync called for {ChunkId}", chunkInfo.ChunkId);
            return Task.CompletedTask;
        }

        public Task DeleteChunkInfoAsync(ChunkId chunkId, CancellationToken cancellationToken = default)
        {

            _logger.LogInformation("DeleteChunkInfoAsync called for {ChunkId}", chunkId);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<ChunkStorageInfo>> GetChunkInfosForFileAsync(FileId fileId, CancellationToken cancellationToken = default)
        {

            _logger.LogWarning("GetChunkInfosForFileAsync not implemented - returning empty list");
            return Task.FromResult<IEnumerable<ChunkStorageInfo>>(new List<ChunkStorageInfo>());
        }

        public Task<StorageStatistics> GetStorageStatisticsAsync(CancellationToken cancellationToken = default)
        {

            var stats = new StorageStatistics(0, 0, 0, 0, DateTime.UtcNow);
            return Task.FromResult(stats);
        }

        public Task<IEnumerable<FileStorageMetadata>> GetMetadataByStatusAsync(string status, CancellationToken cancellationToken = default)
        {

            _logger.LogWarning("GetMetadataByStatusAsync not implemented - returning empty list");
            return Task.FromResult<IEnumerable<FileStorageMetadata>>(new List<FileStorageMetadata>());
        }

        public Task UpdateChunkStatusAsync(ChunkId chunkId, string status, CancellationToken cancellationToken = default)
        {

            _logger.LogInformation("UpdateChunkStatusAsync called for {ChunkId} with status {Status}", chunkId, status);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<ChunkStorageInfo>> GetOrphanedChunksAsync(CancellationToken cancellationToken = default)
        {

            _logger.LogWarning("GetOrphanedChunksAsync not implemented - returning empty list");
            return Task.FromResult<IEnumerable<ChunkStorageInfo>>(new List<ChunkStorageInfo>());
        }
    }
}
