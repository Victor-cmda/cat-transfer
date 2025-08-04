using Akka.Actor;
using Application.Messages;
using Domain.Events;

namespace Application.Actors.Coordination
{
    public class SystemCoordinatorActor : ReceiveActor
    {
        private readonly Dictionary<string, IActorRef> _registeredActors = new();
        private readonly HashSet<string> _activeOperations = new();

        public SystemCoordinatorActor()
        {
            SetupHandlers();
        }

        private void SetupHandlers()
        {
            Receive<RegisterActor>(cmd => HandleRegisterActor(cmd));
            Receive<UnregisterActor>(cmd => HandleUnregisterActor(cmd));
            Receive<StartOperation>(cmd => HandleStartOperation(cmd));
            Receive<CompleteOperation>(cmd => HandleCompleteOperation(cmd));
            Receive<GetSystemStatus>(_ => HandleGetSystemStatus());
            Receive<ShutdownSystem>(_ => HandleShutdownSystem());

            ReceiveAny(message => ForwardToAppropriateActor(message));
        }

        private void HandleRegisterActor(RegisterActor cmd)
        {
            _registeredActors[cmd.ActorName] = cmd.ActorRef;
            Context.Watch(cmd.ActorRef);
            
            Sender.Tell(new ActorRegistered(cmd.ActorName, cmd.ActorRef));
        }

        private void HandleUnregisterActor(UnregisterActor cmd)
        {
            if (_registeredActors.Remove(cmd.ActorName))
            {
                Sender.Tell(new ActorUnregistered(cmd.ActorName));
            }
        }

        private void HandleStartOperation(StartOperation cmd)
        {
            if (_activeOperations.Add(cmd.OperationId))
            {
                Sender.Tell(new OperationStarted(cmd.OperationId, DateTime.UtcNow));
            }
            else
            {
                Sender.Tell(new ApplicationError(
                    "OPERATION_ALREADY_ACTIVE",
                    $"Operation {cmd.OperationId} is already active"));
            }
        }

        private void HandleCompleteOperation(CompleteOperation cmd)
        {
            if (_activeOperations.Remove(cmd.OperationId))
            {
                Sender.Tell(new OperationCompleted(cmd.OperationId, DateTime.UtcNow));
            }
        }

        private void HandleGetSystemStatus()
        {
            var status = new SystemStatusResponse(
                RegisteredActorCount: _registeredActors.Count,
                ActiveOperationCount: _activeOperations.Count,
                RegisteredActors: _registeredActors.Keys.ToList(),
                ActiveOperations: _activeOperations.ToList(),
                SystemUptime: DateTime.UtcNow - DateTime.UtcNow.AddHours(-1)
            );

            Sender.Tell(status);
        }

        private void HandleShutdownSystem()
        {
            foreach (var actor in _registeredActors.Values)
            {
                actor.Tell(PoisonPill.Instance);
            }
            
            Self.Tell(PoisonPill.Instance);
        }

        private void ForwardToAppropriateActor(object message)
        {
            var targetActor = DetermineTargetActor(message);
            
            if (targetActor != null)
            {
                targetActor.Forward(message);
            }
            else
            {
                Sender.Tell(new ApplicationError(
                    "NO_HANDLER_FOUND",
                    $"No handler found for message type: {message.GetType().Name}"));
            }
        }

        private IActorRef? DetermineTargetActor(object message)
        {
            return message switch
            {
                IFileTransferMessage => _registeredActors.GetValueOrDefault("file-transfer-supervisor"),
                IChunkMessage => _registeredActors.GetValueOrDefault("chunk-coordinator"),
                INetworkMessage => _registeredActors.GetValueOrDefault("network-coordinator"),
                _ => null
            };
        }

        public static Props Props() => Akka.Actor.Props.Create<SystemCoordinatorActor>();
    }

    public record RegisterActor(string ActorName, IActorRef ActorRef);
    public record UnregisterActor(string ActorName);
    public record StartOperation(string OperationId);
    public record CompleteOperation(string OperationId);
    public record GetSystemStatus;
    public record ShutdownSystem;

    public record ActorRegistered(string ActorName, IActorRef ActorRef) : IApplicationResponse;
    public record ActorUnregistered(string ActorName) : IApplicationResponse;
    public record OperationStarted(string OperationId, DateTime StartedAt) : IApplicationResponse;
    public record OperationCompleted(string OperationId, DateTime CompletedAt) : IApplicationResponse;
    
    public record SystemStatusResponse(
        int RegisteredActorCount,
        int ActiveOperationCount,
        IReadOnlyList<string> RegisteredActors,
        IReadOnlyList<string> ActiveOperations,
        TimeSpan SystemUptime
    ) : IApplicationResponse;
}
