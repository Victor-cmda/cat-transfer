using Domain.ValueObjects;
using Domain.Aggregates.FileTransfer;

namespace Infrastructure.Storage.Interfaces
{
    public interface IFileRepository
    {
        Task<FileTransfer?> GetByIdAsync(FileId fileId, CancellationToken cancellationToken = default);
        Task<IEnumerable<FileTransfer>> GetByStatusAsync(Domain.Enumerations.TransferStatus status, CancellationToken cancellationToken = default);
        Task<IEnumerable<FileTransfer>> GetActiveTransfersAsync(CancellationToken cancellationToken = default);
        Task<FileTransfer> SaveAsync(FileTransfer fileTransfer, CancellationToken cancellationToken = default);
        Task DeleteAsync(FileId fileId, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(FileId fileId, CancellationToken cancellationToken = default);
        Task<IEnumerable<FileTransfer>> GetTransfersForNodeAsync(NodeId nodeId, CancellationToken cancellationToken = default);
        Task<long> GetTotalStorageUsedAsync(CancellationToken cancellationToken = default);
        Task CleanupCompletedTransfersAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);
    }
}
