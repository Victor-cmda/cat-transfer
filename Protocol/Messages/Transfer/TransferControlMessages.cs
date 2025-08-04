using Domain.ValueObjects;
using Protocol.Contracts;
using Protocol.Definitions;
using System.Text.Json;

namespace Protocol.Messages.Transfer
{
    public class ChunkAckMessage : ProtocolMessageBase, IAcknowledgableMessage
    {
        public ChunkAckMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string transferId,
            ChunkId chunkId,
            long sequenceNumber,
            bool isValid,
            string? correlationId = null,
            string? errorMessage = null)
            : base(MessageTypes.ChunkAck, sourcePeerId, targetPeerId, correlationId, priority: 200)
        {
            TransferId = transferId ?? throw new ArgumentNullException(nameof(transferId));
            ChunkId = chunkId;
            SequenceNumber = sequenceNumber;
            IsValid = isValid;
            ErrorMessage = errorMessage;
            AckId = Guid.NewGuid().ToString();
            ReceivedAt = DateTimeOffset.UtcNow;
        }

        public string TransferId { get; }

        public ChunkId ChunkId { get; }

        public long SequenceNumber { get; }

        public new bool IsValid { get; }

        public string? ErrorMessage { get; }

        public string AckId { get; }

        public DateTimeOffset ReceivedAt { get; }

