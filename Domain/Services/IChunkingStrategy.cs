using Domain.ValueObjects;

namespace Domain.Services
{
    public interface IChunkingStrategy
    {
        IEnumerable<ChunkId> CreateChunks(FileId id, ByteSize size, int chunkSize);
    }
}
