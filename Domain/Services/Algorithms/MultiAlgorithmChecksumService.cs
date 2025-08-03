using Domain.Enumerations;
using Domain.ValueObjects;
using System.Security.Cryptography;

namespace Domain.Services
{
    public sealed class MultiAlgorithmChecksumService : IChecksumService
    {
        public Checksum Compute(Stream source, ChecksumAlgorithm algorithm)
        {
            using HashAlgorithm? alg = algorithm switch
            {
                ChecksumAlgorithm.Sha256 => SHA256.Create(),
                ChecksumAlgorithm.Sha512 => SHA512.Create(),
                _ => null                
            };

            if (alg is null)
                throw new NotSupportedException($"{algorithm} requer implementação externa.");

            var hash = alg.ComputeHash(source);
            return new Checksum(hash, algorithm);
        }

        public bool Verify(Stream source, Checksum checksum) =>
            Compute(source, checksum.algorithm).value.SequenceEqual(checksum.value);
    }
}
