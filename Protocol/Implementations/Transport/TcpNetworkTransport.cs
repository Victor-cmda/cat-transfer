using Protocol.Contracts;
using Protocol.Definitions;
using Protocol.Exceptions;
using Domain.ValueObjects;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace Protocol.Implementations.Transport
{
    public class TcpNetworkTransport : INetworkTransport
    {
        private readonly IPEndPoint _localEndpoint;
        private readonly IMessageCodec _messageCodec;
        private readonly ConcurrentDictionary<string, TcpConnection> _connections;
        private TcpListener? _listener;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isStarted = false;
        private bool _disposed = false;

        public TcpNetworkTransport(IPEndPoint localEndpoint, NodeId localPeerId, IMessageCodec messageCodec)
        {
            _localEndpoint = localEndpoint ?? throw new ArgumentNullException(nameof(localEndpoint));
            LocalPeerId = localPeerId;
            _messageCodec = messageCodec ?? throw new ArgumentNullException(nameof(messageCodec));
            _connections = new ConcurrentDictionary<string, TcpConnection>();
        }

        public NodeId LocalPeerId { get; }

        public string LocalEndpoint => _localEndpoint.ToString();

        public event EventHandler<Protocol.Contracts.MessageReceivedEventArgs>? MessageReceived;

        public event EventHandler<Protocol.Contracts.PeerConnectedEventArgs>? PeerConnected;

        public event EventHandler<Protocol.Contracts.PeerDisconnectedEventArgs>? PeerDisconnected;

        public event EventHandler<TransportErrorEventArgs>? TransportError;

        event EventHandler<Protocol.Contracts.ErrorEventArgs>? INetworkTransport.TransportError
        {
            add => TransportError += (sender, e) => value?.Invoke(sender, new Protocol.Contracts.ErrorEventArgs(e.Exception, e.Context));
            remove => TransportError -= (sender, e) => value?.Invoke(sender, new Protocol.Contracts.ErrorEventArgs(e.Exception, e.Context));
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isStarted)
                return;

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _listener = new TcpListener(_localEndpoint);
                _listener.Start();

                _isStarted = true;

                _ = Task.Run(() => AcceptConnectionsAsync(_cancellationTokenSource.Token), cancellationToken);
            }
            catch (Exception ex)
            {
                OnTransportError(new TransportErrorEventArgs(ex, "Failed to start TCP transport"));
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isStarted)
                return;

            try
            {
                _isStarted = false;
                _cancellationTokenSource?.Cancel();
                _listener?.Stop();

                var disconnectTasks = _connections.Values.Select(conn => DisconnectConnectionAsync(conn));
                await Task.WhenAll(disconnectTasks);

                _connections.Clear();
            }
            catch (Exception ex)
            {
                OnTransportError(new TransportErrorEventArgs(ex, "Failed to stop TCP transport"));
                throw;
            }
        }

        public async Task SendAsync(NodeId targetPeerId, IProtocolMessage message, CancellationToken cancellationToken = default)
        {
            if (_connections.TryGetValue(targetPeerId.value, out var connection))
            {
                await connection.SendMessageAsync(message, cancellationToken);
            }
            else
            {
                throw new PeerNotConnectedException(targetPeerId.value);
            }
        }

        public async Task BroadcastAsync(IBroadcastMessage message, CancellationToken cancellationToken = default)
        {
            var tasks = _connections.Values.Select(conn => conn.SendMessageAsync(message, cancellationToken));
            await Task.WhenAll(tasks);
        }

        public IEnumerable<NodeId> GetConnectedPeers()
        {
            return _connections.Keys.Select(id => new NodeId(id));
        }

        public bool IsPeerConnected(NodeId peerId)
        {
            return _connections.ContainsKey(peerId.value) && _connections[peerId.value].IsConnected;
        }

        public async Task ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default)
        {
            try
            {
                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(endpoint.Address, endpoint.Port);

                var peerId = GeneratePeerIdForEndpoint(endpoint);
                var connection = new TcpConnection(tcpClient, peerId, _messageCodec, false);

                if (_connections.TryAdd(peerId, connection))
                {
                    SetupConnection(connection);
                    OnPeerConnected(new Protocol.Contracts.PeerConnectedEventArgs(new NodeId(peerId), endpoint.ToString()));
                }
            }
            catch (Exception ex)
            {
                OnTransportError(new TransportErrorEventArgs(ex, $"Failed to connect to {endpoint}"));
                throw;
            }
        }

        public async Task DisconnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default)
        {
            var peerId = GeneratePeerIdForEndpoint(endpoint);
            if (_connections.TryRemove(peerId, out var connection))
            {
                await DisconnectConnectionAsync(connection);
            }
        }

        public async Task ConnectToPeerAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default)
        {
            await ConnectAsync(endpoint, cancellationToken);
        }

        public async Task ConnectAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            if (IPEndPoint.TryParse(endpoint, out var ipEndpoint))
            {
                await ConnectAsync(ipEndpoint, cancellationToken);
            }
            else
            {
                throw new ArgumentException($"Invalid endpoint format: {endpoint}");
            }
        }

        public async Task<bool> ConnectToPeerAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            try
            {
                await ConnectAsync(endpoint, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task DisconnectAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            if (IPEndPoint.TryParse(endpoint, out var ipEndpoint))
            {
                await DisconnectAsync(ipEndpoint, cancellationToken);
            }
        }

        public async Task SendMessageAsync(NodeId peerId, IProtocolMessage message, CancellationToken cancellationToken = default)
        {
            await SendAsync(peerId, message, cancellationToken);
        }

        public async Task BroadcastMessageAsync(IProtocolMessage message, CancellationToken cancellationToken = default)
        {
            if (message is IBroadcastMessage broadcastMessage)
            {
                await BroadcastAsync(broadcastMessage, cancellationToken);
            }
            else
            {
                var tasks = _connections.Values.Select(conn => conn.SendMessageAsync(message, cancellationToken));
                await Task.WhenAll(tasks);
            }
        }

        public async Task DisconnectPeerAsync(NodeId peerId, CancellationToken cancellationToken = default)
        {
            if (_connections.TryRemove(peerId.value, out var connection))
            {
                await DisconnectConnectionAsync(connection);
            }
        }

        public bool IsConnectedToPeer(NodeId peerId)
        {
            return IsPeerConnected(peerId);
        }

        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isStarted)
            {
                try
                {
                    if (_listener != null)
                    {
                        var tcpClient = await _listener.AcceptTcpClientAsync();
                        var remoteEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
                        var peerId = GeneratePeerIdForEndpoint(remoteEndpoint);

                        var connection = new TcpConnection(tcpClient, peerId, _messageCodec, true);

                        if (_connections.TryAdd(peerId, connection))
                        {
                            SetupConnection(connection);
                            OnPeerConnected(new Protocol.Contracts.PeerConnectedEventArgs(new NodeId(peerId), remoteEndpoint));
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnTransportError(new TransportErrorEventArgs(ex, "Error accepting connection"));
                }
            }
        }

        private void SetupConnection(TcpConnection connection)
        {
            connection.MessageReceived += OnConnectionMessageReceived;
            connection.Disconnected += OnConnectionDisconnected;

            _ = Task.Run(() => connection.StartReceivingAsync(_cancellationTokenSource?.Token ?? CancellationToken.None));
        }

        private async Task DisconnectConnectionAsync(TcpConnection connection)
        {
            try
            {
                connection.MessageReceived -= OnConnectionMessageReceived;
                connection.Disconnected -= OnConnectionDisconnected;
                connection.Dispose();
            }
            catch (Exception ex)
            {
                OnTransportError(new TransportErrorEventArgs(ex, $"Error disconnecting from peer {connection.PeerId}"));
            }

            await Task.CompletedTask;
        }

        private void OnConnectionMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            var senderId = sender is TcpConnection connection ? new NodeId(connection.PeerId) : LocalPeerId;
            
            MessageReceived?.Invoke(this, new Protocol.Contracts.MessageReceivedEventArgs(
                e.Message, 
                senderId
            ));
        }

        private void OnConnectionDisconnected(object? sender, PeerDisconnectedEventArgs e)
        {
            _connections.TryRemove(e.PeerId, out _);
            PeerDisconnected?.Invoke(this, new Protocol.Contracts.PeerDisconnectedEventArgs(
                new NodeId(e.PeerId), 
                e.Reason
            ));
        }

        private void OnPeerConnected(Protocol.Contracts.PeerConnectedEventArgs e)
        {
            PeerConnected?.Invoke(this, e);
        }

        private void OnPeerDisconnected(Protocol.Contracts.PeerDisconnectedEventArgs e)
        {
            PeerDisconnected?.Invoke(this, e);
        }

        private void OnTransportError(TransportErrorEventArgs e)
        {
            TransportError?.Invoke(this, e);
        }

        private static string GeneratePeerIdForEndpoint(IPEndPoint endpoint)
        {
            return $"tcp-{endpoint.Address}-{endpoint.Port}";
        }

        private static string GeneratePeerIdForEndpoint(string endpoint)
        {
            return $"tcp-{endpoint}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopAsync().GetAwaiter().GetResult();
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }
    }
}
