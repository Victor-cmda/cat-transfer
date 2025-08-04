using Akka.Actor;
using Application.Messages;
using Application.Actors.FileTransfer;
using Domain.ValueObjects;
using Domain.Aggregates.FileTransfer;

namespace Application.Actors.FileTransfer
{
    public class FileTransferSupervisorActor : ReceiveActor
    {
        private readonly Dictionary<FileId, IActorRef> _activeTransfers = new();
        private readonly Dictionary<FileId, FileTransferInfo> _transferMetadata = new();

        public FileTransferSupervisorActor()
        {
            SetupHandlers();
        }

        private void SetupHandlers()
        {
            Receive<StartFileTransferCommand>(cmd => HandleStartFileTransfer(cmd));
            Receive<PauseFileTransferCommand>(cmd => HandlePauseFileTransfer(cmd));
            Receive<ResumeFileTransferCommand>(cmd => HandleResumeFileTransfer(cmd));
            Receive<CancelFileTransferCommand>(cmd => HandleCancelFileTransfer(cmd));
            Receive<GetFileTransferStatusQuery>(query => HandleGetFileTransferStatus(query));
            Receive<GetActiveTransfersQuery>(_ => HandleGetActiveTransfers());

            Receive<FileTransferActorTerminated>(msg => HandleTransferActorTerminated(msg));
            
            ReceiveAny(message => ForwardToTransferActor(message));
        }

        private void HandleStartFileTransfer(StartFileTransferCommand cmd)
        {
            if (_activeTransfers.ContainsKey(cmd.FileId))
            {
                Sender.Tell(new ApplicationError(
                    "TRANSFER_ALREADY_ACTIVE",
                    $"Transfer for file {cmd.FileId} is already active"));
                return;
            }

            var transferActor = Context.ActorOf(
                FileTransferActor.Props(cmd.FileId, cmd.Meta, cmd.InitiatorNode),
                $"transfer-{cmd.FileId}");

            _activeTransfers[cmd.FileId] = transferActor;
            _transferMetadata[cmd.FileId] = new FileTransferInfo(
                cmd.FileId,
                cmd.Meta,
                cmd.InitiatorNode,
                DateTime.UtcNow);

            Context.Watch(transferActor);
            
            transferActor.Forward(cmd);
        }

        private void HandlePauseFileTransfer(PauseFileTransferCommand cmd)
        {
            if (_activeTransfers.TryGetValue(cmd.FileId, out var actor))
            {
                actor.Forward(cmd);
            }
            else
            {
                Sender.Tell(new ApplicationError(
                    "TRANSFER_NOT_FOUND",
                    $"No active transfer found for file {cmd.FileId}"));
            }
        }

        private void HandleResumeFileTransfer(ResumeFileTransferCommand cmd)
        {
            if (_activeTransfers.TryGetValue(cmd.FileId, out var actor))
            {
                actor.Forward(cmd);
            }
            else
            {
                Sender.Tell(new ApplicationError(
                    "TRANSFER_NOT_FOUND",
                    $"No active transfer found for file {cmd.FileId}"));
            }
        }

        private void HandleCancelFileTransfer(CancelFileTransferCommand cmd)
        {
            if (_activeTransfers.TryGetValue(cmd.FileId, out var actor))
            {
                actor.Tell(cmd);
                actor.Tell(PoisonPill.Instance);
            }
            else
            {
                Sender.Tell(new ApplicationError(
                    "TRANSFER_NOT_FOUND",
                    $"No active transfer found for file {cmd.FileId}"));
            }
        }

        private void HandleGetFileTransferStatus(GetFileTransferStatusQuery query)
        {
            if (_activeTransfers.TryGetValue(query.FileId, out var actor))
            {
                actor.Forward(query);
            }
            else
            {
                Sender.Tell(new ApplicationError(
                    "TRANSFER_NOT_FOUND",
                    $"No active transfer found for file {query.FileId}"));
            }
        }

        private void HandleGetActiveTransfers()
        {
            var responses = new List<FileTransferStatusResponse>();
            
            foreach (var (fileId, _) in _activeTransfers)
            {
                if (_transferMetadata.TryGetValue(fileId, out var metadata))
                {
                    responses.Add(new FileTransferStatusResponse(
                        fileId,
                        Domain.Enumerations.TransferStatus.InProgress,
                        0.0,
                        new ByteSize(0),
                        metadata.Meta.Size,
                        DateTime.UtcNow - metadata.StartedAt,
                        new List<NodeId>()));
                }
            }

            Sender.Tell(new ActiveTransfersResponse(responses, responses.Count));
        }

        private void HandleTransferActorTerminated(FileTransferActorTerminated msg)
        {
            _activeTransfers.Remove(msg.FileId);
            _transferMetadata.Remove(msg.FileId);
        }

        private void ForwardToTransferActor(object message)
        {
            if (message is IFileTransferMessage ftMessage)
            {
                if (_activeTransfers.TryGetValue(ftMessage.FileId, out var actor))
                {
                    actor.Forward(message);
                    return;
                }
            }

            Sender.Tell(new ApplicationError(
                "MESSAGE_NOT_HANDLED",
                $"Could not route message of type {message.GetType().Name}"));
        }

        public static Props Props() => Akka.Actor.Props.Create<FileTransferSupervisorActor>();
    }

    public record FileTransferInfo(
        FileId FileId,
        FileMeta Meta,
        NodeId? InitiatorNode,
        DateTime StartedAt);

    public record FileTransferActorTerminated(FileId FileId);
}
