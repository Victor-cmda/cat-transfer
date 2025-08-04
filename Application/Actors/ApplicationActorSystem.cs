using Akka.Actor;
using Application.Actors.FileTransfer;
using Application.Actors.Network;
using Application.Actors.Coordination;

namespace Application.Actors
{
    public class ApplicationActorSystem
    {
        private readonly ActorSystem _actorSystem;
        private readonly IActorRef _fileTransferSupervisor;
        private readonly IActorRef _networkCoordinator;
        private readonly IActorRef _systemCoordinator;

        public ApplicationActorSystem(ActorSystem actorSystem)
        {
            _actorSystem = actorSystem ?? throw new ArgumentNullException(nameof(actorSystem));

            _systemCoordinator = _actorSystem.ActorOf(
                SystemCoordinatorActor.Props(), 
                "system-coordinator");

            _fileTransferSupervisor = _actorSystem.ActorOf(
                FileTransferSupervisorActor.Props(), 
                "file-transfer-supervisor");

            _networkCoordinator = _actorSystem.ActorOf(
                NetworkCoordinatorActor.Props(), 
                "network-coordinator");
        }

        public IActorRef SystemCoordinator => _systemCoordinator;
        public IActorRef FileTransferSupervisor => _fileTransferSupervisor;
        public IActorRef NetworkCoordinator => _networkCoordinator;
        public ActorSystem ActorSystem => _actorSystem;

        public ApplicationStats GetApplicationStats()
        {
            return new ApplicationStats
            {
                SystemCoordinatorActive = IsActorActive(_systemCoordinator),
                FileTransferSupervisorActive = IsActorActive(_fileTransferSupervisor),
                NetworkCoordinatorActive = IsActorActive(_networkCoordinator),
                SystemUptime = DateTime.UtcNow - DateTime.UtcNow.AddHours(-1)
            };
        }

        private bool IsActorActive(IActorRef actorRef)
        {
            return !actorRef.IsNobody();
        }

        public void Dispose()
        {
            _actorSystem?.Terminate();
        }
    }

    public class ApplicationStats
    {
        public bool SystemCoordinatorActive { get; set; }
        public bool FileTransferSupervisorActive { get; set; }
        public bool NetworkCoordinatorActive { get; set; }
        public TimeSpan SystemUptime { get; set; }
    }
}
