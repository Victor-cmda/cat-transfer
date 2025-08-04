using Akka.Actor;
using Infrastructure.Actors.Storage.Messages;
using Infrastructure.Storage.Interfaces;
using Domain.ValueObjects;

namespace Infrastructure.Actors.Storage
{
    public class ChunkStorageActor : ReceiveActor
    {
        private readonly IChunkStorage _chunkStorage;

        public ChunkStorageActor(IChunkStorage chunkStorage)
        {
            _chunkStorage = chunkStorage ?? throw new ArgumentNullException(nameof(chunkStorage));
            
            SetupHandlers();
        }

        private void SetupHandlers()
        {
            ReceiveAsync<StoreChunkCommand>(async cmd =>
            {
                try
                {
                    await _chunkStorage.StoreChunkAsync(
                        cmd.ChunkId, 
                        cmd.Data, 
                        cmd.CancellationToken);
                    
                    Sender.Tell(new ChunkStored(cmd.ChunkId));
                    
                    Context.System.EventStream.Publish(
                        new ChunkStoredEvent(cmd.ChunkId, cmd.Data.Length, DateTime.UtcNow));
                }
                catch (Exception ex)
                {
                    Sender.Tell(new StorageError(ex.Message, ex));
                }
            });

            ReceiveAsync<DeleteChunkCommand>(async cmd =>
            {
                try
                {
                    await _chunkStorage.DeleteChunkAsync(cmd.ChunkId, cmd.CancellationToken);
                    
                    Sender.Tell(new ChunkDeleted(cmd.ChunkId));
                    
                    Context.System.EventStream.Publish(
                        new ChunkDeletedEvent(cmd.ChunkId, DateTime.UtcNow));
                }
                catch (Exception ex)
                {
                    Sender.Tell(new StorageError(ex.Message, ex));
                }
            });

            ReceiveAsync<GetChunkQuery>(async query =>
            {
                try
                {
                    var data = await _chunkStorage.GetChunkAsync(query.ChunkId, query.CancellationToken);
                    if (data != null)
                    {
                        Sender.Tell(new ChunkResult(query.ChunkId, data));
                    }
                    else
                    {
                        Sender.Tell(new StorageError($"Chunk {query.ChunkId} not found"));
                    }
                }
                catch (Exception ex)
                {
                    Sender.Tell(new StorageError(ex.Message, ex));
                }
            });
        }

        public static Props Props(IChunkStorage chunkStorage) =>
            Akka.Actor.Props.Create(() => new ChunkStorageActor(chunkStorage));
    }
}
