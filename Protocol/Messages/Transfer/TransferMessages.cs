using Domain.ValueObjects;
using Domain.Enumerations;
using Protocol.Contracts;
using Protocol.Definitions;
using System.Text.Json;

namespace Protocol.Messages.Transfer
{
    public class TransferRequestMessage : RequestMessageBase
    {
        public TransferRequestMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            FileId fileId,
            string fileName,
            long fileSize,
            Domain.ValueObjects.Checksum fileChecksum,
            IDictionary<string, string>? metadata = null,
            int? preferredChunkSize = null)
            : base(
                MessageTypes.TransferRequest,
                sourcePeerId,
                targetPeerId,
                TimeSpan.FromMinutes(2),
                MessageTypes.TransferResponse,
                priority: 150)
        {
            FileId = fileId;
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            FileSize = fileSize;
            FileChecksum = fileChecksum;
            Metadata = metadata ?? new Dictionary<string, string>();
            PreferredChunkSize = preferredChunkSize ?? ProtocolConstants.DefaultChunkSize;
            TransferId = Guid.NewGuid().ToString();
        }

        public FileId FileId { get; }

        public string FileName { get; }

        public long FileSize { get; }

        public Domain.ValueObjects.Checksum FileChecksum { get; }

        public IDictionary<string, string> Metadata { get; }

        public int PreferredChunkSize { get; }

        public string TransferId { get; }

        protected override bool ValidateRequestContent()
        {
            if (string.IsNullOrEmpty(FileName) || FileName.Length > ProtocolConstants.MaxFileNameLength)
                return false;

            if (FileSize <= 0 || !ProtocolConstants.IsValidFileSize(FileSize))
                return false;

            if (!ProtocolConstants.IsValidChunkSize(PreferredChunkSize))
                return false;

            if (string.IsNullOrEmpty(TransferId))
                return false;

            if (Metadata.Count > 50) 
                return false;

            foreach (var kvp in Metadata)
            {
                if (string.IsNullOrEmpty(kvp.Key) || kvp.Key.Length > 100)
                    return false;
                if (kvp.Value?.Length > 500)
                    return false;
            }

            return true;
        }

