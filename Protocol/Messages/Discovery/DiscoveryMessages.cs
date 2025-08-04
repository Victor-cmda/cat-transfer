using Domain.ValueObjects;
using Protocol.Contracts;
using Protocol.Definitions;
using System.Text.Json;

namespace Protocol.Messages.Discovery
{
    public class PeerAnnouncementMessage : BroadcastMessageBase
    {
        public PeerAnnouncementMessage(
            NodeId sourcePeerId,
            string endpoint,
            IDictionary<string, string>? metadata = null,
            int timeToLive = 3)
            : base(MessageTypes.PeerAnnouncement, sourcePeerId, timeToLive)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            Metadata = metadata ?? new Dictionary<string, string>();
            AnnouncementId = Guid.NewGuid().ToString();
        }

        public string Endpoint { get; }

        public IDictionary<string, string> Metadata { get; }

        public string AnnouncementId { get; }

        protected override bool ValidateBroadcastContent()
        {
            if (string.IsNullOrEmpty(Endpoint))
                return false;

            if (!Uri.TryCreate($"tcp://{Endpoint}", UriKind.Absolute, out _))
                return false;

            if (Metadata.Count > 20) 
                return false;

            foreach (var kvp in Metadata)
            {
                if (string.IsNullOrEmpty(kvp.Key) || kvp.Key.Length > 50)
                    return false;
                if (kvp.Value?.Length > 200)
                    return false;
            }

            return true;
        }

        protected override int GetContentSize()
        {
            var size = Endpoint.Length * 2; 
            size += AnnouncementId.Length * 2;
            
            foreach (var kvp in Metadata)
            {
                size += (kvp.Key.Length + (kvp.Value?.Length ?? 0)) * 2;
            }
            
            return size + 100; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(Endpoint)] = Endpoint;
            properties[nameof(AnnouncementId)] = AnnouncementId;
            properties[nameof(Metadata)] = JsonSerializer.Serialize(Metadata);
            return properties;
        }

        public PeerInfo ToPeerInfo()
        {
            return new PeerInfo(
                SourcePeerId,
                Endpoint,
                ProtocolVersion,
                Timestamp,
                Metadata
            );
        }
    }

    public class PeerDiscoveryMessage : BroadcastMessageBase
    {
        public PeerDiscoveryMessage(
            NodeId sourcePeerId,
            string? requestedCapability = null,
            TimeSpan? maxAge = null,
            int timeToLive = 2)
            : base(MessageTypes.PeerDiscovery, sourcePeerId, timeToLive)
        {
            RequestedCapability = requestedCapability;
            MaxAge = maxAge ?? TimeSpan.FromMinutes(5);
            DiscoveryId = Guid.NewGuid().ToString();
        }

        public string? RequestedCapability { get; }

        public TimeSpan MaxAge { get; }

        public string DiscoveryId { get; }

        protected override bool ValidateBroadcastContent()
        {
            if (RequestedCapability?.Length > 100)
                return false;

            if (MaxAge <= TimeSpan.Zero || MaxAge > TimeSpan.FromHours(1))
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = DiscoveryId.Length * 2;
            size += (RequestedCapability?.Length ?? 0) * 2;
            size += 8; 
            return size + 50; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(DiscoveryId)] = DiscoveryId;
            properties[nameof(MaxAge)] = MaxAge.TotalMilliseconds;
            
            if (!string.IsNullOrEmpty(RequestedCapability))
                properties[nameof(RequestedCapability)] = RequestedCapability;
            
            return properties;
        }
    }

    public class PeerDiscoveryResponseMessage : ResponseMessageBase
    {
        public PeerDiscoveryResponseMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            IEnumerable<PeerInfo> discoveredPeers)
            : base(MessageTypes.PeerDiscoveryResponse, sourcePeerId, targetPeerId, correlationId, true)
        {
            DiscoveredPeers = discoveredPeers?.ToList() ?? new List<PeerInfo>();
            ResponseId = Guid.NewGuid().ToString();
        }

        public PeerDiscoveryResponseMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            int errorCode,
            string errorMessage)
            : base(MessageTypes.PeerDiscoveryResponse, sourcePeerId, targetPeerId, correlationId, false, errorCode, errorMessage)
        {
            DiscoveredPeers = new List<PeerInfo>();
            ResponseId = Guid.NewGuid().ToString();
        }

        public IReadOnlyList<PeerInfo> DiscoveredPeers { get; }

        public string ResponseId { get; }

        protected override bool ValidateResponseContent()
        {
            if (DiscoveredPeers.Count > ProtocolConstants.MaxNetworkPeers)
                return false;

            foreach (var peer in DiscoveredPeers)
            {
                if (string.IsNullOrEmpty(peer.Endpoint))
                    return false;
            }

            return true;
        }

        protected override int GetContentSize()
        {
            var size = ResponseId.Length * 2;
            
            foreach (var peer in DiscoveredPeers)
            {
                size += peer.PeerId.ToString().Length * 2;
                size += peer.Endpoint.Length * 2;
                size += peer.ProtocolVersion.Length * 2;
                size += 8; 
                
                foreach (var kvp in peer.Metadata)
                {
                    size += (kvp.Key.Length + kvp.Value.Length) * 2;
                }
            }
            
            return size + (DiscoveredPeers.Count * 100); 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(ResponseId)] = ResponseId;
            properties["PeerCount"] = DiscoveredPeers.Count;
            
            if (IsSuccess && DiscoveredPeers.Any())
            {
                properties["Peers"] = JsonSerializer.Serialize(DiscoveredPeers.Select(p => new
                {
                    PeerId = p.PeerId.ToString(),
                    Endpoint = p.Endpoint,
                    ProtocolVersion = p.ProtocolVersion,
                    LastSeen = p.LastSeen,
                    Metadata = p.Metadata
                }));
            }
            
            return properties;
        }
    }

    public class PeerLeaveMessage : BroadcastMessageBase
    {
        public PeerLeaveMessage(
            NodeId sourcePeerId,
            string reason = "Normal shutdown",
            int gracePeriodSeconds = 30,
            int timeToLive = 3)
            : base(MessageTypes.PeerLeave, sourcePeerId, timeToLive)
        {
            Reason = reason ?? "Unknown";
            GracePeriodSeconds = gracePeriodSeconds;
            LeaveId = Guid.NewGuid().ToString();
        }

        public string Reason { get; }

        public int GracePeriodSeconds { get; }

        public string LeaveId { get; }

        protected override bool ValidateBroadcastContent()
        {
            if (string.IsNullOrEmpty(Reason) || Reason.Length > 200)
                return false;

            if (GracePeriodSeconds < 0 || GracePeriodSeconds > 300) 
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            return Reason.Length * 2 + LeaveId.Length * 2 + 4 + 50; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(Reason)] = Reason;
            properties[nameof(GracePeriodSeconds)] = GracePeriodSeconds;
            properties[nameof(LeaveId)] = LeaveId;
            return properties;
        }
    }
}
