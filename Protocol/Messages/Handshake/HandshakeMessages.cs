using Domain.ValueObjects;
using Protocol.Definitions;
using System.Text.Json;

namespace Protocol.Messages.Handshake
{
    public class HandshakeRequestMessage : RequestMessageBase
    {
        public HandshakeRequestMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            IEnumerable<string> supportedCapabilities,
            IDictionary<string, string>? clientMetadata = null)
            : base(
                MessageTypes.HandshakeRequest,
                sourcePeerId,
                targetPeerId,
                TimeSpan.FromSeconds(ProtocolConstants.DefaultHandshakeTimeoutMs / 1000),
                MessageTypes.HandshakeResponse)
        {
            SupportedCapabilities = supportedCapabilities?.ToList() ?? new List<string>();
            ClientMetadata = clientMetadata ?? new Dictionary<string, string>();
            HandshakeId = Guid.NewGuid().ToString();
            ClientVersion = Protocol.Definitions.ProtocolVersion.Current;
            RequestedFeatures = new List<string>();
        }

        public IReadOnlyList<string> SupportedCapabilities { get; }

        public IDictionary<string, string> ClientMetadata { get; }

        public string HandshakeId { get; }

        public string ClientVersion { get; }

        public IReadOnlyList<string> RequestedFeatures { get; }

