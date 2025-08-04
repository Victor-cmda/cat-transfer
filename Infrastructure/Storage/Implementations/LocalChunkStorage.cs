using Domain.ValueObjects;
using Infrastructure.Storage.Interfaces;
using Infrastructure.Storage.Models;
using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace Infrastructure.Storage.Implementations
{
    public class LocalChunkStorage : IChunkStorage
    {
        private readonly string _storageBasePath;
        private readonly IMetadataStore _metadataStore;
        private readonly ILogger<LocalChunkStorage> _logger;
        private readonly bool _enableCompression;

        public LocalChunkStorage(
            IMetadataStore metadataStore,
            ILogger<LocalChunkStorage> logger,
            string? storageBasePath = null,
            bool enableCompression = true)
        {
            _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storageBasePath = storageBasePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CatTransfer", "Chunks");
            _enableCompression = enableCompression;

            if (!Directory.Exists(_storageBasePath))
                Directory.CreateDirectory(_storageBasePath);
        }

        public async Task<byte[]?> GetChunkAsync(ChunkId chunkId, CancellationToken cancellationToken = default)
        {
            try
            {
                var chunkInfo = await _metadataStore.GetChunkInfoAsync(chunkId, cancellationToken);
                if (chunkInfo == null)
                    return null;

                var chunkPath = Path.Combine(_storageBasePath, $"{chunkId}.chunk");
                if (!File.Exists(chunkPath))
                    return null;

                var data = await File.ReadAllBytesAsync(chunkPath, cancellationToken);

                if (chunkInfo.IsCompressed)
                {
                    data = DecompressData(data);
                }

                _logger.LogDebug("Retrieved chunk {ChunkId}, size: {Size}", chunkId, data.Length);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chunk {ChunkId}", chunkId);
                return null;
            }
        }

        public async Task StoreChunkAsync(ChunkId chunkId, byte[] data, CancellationToken cancellationToken = default)
        {
            try
            {
                var originalSize = data.Length;
                var dataToStore = data;
                var isCompressed = false;

                if (_enableCompression && data.Length > 1024)
                {
                    var compressedData = CompressData(data);
                    if (compressedData.Length < data.Length * 0.9)
                    {
                        dataToStore = compressedData;
                        isCompressed = true;
                    }
                }

                var chunkPath = Path.Combine(_storageBasePath, $"{chunkId}.chunk");
                await File.WriteAllBytesAsync(chunkPath, dataToStore, cancellationToken);

                var chunkInfo = new ChunkStorageInfo(
                    chunkId,
                    chunkId.file,
                    (int)chunkId.offset,
                    originalSize,
                    "sha256",
                    "Stored",
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    chunkPath,
                    isCompressed,
                    dataToStore.Length,
                    0,
                    DateTime.UtcNow
                );

                await _metadataStore.SaveChunkInfoAsync(chunkInfo, cancellationToken);
                _logger.LogDebug("Stored chunk {ChunkId}, size: {Size}", chunkId, originalSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store chunk {ChunkId}", chunkId);
                throw;
            }
        }

        public async Task<bool> HasChunkAsync(ChunkId chunkId, CancellationToken cancellationToken = default)
        {
            var chunkInfo = await _metadataStore.GetChunkInfoAsync(chunkId, cancellationToken);
            return chunkInfo != null;
        }

        public async Task DeleteChunkAsync(ChunkId chunkId, CancellationToken cancellationToken = default)
        {
            try
            {
                var chunkPath = Path.Combine(_storageBasePath, $"{chunkId}.chunk");
                if (File.Exists(chunkPath))
                {
                    File.Delete(chunkPath);
                }

                await _metadataStore.DeleteChunkInfoAsync(chunkId, cancellationToken);
                _logger.LogDebug("Deleted chunk {ChunkId}", chunkId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete chunk {ChunkId}", chunkId);
                throw;
            }
        }

        public async Task<IEnumerable<ChunkId>> GetChunksForFileAsync(FileId fileId, CancellationToken cancellationToken = default)
        {
            var chunkInfos = await _metadataStore.GetChunkInfosForFileAsync(fileId, cancellationToken);
            return chunkInfos.Select(ci => ci.ChunkId);
        }

        public async Task<long> GetChunkSizeAsync(ChunkId chunkId, CancellationToken cancellationToken = default)
        {
            var chunkInfo = await _metadataStore.GetChunkInfoAsync(chunkId, cancellationToken);
            return chunkInfo?.ChunkSizeBytes ?? 0;
        }

        public async Task<Stream> GetChunkStreamAsync(ChunkId chunkId, CancellationToken cancellationToken = default)
        {
            var data = await GetChunkAsync(chunkId, cancellationToken);
            return data != null ? new MemoryStream(data) : new MemoryStream();
        }

        public async Task StoreChunkStreamAsync(ChunkId chunkId, Stream dataStream, CancellationToken cancellationToken = default)
        {
            using var memoryStream = new MemoryStream();
            await dataStream.CopyToAsync(memoryStream, cancellationToken);
            var data = memoryStream.ToArray();
            await StoreChunkAsync(chunkId, data, cancellationToken);
        }

        public async Task CompressChunkAsync(ChunkId chunkId, CancellationToken cancellationToken = default)
        {
            var data = await GetChunkAsync(chunkId, cancellationToken);
            if (data != null && data.Length > 1024)
            {
                var compressedData = CompressData(data);
                if (compressedData.Length < data.Length * 0.9)
                {
                    await StoreChunkAsync(chunkId, compressedData, cancellationToken);
                }
            }
        }

        public async Task DecompressChunkAsync(ChunkId chunkId, CancellationToken cancellationToken = default)
        {
            var chunkInfo = await _metadataStore.GetChunkInfoAsync(chunkId, cancellationToken);
            if (chunkInfo?.IsCompressed == true)
            {
                var compressedData = await File.ReadAllBytesAsync(chunkInfo.StoragePath, cancellationToken);
                var decompressedData = DecompressData(compressedData);
                await StoreChunkAsync(chunkId, decompressedData, cancellationToken);
            }
        }

        public async Task<long> GetTotalChunkSizeAsync(CancellationToken cancellationToken = default)
        {
            var statistics = await _metadataStore.GetStorageStatisticsAsync(cancellationToken);
            return statistics.CompressedSizeBytes;
        }

        public async Task CleanupOrphanedChunksAsync(CancellationToken cancellationToken = default)
        {
            var orphanedChunks = await _metadataStore.GetOrphanedChunksAsync(cancellationToken);
            
            foreach (var chunkInfo in orphanedChunks)
            {
                await DeleteChunkAsync(chunkInfo.ChunkId, cancellationToken);
            }

            _logger.LogInformation("Cleaned up {Count} orphaned chunks", orphanedChunks.Count());
        }

        private byte[] CompressData(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        private byte[] DecompressData(byte[] compressedData)
        {
            using var input = new MemoryStream(compressedData);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }
}
