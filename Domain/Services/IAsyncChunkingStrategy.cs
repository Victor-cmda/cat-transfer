using Domain.ValueObjects;

namespace Domain.Services
{
    public interface IAsyncChunkingStrategy
    {
        Task<IEnumerable<ChunkId>> CreateChunksAsync(Stream source, ByteSize chunkSize, CancellationToken cancellationToken = default);
        Task<byte[]> ReadChunkAsync(Stream source, ChunkId chunkId, CancellationToken cancellationToken = default);
        Task WriteChunkAsync(Stream destination, ChunkId chunkId, byte[] data, CancellationToken cancellationToken = default);
        Task<bool> ValidateChunkAsync(Stream source, ChunkId chunkId, CancellationToken cancellationToken = default);
    }
}
