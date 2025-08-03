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
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(content.ToArray());
            return FromHash(hash);
        }

        public override string ToString() => value;
    }
}
