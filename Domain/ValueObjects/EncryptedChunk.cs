namespace Domain.ValueObjects
{
    public readonly record struct EncryptedChunk(byte[] data, byte[] nonce)
    {
        public static EncryptedChunk Create(byte[] data, byte[] nonce)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty.", nameof(data));

            if (nonce == null || nonce.Length == 0)
                throw new ArgumentException("Nonce cannot be null or empty.", nameof(nonce));

            return new EncryptedChunk((byte[])data.Clone(), (byte[])nonce.Clone());
        }

        public int Size => data.Length;

        public string NonceHex => Convert.ToHexString(nonce);

        public override string ToString() => $"EncryptedChunk[{Size} bytes, nonce: {NonceHex[..8]}...]";
    }
}
