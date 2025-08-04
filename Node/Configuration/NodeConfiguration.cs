using Domain.ValueObjects;

namespace Node.Configuration;

public class NodeConfiguration
{
    public NodeId NodeId { get; set; } = NodeId.NewGuid();
    public string NodeName { get; set; } = Environment.MachineName;
    public NetworkConfiguration Network { get; set; } = new();
    public StorageConfiguration Storage { get; set; } = new();
    public TransferConfiguration Transfer { get; set; } = new();
    public LoggingConfiguration Logging { get; set; } = new();
}

public class NetworkConfiguration
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
    public int RemotePort { get; set; } = 8081;
    public string[] SeedNodes { get; set; } = Array.Empty<string>();
    public int MaxConnections { get; set; } = 100;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(10);
}

public class StorageConfiguration
{
    public string DataDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CatTransfer");
    public string TempDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "CatTransfer");
    public long MaxFileSize { get; set; } = 10L * 1024 * 1024 * 1024;
    public int MaxConcurrentTransfers { get; set; } = 10;
}

public class TransferConfiguration
{
    public int DefaultChunkSize { get; set; } = 64 * 1024;
    public int MaxChunkSize { get; set; } = 1024 * 1024;
    public int MaxConcurrentChunks { get; set; } = 8;
    public TimeSpan TransferTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan ChunkTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; set; } = 3;
}

public class LoggingConfiguration
{
    public string LogLevel { get; set; } = "Information";
    public string LogDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CatTransfer", "Logs");
    public bool EnableFileLogging { get; set; } = true;
    public bool EnableConsoleLogging { get; set; } = true;
    public int MaxLogFiles { get; set; } = 30;
}
