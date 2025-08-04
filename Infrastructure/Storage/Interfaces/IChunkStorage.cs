using Domain.ValueObjects;

namespace Infrastructure.Storage.Interfaces
{
    public interface IChunkStorage
    {
        Task<byte[]?> GetChunkAsync(ChunkId chunkId, CancellationToken cancellationToken = default);
        Task StoreChunkAsync(ChunkId chunkId, byte[] data, CancellationToken cancellationToken = default);
        Task<bool> HasChunkAsync(ChunkId chunkId, CancellationToken cancellationToken = default);
        Task DeleteChunkAsync(ChunkId chunkId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ChunkId>> GetChunksForFileAsync(FileId fileId, CancellationToken cancellationToken = default);
        Task<long> GetChunkSizeAsync(ChunkId chunkId, CancellationToken cancellationToken = default);
        Task<Stream> GetChunkStreamAsync(ChunkId chunkId, CancellationToken cancellationToken = default);
        Task StoreChunkStreamAsync(ChunkId chunkId, Stream dataStream, CancellationToken cancellationToken = default);
        Task CompressChunkAsync(ChunkId chunkId, CancellationToken cancellationToken = default);
        Task DecompressChunkAsync(ChunkId chunkId, CancellationToken cancellationToken = default);
        Task<long> GetTotalChunkSizeAsync(CancellationToken cancellationToken = default);
        Task CleanupOrphanedChunksAsync(CancellationToken cancellationToken = default);
    }
}
