using Domain.ValueObjects;
using Protocol.Definitions;
using System.Text.Json;

namespace Protocol.Messages.KeyExchange
{
    public class KeyExchangeInitMessage : RequestMessageBase
    {
        public KeyExchangeInitMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            byte[] publicKey,
            string algorithm = "ECDH-P256",
            IEnumerable<string>? supportedAlgorithms = null)
            : base(
                MessageTypes.KeyExchangeInit,
                sourcePeerId,
                targetPeerId,
                TimeSpan.FromSeconds(30),
                MessageTypes.KeyExchangeResponse)
        {
            PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
            Algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
            SupportedAlgorithms = supportedAlgorithms?.ToList() ?? new List<string> { algorithm };
            KeyExchangeId = Guid.NewGuid().ToString();
            Nonce = GenerateNonce();
        }

        public byte[] PublicKey { get; }

        public string Algorithm { get; }

        public IReadOnlyList<string> SupportedAlgorithms { get; }

        public string KeyExchangeId { get; }

        public byte[] Nonce { get; }

        protected override bool ValidateRequestContent()
        {
            if (PublicKey == null || PublicKey.Length == 0)
                return false;

            if (Algorithm == "ECDH-P256" && PublicKey.Length != ProtocolConstants.EcdhPublicKeySize)
                return false;

            if (string.IsNullOrEmpty(Algorithm))
                return false;

            if (SupportedAlgorithms == null || !SupportedAlgorithms.Contains(Algorithm))
                return false;

            if (string.IsNullOrEmpty(KeyExchangeId))
                return false;

            if (Nonce == null || Nonce.Length != 32) 
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            var size = PublicKey.Length;
            size += Algorithm.Length * 2;
            size += KeyExchangeId.Length * 2;
            size += Nonce.Length;
            
            foreach (var alg in SupportedAlgorithms)
            {
                size += alg.Length * 2;
            }
            
            return size + 100; 
        }

