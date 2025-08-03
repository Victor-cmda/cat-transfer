using Domain.Enumerations;
using Domain.ValueObjects;

namespace Domain.Services
{
    public interface IChecksumService
    {
        Checksum Compute(Stream source, ChecksumAlgorithm algorithm);
        bool Verify(Stream source, Checksum checksum);
    }
}
