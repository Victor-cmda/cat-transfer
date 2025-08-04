using Domain.ValueObjects;
using Infrastructure.Storage.Models;

namespace Infrastructure.Storage.Interfaces
{
    public interface ITempFileManager
    {
        Task<StorageLocation> CreateTempFileAsync(FileId fileId, CancellationToken cancellationToken = default);
        Task<StorageLocation> CreateTempChunkAsync(ChunkId chunkId, CancellationToken cancellationToken = default);
        Task DeleteTempFileAsync(StorageLocation location, CancellationToken cancellationToken = default);
        Task<bool> IsTempFileAsync(StorageLocation location, CancellationToken cancellationToken = default);
        Task CleanupExpiredTempFilesAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);
        Task<IEnumerable<StorageLocation>> GetAllTempFilesAsync(CancellationToken cancellationToken = default);
        Task<long> GetTempStorageUsageAsync(CancellationToken cancellationToken = default);
        Task MoveTempToPermanentAsync(StorageLocation tempLocation, StorageLocation permanentLocation, CancellationToken cancellationToken = default);
    }
}
