using Domain.Aggregates.FileTransfer;
using Domain.Enumerations;
using Domain.ValueObjects;
using Infrastructure.Storage.Interfaces;
using Infrastructure.Storage.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Infrastructure.Storage.Implementations
{
    public class LocalFileRepository : IFileRepository
    {
        private readonly string _storageBasePath;
        private readonly ILogger<LocalFileRepository> _logger;
        private readonly Dictionary<FileId, FileTransfer> _fileStorage;
        private readonly object _lock = new();

        public LocalFileRepository(ILogger<LocalFileRepository> logger, string? storageBasePath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storageBasePath = storageBasePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CatTransfer", "Files");
            _fileStorage = new Dictionary<FileId, FileTransfer>();

            if (!Directory.Exists(_storageBasePath))
                Directory.CreateDirectory(_storageBasePath);

            LoadExistingFiles();
        }

        public Task<FileTransfer?> GetByIdAsync(FileId fileId, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _fileStorage.TryGetValue(fileId, out var fileTransfer);
                return Task.FromResult(fileTransfer);
            }
        }

        public Task<IEnumerable<FileTransfer>> GetByStatusAsync(TransferStatus status, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var transfers = _fileStorage.Values.Where(f => f.Status == status).ToList();
                return Task.FromResult<IEnumerable<FileTransfer>>(transfers);
            }
        }

        public Task<IEnumerable<FileTransfer>> GetActiveTransfersAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var activeTransfers = _fileStorage.Values
                    .Where(f => f.Status == TransferStatus.InProgress || f.Status == TransferStatus.Paused)
                    .ToList();
                return Task.FromResult<IEnumerable<FileTransfer>>(activeTransfers);
            }
        }

        public async Task<FileTransfer> SaveAsync(FileTransfer fileTransfer, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _fileStorage[fileTransfer.Id] = fileTransfer;
            }

            await PersistFileAsync(fileTransfer, cancellationToken);
            _logger.LogDebug("File transfer {FileId} saved successfully", fileTransfer.Id);
            
            return fileTransfer;
        }

        public async Task DeleteAsync(FileId fileId, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _fileStorage.Remove(fileId);
            }

            var filePath = GetFileMetadataPath(fileId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _logger.LogDebug("File transfer {FileId} deleted successfully", fileId);
            await Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(FileId fileId, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var exists = _fileStorage.ContainsKey(fileId);
                return Task.FromResult(exists);
            }
        }

        public Task<IEnumerable<FileTransfer>> GetTransfersForNodeAsync(NodeId nodeId, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var transfers = _fileStorage.Values
                    .Where(f => f.Initiator?.Equals(nodeId) == true || f.Sources.Contains(nodeId))
                    .ToList();
                return Task.FromResult<IEnumerable<FileTransfer>>(transfers);
            }
        }

        public Task<long> GetTotalStorageUsedAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var totalBytes = _fileStorage.Values.Sum(f => f.Meta.Size.bytes);
                return Task.FromResult(totalBytes);
            }
        }

        public async Task CleanupCompletedTransfersAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
        {
            var cutoffTime = DateTimeOffset.UtcNow - olderThan;
            var toRemove = new List<FileId>();

            lock (_lock)
            {
                foreach (var transfer in _fileStorage.Values)
                {
                    if (transfer.Status == TransferStatus.Completed && 
                        transfer.CompletedAt.HasValue && 
                        transfer.CompletedAt.Value < cutoffTime)
                    {
                        toRemove.Add(transfer.Id);
                    }
                }
            }

            foreach (var fileId in toRemove)
            {
                await DeleteAsync(fileId, cancellationToken);
            }

            _logger.LogInformation("Cleaned up {Count} completed transfers older than {OlderThan}", toRemove.Count, olderThan);
        }

        private async Task PersistFileAsync(FileTransfer fileTransfer, CancellationToken cancellationToken)
        {
            try
            {
                var filePath = GetFileMetadataPath(fileTransfer.Id);
                var dto = FileTransferDto.FromDomain(fileTransfer);
                var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    IncludeFields = true
                });
                
                await File.WriteAllTextAsync(filePath, json, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist file transfer {FileId}", fileTransfer.Id);
                throw;
            }
        }

        private void LoadExistingFiles()
        {
            try
            {
                var metadataFiles = Directory.GetFiles(_storageBasePath, "*.json");
                foreach (var file in metadataFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var dto = JsonSerializer.Deserialize<FileTransferDto>(json);
                        if (dto != null)
                        {
                            var fileTransfer = dto.ToDomain();
                            _fileStorage[fileTransfer.Id] = fileTransfer;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load file metadata from {File}", file);
                    }
                }

                _logger.LogInformation("Loaded {Count} existing file transfers", _fileStorage.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load existing files from storage");
            }
        }

        private string GetFileMetadataPath(FileId fileId)
        {
            return Path.Combine(_storageBasePath, $"{fileId}.json");
        }
    }
}
