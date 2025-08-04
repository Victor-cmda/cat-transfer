using Domain.ValueObjects;
using Protocol.Definitions;
using System.Text.Json;

namespace Protocol.Messages.Control
{
    public class HeartbeatMessage : ProtocolMessageBase
    {
        public HeartbeatMessage(
            NodeId sourcePeerId,
            NodeId? targetPeerId = null,
            IDictionary<string, object>? status = null)
            : base(MessageTypes.Heartbeat, sourcePeerId, targetPeerId, priority: 255)
        {
            Status = status ?? new Dictionary<string, object>();
            HeartbeatId = Guid.NewGuid().ToString();
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        }

        public IDictionary<string, object> Status { get; }

        public string HeartbeatId { get; }

        public TimeSpan Uptime { get; }

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(HeartbeatId))
                return false;

            if (Uptime < TimeSpan.Zero)
                return false;

            if (Status.Count > 20)

                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = HeartbeatId.Length * 2;
            size += 8;


            foreach (var kvp in Status)
            {
                size += kvp.Key.Length * 2;
                size += (kvp.Value?.ToString()?.Length ?? 0) * 2;
            }

            return size + 100;

        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(HeartbeatId)] = HeartbeatId;
            properties[nameof(Uptime)] = Uptime.TotalMilliseconds;
            properties[nameof(Status)] = JsonSerializer.Serialize(Status);
            return properties;
        }
    }

    public class ErrorMessage : ProtocolMessageBase
    {
        public ErrorMessage(
            NodeId sourcePeerId,
            NodeId? targetPeerId,
            int errorCode,
            string errorDescription,
            string? context = null,
            string? correlationId = null)
            : base(MessageTypes.Error, sourcePeerId, targetPeerId, correlationId, priority: 200)
        {
            ErrorCode = errorCode;
            ErrorDescription = errorDescription ?? throw new ArgumentNullException(nameof(errorDescription));
            Context = context;
            ErrorId = Guid.NewGuid().ToString();
            ErrorTimestamp = DateTimeOffset.UtcNow;
            Severity = DetermineSeverity(errorCode);
        }

        public int ErrorCode { get; }

        public string ErrorDescription { get; }

        public string? Context { get; }

        public string ErrorId { get; }

        public DateTimeOffset ErrorTimestamp { get; }

        public string Severity { get; }

        protected override bool ValidateContent()
        {
            if (ErrorCode < 1000 || ErrorCode > 9999)
                return false;

            if (string.IsNullOrEmpty(ErrorDescription) || ErrorDescription.Length > ProtocolConstants.MaxErrorMessageLength)
                return false;

            if (Context?.Length > 1000)
                return false;

            if (string.IsNullOrEmpty(ErrorId))
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = ErrorDescription.Length * 2;
            size += (Context?.Length ?? 0) * 2;
            size += ErrorId.Length * 2;
            size += Severity.Length * 2;
            size += 4 + 8;

            return size + 100;

        }

        private static string DetermineSeverity(int errorCode)
        {
            return errorCode switch
            {
                >= 1000 and < 2000 => "Warning",
                >= 2000 and < 3000 => "Error",
                >= 3000 and < 4000 => "Critical",
                _ => "Unknown"
            };
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(ErrorCode)] = ErrorCode;
            properties[nameof(ErrorDescription)] = ErrorDescription;
            properties[nameof(ErrorId)] = ErrorId;
            properties[nameof(ErrorTimestamp)] = ErrorTimestamp;
            properties[nameof(Severity)] = Severity;

            if (!string.IsNullOrEmpty(Context))
                properties[nameof(Context)] = Context;

            return properties;
        }
    }

    public class AckMessage : ProtocolMessageBase
    {
        public AckMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            string acknowledgedMessageId,
            bool isSuccess = true,
            string? statusMessage = null)
            : base(MessageTypes.Ack, sourcePeerId, targetPeerId, correlationId, priority: 180)
        {
            AcknowledgedMessageId = acknowledgedMessageId ?? throw new ArgumentNullException(nameof(acknowledgedMessageId));
            IsSuccess = isSuccess;
            StatusMessage = statusMessage;
            AckId = Guid.NewGuid().ToString();
            ProcessingTime = TimeSpan.Zero;

        }

        public string AcknowledgedMessageId { get; }

        public bool IsSuccess { get; }

        public string? StatusMessage { get; }

        public string AckId { get; }

        public TimeSpan ProcessingTime { get; set; }

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(AcknowledgedMessageId))
                return false;

            if (StatusMessage?.Length > 200)
                return false;

            if (string.IsNullOrEmpty(AckId))
                return false;

            if (ProcessingTime < TimeSpan.Zero)
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = AcknowledgedMessageId.Length * 2;
            size += (StatusMessage?.Length ?? 0) * 2;
            size += AckId.Length * 2;
            size += 1 + 8;

            return size + 50;

        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(AcknowledgedMessageId)] = AcknowledgedMessageId;
            properties[nameof(IsSuccess)] = IsSuccess;
            properties[nameof(AckId)] = AckId;
            properties[nameof(ProcessingTime)] = ProcessingTime.TotalMilliseconds;

            if (!string.IsNullOrEmpty(StatusMessage))
                properties[nameof(StatusMessage)] = StatusMessage;

            return properties;
        }
    }

    public class DisconnectMessage : ProtocolMessageBase
    {
        public DisconnectMessage(
            NodeId sourcePeerId,
            NodeId? targetPeerId = null,
            string reason = "Normal disconnect",
            int gracePeriodSeconds = 10,
            bool allowReconnect = true)
            : base(MessageTypes.Disconnect, sourcePeerId, targetPeerId, priority: 250)
        {
            Reason = reason ?? "Unknown";
            GracePeriodSeconds = gracePeriodSeconds;
            AllowReconnect = allowReconnect;
            DisconnectId = Guid.NewGuid().ToString();
            DisconnectType = DetermineDisconnectType(reason ?? "Unknown");
        }

        public string Reason { get; }

        public int GracePeriodSeconds { get; }

        public bool AllowReconnect { get; }

        public string DisconnectId { get; }

        public string DisconnectType { get; }

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(Reason) || Reason.Length > 200)
                return false;

            if (GracePeriodSeconds < 0 || GracePeriodSeconds > 300)

                return false;

            if (string.IsNullOrEmpty(DisconnectId))
                return false;

            if (string.IsNullOrEmpty(DisconnectType))
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            return Reason.Length * 2 + DisconnectId.Length * 2 + DisconnectType.Length * 2 + 4 + 1 + 50;

        }

        private static string DetermineDisconnectType(string reason)
        {
            var lowerReason = reason?.ToLowerInvariant() ?? "unknown";
            return lowerReason switch
            {
                var r when r.Contains("error") || r.Contains("fail") => "error",
                var r when r.Contains("force") || r.Contains("abort") => "forced",
                var r when r.Contains("shutdown") || r.Contains("normal") => "normal",
                _ => "unknown"
            };
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(Reason)] = Reason;
            properties[nameof(GracePeriodSeconds)] = GracePeriodSeconds;
            properties[nameof(AllowReconnect)] = AllowReconnect;
            properties[nameof(DisconnectId)] = DisconnectId;
            properties[nameof(DisconnectType)] = DisconnectType;
            return properties;
        }
    }
}
