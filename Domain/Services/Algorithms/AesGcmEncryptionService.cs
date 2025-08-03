using Domain.ValueObjects;
using System.Security.Cryptography;

namespace Domain.Services.Algorithms
{
    public sealed class AesGcmEncryptionService : IEncryptionService
    {
        private const int NonceSize = 12; 
        private const int TagSize = 16;   
        private const int KeySize = 32;   

        public EncryptedChunk Encrypt(byte[] data, EncryptionKey key)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty.", nameof(data));

            if (key.Algorithm != "AES-256-GCM")
                throw new ArgumentException($"Unsupported algorithm: {key.Algorithm}");

            var nonce = new byte[NonceSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(nonce);

            var ciphertext = new byte[data.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(key.Key, TagSize);
            aes.Encrypt(nonce, data, ciphertext, tag);

            var encryptedData = new byte[ciphertext.Length + tag.Length];
            Array.Copy(ciphertext, 0, encryptedData, 0, ciphertext.Length);
            Array.Copy(tag, 0, encryptedData, ciphertext.Length, tag.Length);

            return EncryptedChunk.Create(encryptedData, nonce);
        }

        public byte[] Decrypt(EncryptedChunk chunk, EncryptionKey key)
        {
            if (key.Algorithm != "AES-256-GCM")
                throw new ArgumentException($"Unsupported algorithm: {key.Algorithm}");

            if (chunk.data.Length < TagSize)
                throw new ArgumentException("Invalid encrypted chunk: too small for authentication tag.");

            var ciphertext = new byte[chunk.data.Length - TagSize];
            var tag = new byte[TagSize];
            
            Array.Copy(chunk.data, 0, ciphertext, 0, ciphertext.Length);
            Array.Copy(chunk.data, ciphertext.Length, tag, 0, TagSize);

            var plaintext = new byte[ciphertext.Length];
            using var aes = new AesGcm(key.Key, TagSize);
            
            try
            {
                aes.Decrypt(chunk.nonce, ciphertext, tag, plaintext);
                return plaintext;
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException("Decryption failed: authentication tag verification failed.");
            }
        }

        public EncryptionKey GenerateKey(string algorithm = "AES-256-GCM")
        {
            if (algorithm != "AES-256-GCM")
                throw new ArgumentException($"Unsupported algorithm: {algorithm}");

            return EncryptionKey.Generate(algorithm, KeySize);
        }

        public bool VerifyIntegrity(EncryptedChunk chunk, EncryptionKey key)
        {
            try
            {
                Decrypt(chunk, key);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
