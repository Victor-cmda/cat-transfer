using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Node.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Application.Actors;
using Application.Messages;
using Domain.Aggregates.FileTransfer;
using Infrastructure.Storage.Interfaces;
using Node.Services;

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
    private readonly IChunkStorage _chunkStorage;
    private readonly ApplicationActorSystem _actorSystem;
    private readonly NodeConfiguration _nodeConfig;
    private readonly JsonSerializerOptions _jsonOptions;
    private NetworkConfiguration? _config;
    private TcpListener? _listener;
    private bool _isStarted;

    public P2PNetworkManager(ILogger<P2PNetworkManager> logger,
        IChunkStorage chunkStorage,
        ApplicationActorSystem actorSystem,
        NodeConfiguration nodeConfig)
    {
        _logger = logger;
        _connectedPeers = new Dictionary<NodeId, PeerConnection>();
        _chunkStorage = chunkStorage;
        _actorSystem = actorSystem;
        _nodeConfig = nodeConfig;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
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

            _isStarted = true;
            await StartListeningAsync(config);
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

            _ = Task.Run(async () =>
            {
                await SendHelloAsync(peerConnection);
                await ProcessPeerMessagesAsync(peerConnection);
            });

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

        var peerConnection = _connectedPeers.Values.FirstOrDefault(p => p.RemoteNodeId == peerId && p.IsConnected);
        if (peerConnection == null)
        {
            _connectedPeers.TryGetValue(peerId, out peerConnection);
            if (peerConnection == null || !peerConnection.IsConnected)
            {
                _logger.LogWarning("Peer {PeerId} não está conectado", peerId);
                return;
            }
        }

        try
        {
            _logger.LogDebug("Enviando mensagem para peer {PeerId}: {MessageType}",
                peerId, message.GetType().Name);

            if (peerConnection.TcpClient?.Connected != true)
            {
                _logger.LogWarning("Conexão TCP com {PeerId} não está ativa", peerId);
                return;
            }

            // atualmente suportamos apenas mensagens JSON de envelope simples
            await SendRawJsonAsync(peerConnection, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar mensagem para peer {PeerId}", peerId);
            throw;
        }
    }

    private Task StartListeningAsync(NetworkConfiguration config)
    {
        IPAddress ipAddress;
        if (config.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            config.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            ipAddress = IPAddress.Loopback;
        }
        else if (config.Host.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase))
        {
            ipAddress = IPAddress.Any;
        }
        else if (!IPAddress.TryParse(config.Host, out ipAddress!))
        {
            try
            {
                var hostEntry = Dns.GetHostEntry(config.Host);
                ipAddress = hostEntry.AddressList.FirstOrDefault(addr => addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                           ?? hostEntry.AddressList.FirstOrDefault()
                           ?? IPAddress.Loopback;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao resolver hostname {Host}, usando loopback", config.Host);
                ipAddress = IPAddress.Loopback;
            }
        }

        _listener = new TcpListener(ipAddress, config.Port);
        _listener.Start();

        _logger.LogInformation("Servidor TCP iniciado em {Host}:{Port} (resolvido para {ResolvedIP})", 
            config.Host, config.Port, ipAddress);

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

            await SendHelloAsync(peerConnection);
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

            while (peerConnection.IsConnected)
            {
                // protocolo simples: 4 bytes de tamanho + JSON UTF8
                var lengthBuf = new byte[4];
                var read = await ReadExactAsync(stream, lengthBuf, 0, 4);
                if (read == 0) break;
                var size = BitConverter.ToInt32(lengthBuf, 0);
                if (size <= 0 || size > 50_000_000) // 50MB de limite
                {
                    _logger.LogWarning("Tamanho de mensagem inválido: {Size}", size);
                    break;
                }
                var payload = new byte[size];
                var got = await ReadExactAsync(stream, payload, 0, size);
                if (got == 0) break;

                var json = Encoding.UTF8.GetString(payload);
                await HandleIncomingJsonAsync(peerConnection, json);
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

    private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = await stream.ReadAsync(buffer, offset + total, count - total);
            if (read == 0) return 0;
            total += read;
        }
        return total;
    }

    private async Task HandleIncomingJsonAsync(PeerConnection peer, string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("type", out var typeProp))
            {
                _logger.LogWarning("Mensagem sem tipo recebida de {Peer}", peer.NodeId);
                return;
            }
            var type = typeProp.GetString();
            switch (type)
            {
                case "hello":
                    var hello = JsonSerializer.Deserialize<HelloEnvelope>(json, _jsonOptions);
                    if (hello != null)
                    {
                        peer.RemoteNodeId = new NodeId(hello.NodeId);
                        _logger.LogInformation("Handshake recebido: conexão {Local} ↔ remoto {Remote}", peer.NodeId, peer.RemoteNodeId);
                    }
                    break;
                case "file_init":
                    var init = JsonSerializer.Deserialize<FileInitEnvelope>(json, _jsonOptions);
                    if (init == null) return;
                    await HandleFileInitAsync(peer, init);
                    break;
                case "file_chunk":
                    var envelope = JsonSerializer.Deserialize<FileChunkEnvelope>(json, _jsonOptions);
                    if (envelope == null) return;
                    await HandleFileChunkAsync(peer, envelope);
                    break;
                default:
                    _logger.LogInformation("Mensagem desconhecida de {Peer}: {Type}", peer.NodeId, type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar JSON de {Peer}", peer.NodeId);
        }
    }

    private Task SendHelloAsync(PeerConnection peer)
    {
        try
        {
            var hello = new HelloEnvelope("hello", _nodeConfig.NodeId.ToString());
            return SendRawJsonAsync(peer, hello);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar handshake para {Peer}", peer.NodeId);
            return Task.CompletedTask;
        }
    }

    private async Task SendRawJsonAsync(PeerConnection peer, object message)
    {
        if (peer.TcpClient?.Connected != true) return;
        var stream = peer.TcpClient.GetStream();
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var data = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(data.Length);
        await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await stream.WriteAsync(data, 0, data.Length);
        await stream.FlushAsync();
    }

    private async Task HandleFileChunkAsync(PeerConnection peer, FileChunkEnvelope env)
    {
        var chunkId = new ChunkId(new FileId(env.FileId), env.Offset);
        try
        {
            await _chunkStorage.StoreChunkAsync(chunkId, env.Data);

            // encaminha para os atores para atualizar progresso
            _actorSystem.FileTransferSupervisor.Tell(new StoreChunkCommand(chunkId, env.Data, new NodeId(env.SourceNodeId)), Akka.Actor.ActorRefs.NoSender);
            _logger.LogDebug("Chunk {Chunk} armazenado de {Peer}", chunkId, peer.NodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao armazenar chunk {Chunk} de {Peer}", chunkId, peer.NodeId);
        }
    }

    private async Task HandleFileInitAsync(PeerConnection peer, FileInitEnvelope env)
    {
        try
        {
            var fileId = new FileId(env.FileId);
            var meta = new FileMeta(
                name: env.FileName,
                size: new ByteSize(env.FileSize),
                chunkSize: env.ChunkSize,
                hash: new Checksum(env.Checksum, env.ChecksumAlgorithm)
            );

            _actorSystem.FileTransferSupervisor.Tell(
                new StartFileTransferCommand(fileId, meta, new NodeId(env.SourceNodeId)),
                Akka.Actor.ActorRefs.NoSender);

            _logger.LogInformation("file_init recebido de {Remote}. Transferência {FileId} iniciada com {Name} ({Size} bytes)",
                peer.RemoteNodeId ?? peer.NodeId, env.FileId, env.FileName, env.FileSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar file_init para {FileId} de {Peer}", env.FileId, peer.NodeId);
        }
        await Task.CompletedTask;
    }
}

internal class PeerConnection
{
    public NodeId NodeId { get; set; } = NodeId.NewGuid();
    public NodeId? RemoteNodeId { get; set; }
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public TcpClient? TcpClient { get; set; }
    public DateTime ConnectedAt { get; set; }
    public bool IsConnected { get; set; }
}

internal record HelloEnvelope(string Type, string NodeId);
internal record FileInitEnvelope(
    string Type,
    string FileId,
    string FileName,
    long FileSize,
    int ChunkSize,
    string SourceNodeId,
    byte[] Checksum,
    Domain.Enumerations.ChecksumAlgorithm ChecksumAlgorithm
);
