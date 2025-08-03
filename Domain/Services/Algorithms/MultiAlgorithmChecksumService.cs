using Domain.Enumerations;
using Domain.ValueObjects;
using System.Security.Cryptography;

namespace Domain.Services
{
    public sealed class MultiAlgorithmChecksumService : IChecksumService
    {
        public Checksum Compute(Stream source, ChecksumAlgorithm algorithm)
        {
            source.Position = 0;

            var hash = algorithm switch
            {
                ChecksumAlgorithm.Sha256 => ComputeSha256(source),
                ChecksumAlgorithm.Sha512 => ComputeSha512(source),
                ChecksumAlgorithm.Shake256 => ComputeShake256(source),
                ChecksumAlgorithm.Blake3 => ComputeBlake3(source),
                _ => throw new NotSupportedException($"Algorithm {algorithm} is not supported.")
            };

            return new Checksum(hash, algorithm);
        }

        public bool Verify(Stream source, Checksum checksum) =>
            Compute(source, checksum.algorithm).value.SequenceEqual(checksum.value);

        private static byte[] ComputeSha256(Stream source)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(source);
        }

        private static byte[] ComputeSha512(Stream source)
        {
            using var sha512 = SHA512.Create();
            return sha512.ComputeHash(source);
        }

        private static byte[] ComputeShake256(Stream source)
        {
            var buffer = new byte[4096];
            var output = new byte[32];
            
            using var shake = new Shake256();
            int bytesRead;
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                shake.AppendData(buffer.AsSpan(0, bytesRead));
            }
            
            shake.GetHashAndReset(output);
            return output;
        }

        //TODO
        private static byte[] ComputeBlake3(Stream source)
        {
            throw new NotImplementedException("BLAKE3 requires external library implementation");
        }
    }

    // TODO
    internal class Shake256 : IDisposable
    {
        private readonly List<byte> _data = new();

        public void AppendData(ReadOnlySpan<byte> data)
        {
            _data.AddRange(data.ToArray());
        }

        public void GetHashAndReset(Span<byte> output)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(_data.ToArray());
            
            if (output.Length <= hash.Length)
            {
                hash.AsSpan(0, output.Length).CopyTo(output);
            }
            else
            {
                hash.CopyTo(output);
            }
            
            _data.Clear();
        }

        public void Dispose()
        {
            _data.Clear();
        }
    }
}
