using Akka.Actor;
using Application.Messages;
using Domain.ValueObjects;
using Domain.Aggregates.NetworkManagement;

namespace Application.Actors.Network
{
    public class NetworkCoordinatorActor : ReceiveActor
    {
        private readonly Dictionary<NodeId, PeerActorInfo> _connectedPeers = new();
        private readonly Dictionary<FileId, HashSet<NodeId>> _fileAvailability = new();
        private P2PNetwork? _network;

        public NetworkCoordinatorActor()
        {
            SetupHandlers();
            InitializeNetwork();
        }

        private void SetupHandlers()
        {
            Receive<ConnectToPeerCommand>(cmd => HandleConnectToPeer(cmd));
            Receive<DisconnectFromPeerCommand>(cmd => HandleDisconnectFromPeer(cmd));
            Receive<BroadcastFileAvailabilityCommand>(cmd => HandleBroadcastFileAvailability(cmd));
            Receive<GetConnectedPeersQuery>(_ => HandleGetConnectedPeers());
            Receive<GetFileAvailabilityQuery>(query => HandleGetFileAvailability(query));
            
            Receive<PeerConnectionResult>(result => HandlePeerConnectionResult(result));
            Receive<PeerDisconnectionResult>(result => HandlePeerDisconnectionResult(result));
            Receive<FileAvailabilityUpdate>(update => HandleFileAvailabilityUpdate(update));
        }

        private void InitializeNetwork()
        {
            var localNodeId = NodeId.NewGuid();
            var localAddress = new PeerAddress("127.0.0.1", 8080);
            
            _network = new P2PNetwork(localNodeId, localAddress);
        }

        private void HandleConnectToPeer(ConnectToPeerCommand cmd)
        {
            try
            {
                if (_connectedPeers.ContainsKey(cmd.PeerId))
                {
                    Sender.Tell(new ApplicationError(
                        "PEER_ALREADY_CONNECTED",
                        $"Peer {cmd.PeerId} is already connected"));
                    return;
                }

                var peerAddress = PeerAddress.Parse(cmd.Endpoint);
                _network?.AddPeer(cmd.PeerId, peerAddress);

                var peerActor = Context.ActorOf(
                    PeerActor.Props(cmd.PeerId, cmd.Endpoint),
                    $"peer-{cmd.PeerId}");

                _connectedPeers[cmd.PeerId] = new PeerActorInfo(
                    cmd.PeerId,
                    peerActor,
                    cmd.Endpoint,
                    DateTime.UtcNow);

                Context.Watch(peerActor);
                
                peerActor.Tell(new EstablishConnection());
            }
            catch (Exception ex)
            {
                Sender.Tell(new ApplicationError(
                    "CONNECT_PEER_FAILED",
                    $"Failed to connect to peer {cmd.PeerId}",
                    ex));
            }
        }

        private void HandleDisconnectFromPeer(DisconnectFromPeerCommand cmd)
        {
            if (_connectedPeers.TryGetValue(cmd.PeerId, out var peerInfo))
            {
                peerInfo.Actor.Tell(new TerminateConnection());
                _connectedPeers.Remove(cmd.PeerId);
                _network?.RemovePeer(cmd.PeerId);

                Sender.Tell(new PeerDisconnected(
                    cmd.PeerId,
                    "Manual disconnection",
                    DateTime.UtcNow));
            }
            else
            {
                Sender.Tell(new ApplicationError(
                    "PEER_NOT_CONNECTED",
                    $"Peer {cmd.PeerId} is not connected"));
            }
        }

