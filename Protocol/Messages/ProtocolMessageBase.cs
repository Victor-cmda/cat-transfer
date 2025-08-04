using Domain.ValueObjects;
using Protocol.Contracts;
using Protocol.Definitions;
using System.Text.Json;

namespace Protocol.Messages
{
    public abstract class ProtocolMessageBase : IProtocolMessage
    {
        protected ProtocolMessageBase(
            string messageType,
            NodeId sourcePeerId,
            NodeId? targetPeerId = null,
            string? correlationId = null,
            byte priority = 128)
        {
            MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
            MessageId = Guid.NewGuid().ToString();
            SourcePeerId = sourcePeerId;
            TargetPeerId = targetPeerId;
            Timestamp = DateTimeOffset.UtcNow;
            ProtocolVersion = Protocol.Definitions.ProtocolVersion.Current;
            CorrelationId = correlationId;
            Priority = priority;
        }

        public string MessageType { get; }

        public string MessageId { get; }

        public NodeId SourcePeerId { get; }

        public NodeId? TargetPeerId { get; }

        public DateTimeOffset Timestamp { get; }

        public string ProtocolVersion { get; }

        public string? CorrelationId { get; }

        public byte Priority { get; }

        public virtual bool IsValid()
        {
            if (string.IsNullOrEmpty(MessageType) || !MessageTypes.IsValid(MessageType))
                return false;

            if (string.IsNullOrEmpty(MessageId))
                return false;

            if (string.IsNullOrEmpty(ProtocolVersion) || !Protocol.Definitions.ProtocolVersion.IsCompatible(ProtocolVersion))
                return false;

            if (Timestamp == default || Timestamp > DateTimeOffset.UtcNow.AddMinutes(5))
                return false;

            return ValidateContent();
        }

        public virtual int GetSize()
        {
            var headerSize = ProtocolConstants.MessageHeaderSize;
            
            var contentSize = GetContentSize();
            
            return headerSize + contentSize;
        }

        protected abstract bool ValidateContent();

        protected abstract int GetContentSize();

        protected string CreateCorrelationId(string responseType)
        {
            return $"{MessageId}:{responseType}:{DateTimeOffset.UtcNow.Ticks}";
        }

        public bool IsResponseTo(string originalMessageId)
        {
            return CorrelationId?.StartsWith($"{originalMessageId}:") == true;
        }

        public virtual IDictionary<string, object> GetProperties()
        {
            var properties = new Dictionary<string, object>
            {
                { nameof(MessageType), MessageType },
                { nameof(MessageId), MessageId },
                { nameof(SourcePeerId), SourcePeerId.ToString() },
                { nameof(Timestamp), Timestamp },
                { nameof(ProtocolVersion), ProtocolVersion },
                { nameof(Priority), Priority }
            };

            if (TargetPeerId != null)
                properties[nameof(TargetPeerId)] = TargetPeerId.Value.ToString();

            if (CorrelationId != null)
                properties[nameof(CorrelationId)] = CorrelationId;

            return properties;
        }

        public override string ToString()
        {
            return $"{MessageType} [{MessageId}] from {SourcePeerId}" +
                   (TargetPeerId != null ? $" to {TargetPeerId}" : " (broadcast)") +
                   $" at {Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
        }
    }

    public abstract class RequestMessageBase : ProtocolMessageBase, IRequestMessage
    {
        protected RequestMessageBase(
            string messageType,
            NodeId sourcePeerId,
            NodeId? targetPeerId,
            TimeSpan requestTimeout,
            string expectedResponseType,
            string? correlationId = null,
            byte priority = 128)
            : base(messageType, sourcePeerId, targetPeerId, correlationId, priority)
        {
            RequestTimeout = requestTimeout;
            ExpectedResponseType = expectedResponseType ?? throw new ArgumentNullException(nameof(expectedResponseType));
        }

        public TimeSpan RequestTimeout { get; }

        public string ExpectedResponseType { get; }

        protected override bool ValidateContent()
        {
            if (RequestTimeout <= TimeSpan.Zero || RequestTimeout > TimeSpan.FromMinutes(10))
                return false;

            if (string.IsNullOrEmpty(ExpectedResponseType))
                return false;

            return ValidateRequestContent();
        }

        protected abstract bool ValidateRequestContent();

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(RequestTimeout)] = RequestTimeout.TotalMilliseconds;
            properties[nameof(ExpectedResponseType)] = ExpectedResponseType;
            return properties;
        }
    }

    public abstract class ResponseMessageBase : ProtocolMessageBase, IResponseMessage
    {
        protected ResponseMessageBase(
            string messageType,
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            bool isSuccess,
            int? errorCode = null,
            string? errorMessage = null,
            byte priority = 128)
            : base(messageType, sourcePeerId, targetPeerId, correlationId, priority)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool IsSuccess { get; }

        public int? ErrorCode { get; }

        public string? ErrorMessage { get; }

        protected override bool ValidateContent()
        {
            if (!IsSuccess && ErrorCode == null)
                return false;

            if (ErrorCode.HasValue && (ErrorCode < 1000 || ErrorCode > 9999))
                return false;

            if (!string.IsNullOrEmpty(ErrorMessage) && ErrorMessage.Length > ProtocolConstants.MaxErrorMessageLength)
                return false;

            return ValidateResponseContent();
        }

        protected abstract bool ValidateResponseContent();

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(IsSuccess)] = IsSuccess;
            
            if (ErrorCode.HasValue)
                properties[nameof(ErrorCode)] = ErrorCode.Value;
            
            if (!string.IsNullOrEmpty(ErrorMessage))
                properties[nameof(ErrorMessage)] = ErrorMessage;
            
            return properties;
        }
    }

    public abstract class BroadcastMessageBase : ProtocolMessageBase, IBroadcastMessage
    {
        protected BroadcastMessageBase(
            string messageType,
            NodeId sourcePeerId,
            int timeToLive = 3,
            string broadcastScope = "local",
            string? correlationId = null,
            byte priority = 128)
            : base(messageType, sourcePeerId, null, correlationId, priority)
        {
            TimeToLive = timeToLive;
            BroadcastScope = broadcastScope ?? "local";
        }

        public int TimeToLive { get; }

        public string BroadcastScope { get; }

        protected override bool ValidateContent()
        {
            if (TimeToLive <= 0 || TimeToLive > 10)
                return false;

            if (string.IsNullOrEmpty(BroadcastScope))
                return false;

            return ValidateBroadcastContent();
        }

        protected abstract bool ValidateBroadcastContent();

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(TimeToLive)] = TimeToLive;
            properties[nameof(BroadcastScope)] = BroadcastScope;
            return properties;
        }
    }
}
