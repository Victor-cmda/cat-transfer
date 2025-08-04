using Domain.ValueObjects;
using Domain.Enumerations;
using Protocol.Definitions;
using System.Text.Json;

namespace Protocol.Messages.Checksum
{
    public class ChecksumRequestMessage : RequestMessageBase
    {
        public ChecksumRequestMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            FileId fileId,
            ChecksumAlgorithm algorithm,
            IEnumerable<long>? specificChunks = null,
            string? transferId = null)
            : base(
                MessageTypes.ChecksumRequest,
                sourcePeerId,
                targetPeerId,
                TimeSpan.FromMinutes(1),
                MessageTypes.ChecksumResponse)
        {
            FileId = fileId;
            Algorithm = algorithm;
            SpecificChunks = specificChunks?.ToList() ?? new List<long>();
            TransferId = transferId;
            ChecksumRequestId = Guid.NewGuid().ToString();
            RequestedAt = DateTimeOffset.UtcNow;
            VerifyWholeFile = !SpecificChunks.Any();
        }

        public FileId FileId { get; }

        public ChecksumAlgorithm Algorithm { get; }

        public IReadOnlyList<long> SpecificChunks { get; }

        public string? TransferId { get; }

        public bool VerifyWholeFile { get; }

        public string ChecksumRequestId { get; }

        public DateTimeOffset RequestedAt { get; }

        protected override bool ValidateRequestContent()
        {
            if (SpecificChunks.Any(chunk => chunk < 0))
                return false;

            if (SpecificChunks.Count > 1000) 
                return false;

            if (string.IsNullOrEmpty(ChecksumRequestId))
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = FileId.ToString().Length * 2;
            size += ChecksumRequestId.Length * 2;
            size += (TransferId?.Length ?? 0) * 2;
            size += 4; 
            size += 1; 
            size += 8; 
            size += SpecificChunks.Count * 8; 
            return size + 100; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(FileId)] = FileId.ToString();
            properties[nameof(Algorithm)] = Algorithm.ToString();
            properties[nameof(VerifyWholeFile)] = VerifyWholeFile;
            properties[nameof(ChecksumRequestId)] = ChecksumRequestId;
            properties[nameof(RequestedAt)] = RequestedAt;
            
            if (!string.IsNullOrEmpty(TransferId))
                properties[nameof(TransferId)] = TransferId;
            
            if (SpecificChunks.Any())
            {
                properties["ChunkCount"] = SpecificChunks.Count;
                properties[nameof(SpecificChunks)] = JsonSerializer.Serialize(SpecificChunks);
            }
            