        private void HandleBroadcastFileAvailability(BroadcastFileAvailabilityCommand cmd)
        {
            try
            {
                if (!_fileAvailability.ContainsKey(cmd.FileId))
                {
                    _fileAvailability[cmd.FileId] = new HashSet<NodeId>();
                }

                _fileAvailability[cmd.FileId].Add(cmd.AdvertisingNode);
                _network?.AdvertiseFile(cmd.FileId, cmd.AdvertisingNode);

                var broadcastCount = 0;
                foreach (var peer in _connectedPeers.Values)
                {
                    peer.Actor.Tell(new BroadcastFileAvailability(cmd.FileId, cmd.AdvertisingNode));
                    broadcastCount++;
                }

                Sender.Tell(new FileAvailabilityBroadcasted(
                    cmd.FileId,
                    cmd.AdvertisingNode,
                    broadcastCount,
                    DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ApplicationError(
                    "BROADCAST_FAILED",
                    $"Failed to broadcast file availability for {cmd.FileId}",
                    ex));
            }
        }

        private void HandleGetConnectedPeers()
        {
            var peers = _connectedPeers.Values.Select(p => 
                new ConnectedPeerInfo(p.PeerId, p.Endpoint, p.ConnectedAt)).ToList();

            Sender.Tell(new ConnectedPeersResponse(peers));
        }

        private void HandleGetFileAvailability(GetFileAvailabilityQuery query)
        {
            var availableFrom = _fileAvailability.GetValueOrDefault(query.FileId, new HashSet<NodeId>());
            
            Sender.Tell(new FileAvailabilityQueryResponse(
                query.FileId,
                availableFrom.ToList()));
        }

        private void HandlePeerConnectionResult(PeerConnectionResult result)
        {
            if (result.Success)
            {
                Sender.Tell(new PeerConnected(
                    result.PeerId,
                    result.Endpoint,
                    DateTime.UtcNow));
            }
            else
            {
                _connectedPeers.Remove(result.PeerId);
                _network?.RemovePeer(result.PeerId);

                Sender.Tell(new ApplicationError(
                    "PEER_CONNECTION_FAILED",
                    $"Failed to establish connection to {result.PeerId}: {result.ErrorMessage}"));
            }
        }

        private void HandlePeerDisconnectionResult(PeerDisconnectionResult result)
        {
            _connectedPeers.Remove(result.PeerId);
            _network?.RemovePeer(result.PeerId);

            Sender.Tell(new PeerDisconnected(
                result.PeerId,
                result.Reason,
                DateTime.UtcNow));
        }

        private void HandleFileAvailabilityUpdate(FileAvailabilityUpdate update)
        {
            if (!_fileAvailability.ContainsKey(update.FileId))
            {
                _fileAvailability[update.FileId] = new HashSet<NodeId>();
            }

            if (update.IsAvailable)
            {
                _fileAvailability[update.FileId].Add(update.PeerId);
                _network?.AdvertiseFile(update.FileId, update.PeerId);
            }
            else
            {
                _fileAvailability[update.FileId].Remove(update.PeerId);
                _network?.RemoveFileAdvertisement(update.FileId, update.PeerId);
            }
        }

        public static Props Props() => Akka.Actor.Props.Create<NetworkCoordinatorActor>();
    }

    public record PeerActorInfo(
        NodeId PeerId,
        IActorRef Actor,
        string Endpoint,
        DateTime ConnectedAt);

    public record GetConnectedPeersQuery : IApplicationMessage;
    public record GetFileAvailabilityQuery(FileId FileId) : IApplicationMessage;

    public record ConnectedPeerInfo(NodeId PeerId, string Endpoint, DateTime ConnectedAt);
    public record ConnectedPeersResponse(IReadOnlyList<ConnectedPeerInfo> Peers) : IApplicationResponse;
    public record FileAvailabilityQueryResponse(FileId FileId, IReadOnlyList<NodeId> AvailableFrom) : IApplicationResponse;

    public record PeerConnectionResult(NodeId PeerId, string Endpoint, bool Success, string? ErrorMessage = null);
    public record PeerDisconnectionResult(NodeId PeerId, string Reason);
    public record FileAvailabilityUpdate(FileId FileId, NodeId PeerId, bool IsAvailable);
}
