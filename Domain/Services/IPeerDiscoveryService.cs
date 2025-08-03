using Domain.ValueObjects;

namespace Domain.Services
{
    public interface IPeerDiscoveryService
    {
        Task<IEnumerable<PeerAddress>> DiscoverPeersAsync();
        Task AnnouncePresenceAsync(NodeId nodeId, PeerAddress address);
        Task<IEnumerable<PeerAddress>> QueryPeerListAsync(PeerAddress peerAddress);
        void RegisterPeerDiscoveryCallback(Action<NodeId, PeerAddress> onPeerDiscovered);
    }
}
