using Domain.Services;
using Domain.Services.Algorithms;

namespace Domain.Factories
{
    public static class DomainServiceFactory
    {
        public static IChecksumService CreateChecksumService()
        {
            return new MultiAlgorithmChecksumService();
        }

        public static IEncryptionService CreateEncryptionService()
        {
            return new AesGcmEncryptionService();
        }

        public static IKeyExchangeService CreateKeyExchangeService()
        {
            return new EcdhKeyExchangeService();
        }

        public static IPeerDiscoveryService CreatePeerDiscoveryService()
        {
            return new MulticastPeerDiscoveryService();
        }

        public static IChunkingStrategy CreateDefaultChunkingStrategy()
        {
            return new DefaultChunkingStrategy();
        }

        public static IChunkingStrategy CreateP2PChunkingStrategy(
            int baseChunkSize = 64 * 1024, 
            int maxChunkSize = 1024 * 1024, 
            double networkSpeedFactor = 1.0)
        {
            return new P2PChunkingStrategy(baseChunkSize, maxChunkSize, networkSpeedFactor);
        }

        public static P2PServiceBundle CreateP2PServiceBundle()
        {
            return new P2PServiceBundle
            {
                ChecksumService = CreateChecksumService(),
                EncryptionService = CreateEncryptionService(),
                KeyExchangeService = CreateKeyExchangeService(),
                PeerDiscoveryService = CreatePeerDiscoveryService(),
                ChunkingStrategy = CreateP2PChunkingStrategy()
            };
        }
    }

    public sealed class P2PServiceBundle : IDisposable
    {
        public required IChecksumService ChecksumService { get; init; }
        public required IEncryptionService EncryptionService { get; init; }
        public required IKeyExchangeService KeyExchangeService { get; init; }
        public required IPeerDiscoveryService PeerDiscoveryService { get; init; }
        public required IChunkingStrategy ChunkingStrategy { get; init; }

        public void Dispose()
        {
            if (KeyExchangeService is IDisposable keyExchangeDisposable)
                keyExchangeDisposable.Dispose();
            
            if (PeerDiscoveryService is IDisposable peerDiscoveryDisposable)
                peerDiscoveryDisposable.Dispose();
        }
    }
}
