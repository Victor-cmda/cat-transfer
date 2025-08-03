using Domain.ValueObjects;

namespace Domain.Services
{
    public sealed class DefaultChunkingStrategy : IChunkingStrategy
    {
        public IEnumerable<ChunkId> CreateChunks(FileId id, ByteSize size, int chunkSize)
        {
            var total = (long)Math.Ceiling(size.bytes / (double)chunkSize);
            for (long i = 0; i < total; i++)
            {
                yield return new ChunkId(id, i * chunkSize);
            }
        }
    }
}
