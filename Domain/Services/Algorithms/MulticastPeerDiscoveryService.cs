using Domain.Events;
using Domain.ValueObjects;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Domain.Services.Algorithms
{
    public sealed class MulticastPeerDiscoveryService : IPeerDiscoveryService
    {
        private const int DiscoveryPort = 8765;
        private const string MulticastAddress = "239.255.255.250"; 
        private const int DiscoveryTimeoutMs = 5000;
        
        private readonly List<Action<NodeId, PeerAddress>> _discoveryCallbacks = new();
        private readonly HashSet<PeerAddress> _knownPeers = new();
        private readonly object _lock = new();
        private CancellationTokenSource? _cancellationTokenSource;

        public async Task<IEnumerable<PeerAddress>> DiscoverPeersAsync()
        {
            var discoveredPeers = new HashSet<PeerAddress>();
            
            try
            {
                using var udpClient = new UdpClient();
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                
                var multicastEndpoint = new IPEndPoint(IPAddress.Parse(MulticastAddress), DiscoveryPort);
                
                var discoveryMessage = CreateDiscoveryMessage();
                await udpClient.SendAsync(discoveryMessage, multicastEndpoint);
                
                var timeoutCts = new CancellationTokenSource(DiscoveryTimeoutMs);
                var startTime = DateTime.UtcNow;
                
                while (!timeoutCts.Token.IsCancellationRequested && 
                       (DateTime.UtcNow - startTime).TotalMilliseconds < DiscoveryTimeoutMs)
                {
                    try
                    {
                        var result = await udpClient.ReceiveAsync().WaitAsync(timeoutCts.Token);
                        var peerAddress = ParseDiscoveryResponse(result.Buffer);
                        
                        if (peerAddress.HasValue)
                        {
                            discoveredPeers.Add(peerAddress.Value);
                            lock (_lock)
                            {
                                _knownPeers.Add(peerAddress.Value);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        DomainEvents.Raise(new PeerDiscoveryError($"Discovery error: {ex.Message}"));
                    }
                }
                
                var localPeers = await ScanLocalNetworkAsync();
                foreach (var peer in localPeers)
                {
                    discoveredPeers.Add(peer);
                }
            }
            catch (Exception ex)
            {
                DomainEvents.Raise(new PeerDiscoveryError($"Discovery failed: {ex.Message}"));
            }

            return discoveredPeers;
        }

        public async Task AnnouncePresenceAsync(NodeId nodeId, PeerAddress address)
        {
            try
            {
                using var udpClient = new UdpClient();
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                
                var multicastEndpoint = new IPEndPoint(IPAddress.Parse(MulticastAddress), DiscoveryPort);
                
                var announcementMessage = CreateAnnouncementMessage(nodeId, address);
                await udpClient.SendAsync(announcementMessage, multicastEndpoint);
                
                DomainEvents.Raise(new PresenceAnnounced(nodeId, address));
            }
            catch (Exception ex)
            {
                DomainEvents.Raise(new PeerDiscoveryError($"Announcement failed: {ex.Message}"));
            }
        }

        public async Task<IEnumerable<PeerAddress>> QueryPeerListAsync(PeerAddress peerAddress)
        {
            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(peerAddress.host, peerAddress.port);
                
                using var stream = tcpClient.GetStream();
                
                var request = CreatePeerListRequest();
                await stream.WriteAsync(request);
                
                var response = new byte[4096];
                var bytesRead = await stream.ReadAsync(response);
                
                var peers = ParsePeerListResponse(response.AsSpan(0, bytesRead));
                
                lock (_lock)
                {
                    foreach (var peer in peers)
                    {
                        _knownPeers.Add(peer);
                    }
                }
                
                return peers;
            }
            catch (Exception ex)
            {
                DomainEvents.Raise(new PeerDiscoveryError($"Peer list query failed for {peerAddress}: {ex.Message}"));
                return Enumerable.Empty<PeerAddress>();
            }
        }

        public void RegisterPeerDiscoveryCallback(Action<NodeId, PeerAddress> onPeerDiscovered)
        {
            lock (_lock)
            {
                _discoveryCallbacks.Add(onPeerDiscovered);
            }
        }

        public void StartContinuousDiscovery(NodeId nodeId, PeerAddress localAddress)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        await AnnouncePresenceAsync(nodeId, localAddress);
                        
                        var discoveredPeers = await DiscoverPeersAsync();
                        
                        foreach (var peerAddress in discoveredPeers)
                        {
                            var assumedNodeId = NodeId.NewGuid();
                            NotifyPeerDiscovered(assumedNodeId, peerAddress);
                        }
                        
                        await Task.Delay(30000, _cancellationTokenSource.Token); 
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        DomainEvents.Raise(new PeerDiscoveryError($"Continuous discovery error: {ex.Message}"));
                        await Task.Delay(5000, _cancellationTokenSource.Token);
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        public void StopContinuousDiscovery()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task<IEnumerable<PeerAddress>> ScanLocalNetworkAsync()
        {
            var localPeers = new List<PeerAddress>();
            var localIp = GetLocalIPAddress();
            
            if (localIp == null) return localPeers;
            
            var baseIp = GetNetworkBase(localIp);
            var tasks = new List<Task>();
            
            for (int i = 1; i <= 254; i++)
            {
                var targetIp = $"{baseIp}.{i}";
                
                tasks.Add(Task.Run(async () =>
                {
                    if (await IsPortOpenAsync(targetIp, DiscoveryPort))
                    {
                        lock (localPeers)
                        {
                            localPeers.Add(PeerAddress.Create(targetIp, DiscoveryPort));
                        }
                    }
                }));
            }
            
            await Task.WhenAll(tasks);
            return localPeers;
        }

        private static async Task<bool> IsPortOpenAsync(string host, int port)
        {
            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(host, port).WaitAsync(TimeSpan.FromSeconds(1));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string? GetLocalIPAddress()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            
            foreach (var ni in networkInterfaces)
            {
                var ipProps = ni.GetIPProperties();
                var ipv4Addresses = ipProps.UnicastAddresses
                    .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ua => ua.Address.ToString());
                
                foreach (var ip in ipv4Addresses)
                {
                    if (!ip.StartsWith("169.254"))
                    {
                        return ip;
                    }
                }
            }
            
            return null;
        }

        private static string GetNetworkBase(string ipAddress)
        {
            var parts = ipAddress.Split('.');
            return $"{parts[0]}.{parts[1]}.{parts[2]}";
        }

        private static byte[] CreateDiscoveryMessage()
        {
            var message = "CAT-TRANSFER-DISCOVERY-REQUEST";
            return System.Text.Encoding.UTF8.GetBytes(message);
        }

        private static byte[] CreateAnnouncementMessage(NodeId nodeId, PeerAddress address)
        {
            var message = $"CAT-TRANSFER-ANNOUNCE:{nodeId}:{address}";
            return System.Text.Encoding.UTF8.GetBytes(message);
        }

        private static byte[] CreatePeerListRequest()
        {
            var message = "CAT-TRANSFER-PEER-LIST-REQUEST";
            return System.Text.Encoding.UTF8.GetBytes(message);
        }

        private static PeerAddress? ParseDiscoveryResponse(byte[] data)
        {
            try
            {
                var message = System.Text.Encoding.UTF8.GetString(data);
                if (message.StartsWith("CAT-TRANSFER-ANNOUNCE:"))
                {
                    var parts = message.Split(':');
                    if (parts.Length >= 4)
                    {
                        var host = parts[2];
                        if (int.TryParse(parts[3], out var port))
                        {
                            return PeerAddress.Create(host, port);
                        }
                    }
                }
            }
            catch
            {
            }
            
            return null;
        }

        private static IEnumerable<PeerAddress> ParsePeerListResponse(ReadOnlySpan<byte> data)
        {
            var addresses = new List<PeerAddress>();
            
            try
            {
                var message = System.Text.Encoding.UTF8.GetString(data);
                var lines = message.Split('\n');
                
                foreach (var line in lines)
                {
                    if (line.Contains(':') && !line.StartsWith("CAT-TRANSFER"))
                    {
                        try
                        {
                            addresses.Add(PeerAddress.Parse(line.Trim()));
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
            
            return addresses;
        }

        private void NotifyPeerDiscovered(NodeId nodeId, PeerAddress address)
        {
            lock (_lock)
            {
                foreach (var callback in _discoveryCallbacks)
                {
                    try
                    {
                        callback(nodeId, address);
                    }
                    catch (Exception ex)
                    {
                        DomainEvents.Raise(new PeerDiscoveryError($"Callback error: {ex.Message}"));
                    }
                }
            }
        }

        public void Dispose()
        {
            StopContinuousDiscovery();
            _cancellationTokenSource?.Dispose();
        }
    }
}
