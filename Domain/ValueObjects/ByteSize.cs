namespace Domain.ValueObjects
{
    public readonly record struct ByteSize(long bytes)
    {
        public double KB => bytes / 1024d;
        public double MB => KB / 1024d;
        public double GB => MB / 1024d;

        public static ByteSize FromKilobytes(double kb)
        {
            return new((long)(kb * 1024));
        }

        public static ByteSize FromMegaBytes(double mb)
        {
            return new((long)(mb * 1024 * 1024));
        }

        public static ByteSize FromGigaBytes(double gb)
        {
            return new((long)(gb * 1024 * 1024 * 1024));
        }

        public override string ToString() =>
            bytes switch
            {
                < 1_048_576 => $"{KB:F2} KB",
                < 1_073_741_824 => $"{MB:F2} MB",
                _ => $"{GB:F2} GB"
            };


    }
}
