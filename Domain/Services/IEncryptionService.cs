using Domain.ValueObjects;

namespace Domain.Services
{
    public interface IEncryptionService
    {
        EncryptedChunk Encrypt(byte[] data, EncryptionKey key);
        byte[] Decrypt(EncryptedChunk chunk, EncryptionKey key);
        EncryptionKey GenerateKey(string algorithm = "AES-256-GCM");
        bool VerifyIntegrity(EncryptedChunk chunk, EncryptionKey key);
    }
}
