using Domain.Enumerations;
using Domain.Events;
using Domain.ValueObjects;

namespace Domain.Aggregates.PeerManagement
{
    public sealed class PeerNode
    {
        public NodeId Id { get; }
        public PeerAddress Address { get; private set; }
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;
        public EncryptionKey? SharedKey { get; private set; }
        public DateTimeOffset LastSeen { get; private set; }
        public int ConnectionAttempts { get; private set; }
        public NetworkTopology KnownPeers { get; private set; } = NetworkTopology.Empty;

        public PeerNode(NodeId id, PeerAddress address)
        {
            Id = id;
            Address = address;
            LastSeen = DateTimeOffset.UtcNow;
        }

        public void UpdateAddress(PeerAddress newAddress)
        {
            if (!Address.Equals(newAddress))
            {
                Address = newAddress;
                DomainEvents.Raise(new PeerAddressUpdated(Id, newAddress));
            }
        }

        public void Connect()
        {
            if (Status == ConnectionStatus.Disconnected || Status == ConnectionStatus.Failed)
            {
                Status = ConnectionStatus.Connecting;
                ConnectionAttempts++;
                DomainEvents.Raise(new PeerConnectionStarted(Id, Address));
            }
        }

        public void MarkConnected()
        {
            if (Status == ConnectionStatus.Connecting)
            {
                Status = ConnectionStatus.Connected;
                LastSeen = DateTimeOffset.UtcNow;
                DomainEvents.Raise(new PeerConnected(Id, Address));
            }
        }

        public void StartAuthentication()
        {
            if (Status == ConnectionStatus.Connected)
            {
                Status = ConnectionStatus.Authenticating;
                DomainEvents.Raise(new PeerAuthenticationStarted(Id));
            }
        }

        public void CompleteHandshake(EncryptionKey sharedKey)
        {
            if (Status == ConnectionStatus.Authenticating)
            {
                Status = ConnectionStatus.Authenticated;
                SharedKey = sharedKey;
                LastSeen = DateTimeOffset.UtcNow;
                ConnectionAttempts = 0;
                DomainEvents.Raise(new HandshakeCompleted(Id, sharedKey));
            }
        }

        public void Disconnect(string? reason = null)
        {
            var previousStatus = Status;
            Status = ConnectionStatus.Disconnected;
            SharedKey = null;

            if (previousStatus != ConnectionStatus.Disconnected)
            {
                DomainEvents.Raise(new PeerDisconnected(Id, reason ?? "Unknown"));
            }
        }

        public void MarkConnectionFailed(string reason)
        {
            Status = ConnectionStatus.Failed;
            DomainEvents.Raise(new PeerConnectionFailed(Id, reason, ConnectionAttempts));
        }

        public void UpdateLastSeen()
        {
            LastSeen = DateTimeOffset.UtcNow;
        }

        public void UpdateKnownPeers(NetworkTopology topology)
        {
            KnownPeers = topology;
            DomainEvents.Raise(new PeerTopologyUpdated(Id, topology.PeerCount));
        }

        public bool IsAuthenticated => Status == ConnectionStatus.Authenticated && SharedKey != null;

        public bool ShouldRetry(TimeSpan timeout, int maxAttempts)
        {
            return ConnectionAttempts < maxAttempts &&
                   DateTimeOffset.UtcNow - LastSeen > timeout &&
                   Status != ConnectionStatus.Authenticated;
        }
    }
}
