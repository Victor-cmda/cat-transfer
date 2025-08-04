using Akka.Actor;
using Infrastructure.Actors.Storage.Messages;
using Infrastructure.Storage.Interfaces;
using Domain.ValueObjects;

namespace Infrastructure.Actors.Storage
{
    public class FileRepositoryActor : ReceiveActor
    {
        private readonly IFileRepository _fileRepository;

        public FileRepositoryActor(IFileRepository fileRepository)
        {
            _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
            
            SetupHandlers();
        }

        private void SetupHandlers()
        {
            ReceiveAsync<SaveFileCommand>(async cmd =>
            {
                try
                {
                    await _fileRepository.SaveAsync(cmd.File, cmd.CancellationToken);
                    
                    Sender.Tell(new FileSaved(cmd.File.Id));
                    
                    Context.System.EventStream.Publish(
                        new FileCreatedEvent(cmd.File.Id, DateTime.UtcNow));
                }
                catch (Exception ex)
                {
                    Sender.Tell(new StorageError(ex.Message, ex));
                }
            });

            ReceiveAsync<DeleteFileCommand>(async cmd =>
            {
                try
                {
                    await _fileRepository.DeleteAsync(cmd.FileId, cmd.CancellationToken);
                    
                    Sender.Tell(new FileDeleted(cmd.FileId));
                    
                    Context.System.EventStream.Publish(
                        new FileDeletedEvent(cmd.FileId, DateTime.UtcNow));
                }
                catch (Exception ex)
                {
                    Sender.Tell(new StorageError(ex.Message, ex));
                }
            });

            ReceiveAsync<GetFileByIdQuery>(async query =>
            {
                try
                {
                    var file = await _fileRepository.GetByIdAsync(query.FileId, query.CancellationToken);
                    Sender.Tell(new FileResult(file));
                }
                catch (Exception ex)
                {
                    Sender.Tell(new StorageError(ex.Message, ex));
                }
            });

            ReceiveAsync<GetFilesByStatusQuery>(async query =>
            {
                try
                {
                    var files = await _fileRepository.GetByStatusAsync(query.Status, query.CancellationToken);
                    Sender.Tell(new FilesResult(files));
                }
                catch (Exception ex)
                {
                    Sender.Tell(new StorageError(ex.Message, ex));
                }
            });

            ReceiveAsync<FileExistsQuery>(async query =>
            {
                try
                {
                    var exists = await _fileRepository.ExistsAsync(query.FileId, query.CancellationToken);
                    Sender.Tell(new FileExistsResult(exists));
                }
                catch (Exception ex)
                {
                    Sender.Tell(new StorageError(ex.Message, ex));
                }
            });
        }

        public static Props Props(IFileRepository fileRepository) =>
            Akka.Actor.Props.Create(() => new FileRepositoryActor(fileRepository));
    }
}
