using Application.Services;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Node.Configuration;
using Node.Network;

namespace Node.Services;

public interface INodeService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<bool> IsRunningAsync();
    Task<NodeStatus> GetStatusAsync();
    Task ConnectToPeerAsync(string address, int port);
    Task DisconnectFromPeerAsync(NodeId peerId);
    Task<IEnumerable<PeerInfo>> GetConnectedPeersAsync();
}

public class NodeService : INodeService
{
    private readonly ILogger<NodeService> _logger;
    private readonly NodeConfiguration _configuration;
    private readonly IP2PNetworkManager _networkManager;
    private readonly IFileTransferService _fileTransferService;
    private bool _isRunning;

    public NodeService(
        ILogger<NodeService> logger,
        NodeConfiguration configuration,
        IP2PNetworkManager networkManager,
        IFileTransferService fileTransferService)
    {
        _logger = logger;
        _configuration = configuration;
        _networkManager = networkManager;
        _fileTransferService = fileTransferService;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("Node já está em execução");
            return;
        }

        try
        {
            _logger.LogInformation("Iniciando nó P2P {NodeName} ({NodeId})", 
                _configuration.NodeName, _configuration.NodeId);

            await CreateDirectoriesAsync();

            _logger.LogInformation("Sistema de atores da aplicação iniciado");

            await _networkManager.StartAsync(_configuration.Network);
            _logger.LogInformation("Gerenciador de rede P2P iniciado na porta {Port}", 
                _configuration.Network.Port);

            await ConnectToSeedNodesAsync();

            _isRunning = true;
            _logger.LogInformation("Nó P2P {NodeName} iniciado com sucesso", _configuration.NodeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar nó P2P");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            _logger.LogWarning("Node não está em execução");
            return;
        }

        try
        {
            _logger.LogInformation("Parando nó P2P {NodeName}", _configuration.NodeName);

            await _networkManager.StopAsync();
            _logger.LogInformation("Gerenciador de rede P2P parado");

            _isRunning = false;
            _logger.LogInformation("Nó P2P {NodeName} parado com sucesso", _configuration.NodeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parar nó P2P");
            throw;
        }
    }

    public Task<bool> IsRunningAsync()
    {
        return Task.FromResult(_isRunning);
    }

    public async Task<NodeStatus> GetStatusAsync()
    {
        var peers = await GetConnectedPeersAsync();

        return new NodeStatus
        {
            NodeId = _configuration.NodeId,
            NodeName = _configuration.NodeName,
            IsRunning = _isRunning,
            ConnectedPeers = peers.Count(),
            ActiveTransfers = 0,
            TotalBytesTransferred = 0,
            Uptime = TimeSpan.Zero
        };
    }

    public async Task ConnectToPeerAsync(string address, int port)
    {
        if (!_isRunning)
            throw new InvalidOperationException("Node não está em execução");

        try
        {
            _logger.LogInformation("Conectando ao peer {Address}:{Port}", address, port);
            await _networkManager.ConnectToPeerAsync(address, port);
            _logger.LogInformation("Conectado ao peer {Address}:{Port} com sucesso", address, port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao conectar ao peer {Address}:{Port}", address, port);
            throw;
        }
    }

    public async Task DisconnectFromPeerAsync(NodeId peerId)
    {
        if (!_isRunning)
            throw new InvalidOperationException("Node não está em execução");

        try
        {
            _logger.LogInformation("Desconectando do peer {PeerId}", peerId);
            await _networkManager.DisconnectFromPeerAsync(peerId);
            _logger.LogInformation("Desconectado do peer {PeerId} com sucesso", peerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desconectar do peer {PeerId}", peerId);
            throw;
        }
    }

    public async Task<IEnumerable<PeerInfo>> GetConnectedPeersAsync()
    {
        if (!_isRunning)
            return Array.Empty<PeerInfo>();

        return await _networkManager.GetConnectedPeersAsync();
    }

    private async Task CreateDirectoriesAsync()
    {
        var directories = new[]
        {
            _configuration.Storage.DataDirectory,
            _configuration.Storage.TempDirectory,
            _configuration.Logging.LogDirectory
        };

        await Task.Run(() =>
        {
            foreach (var directory in directories)
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogDebug("Diretório criado: {Directory}", directory);
                }
            }
        });
    }

    private async Task ConnectToSeedNodesAsync()
    {
        if (_configuration.Network.SeedNodes?.Length > 0)
        {
            _logger.LogInformation("Conectando a {Count} nós seed", _configuration.Network.SeedNodes.Length);

            var connectTasks = _configuration.Network.SeedNodes
                .Select(async seedNode =>
                {
                    try
                    {
                        var parts = seedNode.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[1], out var port))
                        {
                            await ConnectToPeerAsync(parts[0], port);
                        }
                        else
                        {
                            _logger.LogWarning("Formato inválido para nó seed: {SeedNode}", seedNode);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao conectar ao nó seed: {SeedNode}", seedNode);
                    }
                });

            await Task.WhenAll(connectTasks);
        }
    }
}

public class NodeStatus
{
    public NodeId NodeId { get; set; } = NodeId.NewGuid();
    public string NodeName { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public int ConnectedPeers { get; set; }
    public int ActiveTransfers { get; set; }
    public long TotalBytesTransferred { get; set; }
    public TimeSpan Uptime { get; set; }
}

public class PeerInfo
{
    public NodeId NodeId { get; set; } = NodeId.NewGuid();
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public DateTime ConnectedAt { get; set; }
    public bool IsConnected { get; set; }
}
