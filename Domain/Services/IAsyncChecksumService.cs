using Domain.Enumerations;
using Domain.ValueObjects;

namespace Domain.Services
{
    public interface IAsyncChecksumService
    {
        Task<Checksum> ComputeAsync(Stream source, ChecksumAlgorithm algorithm, CancellationToken cancellationToken = default);
        Task<bool> VerifyAsync(Stream source, Checksum checksum, CancellationToken cancellationToken = default);
        Task<Checksum> ComputeAsync(byte[] data, ChecksumAlgorithm algorithm, CancellationToken cancellationToken = default);
        Task<bool> VerifyAsync(byte[] data, Checksum checksum, CancellationToken cancellationToken = default);
    }
}
