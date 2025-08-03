namespace Domain.ValueObjects
{
    public readonly record struct EncryptionKey(byte[] key, string algorithm)
    {
        public static EncryptionKey Create(byte[] key, string algorithm)
        {
            if (key == null || key.Length == 0)
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            
            if (string.IsNullOrWhiteSpace(algorithm))
                throw new ArgumentException("Algorithm cannot be null or empty.", nameof(algorithm));

            return new EncryptionKey((byte[])key.Clone(), algorithm);
        }

        public string ToBase64() => Convert.ToBase64String(key);

        public static EncryptionKey FromBase64(string base64Key, string algorithm)
        {
            var keyBytes = Convert.FromBase64String(base64Key);
            return Create(keyBytes, algorithm);
        }

        public static EncryptionKey Generate(string algorithm, int keySize = 32)
        {
            var keyArray = new byte[keySize];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(keyArray);
            return Create(keyArray, algorithm);
        }

        public override string ToString() => $"{algorithm}:{ToBase64()[..8]}...";
    }
}
