using Akka.Actor;
using Application.Messages;
using Domain.ValueObjects;

namespace Application.Actors.FileTransfer
{
    public class ChunkActor : ReceiveActor
    {
        private readonly ChunkId _chunkId;
        private readonly FileId _fileId;
        private byte[]? _data;
        private NodeId? _sourceNode;
        private bool _isStored = false;
        private readonly List<NodeId> _availableSources = new();

        public ChunkActor(ChunkId chunkId, FileId fileId)
        {
            _chunkId = chunkId;
            _fileId = fileId;
            
            SetupHandlers();
        }

        private void SetupHandlers()
        {
            Receive<RequestChunkCommand>(cmd => HandleRequestChunk(cmd));
            Receive<StoreChunkCommand>(cmd => HandleStoreChunk(cmd));
            Receive<GetChunkAvailabilityQuery>(_ => HandleGetAvailability());
            Receive<AddChunkSource>(cmd => HandleAddSource(cmd));
            Receive<RemoveChunkSource>(cmd => HandleRemoveSource(cmd));
        }

        private void HandleRequestChunk(RequestChunkCommand cmd)
        {
            if (_isStored && _data != null)
            {
                Sender.Tell(new ChunkRequested(
                    _chunkId,
                    cmd.FromNode,
                    cmd.RequestingNode,
                    DateTime.UtcNow));

                Context.Parent.Tell(new ChunkCompleted(_chunkId, _data, _sourceNode));
            }
            else if (_availableSources.Contains(cmd.FromNode))
            {
                Sender.Tell(new ChunkRequested(
                    _chunkId,
                    cmd.FromNode,
                    cmd.RequestingNode,
                    DateTime.UtcNow));
            }
            else
            {
                Sender.Tell(new ChunkRequestFailed(
                    _chunkId,
                    cmd.FromNode,
                    "Chunk not available from requested source",
                    DateTime.UtcNow));
            }
        }

        private void HandleStoreChunk(StoreChunkCommand cmd)
        {
            try
            {
                _data = cmd.Data;
                _sourceNode = cmd.SourceNode;
                _isStored = true;

                Sender.Tell(new ChunkStored(
                    _chunkId,
                    cmd.SourceNode,
                    DateTime.UtcNow));

                Context.Parent.Tell(new ChunkCompleted(_chunkId, cmd.Data, cmd.SourceNode));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ApplicationError(
                    "CHUNK_STORE_FAILED",
                    $"Failed to store chunk {_chunkId}",
                    ex));

                Context.Parent.Tell(new ChunkFailed(_chunkId, cmd.SourceNode, ex.Message));
            }
        }

        private void HandleGetAvailability()
        {
            Sender.Tell(new ChunkAvailabilityResponse(
                _chunkId,
                _availableSources.ToList(),
                _isStored));
        }

        private void HandleAddSource(AddChunkSource cmd)
        {
            if (!_availableSources.Contains(cmd.SourceNode))
            {
                _availableSources.Add(cmd.SourceNode);
                Sender.Tell(new ChunkSourceAdded(_chunkId, cmd.SourceNode));
            }
        }

        private void HandleRemoveSource(RemoveChunkSource cmd)
        {
            if (_availableSources.Remove(cmd.SourceNode))
            {
                Sender.Tell(new ChunkSourceRemoved(_chunkId, cmd.SourceNode));
            }
        }

        public static Props Props(ChunkId chunkId, FileId fileId) =>
            Akka.Actor.Props.Create(() => new ChunkActor(chunkId, fileId));
    }

    public record AddChunkSource(NodeId SourceNode);
    public record RemoveChunkSource(NodeId SourceNode);
    public record ChunkSourceAdded(ChunkId ChunkId, NodeId SourceNode) : IApplicationResponse;
    public record ChunkSourceRemoved(ChunkId ChunkId, NodeId SourceNode) : IApplicationResponse;
}
