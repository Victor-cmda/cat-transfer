using Akka.Actor;
using Application.Messages;
using Application.Actors;
using Application.Actors.Network;
using Domain.ValueObjects;

namespace Application.Services
{
    public interface INetworkService
    {
        Task<PeerConnected> ConnectToPeerAsync(NodeId peerId, string endpoint, CancellationToken cancellationToken = default);
        Task<PeerDisconnected> DisconnectFromPeerAsync(NodeId peerId, CancellationToken cancellationToken = default);
        Task<FileAvailabilityBroadcasted> BroadcastFileAvailabilityAsync(FileId fileId, NodeId advertisingNode, CancellationToken cancellationToken = default);
        Task<ConnectedPeersResponse> GetConnectedPeersAsync(CancellationToken cancellationToken = default);
        Task<FileAvailabilityQueryResponse> GetFileAvailabilityAsync(FileId fileId, CancellationToken cancellationToken = default);
    }

    public class NetworkService : INetworkService
    {
        private readonly ApplicationActorSystem _actorSystem;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);

        public NetworkService(ApplicationActorSystem actorSystem)
        {
            _actorSystem = actorSystem ?? throw new ArgumentNullException(nameof(actorSystem));
        }

        public async Task<PeerConnected> ConnectToPeerAsync(NodeId peerId, string endpoint, CancellationToken cancellationToken = default)
        {
            var command = new ConnectToPeerCommand(peerId, endpoint);
            var response = await _actorSystem.NetworkCoordinator.Ask<IApplicationResponse>(command, _defaultTimeout);

            return response switch
            {
                PeerConnected connected => connected,
                ApplicationError error => throw new ApplicationException($"Failed to connect to peer: {error.Message}", error.Exception),
                _ => throw new ApplicationException($"Unexpected response type: {response.GetType().Name}")
            };
        }

        public async Task<PeerDisconnected> DisconnectFromPeerAsync(NodeId peerId, CancellationToken cancellationToken = default)
        {
            var command = new DisconnectFromPeerCommand(peerId);
            var response = await _actorSystem.NetworkCoordinator.Ask<IApplicationResponse>(command, _defaultTimeout);

            return response switch
            {
                PeerDisconnected disconnected => disconnected,
                ApplicationError error => throw new ApplicationException($"Failed to disconnect from peer: {error.Message}", error.Exception),
                _ => throw new ApplicationException($"Unexpected response type: {response.GetType().Name}")
            };
        }

        public async Task<FileAvailabilityBroadcasted> BroadcastFileAvailabilityAsync(FileId fileId, NodeId advertisingNode, CancellationToken cancellationToken = default)
        {
            var command = new BroadcastFileAvailabilityCommand(fileId, advertisingNode);
            var response = await _actorSystem.NetworkCoordinator.Ask<IApplicationResponse>(command, _defaultTimeout);

            return response switch
            {
                FileAvailabilityBroadcasted broadcasted => broadcasted,
                ApplicationError error => throw new ApplicationException($"Failed to broadcast file availability: {error.Message}", error.Exception),
                _ => throw new ApplicationException($"Unexpected response type: {response.GetType().Name}")
            };
        }

        public async Task<ConnectedPeersResponse> GetConnectedPeersAsync(CancellationToken cancellationToken = default)
        {
            var query = new GetConnectedPeersQuery();
            var response = await _actorSystem.NetworkCoordinator.Ask<IApplicationResponse>(query, _defaultTimeout);

            return response switch
            {
                ConnectedPeersResponse peers => peers,
                ApplicationError error => throw new ApplicationException($"Failed to get connected peers: {error.Message}", error.Exception),
                _ => throw new ApplicationException($"Unexpected response type: {response.GetType().Name}")
            };
        }

        public async Task<FileAvailabilityQueryResponse> GetFileAvailabilityAsync(FileId fileId, CancellationToken cancellationToken = default)
        {
            var query = new GetFileAvailabilityQuery(fileId);
            var response = await _actorSystem.NetworkCoordinator.Ask<IApplicationResponse>(query, _defaultTimeout);

            return response switch
            {
                FileAvailabilityQueryResponse availability => availability,
                ApplicationError error => throw new ApplicationException($"Failed to get file availability: {error.Message}", error.Exception),
                _ => throw new ApplicationException($"Unexpected response type: {response.GetType().Name}")
            };
        }
    }
}
