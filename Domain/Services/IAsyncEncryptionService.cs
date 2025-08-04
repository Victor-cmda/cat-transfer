using Domain.ValueObjects;

namespace Domain.Services
{
    public interface IAsyncEncryptionService
    {
        Task<byte[]> EncryptAsync(byte[] data, byte[] key, CancellationToken cancellationToken = default);
        Task<byte[]> DecryptAsync(byte[] encryptedData, byte[] key, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateKeyAsync(int keySize = 256, CancellationToken cancellationToken = default);
        Task<bool> ValidateKeyAsync(byte[] key, CancellationToken cancellationToken = default);
    }
}
