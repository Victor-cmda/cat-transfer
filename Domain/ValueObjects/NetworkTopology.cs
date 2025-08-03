namespace Domain.ValueObjects
{
    public readonly record struct NetworkTopology(IReadOnlySet<NodeId> peers)
    {
        public NetworkTopology(IEnumerable<NodeId> peers) : this(peers.ToHashSet())
        {
        }

        public NetworkTopology AddPeer(NodeId peer)
        {
            var newPeers = new HashSet<NodeId>(peers) { peer };
            return new NetworkTopology(newPeers);
        }

        public NetworkTopology RemovePeer(NodeId peer)
        {
            var newPeers = new HashSet<NodeId>(peers);
            newPeers.Remove(peer);
            return new NetworkTopology(newPeers);
        }

        public bool ContainsPeer(NodeId peer) => peers.Contains(peer);

        public int PeerCount => peers.Count;

        public static NetworkTopology Empty => new(new HashSet<NodeId>());
    }
}
