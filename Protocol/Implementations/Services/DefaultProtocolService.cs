using Protocol.Contracts;
using Protocol.Definitions;
using Protocol.Implementations.Transport;
using Protocol.Implementations.Handlers;
using Protocol.Serialization.Codecs;
using Domain.ValueObjects;
using System.Net;
using System.Collections.Concurrent;
using Protocol.Messages.Handshake;

namespace Protocol.Implementations.Services
{
    public class DefaultProtocolService : IProtocolService
    {
        private readonly NodeId _localNodeId;
        private readonly INetworkTransport _networkTransport;
        private readonly IMessageHandler _messageHandler;
        private readonly IMessageCodec _messageCodec;
        private readonly ConcurrentDictionary<string, PeerInfo> _peers;
        private readonly object _stateLock = new();
        private bool _disposed = false;
        private bool _isStarted = false;

        public DefaultProtocolService(
            NodeId localNodeId,
            IPEndPoint localEndpoint,
            IMessageCodec? messageCodec = null,
            IMessageHandler? messageHandler = null)
        {
            _localNodeId = localNodeId;
            _messageCodec = messageCodec ?? new MessageCodec();
            _messageHandler = messageHandler ?? new DefaultMessageHandler(localNodeId);
            _networkTransport = new TcpNetworkTransport(localEndpoint, localNodeId, _messageCodec);
            _peers = new ConcurrentDictionary<string, PeerInfo>();

            _networkTransport.MessageReceived += OnMessageReceived;
            _networkTransport.PeerConnected += OnPeerConnected;
            _networkTransport.PeerDisconnected += OnPeerDisconnected;
            _networkTransport.TransportError += OnTransportError;

            _messageHandler.MessageProcessed += OnMessageProcessed;
            _messageHandler.HandlerError += OnHandlerError;
        }

        public NodeId LocalNodeId => _localNodeId;

        public bool IsStarted => _isStarted;

        public IEnumerable<string> ConnectedPeers => _peers.Keys.ToList();

        public event EventHandler<Protocol.Contracts.PeerConnectedEventArgs>? PeerConnected;

        public event EventHandler<Protocol.Contracts.PeerDisconnectedEventArgs>? PeerDisconnected;

        public event EventHandler<Protocol.Contracts.MessageReceivedEventArgs>? MessageReceived;

        public event EventHandler<Protocol.Contracts.ErrorEventArgs>? Error;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_stateLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(DefaultProtocolService));

                if (_isStarted)
                    throw new InvalidOperationException("Protocol service is already started");

