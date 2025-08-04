using Akka.Actor;
using Infrastructure.Actors.Storage;
using Infrastructure.Actors.Supervision;
using Infrastructure.Storage.Interfaces;
using Infrastructure.Actors.Storage.Messages;

namespace Infrastructure.Actors.Supervision
{
    public class SimplifiedStorageSupervisorActor : ReceiveActor
    {
        private readonly IFileRepository _fileRepository;
        private readonly IChunkStorage _chunkStorage;
        
        private IActorRef? _fileRepositoryActor;
        private IActorRef? _chunkStorageActor;

        public SimplifiedStorageSupervisorActor(IFileRepository fileRepository, IChunkStorage chunkStorage)
        {
            _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
            _chunkStorage = chunkStorage ?? throw new ArgumentNullException(nameof(chunkStorage));
            
            SetupHandlers();
            CreateChildActors();
        }

        protected override SupervisorStrategy SupervisorStrategy() => 
            StorageSupervisionStrategies.SystemWideStrategy;

        private void SetupHandlers()
        {
            Receive<CreateChildActors>(_ => CreateChildActors());
            Receive<RestartChildActors>(_ => RestartChildActors());
            Receive<GetChildActorRefs>(_ => SendChildActorRefs());
            
            Receive<HealthCheck>(_ => HandleHealthCheck());
            
            ReceiveAny(message => ForwardToAppropriateChild(message));
        }

        private void CreateChildActors()
        {
            try
            {
                _fileRepositoryActor = Context.ActorOf(
                    FileRepositoryActor.Props(_fileRepository),
                    "file-repository");

                _chunkStorageActor = Context.ActorOf(
                    ChunkStorageActor.Props(_chunkStorage),
                    "chunk-storage");

                Context.Watch(_fileRepositoryActor);
                Context.Watch(_chunkStorageActor);
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void RestartChildActors()
        {
            try
            {
                _fileRepositoryActor?.Tell(PoisonPill.Instance);
                _chunkStorageActor?.Tell(PoisonPill.Instance);

                CreateChildActors();
            }
            catch (Exception)
            {
            }
        }

        private void SendChildActorRefs()
        {
            Sender.Tell(new ChildActorRefs(_fileRepositoryActor, _chunkStorageActor));
        }

        private void HandleHealthCheck()
        {
            var fileRepoHealthy = _fileRepositoryActor != null;
            var chunkStorageHealthy = _chunkStorageActor != null;

            var status = new HealthCheckResult
            {
                IsHealthy = fileRepoHealthy && chunkStorageHealthy,
                FileRepositoryHealthy = fileRepoHealthy,
                ChunkStorageHealthy = chunkStorageHealthy,
                CheckTime = DateTime.UtcNow
            };

            Sender.Tell(status);
        }

        private void ForwardToAppropriateChild(object message)
        {
            var targetActor = DetermineTargetActor(message);
            
            if (targetActor != null)
            {
                targetActor.Forward(message);
            }
            else
            {
                Sender.Tell(new StorageError($"Nenhum handler encontrado para mensagem: {message.GetType()}"));
            }
        }

        private IActorRef? DetermineTargetActor(object message)
        {
            return message.GetType().Name switch
            {
                var name when name.Contains("File") => _fileRepositoryActor,
                
                var name when name.Contains("Chunk") => _chunkStorageActor,
                
                _ => null
            };
        }

        public static Props Props(IFileRepository fileRepository, IChunkStorage chunkStorage) =>
            Akka.Actor.Props.Create(() => new SimplifiedStorageSupervisorActor(fileRepository, chunkStorage));
    }

    public record CreateChildActors;
    public record RestartChildActors;
    public record GetChildActorRefs;
    public record ChildActorRefs(IActorRef? FileRepository, IActorRef? ChunkStorage);
    
    public record HealthCheck;
    public record HealthCheckResult
    {
        public bool IsHealthy { get; set; }
        public bool FileRepositoryHealthy { get; set; }
        public bool ChunkStorageHealthy { get; set; }
        public DateTime CheckTime { get; set; }
    }
}
