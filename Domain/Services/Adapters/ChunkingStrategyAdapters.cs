using Domain.Services;
using Domain.ValueObjects;

namespace Domain.Services.Adapters
{
    public class SyncToAsyncChunkingStrategyAdapter : IChunkingStrategy
    {
        private readonly IAsyncChunkingStrategy _asyncStrategy;

        public SyncToAsyncChunkingStrategyAdapter(IAsyncChunkingStrategy asyncStrategy)
        {
            _asyncStrategy = asyncStrategy ?? throw new ArgumentNullException(nameof(asyncStrategy));
        }

        public IEnumerable<ChunkId> CreateChunks(FileId id, ByteSize size, int chunkSize)
        {
            using var stream = new MemoryStream(new byte[size.bytes]);
            var byteSize = new ByteSize(chunkSize);
            return _asyncStrategy.CreateChunksAsync(stream, byteSize).GetAwaiter().GetResult();
        }
    }

    public class AsyncToSyncChunkingStrategyAdapter : IAsyncChunkingStrategy
    {
        private readonly IChunkingStrategy _syncStrategy;

        public AsyncToSyncChunkingStrategyAdapter(IChunkingStrategy syncStrategy)
        {
            _syncStrategy = syncStrategy ?? throw new ArgumentNullException(nameof(syncStrategy));
        }

        public Task<IEnumerable<ChunkId>> CreateChunksAsync(Stream source, ByteSize chunkSize, CancellationToken cancellationToken = default)
        {
            var fileId = new FileId(Guid.NewGuid().ToString());
            var size = new ByteSize(source.Length);
            return Task.FromResult(_syncStrategy.CreateChunks(fileId, size, (int)chunkSize.bytes));
        }

        public Task<byte[]> ReadChunkAsync(Stream source, ChunkId chunkId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Original IChunkingStrategy doesn't support ReadChunk operation");
        }

        public Task WriteChunkAsync(Stream destination, ChunkId chunkId, byte[] data, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Original IChunkingStrategy doesn't support WriteChunk operation");
        }

        public Task<bool> ValidateChunkAsync(Stream source, ChunkId chunkId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Original IChunkingStrategy doesn't support ValidateChunk operation");
        }
    }
}
