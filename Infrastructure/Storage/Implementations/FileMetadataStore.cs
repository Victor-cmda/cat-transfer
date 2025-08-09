using Domain.ValueObjects;
using Infrastructure.Storage.Interfaces;
using Infrastructure.Storage.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Infrastructure.Storage.Implementations
{
    public class FileMetadataStore : IMetadataStore
    {
        private readonly string _basePath;
        private readonly ILogger<FileMetadataStore> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            IncludeFields = true
        };

        public FileMetadataStore(ILogger<FileMetadataStore> logger, string? basePath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _basePath = basePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CatTransfer", "Metadata");
            Directory.CreateDirectory(_basePath);
            Directory.CreateDirectory(Path.Combine(_basePath, "files"));
            Directory.CreateDirectory(Path.Combine(_basePath, "chunks"));
        }

        public async Task<FileStorageMetadata?> GetFileMetadataAsync(FileId fileId, CancellationToken cancellationToken = default)
        {
            var path = GetFileMetaPath(fileId);
            if (!File.Exists(path)) return null;
            try
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                return JsonSerializer.Deserialize<FileStorageMetadata>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read file metadata for {FileId}", fileId);
                return null;
            }
        }

        public async Task SaveFileMetadataAsync(FileStorageMetadata metadata, CancellationToken cancellationToken = default)
        {
            var path = GetFileMetaPath(metadata.FileId);
            try
            {
                var json = JsonSerializer.Serialize(metadata, _jsonOptions);
                await File.WriteAllTextAsync(path, json, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save file metadata for {FileId}", metadata.FileId);
                throw;
            }
        }

        public Task DeleteFileMetadataAsync(FileId fileId, CancellationToken cancellationToken = default)
        {
            var path = GetFileMetaPath(fileId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return Task.CompletedTask;
        }

        public async Task<ChunkStorageInfo?> GetChunkInfoAsync(ChunkId chunkId, CancellationToken cancellationToken = default)
        {
            var path = GetChunkMetaPath(chunkId);
            if (!File.Exists(path)) return null;
            try
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                return JsonSerializer.Deserialize<ChunkStorageInfo>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read chunk metadata for {ChunkId}", chunkId);
                return null;
            }
        }

        public async Task SaveChunkInfoAsync(ChunkStorageInfo chunkInfo, CancellationToken cancellationToken = default)
        {
            var path = GetChunkMetaPath(chunkInfo.ChunkId);
            try
            {
                var json = JsonSerializer.Serialize(chunkInfo, _jsonOptions);
                await File.WriteAllTextAsync(path, json, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save chunk metadata for {ChunkId}", chunkInfo.ChunkId);
                throw;
            }
        }

        public Task DeleteChunkInfoAsync(ChunkId chunkId, CancellationToken cancellationToken = default)
        {
            var path = GetChunkMetaPath(chunkId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return Task.CompletedTask;
        }

        public async Task<IEnumerable<ChunkStorageInfo>> GetChunkInfosForFileAsync(FileId fileId, CancellationToken cancellationToken = default)
        {
            var dir = Path.Combine(_basePath, "chunks");
            var prefix = fileId.ToString() + "_";
            var results = new List<ChunkStorageInfo>();
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var name = Path.GetFileName(file);
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken);
                    var info = JsonSerializer.Deserialize<ChunkStorageInfo>(json, _jsonOptions);
                    if (info != null) results.Add(info);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read chunk file {File}", file);
                }
            }
            return results;
        }

        public Task<StorageStatistics> GetStorageStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var filesCount = Directory.GetFiles(Path.Combine(_basePath, "files"), "*.json").Length;
                var chunkFiles = Directory.GetFiles(Path.Combine(_basePath, "chunks"), "*.json");
                long totalChunkSize = 0;
                foreach (var metaPath in chunkFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(metaPath);
                        var info = JsonSerializer.Deserialize<ChunkStorageInfo>(json, _jsonOptions);
                        if (info != null) totalChunkSize += info.CompressedSizeBytes;
                    }
                    catch { /* ignore individual errors */ }
                }
                var stats = new StorageStatistics(filesCount, chunkFiles.Length, totalChunkSize, totalChunkSize, DateTime.UtcNow);
                return Task.FromResult(stats);
            }
            catch
            {
                var stats = new StorageStatistics(0, 0, 0, 0, DateTime.UtcNow);
                return Task.FromResult(stats);
            }
        }

        public async Task<IEnumerable<FileStorageMetadata>> GetMetadataByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            var dir = Path.Combine(_basePath, "files");
            var results = new List<FileStorageMetadata>();
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken);
                    var meta = JsonSerializer.Deserialize<FileStorageMetadata>(json, _jsonOptions);
                    if (meta != null && string.Equals(meta.Status, status, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(meta);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read file metadata {File}", file);
                }
            }
            return results;
        }

        public async Task UpdateChunkStatusAsync(ChunkId chunkId, string status, CancellationToken cancellationToken = default)
        {
            var info = await GetChunkInfoAsync(chunkId, cancellationToken);
            if (info == null) return;
            var updated = info with { Status = status, LastAccessedAt = DateTime.UtcNow };
            await SaveChunkInfoAsync(updated, cancellationToken);
        }

        public async Task<IEnumerable<ChunkStorageInfo>> GetOrphanedChunksAsync(CancellationToken cancellationToken = default)
        {
            var chunks = await GetAllChunkInfosAsync(cancellationToken);
            var orphans = new List<ChunkStorageInfo>();
            foreach (var chunk in chunks)
            {
                var fileMeta = await GetFileMetadataAsync(chunk.FileId, cancellationToken);
                if (fileMeta == null)
                {
                    orphans.Add(chunk);
                }
            }
            return orphans;
        }

        private async Task<IEnumerable<ChunkStorageInfo>> GetAllChunkInfosAsync(CancellationToken cancellationToken)
        {
            var dir = Path.Combine(_basePath, "chunks");
            var results = new List<ChunkStorageInfo>();
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken);
                    var info = JsonSerializer.Deserialize<ChunkStorageInfo>(json, _jsonOptions);
                    if (info != null) results.Add(info);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read chunk file {File}", file);
                }
            }
            return results;
        }

        private string GetFileMetaPath(FileId fileId)
        {
            return Path.Combine(_basePath, "files", $"{fileId}.json");
        }

        private string GetChunkMetaPath(ChunkId chunkId)
        {
            return Path.Combine(_basePath, "chunks", $"{chunkId.file}_{chunkId.offset}.json");
        }
    }
}
