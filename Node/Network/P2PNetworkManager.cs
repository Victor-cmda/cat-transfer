using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Node.Configuration;
using Node.Services;
using System.Net;
using System.Net.Sockets;

namespace Node.Network;

public interface IP2PNetworkManager
{
    Task StartAsync(NetworkConfiguration config);
    Task StopAsync();
    Task ConnectToPeerAsync(string address, int port);
    Task DisconnectFromPeerAsync(NodeId peerId);
    Task<IEnumerable<PeerInfo>> GetConnectedPeersAsync();
    Task BroadcastMessageAsync(object message);
    Task SendMessageToPeerAsync(NodeId peerId, object message);
}

public class P2PNetworkManager : IP2PNetworkManager
{
    private readonly ILogger<P2PNetworkManager> _logger;
    private readonly Dictionary<NodeId, PeerConnection> _connectedPeers;
    private NetworkConfiguration? _config;
    private TcpListener? _listener;
    private bool _isStarted;

    public P2PNetworkManager(ILogger<P2PNetworkManager> logger)
    {
        _logger = logger;
        _connectedPeers = new Dictionary<NodeId, PeerConnection>();
    }

    public async Task StartAsync(NetworkConfiguration config)
    {
        if (_isStarted)
        {
            _logger.LogWarning("Gerenciador de rede já está iniciado");
            return;
        }

        _config = config;

        try
        {
            _logger.LogInformation("Iniciando gerenciador de rede P2P em {Host}:{Port}", 
                config.Host, config.Port);

            await StartListeningAsync(config);

            _isStarted = true;
            _logger.LogInformation("Gerenciador de rede P2P iniciado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar gerenciador de rede P2P");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!_isStarted)
        {
            _logger.LogWarning("Gerenciador de rede não está iniciado");
            return;
        }

        try
        {
            _logger.LogInformation("Parando gerenciador de rede P2P");

            var disconnectTasks = _connectedPeers.Keys
                .Select(DisconnectFromPeerAsync)
                .ToArray();

            await Task.WhenAll(disconnectTasks);

            _listener?.Stop();
            _listener = null;

            _isStarted = false;
            _logger.LogInformation("Gerenciador de rede P2P parado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parar gerenciador de rede P2P");
            throw;
        }
    }

