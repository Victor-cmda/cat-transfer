using Domain.ValueObjects;

namespace Domain.Services
{
    public interface IAsyncPeerDiscoveryService
    {
        Task<IEnumerable<PeerAddress>> DiscoverPeersAsync(CancellationToken cancellationToken = default);
        Task AnnouncePresenceAsync(NodeId nodeId, PeerAddress address, CancellationToken cancellationToken = default);
        Task<IEnumerable<PeerAddress>> QueryPeerListAsync(PeerAddress peerAddress, CancellationToken cancellationToken = default);
        Task StartDiscoveryAsync(CancellationToken cancellationToken = default);
        Task StopDiscoveryAsync(CancellationToken cancellationToken = default);
    }
}
