using Akka.Actor;
using Application.Messages;
using Domain.ValueObjects;
using Domain.Aggregates.FileTransfer;
using Domain.Enumerations;
using Domain.Events;
using AppMessages = Application.Messages;

namespace Application.Actors.FileTransfer
{
    public class FileTransferActor : ReceiveActor
    {
        private readonly FileId _fileId;
        private readonly FileMeta _fileMeta;
        private readonly NodeId? _initiatorNode;
        private Domain.Aggregates.FileTransfer.FileTransfer? _fileTransfer;
        private readonly Dictionary<ChunkId, IActorRef> _chunkActors = new();

        public FileTransferActor(FileId fileId, FileMeta fileMeta, NodeId? initiatorNode)
        {
            _fileId = fileId;
            _fileMeta = fileMeta;
            _initiatorNode = initiatorNode;
            
            SetupHandlers();
            InitializeFileTransfer();
        }

        private void SetupHandlers()
        {
            Receive<StartFileTransferCommand>(cmd => HandleStartTransfer(cmd));
            Receive<PauseFileTransferCommand>(cmd => HandlePauseTransfer(cmd));
            Receive<ResumeFileTransferCommand>(cmd => HandleResumeTransfer(cmd));
            Receive<CancelFileTransferCommand>(cmd => HandleCancelTransfer(cmd));
            Receive<GetFileTransferStatusQuery>(_ => HandleGetStatus());
            
            Receive<RequestChunkCommand>(cmd => HandleRequestChunk(cmd));
            Receive<StoreChunkCommand>(cmd => HandleStoreChunk(cmd));
            
            Receive<ChunkCompleted>(evt => HandleChunkCompleted(evt));
            Receive<ChunkFailed>(evt => HandleChunkFailed(evt));
        }

        private void InitializeFileTransfer()
        {
            var chunkingStrategy = new Domain.Services.DefaultChunkingStrategy();
            var chunks = chunkingStrategy.CreateChunks(_fileId, _fileMeta.Size, _fileMeta.ChunkSize)
                .Select(chunkId => new ChunkState(chunkId))
                .ToList();

            _fileTransfer = new Domain.Aggregates.FileTransfer.FileTransfer(
                _fileId,
                _fileMeta,
                chunks,
                _initiatorNode);
        }

        private void HandleStartTransfer(StartFileTransferCommand cmd)
        {
            try
            {
                _fileTransfer?.Start(_initiatorNode);
                
                Sender.Tell(new AppMessages.FileTransferStarted(
                    _fileId,
                    _initiatorNode ?? NodeId.NewGuid(),
                    DateTime.UtcNow));

                CreateChunkActors();
            }
            catch (Exception ex)
            {
                Sender.Tell(new ApplicationError(
                    "START_TRANSFER_FAILED",
                    $"Failed to start transfer for file {_fileId}",
                    ex));
            }
        }

