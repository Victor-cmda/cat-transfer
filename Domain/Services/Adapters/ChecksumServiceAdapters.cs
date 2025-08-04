using Domain.Services;
using Domain.Enumerations;
using Domain.ValueObjects;

namespace Domain.Services.Adapters
{
    public class SyncToAsyncChecksumServiceAdapter : IChecksumService
    {
        private readonly IAsyncChecksumService _asyncService;

        public SyncToAsyncChecksumServiceAdapter(IAsyncChecksumService asyncService)
        {
            _asyncService = asyncService ?? throw new ArgumentNullException(nameof(asyncService));
        }

        public Checksum Compute(Stream source, ChecksumAlgorithm algorithm)
        {
            return _asyncService.ComputeAsync(source, algorithm).GetAwaiter().GetResult();
        }

        public bool Verify(Stream source, Checksum checksum)
        {
            return _asyncService.VerifyAsync(source, checksum).GetAwaiter().GetResult();
        }
    }

    public class AsyncToSyncChecksumServiceAdapter : IAsyncChecksumService
    {
        private readonly IChecksumService _syncService;

        public AsyncToSyncChecksumServiceAdapter(IChecksumService syncService)
        {
            _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        }

        public Task<Checksum> ComputeAsync(Stream source, ChecksumAlgorithm algorithm, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_syncService.Compute(source, algorithm));
        }

        public Task<bool> VerifyAsync(Stream source, Checksum checksum, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_syncService.Verify(source, checksum));
        }

        public Task<Checksum> ComputeAsync(byte[] data, ChecksumAlgorithm algorithm, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream(data);
            return Task.FromResult(_syncService.Compute(stream, algorithm));
        }

        public Task<bool> VerifyAsync(byte[] data, Checksum checksum, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream(data);
            return Task.FromResult(_syncService.Verify(stream, checksum));
        }
    }
}