        protected override int GetContentSize()
        {
            var size = FileId.ToString().Length * 2;
            size += FileName.Length * 2;
            size += 8; 
            size += FileChecksum.ToString().Length * 2;
            size += TransferId.Length * 2;
            size += 4; 
            
            foreach (var kvp in Metadata)
            {
                size += (kvp.Key.Length + (kvp.Value?.Length ?? 0)) * 2;
            }
            
            return size + 200; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(FileId)] = FileId.ToString();
            properties[nameof(FileName)] = FileName;
            properties[nameof(FileSize)] = FileSize;
            properties[nameof(FileChecksum)] = FileChecksum.ToString();
            properties[nameof(TransferId)] = TransferId;
            properties[nameof(PreferredChunkSize)] = PreferredChunkSize;
            properties[nameof(Metadata)] = JsonSerializer.Serialize(Metadata);
            return properties;
        }
    }

    public class TransferResponseMessage : ResponseMessageBase
    {
        public TransferResponseMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            string transferId,
            bool accepted,
            int? suggestedChunkSize = null,
            IEnumerable<long>? missingChunks = null,
            string? rejectionReason = null)
            : base(MessageTypes.TransferResponse, sourcePeerId, targetPeerId, correlationId, accepted)
        {
            TransferId = transferId ?? throw new ArgumentNullException(nameof(transferId));
            Accepted = accepted;
            SuggestedChunkSize = suggestedChunkSize ?? ProtocolConstants.DefaultChunkSize;
            MissingChunks = missingChunks?.ToList() ?? new List<long>();
            RejectionReason = rejectionReason;
            ResponseId = Guid.NewGuid().ToString();
        }

        public TransferResponseMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            string transferId,
            int errorCode,
            string errorMessage)
            : base(MessageTypes.TransferResponse, sourcePeerId, targetPeerId, correlationId, false, errorCode, errorMessage)
        {
            TransferId = transferId ?? throw new ArgumentNullException(nameof(transferId));
            Accepted = false;
            SuggestedChunkSize = ProtocolConstants.DefaultChunkSize;
            MissingChunks = new List<long>();
            RejectionReason = errorMessage;
            ResponseId = Guid.NewGuid().ToString();
        }

        public string TransferId { get; }

        public bool Accepted { get; }

        public int SuggestedChunkSize { get; }

        public IReadOnlyList<long> MissingChunks { get; }

        public string? RejectionReason { get; }

        public string ResponseId { get; }

        protected override bool ValidateResponseContent()
        {
            if (string.IsNullOrEmpty(TransferId))
                return false;

            if (Accepted && !ProtocolConstants.IsValidChunkSize(SuggestedChunkSize))
                return false;

            if (!Accepted && string.IsNullOrEmpty(RejectionReason))
                return false;

            if (RejectionReason?.Length > ProtocolConstants.MaxErrorMessageLength)
                return false;

            if (MissingChunks.Count > 10000) 
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = TransferId.Length * 2;
            size += ResponseId.Length * 2;
            size += 1 + 4; 
            size += (RejectionReason?.Length ?? 0) * 2;
            size += MissingChunks.Count * 8; 
            return size + 100; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(TransferId)] = TransferId;
            properties[nameof(Accepted)] = Accepted;
            properties[nameof(ResponseId)] = ResponseId;
            
            if (Accepted)
            {
                properties[nameof(SuggestedChunkSize)] = SuggestedChunkSize;
                if (MissingChunks.Any())
                    properties["MissingChunkCount"] = MissingChunks.Count;
            }
            else if (!string.IsNullOrEmpty(RejectionReason))
            {
                properties[nameof(RejectionReason)] = RejectionReason;
            }
            
            return properties;
        }
    }

    public class FileMetadataMessage : ProtocolMessageBase
    {
        public FileMetadataMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string transferId,
            FileId fileId,
            string fileName,
            long fileSize,
            Domain.ValueObjects.Checksum fileChecksum,
            long totalChunks,
            int chunkSize,
            ChecksumAlgorithm checksumAlgorithm,
            IDictionary<string, string>? metadata = null,
            string? correlationId = null)
            : base(MessageTypes.FileMetadata, sourcePeerId, targetPeerId, correlationId, priority: 180)
        {
            TransferId = transferId ?? throw new ArgumentNullException(nameof(transferId));
            FileId = fileId;
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            FileSize = fileSize;
            FileChecksum = fileChecksum;
            TotalChunks = totalChunks;
            ChunkSize = chunkSize;
            ChecksumAlgorithm = checksumAlgorithm;
            Metadata = metadata ?? new Dictionary<string, string>();
            MetadataId = Guid.NewGuid().ToString();
        }

        public string TransferId { get; }

        public FileId FileId { get; }

        public string FileName { get; }

        public long FileSize { get; }

        public Domain.ValueObjects.Checksum FileChecksum { get; }

        public long TotalChunks { get; }

        public int ChunkSize { get; }

        public ChecksumAlgorithm ChecksumAlgorithm { get; }

        public IDictionary<string, string> Metadata { get; }

        public string MetadataId { get; }

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(TransferId))
                return false;

            if (string.IsNullOrEmpty(FileName) || FileName.Length > ProtocolConstants.MaxFileNameLength)
                return false;

            if (FileSize <= 0 || !ProtocolConstants.IsValidFileSize(FileSize))
                return false;

            if (TotalChunks <= 0)
                return false;

            if (!ProtocolConstants.IsValidChunkSize(ChunkSize))
                return false;

            if (string.IsNullOrEmpty(MetadataId))
                return false;

            var expectedChunks = (FileSize + ChunkSize - 1) / ChunkSize;
            if (TotalChunks != expectedChunks)
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = TransferId.Length * 2;
            size += FileId.ToString().Length * 2;
            size += FileName.Length * 2;
            size += FileChecksum.ToString().Length * 2;
            size += MetadataId.Length * 2;
            size += 8 + 8 + 4 + 4; 
            
            foreach (var kvp in Metadata)
            {
                size += (kvp.Key.Length + (kvp.Value?.Length ?? 0)) * 2;
            }
            
            return size + 200; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(TransferId)] = TransferId;
            properties[nameof(FileId)] = FileId.ToString();
            properties[nameof(FileName)] = FileName;
            properties[nameof(FileSize)] = FileSize;
            properties[nameof(FileChecksum)] = FileChecksum.ToString();
            properties[nameof(TotalChunks)] = TotalChunks;
            properties[nameof(ChunkSize)] = ChunkSize;
            properties[nameof(ChecksumAlgorithm)] = ChecksumAlgorithm.ToString();
            properties[nameof(MetadataId)] = MetadataId;
            properties[nameof(Metadata)] = JsonSerializer.Serialize(Metadata);
            return properties;
        }
    }

    public class FileChunkMessage : ProtocolMessageBase, IStreamingMessage
    {
        public FileChunkMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string transferId,
            ChunkId chunkId,
            long sequenceNumber,
            long totalMessages,
            byte[] chunkData,
            Domain.ValueObjects.Checksum chunkChecksum,
            string? correlationId = null)
            : base(MessageTypes.FileChunk, sourcePeerId, targetPeerId, correlationId, priority: 100)
        {
            TransferId = transferId ?? throw new ArgumentNullException(nameof(transferId));
            ChunkId = chunkId;
            SequenceNumber = sequenceNumber;
            TotalMessages = totalMessages;
            ChunkData = chunkData ?? throw new ArgumentNullException(nameof(chunkData));
            ChunkChecksum = chunkChecksum;
            StreamId = transferId; 
            IsLastMessage = sequenceNumber == totalMessages - 1;
            ChunkMessageId = Guid.NewGuid().ToString();
        }

        public string TransferId { get; }

        public ChunkId ChunkId { get; }

        public byte[] ChunkData { get; }

        public Domain.ValueObjects.Checksum ChunkChecksum { get; }

        public string ChunkMessageId { get; }

        public long SequenceNumber { get; }
        public long TotalMessages { get; }
        public string StreamId { get; }
        public bool IsLastMessage { get; }

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(TransferId))
                return false;

            if (ChunkData == null || ChunkData.Length == 0)
                return false;

            if (ChunkData.Length > ProtocolConstants.MaxChunkSize)
                return false;

            if (SequenceNumber < 0 || SequenceNumber >= TotalMessages)
                return false;

            if (TotalMessages <= 0)
                return false;

            if (string.IsNullOrEmpty(ChunkMessageId))
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = TransferId.Length * 2;
            size += ChunkId.ToString().Length * 2;
            size += ChunkChecksum.ToString().Length * 2;
            size += ChunkMessageId.Length * 2;
            size += ChunkData.Length;
            size += 8 + 8 + 1; 
            return size + 100; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(TransferId)] = TransferId;
            properties[nameof(ChunkId)] = ChunkId.ToString();
            properties[nameof(SequenceNumber)] = SequenceNumber;
            properties[nameof(TotalMessages)] = TotalMessages;
            properties[nameof(IsLastMessage)] = IsLastMessage;
            properties[nameof(ChunkMessageId)] = ChunkMessageId;
            properties["ChunkDataSize"] = ChunkData.Length;
            properties[nameof(ChunkChecksum)] = ChunkChecksum.ToString();
            return properties;
        }
    }
}
