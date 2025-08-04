using Akka.Actor;
using Infrastructure.Storage.Interfaces;
using Infrastructure.Actors.Storage;

namespace Infrastructure.Actors
{
    public class StorageActorSystem
    {
        private readonly ActorSystem _actorSystem;
        private readonly IActorRef _fileRepositoryActor;
        private readonly IActorRef _chunkStorageActor;

        public StorageActorSystem(
            ActorSystem actorSystem,
            IFileRepository fileRepository,
            IChunkStorage chunkStorage)
        {
            _actorSystem = actorSystem ?? throw new ArgumentNullException(nameof(actorSystem));

            _fileRepositoryActor = _actorSystem.ActorOf(
                FileRepositoryActor.Props(fileRepository), 
                "file-repository");
                
            _chunkStorageActor = _actorSystem.ActorOf(
                ChunkStorageActor.Props(chunkStorage), 
                "chunk-storage");
        }

        public IActorRef FileRepository => _fileRepositoryActor;

        public IActorRef ChunkStorage => _chunkStorageActor;

        public ActorSystem ActorSystem => _actorSystem;

        public ActorSystemStats GetStats()
        {
            var fileRepoStats = GetActorStats(_fileRepositoryActor);
            var chunkStorageStats = GetActorStats(_chunkStorageActor);

            return new ActorSystemStats
            {
                FileRepositoryStats = fileRepoStats,
                ChunkStorageStats = chunkStorageStats,
                TotalActors = 2,
                SystemUptime = DateTime.UtcNow - DateTime.UtcNow.AddHours(-1) // Simplified uptime
            };
        }

        private ActorStats GetActorStats(IActorRef actorRef)
        {
            try
            {
                return new ActorStats
                {
                    Name = actorRef.Path.Name,
                    IsActive = true,
                    Path = actorRef.Path.ToString()
                };
            }
            catch
            {
                return new ActorStats
                {
                    Name = actorRef.Path.Name,
                    IsActive = false,
                    Path = actorRef.Path.ToString()
                };
            }
        }

        public void Dispose()
        {
            _actorSystem?.Terminate();
        }
    }

    public class ActorSystemStats
    {
        public ActorStats FileRepositoryStats { get; set; } = new();
        public ActorStats ChunkStorageStats { get; set; } = new();
        public int TotalActors { get; set; }
        public TimeSpan SystemUptime { get; set; }
    }

    public class ActorStats
    {
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string Path { get; set; } = string.Empty;
    }
}
