namespace Protocol.Exceptions
{
    public abstract class ProtocolException : Exception
    {
        protected ProtocolException(string message) : base(message)
        {
        }

        protected ProtocolException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public abstract int ErrorCode { get; }

        public virtual bool IsRecoverable => false;

        public virtual IDictionary<string, object> ErrorDetails => new Dictionary<string, object>();
    }

    public class ProtocolVersionException : ProtocolException
    {
        public ProtocolVersionException(string localVersion, string remoteVersion)
            : base($"Protocol version mismatch: local={localVersion}, remote={remoteVersion}")
        {
            LocalVersion = localVersion;
            RemoteVersion = remoteVersion;
        }

        public ProtocolVersionException(string localVersion, string remoteVersion, Exception innerException)
            : base($"Protocol version mismatch: local={localVersion}, remote={remoteVersion}", innerException)
        {
            LocalVersion = localVersion;
            RemoteVersion = remoteVersion;
        }

        public string LocalVersion { get; }
        public string RemoteVersion { get; }

        public override int ErrorCode => 1001;

        public override IDictionary<string, object> ErrorDetails => new Dictionary<string, object>
        {
            { "LocalVersion", LocalVersion },
            { "RemoteVersion", RemoteVersion }
        };
    }

    public class MessageSerializationException : ProtocolException
    {
        public MessageSerializationException(string messageType, string operation)
            : base($"Failed to {operation} message of type '{messageType}'")
        {
            MessageType = messageType;
            Operation = operation;
        }

        public MessageSerializationException(string messageType, string operation, Exception innerException)
            : base($"Failed to {operation} message of type '{messageType}'", innerException)
        {
            MessageType = messageType;
            Operation = operation;
        }

        public string MessageType { get; }
        public string Operation { get; }

        public override int ErrorCode => 1007;

        public override IDictionary<string, object> ErrorDetails => new Dictionary<string, object>
        {
            { "MessageType", MessageType },
            { "Operation", Operation }
        };
    }

    public class HandshakeException : ProtocolException
    {
        public HandshakeException(string reason)
            : base($"Handshake failed: {reason}")
        {
            Reason = reason;
        }

        public HandshakeException(string reason, Exception innerException)
            : base($"Handshake failed: {reason}", innerException)
        {
            Reason = reason;
        }

        public string Reason { get; }

        public override int ErrorCode => 1002;
        public override bool IsRecoverable => true;

        public override IDictionary<string, object> ErrorDetails => new Dictionary<string, object>
        {
            { "Reason", Reason }
        };
    }

    public class KeyExchangeException : ProtocolException
    {
        public KeyExchangeException(string phase, string reason)
            : base($"Key exchange failed in {phase} phase: {reason}")
        {
            Phase = phase;
            Reason = reason;
        }

        public KeyExchangeException(string phase, string reason, Exception innerException)
            : base($"Key exchange failed in {phase} phase: {reason}", innerException)
        {
            Phase = phase;
            Reason = reason;
        }

        public string Phase { get; }
        public string Reason { get; }

        public override int ErrorCode => 1003;
        public override bool IsRecoverable => true;

        public override IDictionary<string, object> ErrorDetails => new Dictionary<string, object>
        {
            { "Phase", Phase },
            { "Reason", Reason }
        };
    }

    public class NetworkException : ProtocolException
    {
        public NetworkException(string operation, string reason)
            : base($"Network operation '{operation}' failed: {reason}")
        {
            Operation = operation;
            Reason = reason;
        }

        public NetworkException(string operation, string reason, Exception innerException)
            : base($"Network operation '{operation}' failed: {reason}", innerException)
        {
            Operation = operation;
            Reason = reason;
        }

        public string Operation { get; }
        public string Reason { get; }

        public override int ErrorCode => 1006;
        public override bool IsRecoverable => true;

        public override IDictionary<string, object> ErrorDetails => new Dictionary<string, object>
        {
            { "Operation", Operation },
            { "Reason", Reason }
        };
    }

    public class MessageValidationException : ProtocolException
    {
        public MessageValidationException(string messageType, string validationError)
            : base($"Message validation failed for '{messageType}': {validationError}")
        {
            MessageType = messageType;
            ValidationError = validationError;
        }

        public MessageValidationException(string messageType, string validationError, Exception innerException)
            : base($"Message validation failed for '{messageType}': {validationError}", innerException)
        {
            MessageType = messageType;
            ValidationError = validationError;
        }

        public string MessageType { get; }
        public string ValidationError { get; }

