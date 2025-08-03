using Domain.Enumerations;
using Domain.ValueObjects;

namespace Domain.Services
{
    public sealed class P2PChunkingStrategy : IChunkingStrategy
    {
        private readonly int _baseChunkSize;
        private readonly int _maxChunkSize;
        private readonly double _networkSpeedFactor;

        public P2PChunkingStrategy(int baseChunkSize = 64 * 1024, int maxChunkSize = 1024 * 1024, double networkSpeedFactor = 1.0)
        {
            _baseChunkSize = baseChunkSize;
            _maxChunkSize = maxChunkSize;
            _networkSpeedFactor = networkSpeedFactor;
        }

        public IEnumerable<ChunkId> CreateChunks(FileId fileId, ByteSize size, int chunkSize)
        {
            var adaptiveChunkSize = CalculateAdaptiveChunkSize(size, chunkSize);
            var totalChunks = (long)Math.Ceiling(size.bytes / (double)adaptiveChunkSize);
            
            for (long i = 0; i < totalChunks; i++)
            {
                var offset = i * adaptiveChunkSize;
                yield return new ChunkId(fileId, offset);
            }
        }

        public IEnumerable<ChunkId> CreatePrioritizedChunks(FileId fileId, ByteSize size, int chunkSize)
        {
            var chunks = CreateChunks(fileId, size, chunkSize).ToList();
            
            var firstChunks = chunks.Take(Math.Min(5, chunks.Count / 10));
            var lastChunks = chunks.TakeLast(Math.Min(5, chunks.Count / 10));
            var middleChunks = chunks.Skip(5).SkipLast(5);

            return firstChunks
                .Concat(lastChunks)
                .Concat(middleChunks);
        }

        private int CalculateAdaptiveChunkSize(ByteSize fileSize, int requestedChunkSize)
        {
            var adaptiveSize = requestedChunkSize;

            if (fileSize.GB > 1)
            {
                adaptiveSize = Math.Min(_maxChunkSize, requestedChunkSize * 2);
            }
            else if (fileSize.MB < 10)
            {
                adaptiveSize = Math.Max(_baseChunkSize, requestedChunkSize / 2);
            }

            adaptiveSize = (int)(adaptiveSize * _networkSpeedFactor);

            return Math.Max(_baseChunkSize, Math.Min(_maxChunkSize, adaptiveSize));
        }
    }
}