        public bool RequireAcknowledgment => false; 
        public TimeSpan AcknowledgmentTimeout => TimeSpan.FromSeconds(5);
        public int MaxRetries => 2;

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(TransferId))
                return false;

            if (SequenceNumber < 0)
                return false;

            if (!IsValid && string.IsNullOrEmpty(ErrorMessage))
                return false;

            if (ErrorMessage?.Length > ProtocolConstants.MaxErrorMessageLength)
                return false;

            if (string.IsNullOrEmpty(AckId))
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = TransferId.Length * 2;
            size += ChunkId.ToString().Length * 2;
            size += AckId.Length * 2;
            size += (ErrorMessage?.Length ?? 0) * 2;
            size += 8 + 1 + 8; 
            return size + 100; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(TransferId)] = TransferId;
            properties[nameof(ChunkId)] = ChunkId.ToString();
            properties[nameof(SequenceNumber)] = SequenceNumber;
            properties[nameof(IsValid)] = IsValid;
            properties[nameof(AckId)] = AckId;
            properties[nameof(ReceivedAt)] = ReceivedAt;
            
            if (!string.IsNullOrEmpty(ErrorMessage))
                properties[nameof(ErrorMessage)] = ErrorMessage;
            
            return properties;
        }
    }

    public class ChunkResendRequestMessage : RequestMessageBase
    {
        public ChunkResendRequestMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string transferId,
            IEnumerable<long> missingSequenceNumbers,
            string reason = "Chunk validation failed")
            : base(
                MessageTypes.ChunkResendRequest,
                sourcePeerId,
                targetPeerId,
                TimeSpan.FromMinutes(1),
                MessageTypes.FileChunk)
        {
            TransferId = transferId ?? throw new ArgumentNullException(nameof(transferId));
            MissingSequenceNumbers = missingSequenceNumbers?.ToList() ?? throw new ArgumentNullException(nameof(missingSequenceNumbers));
            Reason = reason ?? "Unknown";
            ResendRequestId = Guid.NewGuid().ToString();
            RequestedAt = DateTimeOffset.UtcNow;
        }

        public string TransferId { get; }

        public IReadOnlyList<long> MissingSequenceNumbers { get; }

        public string Reason { get; }

        public string ResendRequestId { get; }

        public DateTimeOffset RequestedAt { get; }

        protected override bool ValidateRequestContent()
        {
            if (string.IsNullOrEmpty(TransferId))
                return false;

            if (MissingSequenceNumbers == null || !MissingSequenceNumbers.Any())
                return false;

            if (MissingSequenceNumbers.Count > ProtocolConstants.MaxConcurrentChunks * 10) 
                return false;

            if (MissingSequenceNumbers.Any(seq => seq < 0))
                return false;

            if (string.IsNullOrEmpty(Reason) || Reason.Length > 200)
                return false;

            if (string.IsNullOrEmpty(ResendRequestId))
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = TransferId.Length * 2;
            size += Reason.Length * 2;
            size += ResendRequestId.Length * 2;
            size += MissingSequenceNumbers.Count * 8; 
            size += 8; 
            return size + 100; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(TransferId)] = TransferId;
            properties[nameof(Reason)] = Reason;
            properties[nameof(ResendRequestId)] = ResendRequestId;
            properties[nameof(RequestedAt)] = RequestedAt;
            properties["MissingChunkCount"] = MissingSequenceNumbers.Count;
            properties["MissingSequenceNumbers"] = JsonSerializer.Serialize(MissingSequenceNumbers);
            return properties;
        }
    }

    public class TransferProgressMessage : ProtocolMessageBase
    {
        public TransferProgressMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string transferId,
            long chunksTransferred,
            long totalChunks,
            long bytesTransferred,
            long totalBytes,
            TimeSpan elapsedTime,
            double? transferRate = null,
            string? correlationId = null)
            : base(MessageTypes.TransferProgress, sourcePeerId, targetPeerId, correlationId, priority: 50)
        {
            TransferId = transferId ?? throw new ArgumentNullException(nameof(transferId));
            ChunksTransferred = chunksTransferred;
            TotalChunks = totalChunks;
            BytesTransferred = bytesTransferred;
            TotalBytes = totalBytes;
            ElapsedTime = elapsedTime;
            TransferRate = transferRate ?? CalculateTransferRate(bytesTransferred, elapsedTime);
            ProgressId = Guid.NewGuid().ToString();
            ProgressTimestamp = DateTimeOffset.UtcNow;
            PercentageComplete = totalBytes > 0 ? (double)bytesTransferred / totalBytes * 100 : 0;
        }

        public string TransferId { get; }

        public long ChunksTransferred { get; }

        public long TotalChunks { get; }

        public long BytesTransferred { get; }

        public long TotalBytes { get; }

        public TimeSpan ElapsedTime { get; }

        public double TransferRate { get; }

        public double PercentageComplete { get; }

        public string ProgressId { get; }

        public DateTimeOffset ProgressTimestamp { get; }

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(TransferId))
                return false;

            if (ChunksTransferred < 0 || ChunksTransferred > TotalChunks)
                return false;

            if (TotalChunks <= 0)
                return false;

            if (BytesTransferred < 0 || BytesTransferred > TotalBytes)
                return false;

            if (TotalBytes <= 0)
                return false;

            if (ElapsedTime < TimeSpan.Zero)
                return false;

            if (TransferRate < 0)
                return false;

            if (PercentageComplete < 0 || PercentageComplete > 100)
                return false;

            if (string.IsNullOrEmpty(ProgressId))
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = TransferId.Length * 2;
            size += ProgressId.Length * 2;
            size += 8 * 4; 
            size += 8 * 3; 
            size += 8; 
            return size + 100; 
        }

        private static double CalculateTransferRate(long bytesTransferred, TimeSpan elapsedTime)
        {
            if (elapsedTime.TotalSeconds <= 0)
                return 0;
            
            return bytesTransferred / elapsedTime.TotalSeconds;
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(TransferId)] = TransferId;
            properties[nameof(ChunksTransferred)] = ChunksTransferred;
            properties[nameof(TotalChunks)] = TotalChunks;
            properties[nameof(BytesTransferred)] = BytesTransferred;
            properties[nameof(TotalBytes)] = TotalBytes;
            properties[nameof(ElapsedTime)] = ElapsedTime.TotalMilliseconds;
            properties[nameof(TransferRate)] = TransferRate;
            properties[nameof(PercentageComplete)] = Math.Round(PercentageComplete, 2);
            properties[nameof(ProgressId)] = ProgressId;
            properties[nameof(ProgressTimestamp)] = ProgressTimestamp;
            return properties;
        }

        public TimeSpan GetEstimatedTimeRemaining()
        {
            if (TransferRate <= 0 || BytesTransferred >= TotalBytes)
                return TimeSpan.Zero;

            var remainingBytes = TotalBytes - BytesTransferred;
            var secondsRemaining = remainingBytes / TransferRate;
            
            return TimeSpan.FromSeconds(Math.Max(0, secondsRemaining));
        }
    }

    public class TransferCompleteMessage : ProtocolMessageBase
    {
        public TransferCompleteMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string transferId,
            bool isSuccess,
            Domain.ValueObjects.Checksum? finalChecksum = null,
            long? totalBytesTransferred = null,
            TimeSpan? totalTime = null,
            string? failureReason = null,
            string? correlationId = null)
            : base(MessageTypes.TransferComplete, sourcePeerId, targetPeerId, correlationId, priority: 220)
        {
            TransferId = transferId ?? throw new ArgumentNullException(nameof(transferId));
            IsSuccess = isSuccess;
            FinalChecksum = finalChecksum;
            TotalBytesTransferred = totalBytesTransferred;
            TotalTime = totalTime;
            FailureReason = failureReason;
            CompletionId = Guid.NewGuid().ToString();
            CompletedAt = DateTimeOffset.UtcNow;
        }

        public string TransferId { get; }

        public bool IsSuccess { get; }

        public Domain.ValueObjects.Checksum? FinalChecksum { get; }

        public long? TotalBytesTransferred { get; }

        public TimeSpan? TotalTime { get; }

        public string? FailureReason { get; }

        public string CompletionId { get; }

        public DateTimeOffset CompletedAt { get; }

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(TransferId))
                return false;

            if (IsSuccess && FinalChecksum == null)
                return false;

            if (!IsSuccess && string.IsNullOrEmpty(FailureReason))
                return false;

            if (FailureReason?.Length > ProtocolConstants.MaxErrorMessageLength)
                return false;

            if (TotalBytesTransferred.HasValue && TotalBytesTransferred < 0)
                return false;

            if (TotalTime.HasValue && TotalTime < TimeSpan.Zero)
                return false;

            if (string.IsNullOrEmpty(CompletionId))
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = TransferId.Length * 2;
            size += CompletionId.Length * 2;
            size += (FinalChecksum?.ToString().Length ?? 0) * 2;
            size += (FailureReason?.Length ?? 0) * 2;
            size += 1; 
            size += 8; 

            if (TotalBytesTransferred.HasValue)
                size += 8;
            
            if (TotalTime.HasValue)
                size += 8;

            return size + 100; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(TransferId)] = TransferId;
            properties[nameof(IsSuccess)] = IsSuccess;
            properties[nameof(CompletionId)] = CompletionId;
            properties[nameof(CompletedAt)] = CompletedAt;

            if (IsSuccess)
            {
                if (FinalChecksum != null)
                    properties[nameof(FinalChecksum)] = FinalChecksum.Value.ToString();
                
                if (TotalBytesTransferred.HasValue)
                    properties[nameof(TotalBytesTransferred)] = TotalBytesTransferred.Value;
                
                if (TotalTime.HasValue)
                    properties[nameof(TotalTime)] = TotalTime.Value.TotalMilliseconds;
            }
            else if (!string.IsNullOrEmpty(FailureReason))
            {
                properties[nameof(FailureReason)] = FailureReason;
            }

            return properties;
        }
    }

    public class TransferCancelMessage : ProtocolMessageBase
    {
        public TransferCancelMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string transferId,
            string reason = "User cancelled",
            bool allowResume = false,
            string? correlationId = null)
            : base(MessageTypes.TransferCancel, sourcePeerId, targetPeerId, correlationId, priority: 250)
        {
            TransferId = transferId ?? throw new ArgumentNullException(nameof(transferId));
            Reason = reason ?? "Unknown";
            AllowResume = allowResume;
            CancellationId = Guid.NewGuid().ToString();
            CancelledAt = DateTimeOffset.UtcNow;
        }

        public string TransferId { get; }

        public string Reason { get; }

        public bool AllowResume { get; }

        public string CancellationId { get; }

        public DateTimeOffset CancelledAt { get; }

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(TransferId))
                return false;

            if (string.IsNullOrEmpty(Reason) || Reason.Length > 200)
                return false;

            if (string.IsNullOrEmpty(CancellationId))
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            return TransferId.Length * 2 + Reason.Length * 2 + CancellationId.Length * 2 + 1 + 8 + 50; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(TransferId)] = TransferId;
            properties[nameof(Reason)] = Reason;
            properties[nameof(AllowResume)] = AllowResume;
            properties[nameof(CancellationId)] = CancellationId;
            properties[nameof(CancelledAt)] = CancelledAt;
            return properties;
        }
    }
}