        public override int ErrorCode => 1007;

        public override IDictionary<string, object> ErrorDetails => new Dictionary<string, object>
        {
            { "MessageType", MessageType },
            { "ValidationError", ValidationError }
        };
    }

    public class CryptographyException : ProtocolException
    {
        public CryptographyException(string operation, string algorithm, string reason)
            : base($"Cryptographic operation '{operation}' failed for algorithm '{algorithm}': {reason}")
        {
            Operation = operation;
            Algorithm = algorithm;
            Reason = reason;
        }

        public CryptographyException(string operation, string algorithm, string reason, Exception innerException)
            : base($"Cryptographic operation '{operation}' failed for algorithm '{algorithm}': {reason}", innerException)
        {
            Operation = operation;
            Algorithm = algorithm;
            Reason = reason;
        }

        public string Operation { get; }
        public string Algorithm { get; }
        public string Reason { get; }

        public override int ErrorCode => 1003;

        public override IDictionary<string, object> ErrorDetails => new Dictionary<string, object>
        {
            { "Operation", Operation },
            { "Algorithm", Algorithm },
            { "Reason", Reason }
        };
    }

    public class ProtocolTimeoutException : ProtocolException
    {
        public ProtocolTimeoutException(string operation, TimeSpan timeout)
            : base($"Operation '{operation}' timed out after {timeout.TotalSeconds:F1} seconds")
        {
            Operation = operation;
            Timeout = timeout;
        }

        public ProtocolTimeoutException(string operation, TimeSpan timeout, Exception innerException)
            : base($"Operation '{operation}' timed out after {timeout.TotalSeconds:F1} seconds", innerException)
        {
            Operation = operation;
            Timeout = timeout;
        }

        public string Operation { get; }
        public TimeSpan Timeout { get; }

        public override int ErrorCode => 1006;
        public override bool IsRecoverable => true;

        public override IDictionary<string, object> ErrorDetails => new Dictionary<string, object>
        {
            { "Operation", Operation },
            { "TimeoutSeconds", Timeout.TotalSeconds }
        };
    }

    public class TransferException : ProtocolException
    {
        public TransferException(string transferId, string operation, string reason)
            : base($"Transfer '{transferId}' failed during '{operation}': {reason}")
        {
            TransferId = transferId;
            Operation = operation;
            Reason = reason;
        }

        public TransferException(string transferId, string operation, string reason, Exception innerException)
            : base($"Transfer '{transferId}' failed during '{operation}': {reason}", innerException)
        {
            TransferId = transferId;
            Operation = operation;
            Reason = reason;
        }

        public string TransferId { get; }
        public string Operation { get; }
        public string Reason { get; }

        public override int ErrorCode => 1004;
        public override bool IsRecoverable => true;

        public override IDictionary<string, object> ErrorDetails => new Dictionary<string, object>
        {
            { "TransferId", TransferId },
            { "Operation", Operation },
            { "Reason", Reason }
        };
    }

    public class ChecksumException : ProtocolException
    {
        public ChecksumException(string expected, string actual, string algorithm)
            : base($"Checksum verification failed using {algorithm}: expected={expected}, actual={actual}")
        {
            Expected = expected;
            Actual = actual;
            Algorithm = algorithm;
        }

        public ChecksumException(string expected, string actual, string algorithm, Exception innerException)
            : base($"Checksum verification failed using {algorithm}: expected={expected}, actual={actual}", innerException)
        {
            Expected = expected;
            Actual = actual;
            Algorithm = algorithm;
        }

        public string Expected { get; }
        public string Actual { get; }
        public string Algorithm { get; }

        public override int ErrorCode => 1005;

        public override IDictionary<string, object> ErrorDetails => new Dictionary<string, object>
        {
            { "Expected", Expected },
            { "Actual", Actual },
            { "Algorithm", Algorithm }
        };
    }

    public class PeerNotConnectedException : ProtocolException
    {
        public PeerNotConnectedException(string peerId)
            : base($"Peer {peerId} is not connected")
        {
            PeerId = peerId;
        }

        public PeerNotConnectedException(string peerId, Exception innerException)
            : base($"Peer {peerId} is not connected", innerException)
        {
            PeerId = peerId;
        }

        public string PeerId { get; }

        public override int ErrorCode => 404;

        public override bool IsRecoverable => true;

        public override IDictionary<string, object> ErrorDetails => new Dictionary<string, object>
        {
            { "PeerId", PeerId }
        };
    }
}
