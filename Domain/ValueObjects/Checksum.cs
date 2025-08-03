using Domain.Enumerations;

namespace Domain.ValueObjects
{
    public readonly record struct Checksum(byte[] value, ChecksumAlgorithm algorithm)
    {
        public string Hex()
        {
            return Convert.ToHexString(value);
        }

        public override string ToString()
        {
            return $"{algorithm}:{Hex()}";
        }
    }
}