                _isStarted = true;
            }

            try
            {
                await _networkTransport.StartAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                {
                    _isStarted = false;
                }
                Error?.Invoke(this, new Protocol.Contracts.ErrorEventArgs(ex, "Failed to start protocol service"));
                throw;
            }
        }

        public async Task SendMessageAsync(NodeId peerId, IProtocolMessage message, CancellationToken cancellationToken = default)
        {
            try
            {
                await _networkTransport.SendAsync(peerId, message, cancellationToken);
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Protocol.Contracts.ErrorEventArgs(ex, $"Failed to send message to peer {peerId}"));
                throw;
            }
        }

        public async Task<bool> BroadcastMessageAsync(IBroadcastMessage message, CancellationToken cancellationToken = default)
        {
            try
            {
                await _networkTransport.BroadcastAsync(message, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Protocol.Contracts.ErrorEventArgs(ex, "Failed to broadcast message"));
                return false;
            }
        }

        public async Task BroadcastMessageAsync(IProtocolMessage message, CancellationToken cancellationToken = default)
        {
            if (message is IBroadcastMessage broadcastMessage)
            {
                await BroadcastMessageAsync(broadcastMessage, cancellationToken);
            }
            else
            {
                var tasks = _peers.Keys.Select(async peerIdStr =>
                {
                    try
                    {
                        var peerId = new NodeId(peerIdStr);
                        await _networkTransport.SendAsync(peerId, message, cancellationToken);
                    }
                    catch
                    {
                    }
                });
                await Task.WhenAll(tasks);
            }
        }

        public async Task ConnectToPeerAsync(string address, int port, CancellationToken cancellationToken = default)
        {
            var endpoint = $"tcp://{address}:{port}";
            await ConnectToPeerAsync(endpoint, cancellationToken);
        }

        public async Task DisconnectPeerAsync(NodeId peerId, CancellationToken cancellationToken = default)
        {
            await DisconnectFromPeerAsync(peerId, cancellationToken);
        }

        public IEnumerable<NodeId> GetConnectedPeers()
        {
            return _peers.Keys.Select(peerIdStr => new NodeId(peerIdStr));
        }

        public async Task ConnectToPeerAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            try
            {
                await _networkTransport.ConnectAsync(endpoint, cancellationToken);
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Protocol.Contracts.ErrorEventArgs(ex, $"Failed to connect to peer at {endpoint}"));
                throw;
            }
        }

        public async Task DisconnectFromPeerAsync(NodeId peerId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_peers.TryGetValue(peerId.ToString(), out var peerInfo))
                {
                    await _networkTransport.DisconnectAsync(peerInfo.Endpoint, cancellationToken);
                    _peers.TryRemove(peerId.ToString(), out _);
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Protocol.Contracts.ErrorEventArgs(ex, $"Failed to disconnect from peer {peerId}"));
                throw;
            }
        }

        public bool IsPeerConnected(NodeId peerId)
        {
            return _peers.ContainsKey(peerId.ToString()) && 
                   _networkTransport.IsPeerConnected(peerId);
        }

        public PeerInfo? GetPeerInfo(NodeId peerId)
        {
            return _peers.TryGetValue(peerId.ToString(), out var peerInfo) ? peerInfo : null;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            lock (_stateLock)
            {
                if (!_isStarted)
                    return;

                _isStarted = false;
            }

            try
            {
                await _networkTransport.StopAsync(cancellationToken);
                _peers.Clear();
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Protocol.Contracts.ErrorEventArgs(ex, "Failed to stop protocol service"));
                throw;
            }
        }

        public async Task InitiateHandshakeAsync(NodeId peerId, CancellationToken cancellationToken = default)
        {
            try
            {
                var handshakeRequest = new HandshakeRequestMessage(
                    _localNodeId,
                    peerId,
                    new[] { "file-transfer", "peer-discovery" },
                    new Dictionary<string, string>
                    {
                        ["client_version"] = ProtocolConstants.ProtocolVersion.ToString(),
                        ["client_type"] = "cat-transfer-node"
                    });

                await _networkTransport.SendAsync(peerId, handshakeRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Protocol.Contracts.ErrorEventArgs(ex, $"Failed to initiate handshake with peer: {peerId}"));
                throw;
            }
        }

        private async void OnMessageReceived(object? sender, Protocol.Contracts.MessageReceivedEventArgs e)
        {
            try
            {
                await _messageHandler.HandleMessageAsync(e.Message, e.SourcePeerId, "default", CancellationToken.None);
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new Protocol.Contracts.ErrorEventArgs(ex, $"Error processing message from peer: {e.SourcePeerId}"));
            }
        }

        private void OnPeerConnected(object? sender, Protocol.Contracts.PeerConnectedEventArgs e)
        {
            var peerInfo = new PeerInfo(
                e.PeerId,
                e.Endpoint,
                ProtocolConstants.ProtocolVersion.ToString(),
                DateTimeOffset.UtcNow,
                new Dictionary<string, string>());

            _peers.TryAdd(e.PeerId.ToString(), peerInfo);
            PeerConnected?.Invoke(this, e);
        }

        private void OnPeerDisconnected(object? sender, Protocol.Contracts.PeerDisconnectedEventArgs e)
        {
            _peers.TryRemove(e.PeerId.ToString(), out _);
            PeerDisconnected?.Invoke(this, e);
        }

        private void OnTransportError(object? sender, Protocol.Contracts.ErrorEventArgs e)
        {
            Error?.Invoke(this, e);
        }

        private void OnMessageProcessed(object? sender, Protocol.Contracts.MessageProcessedEventArgs e)
        {
        }

        private void OnHandlerError(object? sender, Protocol.Contracts.MessageHandlerErrorEventArgs e)
        {
            Error?.Invoke(this, new Protocol.Contracts.ErrorEventArgs(e.Error, $"Message handler error for peer: {e.SourceNodeId}"));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                if (_isStarted)
                {
                    StopAsync().GetAwaiter().GetResult();
                }
            }
            catch
            {
            }

            _networkTransport?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
