using System.Security.Cryptography;

namespace Domain.ValueObjects
{
    public readonly record struct FileId(string value)
    {
        public static FileId FromHash(ReadOnlySpan<byte> hash)
        {
            return new(Convert.ToHexString(hash));
        }

        public static FileId FromContent(ReadOnlySpan<byte> content)
        {
            Span<byte> hash = stackalloc byte[32];
            Shake256.HashData(content, hash);
            return FromHash(hash);
        }
    }
}
