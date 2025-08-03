using Domain.Events;
using Domain.ValueObjects;
using System.Security.Cryptography;

namespace Domain.Services.Algorithms
{
    public sealed class EcdhKeyExchangeService : IKeyExchangeService, IDisposable
    {
        private readonly Dictionary<NodeId, (ECDiffieHellman ecdh, DateTimeOffset createdAt)> _activeExchanges = new();
        private readonly object _lock = new();
        private readonly TimeSpan _exchangeTimeout = TimeSpan.FromMinutes(5);

        public byte[] InitiateKeyExchange(NodeId peerId)
        {
            lock (_lock)
            {
                CleanupExpiredExchanges();
                
                var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
                _activeExchanges[peerId] = (ecdh, DateTimeOffset.UtcNow);

                var publicKey = ecdh.PublicKey.ExportSubjectPublicKeyInfo();
                DomainEvents.Raise(new KeyExchangeInitiated(peerId, publicKey.Length));
                return publicKey;
            }
        }

        public EncryptionKey CompleteKeyExchange(NodeId peerId, byte[] peerPublicKey)
        {
            lock (_lock)
            {
                if (!_activeExchanges.TryGetValue(peerId, out var exchange))
                {
                    throw new InvalidOperationException($"No active key exchange found for peer {peerId}");
                }

                try
                {
                    using var peerEcdh = ECDiffieHellman.Create();
                    peerEcdh.ImportSubjectPublicKeyInfo(peerPublicKey, out _);

                    var sharedSecret = exchange.ecdh.DeriveKeyMaterial(peerEcdh.PublicKey);
                    
                    var encryptionKey = DeriveEncryptionKey(sharedSecret, peerId);
                    
                    exchange.ecdh.Dispose();
                    _activeExchanges.Remove(peerId);
                    
                    DomainEvents.Raise(new KeyExchangeCompleted(peerId, encryptionKey));
                    return encryptionKey;
                }
                catch (Exception ex)
                {
                    exchange.ecdh.Dispose();
                    _activeExchanges.Remove(peerId);
                    DomainEvents.Raise(new KeyExchangeFailed(peerId, ex.Message));
                    throw;
                }
            }
        }

        public (byte[] publicKey, EncryptionKey sharedKey) RespondToKeyExchange(NodeId peerId, byte[] peerPublicKey)
        {
            using var ourEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            
            using var peerEcdh = ECDiffieHellman.Create();
            peerEcdh.ImportSubjectPublicKeyInfo(peerPublicKey, out _);

            var sharedSecret = ourEcdh.DeriveKeyMaterial(peerEcdh.PublicKey);
            
            var encryptionKey = DeriveEncryptionKey(sharedSecret, peerId);
            
            var ourPublicKey = ourEcdh.PublicKey.ExportSubjectPublicKeyInfo();
            
            DomainEvents.Raise(new KeyExchangeResponded(peerId, encryptionKey));
            return (ourPublicKey, encryptionKey);
        }

        public EncryptionKey RotateKey(NodeId peerId, EncryptionKey currentKey)
        {
            var info = System.Text.Encoding.UTF8.GetBytes($"rotation-{peerId}-{DateTimeOffset.UtcNow.Ticks}");
            var newKeyMaterial = HKDF.DeriveKey(HashAlgorithmName.SHA256, currentKey.Key, 32, Array.Empty<byte>(), info);
            
            var newKey = EncryptionKey.Create(newKeyMaterial, currentKey.Algorithm);
            DomainEvents.Raise(new EncryptionKeyRotated(peerId, newKey));
            
            return newKey;
        }

        private static EncryptionKey DeriveEncryptionKey(byte[] sharedSecret, NodeId peerId)
        {
            var salt = System.Text.Encoding.UTF8.GetBytes("cat-transfer-p2p-salt");
            var info = System.Text.Encoding.UTF8.GetBytes($"aes-gcm-key-{peerId}");
            
            var keyMaterial = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, salt, info);
            return EncryptionKey.Create(keyMaterial, "AES-256-GCM");
        }

        private void CleanupExpiredExchanges()
        {
            var now = DateTimeOffset.UtcNow;
            var expiredKeys = _activeExchanges
                .Where(kvp => now - kvp.Value.createdAt > _exchangeTimeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                if (_activeExchanges.TryGetValue(key, out var exchange))
                {
                    exchange.ecdh.Dispose();
                    _activeExchanges.Remove(key);
                    DomainEvents.Raise(new KeyExchangeFailed(key, "Exchange timeout"));
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var exchange in _activeExchanges.Values)
                {
                    exchange.ecdh.Dispose();
                }
                _activeExchanges.Clear();
            }
        }
    }
}