        protected override bool ValidateRequestContent()
        {
            if (string.IsNullOrEmpty(HandshakeId))
                return false;

            if (string.IsNullOrEmpty(ClientVersion) || !Protocol.Definitions.ProtocolVersion.IsCompatible(ClientVersion))
                return false;

            if (SupportedCapabilities.Count > 50) 
                return false;

            if (ClientMetadata.Count > 20)
                return false;

            foreach (var capability in SupportedCapabilities)
            {
                if (string.IsNullOrEmpty(capability) || capability.Length > 100)
                    return false;
            }

            foreach (var kvp in ClientMetadata)
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
            var size = HandshakeId.Length * 2;
            size += ClientVersion.Length * 2;
            
            foreach (var capability in SupportedCapabilities)
            {
                size += capability.Length * 2;
            }
            
            foreach (var kvp in ClientMetadata)
            {
                size += (kvp.Key.Length + (kvp.Value?.Length ?? 0)) * 2;
            }
            
            foreach (var feature in RequestedFeatures)
            {
                size += feature.Length * 2;
            }
            
            return size + 200; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(HandshakeId)] = HandshakeId;
            properties[nameof(ClientVersion)] = ClientVersion;
            properties[nameof(SupportedCapabilities)] = JsonSerializer.Serialize(SupportedCapabilities);
            properties[nameof(ClientMetadata)] = JsonSerializer.Serialize(ClientMetadata);
            properties[nameof(RequestedFeatures)] = JsonSerializer.Serialize(RequestedFeatures);
            return properties;
        }
    }

    public class HandshakeResponseMessage : ResponseMessageBase
    {
        public HandshakeResponseMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            string handshakeId,
            IEnumerable<string> acceptedCapabilities,
            IDictionary<string, string>? serverMetadata = null,
            IEnumerable<string>? grantedFeatures = null)
            : base(MessageTypes.HandshakeResponse, sourcePeerId, targetPeerId, correlationId, true)
        {
            HandshakeId = handshakeId ?? throw new ArgumentNullException(nameof(handshakeId));
            AcceptedCapabilities = acceptedCapabilities?.ToList() ?? new List<string>();
            ServerMetadata = serverMetadata ?? new Dictionary<string, string>();
            GrantedFeatures = grantedFeatures?.ToList() ?? new List<string>();
            ServerVersion = Protocol.Definitions.ProtocolVersion.Current;
            SessionToken = Guid.NewGuid().ToString();
        }

        public HandshakeResponseMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            string handshakeId,
            int errorCode,
            string errorMessage)
            : base(MessageTypes.HandshakeResponse, sourcePeerId, targetPeerId, correlationId, false, errorCode, errorMessage)
        {
            HandshakeId = handshakeId ?? throw new ArgumentNullException(nameof(handshakeId));
            AcceptedCapabilities = new List<string>();
            ServerMetadata = new Dictionary<string, string>();
            GrantedFeatures = new List<string>();
            ServerVersion = Protocol.Definitions.ProtocolVersion.Current;
            SessionToken = string.Empty;
        }

        public string HandshakeId { get; }

        public IReadOnlyList<string> AcceptedCapabilities { get; }

        public IDictionary<string, string> ServerMetadata { get; }

        public IReadOnlyList<string> GrantedFeatures { get; }

        public string ServerVersion { get; }

        public string SessionToken { get; }

        protected override bool ValidateResponseContent()
        {
            if (string.IsNullOrEmpty(HandshakeId))
                return false;

            if (string.IsNullOrEmpty(ServerVersion) || !Protocol.Definitions.ProtocolVersion.IsCompatible(ServerVersion))
                return false;

            if (IsSuccess && string.IsNullOrEmpty(SessionToken))
                return false;

            if (AcceptedCapabilities.Count > 50)
                return false;

            if (ServerMetadata.Count > 20)
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = HandshakeId.Length * 2;
            size += ServerVersion.Length * 2;
            size += SessionToken.Length * 2;
            
            foreach (var capability in AcceptedCapabilities)
            {
                size += capability.Length * 2;
            }
            
            foreach (var kvp in ServerMetadata)
            {
                size += (kvp.Key.Length + (kvp.Value?.Length ?? 0)) * 2;
            }
            
            foreach (var feature in GrantedFeatures)
            {
                size += feature.Length * 2;
            }
            
            return size + 200; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(HandshakeId)] = HandshakeId;
            properties[nameof(ServerVersion)] = ServerVersion;
            properties[nameof(AcceptedCapabilities)] = JsonSerializer.Serialize(AcceptedCapabilities);
            properties[nameof(ServerMetadata)] = JsonSerializer.Serialize(ServerMetadata);
            properties[nameof(GrantedFeatures)] = JsonSerializer.Serialize(GrantedFeatures);
            
            if (IsSuccess)
                properties[nameof(SessionToken)] = SessionToken;
            
            return properties;
        }
    }

    public class HandshakeAckMessage : ProtocolMessageBase
    {
        public HandshakeAckMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            string handshakeId,
            string sessionToken)
            : base(MessageTypes.HandshakeAck, sourcePeerId, targetPeerId, correlationId)
        {
            HandshakeId = handshakeId ?? throw new ArgumentNullException(nameof(handshakeId));
            SessionToken = sessionToken ?? throw new ArgumentNullException(nameof(sessionToken));
            AckId = Guid.NewGuid().ToString();
            ConnectionEstablished = DateTimeOffset.UtcNow;
        }

        public string HandshakeId { get; }

        public string SessionToken { get; }

        public string AckId { get; }

        public DateTimeOffset ConnectionEstablished { get; }

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(HandshakeId))
                return false;

            if (string.IsNullOrEmpty(SessionToken))
                return false;

            if (string.IsNullOrEmpty(AckId))
                return false;

            if (ConnectionEstablished == default)
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            return HandshakeId.Length * 2 + SessionToken.Length * 2 + AckId.Length * 2 + 8 + 50; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(HandshakeId)] = HandshakeId;
            properties[nameof(SessionToken)] = SessionToken;
            properties[nameof(AckId)] = AckId;
            properties[nameof(ConnectionEstablished)] = ConnectionEstablished;
            return properties;
        }
    }

    public class HandshakeFailureMessage : ProtocolMessageBase
    {
        public HandshakeFailureMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            string handshakeId,
            int errorCode,
            string errorMessage,
            string failureReason)
            : base(MessageTypes.HandshakeFailure, sourcePeerId, targetPeerId, correlationId)
        {
            HandshakeId = handshakeId ?? throw new ArgumentNullException(nameof(handshakeId));
            ErrorCode = errorCode;
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
            FailureReason = failureReason ?? throw new ArgumentNullException(nameof(failureReason));
            FailureId = Guid.NewGuid().ToString();
            CanRetry = DetermineRetryability(errorCode);
        }

        public string HandshakeId { get; }

        public int ErrorCode { get; }

        public string ErrorMessage { get; }

        public string FailureReason { get; }

        public string FailureId { get; }

        public bool CanRetry { get; }

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(HandshakeId))
                return false;

            if (ErrorCode < 1000 || ErrorCode > 9999)
                return false;

            if (string.IsNullOrEmpty(ErrorMessage) || ErrorMessage.Length > ProtocolConstants.MaxErrorMessageLength)
                return false;

            if (string.IsNullOrEmpty(FailureReason) || FailureReason.Length > 500)
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            return HandshakeId.Length * 2 + ErrorMessage.Length * 2 + FailureReason.Length * 2 + FailureId.Length * 2 + 4 + 1 + 50; 
        }

        private static bool DetermineRetryability(int errorCode)
        {
            return errorCode switch
            {
                ProtocolConstants.ErrorTimeout => true,
                ProtocolConstants.ErrorProtocol => false,
                ProtocolConstants.ErrorVersionIncompatible => false,
                ProtocolConstants.ErrorHandshakeFailed => true,
                _ => false
            };
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(HandshakeId)] = HandshakeId;
            properties[nameof(ErrorCode)] = ErrorCode;
            properties[nameof(ErrorMessage)] = ErrorMessage;
            properties[nameof(FailureReason)] = FailureReason;
            properties[nameof(FailureId)] = FailureId;
            properties[nameof(CanRetry)] = CanRetry;
            return properties;
        }
    }
}