            return properties;
        }
    }

    public class ChecksumResponseMessage : ResponseMessageBase
    {
        public ChecksumResponseMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            string checksumRequestId,
            FileId fileId,
            ChecksumAlgorithm algorithm,
            Domain.ValueObjects.Checksum fileChecksum,
            IDictionary<long, Domain.ValueObjects.Checksum>? chunkChecksums = null,
            TimeSpan? verificationTime = null)
            : base(MessageTypes.ChecksumResponse, sourcePeerId, targetPeerId, correlationId, true)
        {
            ChecksumRequestId = checksumRequestId ?? throw new ArgumentNullException(nameof(checksumRequestId));
            FileId = fileId;
            Algorithm = algorithm;
            FileChecksum = fileChecksum;
            ChunkChecksums = chunkChecksums ?? new Dictionary<long, Domain.ValueObjects.Checksum>();
            VerificationTime = verificationTime;
            ChecksumResponseId = Guid.NewGuid().ToString();
            VerifiedAt = DateTimeOffset.UtcNow;
        }

        public ChecksumResponseMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            string checksumRequestId,
            FileId fileId,
            int errorCode,
            string errorMessage)
            : base(MessageTypes.ChecksumResponse, sourcePeerId, targetPeerId, correlationId, false, errorCode, errorMessage)
        {
            ChecksumRequestId = checksumRequestId ?? throw new ArgumentNullException(nameof(checksumRequestId));
            FileId = fileId;
            Algorithm = ChecksumAlgorithm.Shake256; 
            FileChecksum = new Domain.ValueObjects.Checksum(Array.Empty<byte>(), ChecksumAlgorithm.Shake256);
            ChunkChecksums = new Dictionary<long, Domain.ValueObjects.Checksum>();
            VerificationTime = null;
            ChecksumResponseId = Guid.NewGuid().ToString();
            VerifiedAt = DateTimeOffset.UtcNow;
        }

        public string ChecksumRequestId { get; }

        public FileId FileId { get; }

        public ChecksumAlgorithm Algorithm { get; }

        public Domain.ValueObjects.Checksum FileChecksum { get; }

        public IDictionary<long, Domain.ValueObjects.Checksum> ChunkChecksums { get; }

        public TimeSpan? VerificationTime { get; }

        public string ChecksumResponseId { get; }

        public DateTimeOffset VerifiedAt { get; }

        protected override bool ValidateResponseContent()
        {
            if (string.IsNullOrEmpty(ChecksumRequestId))
                return false;

            if (IsSuccess)
            {
                if (FileChecksum.Equals(default(Domain.ValueObjects.Checksum)))
                    return false;

                if (VerificationTime.HasValue && VerificationTime < TimeSpan.Zero)
                    return false;

                foreach (var kvp in ChunkChecksums)
                {
                    if (kvp.Key < 0 || kvp.Value.Equals(default(Domain.ValueObjects.Checksum)))
                        return false;
                }
            }

            if (string.IsNullOrEmpty(ChecksumResponseId))
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = ChecksumRequestId.Length * 2;
            size += FileId.ToString().Length * 2;
            size += ChecksumResponseId.Length * 2;
            size += 4; 
            size += 8; 

            if (IsSuccess)
            {
                size += FileChecksum.ToString().Length * 2;
                
                if (VerificationTime.HasValue)
                    size += 8;

                foreach (var kvp in ChunkChecksums)
                {
                    size += 8; 
                    size += kvp.Value.ToString().Length * 2;
                }
            }

            return size + 100; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(ChecksumRequestId)] = ChecksumRequestId;
            properties[nameof(FileId)] = FileId.ToString();
            properties[nameof(Algorithm)] = Algorithm.ToString();
            properties[nameof(ChecksumResponseId)] = ChecksumResponseId;
            properties[nameof(VerifiedAt)] = VerifiedAt;

            if (IsSuccess)
            {
                properties[nameof(FileChecksum)] = FileChecksum.ToString();
                
                if (VerificationTime.HasValue)
                    properties[nameof(VerificationTime)] = VerificationTime.Value.TotalMilliseconds;
                
                if (ChunkChecksums.Any())
                {
                    properties["ChunkChecksumCount"] = ChunkChecksums.Count;
                    properties[nameof(ChunkChecksums)] = JsonSerializer.Serialize(
                        ChunkChecksums.ToDictionary(
                            kvp => kvp.Key.ToString(),
                            kvp => kvp.Value.ToString()
                        )
                    );
                }
            }

            return properties;
        }
    }

    public class ChunkChecksumMessage : ProtocolMessageBase
    {
        public ChunkChecksumMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string transferId,
            ChunkId chunkId,
            long sequenceNumber,
            Domain.ValueObjects.Checksum chunkChecksum,
            ChecksumAlgorithm algorithm,
            bool isValid,
            string? correlationId = null,
            string? validationError = null)
            : base(MessageTypes.ChunkChecksum, sourcePeerId, targetPeerId, correlationId, priority: 150)
        {
            TransferId = transferId ?? throw new ArgumentNullException(nameof(transferId));
            ChunkId = chunkId;
            SequenceNumber = sequenceNumber;
            ChunkChecksum = chunkChecksum;
            Algorithm = algorithm;
            IsValid = isValid;
            ValidationError = validationError;
            ChecksumMessageId = Guid.NewGuid().ToString();
            ValidatedAt = DateTimeOffset.UtcNow;
        }

        public string TransferId { get; }

        public ChunkId ChunkId { get; }

        public long SequenceNumber { get; }

        public Domain.ValueObjects.Checksum ChunkChecksum { get; }

        public ChecksumAlgorithm Algorithm { get; }

        public new bool IsValid { get; }

        public string? ValidationError { get; }

        public string ChecksumMessageId { get; }

        public DateTimeOffset ValidatedAt { get; }

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(TransferId))
                return false;

            if (SequenceNumber < 0)
                return false;

            if (ChunkChecksum.Equals(default(Domain.ValueObjects.Checksum)))
                return false;

            if (!IsValid && string.IsNullOrEmpty(ValidationError))
                return false;

            if (ValidationError?.Length > ProtocolConstants.MaxErrorMessageLength)
                return false;

            if (string.IsNullOrEmpty(ChecksumMessageId))
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = TransferId.Length * 2;
            size += ChunkId.ToString().Length * 2;
            size += ChunkChecksum.ToString().Length * 2;
            size += ChecksumMessageId.Length * 2;
            size += (ValidationError?.Length ?? 0) * 2;
            size += 8 + 4 + 1 + 8; 
            return size + 100; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(TransferId)] = TransferId;
            properties[nameof(ChunkId)] = ChunkId.ToString();
            properties[nameof(SequenceNumber)] = SequenceNumber;
            properties[nameof(ChunkChecksum)] = ChunkChecksum.ToString();
            properties[nameof(Algorithm)] = Algorithm.ToString();
            properties[nameof(IsValid)] = IsValid;
            properties[nameof(ChecksumMessageId)] = ChecksumMessageId;
            properties[nameof(ValidatedAt)] = ValidatedAt;

            if (!string.IsNullOrEmpty(ValidationError))
                properties[nameof(ValidationError)] = ValidationError;

            return properties;
        }
    }
}