    public async Task ConnectToPeerAsync(string address, int port)
    {
        if (!_isStarted)
            throw new InvalidOperationException("Gerenciador de rede não está iniciado");

        try
        {
            _logger.LogInformation("Conectando ao peer {Address}:{Port}", address, port);

            var existingPeer = _connectedPeers.Values
                .FirstOrDefault(p => p.Address == address && p.Port == port);

            if (existingPeer != null)
            {
                _logger.LogWarning("Já conectado ao peer {Address}:{Port}", address, port);
                return;
            }

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(address, port);

            var nodeId = NodeId.NewGuid();
            var peerConnection = new PeerConnection
            {
                NodeId = nodeId,
                Address = address,
                Port = port,
                TcpClient = tcpClient,
                ConnectedAt = DateTime.UtcNow,
                IsConnected = true
            };

            _connectedPeers[nodeId] = peerConnection;

            _ = Task.Run(() => ProcessPeerMessagesAsync(peerConnection));

            _logger.LogInformation("Conectado ao peer {Address}:{Port} com NodeId {NodeId}", 
                address, port, nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao conectar ao peer {Address}:{Port}", address, port);
            throw;
        }
    }

    public Task DisconnectFromPeerAsync(NodeId peerId)
    {
        if (!_isStarted)
            throw new InvalidOperationException("Gerenciador de rede não está iniciado");

        if (!_connectedPeers.TryGetValue(peerId, out var peerConnection))
        {
            _logger.LogWarning("Peer {PeerId} não encontrado", peerId);
            return Task.CompletedTask;
        }

        try
        {
            _logger.LogInformation("Desconectando do peer {PeerId}", peerId);

            peerConnection.IsConnected = false;
            peerConnection.TcpClient?.Close();
            peerConnection.TcpClient?.Dispose();

            _connectedPeers.Remove(peerId);

            _logger.LogInformation("Desconectado do peer {PeerId} com sucesso", peerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desconectar do peer {PeerId}", peerId);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<PeerInfo>> GetConnectedPeersAsync()
    {
        var peerInfos = _connectedPeers.Values
            .Where(p => p.IsConnected)
            .Select(p => new PeerInfo
            {
                NodeId = p.NodeId,
                Address = p.Address,
                Port = p.Port,
                ConnectedAt = p.ConnectedAt,
                IsConnected = p.IsConnected
            });

        return Task.FromResult(peerInfos);
    }

    public async Task BroadcastMessageAsync(object message)
    {
        if (!_isStarted)
            throw new InvalidOperationException("Gerenciador de rede não está iniciado");

        var connectedPeers = _connectedPeers.Values
            .Where(p => p.IsConnected)
            .ToArray();

        if (connectedPeers.Length == 0)
        {
            _logger.LogWarning("Nenhum peer conectado para broadcast");
            return;
        }

        _logger.LogDebug("Fazendo broadcast da mensagem para {Count} peers", connectedPeers.Length);

        var broadcastTasks = connectedPeers
            .Select(peer => SendMessageToPeerAsync(peer.NodeId, message));

        await Task.WhenAll(broadcastTasks);
    }

    public async Task SendMessageToPeerAsync(NodeId peerId, object message)
    {
        if (!_isStarted)
            throw new InvalidOperationException("Gerenciador de rede não está iniciado");

        if (!_connectedPeers.TryGetValue(peerId, out var peerConnection) || !peerConnection.IsConnected)
        {
            _logger.LogWarning("Peer {PeerId} não está conectado", peerId);
            return;
        }

        try
        {
            _logger.LogDebug("Enviando mensagem para peer {PeerId}: {MessageType}", 
                peerId, message.GetType().Name);

            await Task.Delay(1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar mensagem para peer {PeerId}", peerId);
            throw;
        }
    }

    private Task StartListeningAsync(NetworkConfiguration config)
    {
        _listener = new TcpListener(IPAddress.Parse(config.Host), config.Port);
        _listener.Start();

        _logger.LogInformation("Servidor TCP iniciado em {Host}:{Port}", config.Host, config.Port);

        _ = Task.Run(async () =>
        {
            while (_isStarted && _listener != null)
            {
                try
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    var endpoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                    
                    if (endpoint != null)
                    {
                        _logger.LogInformation("Nova conexão aceita de {Address}:{Port}", 
                            endpoint.Address, endpoint.Port);

                        _ = Task.Run(() => HandleNewConnectionAsync(tcpClient, endpoint));
                    }
                }
                catch (Exception ex) when (_isStarted)
                {
                    _logger.LogError(ex, "Erro ao aceitar conexão TCP");
                }
            }
        });

        return Task.CompletedTask;
    }

    private async Task HandleNewConnectionAsync(TcpClient tcpClient, IPEndPoint endpoint)
    {
        try
        {
            var nodeId = NodeId.NewGuid();
            var peerConnection = new PeerConnection
            {
                NodeId = nodeId,
                Address = endpoint.Address.ToString(),
                Port = endpoint.Port,
                TcpClient = tcpClient,
                ConnectedAt = DateTime.UtcNow,
                IsConnected = true
            };

            _connectedPeers[nodeId] = peerConnection;

            _logger.LogInformation("Peer {NodeId} conectado de {Address}:{Port}", 
                nodeId, endpoint.Address, endpoint.Port);

            await ProcessPeerMessagesAsync(peerConnection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar nova conexão de {Address}:{Port}", 
                endpoint.Address, endpoint.Port);
            tcpClient.Close();
        }
    }

    private async Task ProcessPeerMessagesAsync(PeerConnection peerConnection)
    {
        try
        {
            var stream = peerConnection.TcpClient?.GetStream();
            if (stream == null) return;

            var buffer = new byte[4096];

            while (peerConnection.IsConnected)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    break;
                }

                _logger.LogDebug("Mensagem recebida do peer {NodeId}: {Bytes} bytes", 
                    peerConnection.NodeId, bytesRead);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagens do peer {NodeId}", 
                peerConnection.NodeId);
        }
        finally
        {
            await DisconnectFromPeerAsync(peerConnection.NodeId);
        }
    }
}

internal class PeerConnection
{
    public NodeId NodeId { get; set; } = NodeId.NewGuid();
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public TcpClient? TcpClient { get; set; }
    public DateTime ConnectedAt { get; set; }
    public bool IsConnected { get; set; }
}
