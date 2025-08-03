using Domain.ValueObjects;

namespace Domain.Services
{
    public interface IKeyExchangeService
    {
        byte[] InitiateKeyExchange(NodeId peerId);
        EncryptionKey CompleteKeyExchange(NodeId peerId, byte[] peerPublicKey);
        (byte[] publicKey, EncryptionKey sharedKey) RespondToKeyExchange(NodeId peerId, byte[] peerPublicKey);
        EncryptionKey RotateKey(NodeId peerId, EncryptionKey currentKey);
    }
}
