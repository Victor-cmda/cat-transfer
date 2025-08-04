using Akka.Actor;
using Application.Messages;
using Application.Actors;
using Domain.ValueObjects;
using Domain.Aggregates.FileTransfer;

namespace Application.Services
{
    public interface IFileTransferService
    {
        Task<FileTransferStarted> StartTransferAsync(FileId fileId, FileMeta meta, NodeId? initiatorNode = null, CancellationToken cancellationToken = default);
        Task<FileTransferPaused> PauseTransferAsync(FileId fileId, NodeId requestingNode, CancellationToken cancellationToken = default);
        Task<FileTransferResumed> ResumeTransferAsync(FileId fileId, NodeId requestingNode, CancellationToken cancellationToken = default);
        Task<FileTransferCancelled> CancelTransferAsync(FileId fileId, NodeId requestingNode, CancellationToken cancellationToken = default);
        Task<FileTransferStatusResponse> GetTransferStatusAsync(FileId fileId, CancellationToken cancellationToken = default);
        Task<ActiveTransfersResponse> GetActiveTransfersAsync(CancellationToken cancellationToken = default);
    }

    public class FileTransferService : IFileTransferService
    {
        private readonly ApplicationActorSystem _actorSystem;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);

        public FileTransferService(ApplicationActorSystem actorSystem)
        {
            _actorSystem = actorSystem ?? throw new ArgumentNullException(nameof(actorSystem));
        }

        public async Task<FileTransferStarted> StartTransferAsync(FileId fileId, FileMeta meta, NodeId? initiatorNode = null, CancellationToken cancellationToken = default)
        {
            var command = new StartFileTransferCommand(fileId, meta, initiatorNode);
            var response = await _actorSystem.FileTransferSupervisor.Ask<IApplicationResponse>(command, _defaultTimeout);

            return response switch
            {
                FileTransferStarted started => started,
                ApplicationError error => throw new ApplicationException($"Failed to start transfer: {error.Message}", error.Exception),
                _ => throw new ApplicationException($"Unexpected response type: {response.GetType().Name}")
            };
        }

        public async Task<FileTransferPaused> PauseTransferAsync(FileId fileId, NodeId requestingNode, CancellationToken cancellationToken = default)
        {
            var command = new PauseFileTransferCommand(fileId, requestingNode);
            var response = await _actorSystem.FileTransferSupervisor.Ask<IApplicationResponse>(command, _defaultTimeout);

            return response switch
            {
                FileTransferPaused paused => paused,
                ApplicationError error => throw new ApplicationException($"Failed to pause transfer: {error.Message}", error.Exception),
                _ => throw new ApplicationException($"Unexpected response type: {response.GetType().Name}")
            };
        }

        public async Task<FileTransferResumed> ResumeTransferAsync(FileId fileId, NodeId requestingNode, CancellationToken cancellationToken = default)
        {
            var command = new ResumeFileTransferCommand(fileId, requestingNode);
            var response = await _actorSystem.FileTransferSupervisor.Ask<IApplicationResponse>(command, _defaultTimeout);

            return response switch
            {
                FileTransferResumed resumed => resumed,
                ApplicationError error => throw new ApplicationException($"Failed to resume transfer: {error.Message}", error.Exception),
                _ => throw new ApplicationException($"Unexpected response type: {response.GetType().Name}")
            };
        }

        public async Task<FileTransferCancelled> CancelTransferAsync(FileId fileId, NodeId requestingNode, CancellationToken cancellationToken = default)
        {
            var command = new CancelFileTransferCommand(fileId, requestingNode);
            var response = await _actorSystem.FileTransferSupervisor.Ask<IApplicationResponse>(command, _defaultTimeout);

            return response switch
            {
                FileTransferCancelled cancelled => cancelled,
                ApplicationError error => throw new ApplicationException($"Failed to cancel transfer: {error.Message}", error.Exception),
                _ => throw new ApplicationException($"Unexpected response type: {response.GetType().Name}")
            };
        }

        public async Task<FileTransferStatusResponse> GetTransferStatusAsync(FileId fileId, CancellationToken cancellationToken = default)
        {
            var query = new GetFileTransferStatusQuery(fileId);
            var response = await _actorSystem.FileTransferSupervisor.Ask<IApplicationResponse>(query, _defaultTimeout);

            return response switch
            {
                FileTransferStatusResponse status => status,
                ApplicationError error => throw new ApplicationException($"Failed to get transfer status: {error.Message}", error.Exception),
                _ => throw new ApplicationException($"Unexpected response type: {response.GetType().Name}")
            };
        }

        public async Task<ActiveTransfersResponse> GetActiveTransfersAsync(CancellationToken cancellationToken = default)
        {
            var query = new GetActiveTransfersQuery();
            var response = await _actorSystem.FileTransferSupervisor.Ask<IApplicationResponse>(query, _defaultTimeout);

            return response switch
            {
                ActiveTransfersResponse transfers => transfers,
                ApplicationError error => throw new ApplicationException($"Failed to get active transfers: {error.Message}", error.Exception),
                _ => throw new ApplicationException($"Unexpected response type: {response.GetType().Name}")
            };
        }
    }
}
