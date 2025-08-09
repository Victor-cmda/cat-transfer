using Akka.Actor;
using Akka.Event;
using Application.Messages;
using Application.Actors.FileTransfer;
using Domain.ValueObjects;
using Domain.Aggregates.FileTransfer;
using Akka.Pattern;

namespace Application.Actors.FileTransfer
{
    public class FileTransferSupervisorActor : ReceiveActor
    {
        private readonly Dictionary<FileId, IActorRef> _activeTransfers = new();
        private readonly Dictionary<FileId, FileTransferInfo> _transferMetadata = new();
    private readonly Dictionary<FileId, long> _outboundProgress = new();
        private readonly ILoggingAdapter _logger;

        public FileTransferSupervisorActor()
        {
            _logger = Context.GetLogger();
            _logger.Info("FileTransferSupervisorActor iniciado");
            SetupHandlers();
        }

        private void SetupHandlers()
        {
            Receive<StartFileTransferCommand>(cmd => HandleStartFileTransfer(cmd));
            Receive<PauseFileTransferCommand>(cmd => HandlePauseFileTransfer(cmd));
            Receive<ResumeFileTransferCommand>(cmd => HandleResumeFileTransfer(cmd));
            Receive<CancelFileTransferCommand>(cmd => HandleCancelFileTransfer(cmd));
            Receive<GetFileTransferStatusQuery>(query => HandleGetFileTransferStatus(query));
            ReceiveAsync<GetActiveTransfersQuery>(async _ => await HandleGetActiveTransfersAsync());

            Receive<StoreChunkCommand>(cmd =>
            {
                var fileId = cmd.ChunkId.file;
                if (_activeTransfers.TryGetValue(fileId, out var actor))
                {
                    actor.Forward(cmd);
                }
                else
                {
                    Sender.Tell(new ApplicationError(
                        "TRANSFER_NOT_FOUND",
                        $"No active transfer found for file {fileId} to store chunk"));
                }
            });

            Receive<FileTransferActorTerminated>(msg => HandleTransferActorTerminated(msg));
            Receive<OutboundChunkSentNotice>(notice => HandleOutboundChunkSent(notice));
            
            ReceiveAny(message => ForwardToTransferActor(message));
        }

        private void HandleStartFileTransfer(StartFileTransferCommand cmd)
        {
            _logger.Info("Supervisor recebeu comando para iniciar transferência: {0}", cmd.FileId);
            
            if (_activeTransfers.ContainsKey(cmd.FileId))
            {
                _logger.Warning("Transferência já ativa para arquivo {0}", cmd.FileId);
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

            _logger.Info("FileTransferActor criado e adicionado aos ativos. Total: {0}", _activeTransfers.Count);

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

        private async Task HandleGetActiveTransfersAsync()
        {
            _logger.Info("Consulta de transferências ativas. Registradas: {0}", _activeTransfers.Count);

            var tasks = new List<Task<IApplicationResponse>>();
            var mapping = new List<(FileId fileId, IActorRef actor)>();

            foreach (var kv in _activeTransfers)
            {
                var fileId = kv.Key;
                var actor = kv.Value;
                mapping.Add((fileId, actor));
                tasks.Add(actor.Ask<IApplicationResponse>(new GetFileTransferStatusQuery(fileId), TimeSpan.FromSeconds(3)));
            }

            var responses = new List<FileTransferStatusResponse>();
            try
            {
                var results = await Task.WhenAll(tasks);
                for (int i = 0; i < results.Length; i++)
                {
                    switch (results[i])
                    {
                        case FileTransferStatusResponse status:
                            // Mescla progresso de envio (quando este nó é o remetente)
                            if (_outboundProgress.TryGetValue(status.FileId, out var sent))
                            {
                                var total = status.TotalBytes.bytes;
                                var mergedTransferred = Math.Max(status.TransferredBytes.bytes, Math.Min(sent, total));
                                var mergedPct = total > 0 ? (mergedTransferred / (double)total) * 100.0 : status.CompletionPercentage;
                                status = new FileTransferStatusResponse(
                                    status.FileId,
                                    status.Status,
                                    mergedPct,
                                    new ByteSize(mergedTransferred),
                                    status.TotalBytes,
                                    status.Duration,
                                    status.Sources
                                );
                            }
                            responses.Add(status);
                            break;
                        case ApplicationError err:
                            _logger.Warning("Falha ao obter status para {0}: {1}", mapping[i].fileId, err.Message);
                            if (_transferMetadata.TryGetValue(mapping[i].fileId, out var meta))
                            {
                                var sentBytes = _outboundProgress.TryGetValue(mapping[i].fileId, out var s) ? s : 0L;
                                var total = meta.Meta.Size.bytes;
                                var pct = total > 0 ? (sentBytes / (double)total) * 100.0 : 0.0;
                                responses.Add(new FileTransferStatusResponse(
                                    mapping[i].fileId,
                                    Domain.Enumerations.TransferStatus.InProgress,
                                    pct,
                                    new ByteSize(Math.Min(sentBytes, total)),
                                    meta.Meta.Size,
                                    DateTime.UtcNow - meta.StartedAt,
                                    new List<NodeId>()));
                            }
                            break;
                        default:
                            _logger.Warning("Resposta inesperada de status para {0}: {1}", mapping[i].fileId, results[i].GetType().Name);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Erro ao agregar status das transferências");
            }

            _logger.Info("Enviando {0} transferências ativas", responses.Count);
            Sender.Tell(new ActiveTransfersResponse(responses, responses.Count));
        }

        private void HandleTransferActorTerminated(FileTransferActorTerminated msg)
        {
            _logger.Info("FileTransferActor terminado para {0}. Removendo das listas", msg.FileId);
            
            var removed1 = _activeTransfers.Remove(msg.FileId);
            var removed2 = _transferMetadata.Remove(msg.FileId);
            _outboundProgress.Remove(msg.FileId);
            
            _logger.Info("Remoção - ActiveTransfers: {0}, Metadata: {1}. Total restante: {2}", 
                removed1, removed2, _activeTransfers.Count);
        }

        private void HandleOutboundChunkSent(OutboundChunkSentNotice notice)
        {
            _outboundProgress[notice.FileId] = Math.Max(
                _outboundProgress.TryGetValue(notice.FileId, out var cur) ? cur : 0L,
                notice.BytesSentSoFar);
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
