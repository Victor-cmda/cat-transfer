using Akka.Actor;
using Application.Messages;
using Domain.ValueObjects;

namespace Application.Actors.Network
{
    public class PeerActor : ReceiveActor
    {
        private readonly NodeId _peerId;
        private readonly string _endpoint;
        private bool _isConnected = false;
        private readonly HashSet<FileId> _advertisedFiles = new();

        public PeerActor(NodeId peerId, string endpoint)
        {
            _peerId = peerId;
            _endpoint = endpoint;
            
            SetupHandlers();
        }

        private void SetupHandlers()
        {
            Receive<EstablishConnection>(_ => HandleEstablishConnection());
            Receive<TerminateConnection>(_ => HandleTerminateConnection());
            Receive<BroadcastFileAvailability>(msg => HandleBroadcastFileAvailability(msg));
            Receive<SendMessageToPeer>(msg => HandleSendMessage(msg));
            Receive<GetPeerStatus>(_ => HandleGetStatus());
        }

        private void HandleEstablishConnection()
        {
            try
            {
                _isConnected = true;
                
                Context.Parent.Tell(new PeerConnectionResult(
                    _peerId,
                    _endpoint,
                    true));

                Sender.Tell(new PeerConnected(
                    _peerId,
                    _endpoint,
                    DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                Context.Parent.Tell(new PeerConnectionResult(
                    _peerId,
                    _endpoint,
                    false,
                    ex.Message));

                Sender.Tell(new ApplicationError(
                    "CONNECTION_FAILED",
                    $"Failed to connect to peer {_peerId}",
                    ex));
            }
        }

        private void HandleTerminateConnection()
        {
            try
            {
                _isConnected = false;
                _advertisedFiles.Clear();

                Context.Parent.Tell(new PeerDisconnectionResult(
                    _peerId,
                    "Connection terminated"));

                Sender.Tell(new PeerDisconnected(
                    _peerId,
                    "Connection terminated",
                    DateTime.UtcNow));

                Self.Tell(PoisonPill.Instance);
            }
            catch (Exception ex)
            {
                Sender.Tell(new ApplicationError(
                    "DISCONNECTION_FAILED",
                    $"Failed to disconnect from peer {_peerId}",
                    ex));
            }
        }

        private void HandleBroadcastFileAvailability(BroadcastFileAvailability msg)
        {
            if (!_isConnected)
            {
                Sender.Tell(new ApplicationError(
                    "PEER_NOT_CONNECTED",
                    $"Cannot broadcast to disconnected peer {_peerId}"));
                return;
            }

            try
            {
                _advertisedFiles.Add(msg.FileId);

                Sender.Tell(new FileAvailabilityBroadcasted(
                    msg.FileId,
                    msg.AdvertisingNode,
                    1,
                    DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ApplicationError(
                    "BROADCAST_FAILED",
                    $"Failed to broadcast to peer {_peerId}",
                    ex));
            }
        }

        private void HandleSendMessage(SendMessageToPeer msg)
        {
            if (!_isConnected)
            {
                Sender.Tell(new ApplicationError(
                    "PEER_NOT_CONNECTED",
                    $"Cannot send message to disconnected peer {_peerId}"));
                return;
            }

            try
            {
                Sender.Tell(new MessageSentToPeer(
                    _peerId,
                    msg.Message.GetType().Name,
                    DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ApplicationError(
                    "MESSAGE_SEND_FAILED",
                    $"Failed to send message to peer {_peerId}",
                    ex));
            }
        }

        private void HandleGetStatus()
        {
            Sender.Tell(new PeerStatusResponse(
                _peerId,
                _endpoint,
                _isConnected,
                _advertisedFiles.Count,
                _advertisedFiles.ToList()));
        }

        public static Props Props(NodeId peerId, string endpoint) =>
            Akka.Actor.Props.Create(() => new PeerActor(peerId, endpoint));
    }

    public record EstablishConnection;
    public record TerminateConnection;
    public record BroadcastFileAvailability(FileId FileId, NodeId AdvertisingNode);
    public record SendMessageToPeer(IApplicationMessage Message);
    public record GetPeerStatus;

    public record MessageSentToPeer(NodeId PeerId, string MessageType, DateTime SentAt) : IApplicationResponse;
    public record PeerStatusResponse(
        NodeId PeerId,
        string Endpoint,
        bool IsConnected,
        int AdvertisedFileCount,
        IReadOnlyList<FileId> AdvertisedFiles
    ) : IApplicationResponse;
}
