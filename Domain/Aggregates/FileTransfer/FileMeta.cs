using Domain.ValueObjects;

namespace Domain.Aggregates.FileTransfer
{
    public sealed class FileMeta
    {
        public string Name { get; }
        public ByteSize Size { get; }
        public int ChunkSize { get; }
        public Checksum  Hash { get; }

        public FileMeta(string name, ByteSize size, int chunkSize, Checksum hash)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("File name cannot be null or empty.", nameof(name));
            }

            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be greater than zero.");
            }

            Name = name;
            Size = size;
            ChunkSize = chunkSize;
            Hash = hash;
        }
    }
}
