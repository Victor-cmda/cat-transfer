using Domain.ValueObjects;
using Infrastructure.Storage.Interfaces;
using Infrastructure.Storage.Models;

namespace Infrastructure.Actors.Storage.Messages
{

    public abstract record StorageMessage;
    public abstract record StorageCommand : StorageMessage;
    public abstract record StorageQuery : StorageMessage;
    public abstract record StorageEvent : StorageMessage;
    public abstract record StorageResponse : StorageMessage;


    public record SaveFileCommand(Domain.Aggregates.FileTransfer.FileTransfer File, CancellationToken CancellationToken = default) : StorageCommand;
    public record DeleteFileCommand(FileId FileId, CancellationToken CancellationToken = default) : StorageCommand;
    public record StoreChunkCommand(ChunkId ChunkId, byte[] Data, CancellationToken CancellationToken = default) : StorageCommand;
    public record DeleteChunkCommand(ChunkId ChunkId, CancellationToken CancellationToken = default) : StorageCommand;


    public record GetFileByIdQuery(FileId FileId, CancellationToken CancellationToken = default) : StorageQuery;
    public record FileExistsQuery(FileId FileId, CancellationToken CancellationToken = default) : StorageQuery;
    public record GetFilesByStatusQuery(Domain.Enumerations.TransferStatus Status, CancellationToken CancellationToken = default) : StorageQuery;
    public record GetChunkQuery(ChunkId ChunkId, CancellationToken CancellationToken = default) : StorageQuery;
    public record ChunkExistsQuery(ChunkId ChunkId, CancellationToken CancellationToken = default) : StorageQuery;


    public record FileSaved(FileId FileId) : StorageResponse;
    public record FileDeleted(FileId FileId) : StorageResponse;
    public record FileResult(Domain.Aggregates.FileTransfer.FileTransfer? File) : StorageResponse;
    public record FilesResult(IEnumerable<Domain.Aggregates.FileTransfer.FileTransfer> Files) : StorageResponse;
    public record FileExistsResult(bool Exists) : StorageResponse;
    
    public record ChunkStored(ChunkId ChunkId) : StorageResponse;
    public record ChunkDeleted(ChunkId ChunkId) : StorageResponse;
    public record ChunkResult(ChunkId ChunkId, byte[] Data) : StorageResponse;
    public record ChunkExistsResult(ChunkId ChunkId, bool Exists) : StorageResponse;
    

    public record FileCreatedEvent(FileId FileId, DateTime Timestamp) : StorageEvent;
    public record FileDeletedEvent(FileId FileId, DateTime Timestamp) : StorageEvent;
    public record ChunkStoredEvent(ChunkId ChunkId, long Size, DateTime Timestamp) : StorageEvent;
    public record ChunkDeletedEvent(ChunkId ChunkId, DateTime Timestamp) : StorageEvent;


    public record StorageError(string Message, Exception? Exception = null) : StorageResponse;
}
