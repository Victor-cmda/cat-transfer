using Domain.ValueObjects;
using Infrastructure.Storage.Interfaces;
using Infrastructure.Storage.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Infrastructure.Storage.Implementations
{
    public class TempFileManager : ITempFileManager, IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ConcurrentDictionary<string, TempFileInfo> _tempFiles;
        private readonly ILogger<TempFileManager> _logger;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(30);

        public TempFileManager(ILogger<TempFileManager> logger, string? tempDirectory = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tempDirectory = tempDirectory ?? Path.Combine(Path.GetTempPath(), "cat-transfer");
            _tempFiles = new ConcurrentDictionary<string, TempFileInfo>();

            if (!Directory.Exists(_tempDirectory))
                Directory.CreateDirectory(_tempDirectory);

            _cleanupTimer = new Timer(async _ => await CleanupExpiredTempFilesAsync(TimeSpan.FromHours(24)), null, _cleanupInterval, _cleanupInterval);
        }

        public Task<StorageLocation> CreateTempFileAsync(FileId fileId, CancellationToken cancellationToken = default)
        {
            var fileName = $"file_{fileId}_{Guid.NewGuid():N}.tmp";
            var fullPath = Path.Combine(_tempDirectory, fileName);

            using (var stream = File.Create(fullPath))
            {
            }

            var tempFileInfo = new TempFileInfo(fullPath, DateTime.UtcNow, 0);
            _tempFiles.TryAdd(fullPath, tempFileInfo);

            _logger.LogDebug("Created temporary file for FileId {FileId}: {FilePath}", fileId, fullPath);
            
            var storageLocation = new StorageLocation(
                _tempDirectory,
                fileName,
                fullPath,
                StorageType.Temporary,
                false
            );
            
            return Task.FromResult(storageLocation);
        }

        public Task<StorageLocation> CreateTempChunkAsync(ChunkId chunkId, CancellationToken cancellationToken = default)
        {
            var fileName = $"chunk_{chunkId}_{Guid.NewGuid():N}.tmp";
            var fullPath = Path.Combine(_tempDirectory, fileName);

            using (var stream = File.Create(fullPath))
            {
            }

            var tempFileInfo = new TempFileInfo(fullPath, DateTime.UtcNow, 0);
            _tempFiles.TryAdd(fullPath, tempFileInfo);

            _logger.LogDebug("Created temporary chunk file for ChunkId {ChunkId}: {FilePath}", chunkId, fullPath);
            
            var storageLocation = new StorageLocation(
                _tempDirectory,
                fileName,
                fullPath,
                StorageType.Temporary,
                false
            );
            
            return Task.FromResult(storageLocation);
        }

        public Task DeleteTempFileAsync(StorageLocation location, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_tempFiles.TryRemove(location.FullPath, out var tempFileInfo))
                {
                    if (tempFileInfo.IsDirectory)
                    {
                        if (Directory.Exists(location.FullPath))
                        {
                            Directory.Delete(location.FullPath, recursive: true);
                        }
                    }
                    else
                    {
                        if (File.Exists(location.FullPath))
                        {
                            File.Delete(location.FullPath);
                        }
                    }

                    _logger.LogDebug("Deleted temporary file/directory: {Path}", location.FullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete temporary file/directory: {Path}", location.FullPath);
                throw;
            }
            
            return Task.CompletedTask;
        }

        public Task<bool> IsTempFileAsync(StorageLocation location, CancellationToken cancellationToken = default)
        {
            var isTempFile = _tempFiles.ContainsKey(location.FullPath);
            return Task.FromResult(isTempFile);
        }

        public async Task CleanupExpiredTempFilesAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            var expiredFiles = new List<string>();
            var cutoffTime = DateTime.UtcNow - maxAge;

            foreach (var kvp in _tempFiles)
            {
                if (kvp.Value.CreatedAt < cutoffTime)
                {
                    expiredFiles.Add(kvp.Key);
                }
            }

            foreach (var expiredFile in expiredFiles)
            {
                var location = new StorageLocation(
                    _tempDirectory,
                    Path.GetFileName(expiredFile),
                    expiredFile,
                    StorageType.Temporary,
                    false
                );
                await DeleteTempFileAsync(location, cancellationToken);
            }

            if (expiredFiles.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired temporary files", expiredFiles.Count);
            }
        }

        public Task<IEnumerable<StorageLocation>> GetAllTempFilesAsync(CancellationToken cancellationToken = default)
        {
            var locations = _tempFiles.Keys.Select(path => new StorageLocation(
                _tempDirectory,
                Path.GetFileName(path),
                path,
                StorageType.Temporary,
                false
            )).ToList();
            
            return Task.FromResult<IEnumerable<StorageLocation>>(locations);
        }

        public Task<long> GetTempStorageUsageAsync(CancellationToken cancellationToken = default)
        {
            long totalSize = 0;

            foreach (var kvp in _tempFiles)
            {
                try
                {
                    if (kvp.Value.IsDirectory)
                    {
                        if (Directory.Exists(kvp.Key))
                        {
                            totalSize += GetDirectorySize(kvp.Key);
                        }
                    }
                    else
                    {
                        if (File.Exists(kvp.Key))
                        {
                            var fileInfo = new FileInfo(kvp.Key);
                            totalSize += fileInfo.Length;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get size for temp file: {Path}", kvp.Key);
                }
            }

            return Task.FromResult(totalSize);
        }

        public Task MoveTempToPermanentAsync(StorageLocation tempLocation, StorageLocation permanentLocation, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(tempLocation.FullPath))
                    throw new FileNotFoundException($"Temporary file not found: {tempLocation.FullPath}");

                var permanentDir = Path.GetDirectoryName(permanentLocation.FullPath);
                if (!string.IsNullOrEmpty(permanentDir) && !Directory.Exists(permanentDir))
                    Directory.CreateDirectory(permanentDir);

                File.Move(tempLocation.FullPath, permanentLocation.FullPath);

                _tempFiles.TryRemove(tempLocation.FullPath, out _);

                _logger.LogDebug("Moved temporary file from {TempPath} to {PermanentPath}", tempLocation.FullPath, permanentLocation.FullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move temporary file from {TempPath} to {PermanentPath}", tempLocation.FullPath, permanentLocation.FullPath);
                throw;
            }
            
            return Task.CompletedTask;
        }

        private long GetDirectorySize(string directoryPath)
        {
            var directoryInfo = new DirectoryInfo(directoryPath);
            return directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            Task.Run(async () => await CleanupAllTempFilesAsync()).Wait(TimeSpan.FromSeconds(10));
        }

        private async Task CleanupAllTempFilesAsync()
        {
            var allFiles = _tempFiles.Keys.ToList();

            foreach (var file in allFiles)
            {
                var location = new StorageLocation(
                    _tempDirectory,
                    Path.GetFileName(file),
                    file,
                    StorageType.Temporary,
                    false
                );
                await DeleteTempFileAsync(location, default);
            }

            _logger.LogInformation("Cleaned up all {Count} temporary files", allFiles.Count);
        }

        internal class TempFileInfo
        {
            public string FilePath { get; }
            public DateTime CreatedAt { get; }
            public long Size { get; }
            public bool IsDirectory { get; }

            public TempFileInfo(string filePath, DateTime createdAt, long size, bool isDirectory = false)
            {
                FilePath = filePath;
                CreatedAt = createdAt;
                Size = size;
                IsDirectory = isDirectory;
            }
        }
    }
}