        private static byte[] GenerateNonce()
        {
            var nonce = new byte[32];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(nonce);
            return nonce;
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(KeyExchangeId)] = KeyExchangeId;
            properties[nameof(Algorithm)] = Algorithm;
            properties[nameof(SupportedAlgorithms)] = JsonSerializer.Serialize(SupportedAlgorithms);
            properties["PublicKeySize"] = PublicKey.Length;
            properties["NonceSize"] = Nonce.Length;
            return properties;
        }
    }

    public class KeyExchangeResponseMessage : ResponseMessageBase
    {
        public KeyExchangeResponseMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            string keyExchangeId,
            byte[] publicKey,
            string algorithm,
            byte[] nonce)
            : base(MessageTypes.KeyExchangeResponse, sourcePeerId, targetPeerId, correlationId, true)
        {
            KeyExchangeId = keyExchangeId ?? throw new ArgumentNullException(nameof(keyExchangeId));
            PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
            Algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
            Nonce = nonce ?? throw new ArgumentNullException(nameof(nonce));
            ResponseNonce = GenerateNonce();
        }

        public KeyExchangeResponseMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            string keyExchangeId,
            int errorCode,
            string errorMessage)
            : base(MessageTypes.KeyExchangeResponse, sourcePeerId, targetPeerId, correlationId, false, errorCode, errorMessage)
        {
            KeyExchangeId = keyExchangeId ?? throw new ArgumentNullException(nameof(keyExchangeId));
            PublicKey = Array.Empty<byte>();
            Algorithm = string.Empty;
            Nonce = Array.Empty<byte>();
            ResponseNonce = Array.Empty<byte>();
        }

        public string KeyExchangeId { get; }

        public byte[] PublicKey { get; }

        public string Algorithm { get; }

        public byte[] Nonce { get; }

        public byte[] ResponseNonce { get; }

        protected override bool ValidateResponseContent()
        {
            if (string.IsNullOrEmpty(KeyExchangeId))
                return false;

            if (IsSuccess)
            {
                if (PublicKey == null || PublicKey.Length == 0)
                    return false;

                if (Algorithm == "ECDH-P256" && PublicKey.Length != ProtocolConstants.EcdhPublicKeySize)
                    return false;

                if (string.IsNullOrEmpty(Algorithm))
                    return false;

                if (Nonce == null || Nonce.Length != 32)
                    return false;

                if (ResponseNonce == null || ResponseNonce.Length != 32)
                    return false;
            }

            return true;
        }

        protected override int GetContentSize()
        {
            var size = KeyExchangeId.Length * 2;
            
            if (IsSuccess)
            {
                size += PublicKey.Length;
                size += Algorithm.Length * 2;
                size += Nonce.Length;
                size += ResponseNonce.Length;
            }
            
            return size + 100; 
        }

        private static byte[] GenerateNonce()
        {
            var nonce = new byte[32];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(nonce);
            return nonce;
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(KeyExchangeId)] = KeyExchangeId;
            
            if (IsSuccess)
            {
                properties[nameof(Algorithm)] = Algorithm;
                properties["PublicKeySize"] = PublicKey.Length;
                properties["NonceSize"] = Nonce.Length;
                properties["ResponseNonceSize"] = ResponseNonce.Length;
            }
            
            return properties;
        }
    }

    public class KeyExchangeCompleteMessage : ProtocolMessageBase
    {
        public KeyExchangeCompleteMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string correlationId,
            string keyExchangeId,
            byte[] verificationHash,
            byte[] responseNonce)
            : base(MessageTypes.KeyExchangeComplete, sourcePeerId, targetPeerId, correlationId)
        {
            KeyExchangeId = keyExchangeId ?? throw new ArgumentNullException(nameof(keyExchangeId));
            VerificationHash = verificationHash ?? throw new ArgumentNullException(nameof(verificationHash));
            ResponseNonce = responseNonce ?? throw new ArgumentNullException(nameof(responseNonce));
            CompletionId = Guid.NewGuid().ToString();
            KeyEstablished = DateTimeOffset.UtcNow;
        }

        public string KeyExchangeId { get; }

        public byte[] VerificationHash { get; }

        public byte[] ResponseNonce { get; }

        public string CompletionId { get; }

        public DateTimeOffset KeyEstablished { get; }

        protected override bool ValidateContent()
        {
            if (string.IsNullOrEmpty(KeyExchangeId))
                return false;

            if (VerificationHash == null || VerificationHash.Length != 32) 
                return false;

            if (ResponseNonce == null || ResponseNonce.Length != 32)
                return false;

            if (string.IsNullOrEmpty(CompletionId))
                return false;

            if (KeyEstablished == default)
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            return KeyExchangeId.Length * 2 + VerificationHash.Length + ResponseNonce.Length + CompletionId.Length * 2 + 8 + 50; 
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(KeyExchangeId)] = KeyExchangeId;
            properties[nameof(CompletionId)] = CompletionId;
            properties[nameof(KeyEstablished)] = KeyEstablished;
            properties["VerificationHashSize"] = VerificationHash.Length;
            properties["ResponseNonceSize"] = ResponseNonce.Length;
            return properties;
        }
    }

    public class KeyRotationMessage : RequestMessageBase
    {
        public KeyRotationMessage(
            NodeId sourcePeerId,
            NodeId targetPeerId,
            string currentKeyId,
            byte[] newPublicKey,
            string reason = "Scheduled rotation")
            : base(
                MessageTypes.KeyRotation,
                sourcePeerId,
                targetPeerId,
                TimeSpan.FromSeconds(60),
                MessageTypes.KeyExchangeResponse)
        {
            CurrentKeyId = currentKeyId ?? throw new ArgumentNullException(nameof(currentKeyId));
            NewPublicKey = newPublicKey ?? throw new ArgumentNullException(nameof(newPublicKey));
            Reason = reason ?? "Unknown";
            RotationId = Guid.NewGuid().ToString();
            RotationNonce = GenerateNonce();
        }

        public string CurrentKeyId { get; }

        public byte[] NewPublicKey { get; }

        public string Reason { get; }

        public string RotationId { get; }

        public byte[] RotationNonce { get; }

        protected override bool ValidateRequestContent()
        {
            if (string.IsNullOrEmpty(CurrentKeyId))
                return false;

            if (NewPublicKey == null || NewPublicKey.Length != ProtocolConstants.EcdhPublicKeySize)
                return false;

            if (string.IsNullOrEmpty(Reason) || Reason.Length > 200)
                return false;

            if (string.IsNullOrEmpty(RotationId))
                return false;

            if (RotationNonce == null || RotationNonce.Length != 32)
                return false;

            return true;
        }

        protected override int GetContentSize()
        {
            return CurrentKeyId.Length * 2 + NewPublicKey.Length + Reason.Length * 2 + RotationId.Length * 2 + RotationNonce.Length + 100; 
        }

        private static byte[] GenerateNonce()
        {
            var nonce = new byte[32];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(nonce);
            return nonce;
        }

        public override IDictionary<string, object> GetProperties()
        {
            var properties = base.GetProperties();
            properties[nameof(CurrentKeyId)] = CurrentKeyId;
            properties[nameof(Reason)] = Reason;
            properties[nameof(RotationId)] = RotationId;
            properties["NewPublicKeySize"] = NewPublicKey.Length;
            properties["RotationNonceSize"] = RotationNonce.Length;
            return properties;
        }
    }
}