        private void HandlePauseTransfer(PauseFileTransferCommand cmd)
        {
            try
            {
                _fileTransfer?.Pause(cmd.RequestingNode);
                
                Sender.Tell(new AppMessages.FileTransferPaused(
                    _fileId,
                    cmd.RequestingNode,
                    DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ApplicationError(
                    "PAUSE_TRANSFER_FAILED",
                    $"Failed to pause transfer for file {_fileId}",
                    ex));
            }
        }

        private void HandleResumeTransfer(ResumeFileTransferCommand cmd)
        {
            try
            {
                _fileTransfer?.Resume(cmd.RequestingNode);
                
                Sender.Tell(new AppMessages.FileTransferResumed(
                    _fileId,
                    cmd.RequestingNode,
                    DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ApplicationError(
                    "RESUME_TRANSFER_FAILED",
                    $"Failed to resume transfer for file {_fileId}",
                    ex));
            }
        }

        private void HandleCancelTransfer(CancelFileTransferCommand cmd)
        {
            try
            {
                _fileTransfer?.Cancel(cmd.RequestingNode);
                
                foreach (var chunkActor in _chunkActors.Values)
                {
                    chunkActor.Tell(PoisonPill.Instance);
                }

                Sender.Tell(new AppMessages.FileTransferCancelled(
                    _fileId,
                    cmd.RequestingNode,
                    DateTime.UtcNow));

                Context.Parent.Tell(new FileTransferActorTerminated(_fileId));
                Self.Tell(PoisonPill.Instance);
            }
            catch (Exception ex)
            {
                Sender.Tell(new ApplicationError(
                    "CANCEL_TRANSFER_FAILED",
                    $"Failed to cancel transfer for file {_fileId}",
                    ex));
            }
        }

        private void HandleGetStatus()
        {
            if (_fileTransfer == null)
            {
                Sender.Tell(new ApplicationError(
                    "TRANSFER_NOT_INITIALIZED",
                    $"Transfer for file {_fileId} is not initialized"));
                return;
            }

            var response = new FileTransferStatusResponse(
                _fileId,
                _fileTransfer.Status,
                _fileTransfer.CompletionPercentage,
                _fileTransfer.TransferredBytes,
                _fileMeta.Size,
                _fileTransfer.TransferDuration,
                _fileTransfer.Sources.ToList());

            Sender.Tell(response);
        }

        private void HandleRequestChunk(RequestChunkCommand cmd)
        {
            if (_chunkActors.TryGetValue(cmd.ChunkId, out var actor))
            {
                actor.Forward(cmd);
            }
            else
            {
                Sender.Tell(new ApplicationError(
                    "CHUNK_ACTOR_NOT_FOUND",
                    $"No chunk actor found for chunk {cmd.ChunkId}"));
            }
        }

        private void HandleStoreChunk(StoreChunkCommand cmd)
        {
            try
            {
                _fileTransfer?.MarkChunkReceived(cmd.ChunkId, cmd.SourceNode);
                
                Sender.Tell(new ChunkStored(
                    cmd.ChunkId,
                    cmd.SourceNode,
                    DateTime.UtcNow));

                if (_fileTransfer?.Status == TransferStatus.Completed)
                {
                    Sender.Tell(new AppMessages.FileTransferCompleted(
                        _fileId,
                        DateTime.UtcNow,
                        _fileMeta.Size));

                    Context.Parent.Tell(new FileTransferActorTerminated(_fileId));
                    Self.Tell(PoisonPill.Instance);
                }
            }
            catch (Exception ex)
            {
                Sender.Tell(new ApplicationError(
                    "STORE_CHUNK_FAILED",
                    $"Failed to store chunk {cmd.ChunkId}",
                    ex));
            }
        }

        private void HandleChunkCompleted(ChunkCompleted evt)
        {
            HandleStoreChunk(new StoreChunkCommand(evt.ChunkId, evt.Data, evt.SourceNode));
        }

        private void HandleChunkFailed(ChunkFailed evt)
        {
            Sender.Tell(new AppMessages.ChunkRequestFailed(
                evt.ChunkId,
                evt.SourceNode ?? NodeId.NewGuid(),
                evt.Reason,
                DateTime.UtcNow));
        }

        private void CreateChunkActors()
        {
            if (_fileTransfer == null) return;

            foreach (var chunk in _fileTransfer.Chunks)
            {
                var chunkActor = Context.ActorOf(
                    ChunkActor.Props(chunk.Id, _fileId),
                    $"chunk-{chunk.Id}");

                _chunkActors[chunk.Id] = chunkActor;
                Context.Watch(chunkActor);
            }
        }

        public static Props Props(FileId fileId, FileMeta fileMeta, NodeId? initiatorNode) =>
            Akka.Actor.Props.Create(() => new FileTransferActor(fileId, fileMeta, initiatorNode));
    }

    public record ChunkCompleted(ChunkId ChunkId, byte[] Data, NodeId? SourceNode);
    public record ChunkFailed(ChunkId ChunkId, NodeId? SourceNode, string Reason);
}
