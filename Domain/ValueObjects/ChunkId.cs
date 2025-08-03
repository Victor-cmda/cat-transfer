namespace Domain.ValueObjects
{
    public readonly record struct ChunkId(FileId file, long offset)
    {
        public override string ToString()
        {
            return $"{file}:{offset}";
        }
    }
}
