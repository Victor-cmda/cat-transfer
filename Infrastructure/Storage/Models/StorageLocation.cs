namespace Infrastructure.Storage.Models
{
    public record StorageLocation(
        string BasePath,
        string RelativePath,
        string FullPath,
        StorageType Type,
        bool IsCompressed = false
    );

    public enum StorageType
    {
        FileSystem,
        MemoryMapped,
        InMemory,
        Compressed,
        Temporary
    }
}
