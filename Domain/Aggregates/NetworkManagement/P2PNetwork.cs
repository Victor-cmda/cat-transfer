using Domain.Aggregates.PeerManagement;
using Domain.Events;
using Domain.ValueObjects;

namespace Domain.Aggregates.NetworkManagement
{
    public sealed class P2PNetwork
    {
        private readonly Dictionary<NodeId, PeerNode> _peers = new();
        private readonly Dictionary<FileId, HashSet<NodeId>> _fileAvailability = new();

        public NodeId LocalNodeId { get; }
        public PeerAddress LocalAddress { get; private set; }
        public NetworkTopology Topology => new(_peers.Keys);
        public DateTimeOffset CreatedAt { get; }

        public IReadOnlyDictionary<NodeId, PeerNode> Peers => _peers.AsReadOnly();
        public int ConnectedPeerCount => _peers.Values.Count(p => p.IsAuthenticated);

        public P2PNetwork(NodeId localNodeId, PeerAddress localAddress)
        {
            LocalNodeId = localNodeId;
            LocalAddress = localAddress;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        public void UpdateLocalAddress(PeerAddress newAddress)
        {
            if (!LocalAddress.Equals(newAddress))
            {
                LocalAddress = newAddress;
                DomainEvents.Raise(new LocalAddressUpdated(LocalNodeId, newAddress));
            }
        }

        public void AddPeer(NodeId peerId, PeerAddress address)
        {
            if (!_peers.ContainsKey(peerId) && !peerId.Equals(LocalNodeId))
            {
                var peer = new PeerNode(peerId, address);
                _peers[peerId] = peer;
                DomainEvents.Raise(new PeerDiscovered(peerId, address));
                DomainEvents.Raise(new NetworkTopologyChanged(Topology));
            }
        }

        public void RemovePeer(NodeId peerId)
        {
            if (_peers.Remove(peerId))
            {
                foreach (var fileAvailability in _fileAvailability.Values)
                {
                    fileAvailability.Remove(peerId);
                }

                DomainEvents.Raise(new PeerRemoved(peerId));
                DomainEvents.Raise(new NetworkTopologyChanged(Topology));
            }
        }

        public PeerNode? GetPeer(NodeId peerId)
        {
            _peers.TryGetValue(peerId, out var peer);
            return peer;
        }

        public void ConnectToPeer(NodeId peerId)
        {
            if (_peers.TryGetValue(peerId, out var peer))
            {
                peer.Connect();
            }
        }

        public void HandlePeerConnected(NodeId peerId)
        {
            if (_peers.TryGetValue(peerId, out var peer))
            {
                peer.MarkConnected();
            }
        }

        public void HandlePeerDisconnected(NodeId peerId, string reason)
        {
            if (_peers.TryGetValue(peerId, out var peer))
            {
                peer.Disconnect(reason);
            }
        }

        public void HandleHandshakeCompleted(NodeId peerId, EncryptionKey sharedKey)
        {
            if (_peers.TryGetValue(peerId, out var peer))
            {
                peer.CompleteHandshake(sharedKey);
            }
        }

        public void AdvertiseFile(FileId fileId, NodeId peerId)
        {
            if (!_fileAvailability.ContainsKey(fileId))
            {
                _fileAvailability[fileId] = new HashSet<NodeId>();
            }

            if (_fileAvailability[fileId].Add(peerId))
            {
                DomainEvents.Raise(new FileAvailabilityUpdated(fileId, peerId, true));
            }
        }

        public void RemoveFileAdvertisement(FileId fileId, NodeId peerId)
        {
            if (_fileAvailability.TryGetValue(fileId, out var peers))
            {
                if (peers.Remove(peerId))
                {
                    DomainEvents.Raise(new FileAvailabilityUpdated(fileId, peerId, false));
                    
                    if (peers.Count == 0)
                    {
                        _fileAvailability.Remove(fileId);
                    }
                }
            }
        }

        public IEnumerable<NodeId> GetPeersWithFile(FileId fileId)
        {
            if (_fileAvailability.TryGetValue(fileId, out var peers))
            {
                return peers.Where(peerId => 
                    _peers.TryGetValue(peerId, out var peer) && peer.IsAuthenticated);
            }
            return Enumerable.Empty<NodeId>();
        }

        public IEnumerable<NodeId> GetConnectedPeers()
        {
            return _peers.Values
                .Where(p => p.IsAuthenticated)
                .Select(p => p.Id);
        }

        public void BroadcastToConnectedPeers<T>(T domainEvent) where T : class
        {
            var connectedPeers = GetConnectedPeers().ToList();
            if (connectedPeers.Any())
            {
                DomainEvents.Raise(new BroadcastRequested(typeof(T).Name, connectedPeers.ToList()));
            }
        }

        public void UpdatePeerTopology(NodeId peerId, IEnumerable<NodeId> knownPeers)
        {
            if (_peers.TryGetValue(peerId, out var peer))
            {
                var topology = new NetworkTopology(knownPeers);
                peer.UpdateKnownPeers(topology);

                foreach (var unknownPeer in knownPeers.Where(p => !_peers.ContainsKey(p) && !p.Equals(LocalNodeId)))
                {
                    DomainEvents.Raise(new PotentialPeerDiscovered(unknownPeer, peerId));
                }
            }
        }

        public NetworkStatistics GetNetworkStatistics()
        {
            return new NetworkStatistics(
                TotalPeers: _peers.Count,
                ConnectedPeers: ConnectedPeerCount,
                AvailableFiles: _fileAvailability.Count,
                AverageConnectionAttempts: _peers.Values.Any() ? _peers.Values.Average(p => p.ConnectionAttempts) : 0
            );
        }
    }

    public record NetworkStatistics(
        int TotalPeers,
        int ConnectedPeers,
        int AvailableFiles,
        double AverageConnectionAttempts
    );
}
