using Microsoft.AspNetCore.SignalR;
using Node.Services;
using Application.Services;
using Domain.ValueObjects;

namespace Node.Api.Hubs;

public class CatTransferHub : Hub
{
    private readonly INodeService _nodeService;
    private readonly IFileTransferService _fileTransferService;

    public CatTransferHub(INodeService nodeService, IFileTransferService fileTransferService)
    {
        _nodeService = nodeService;
        _fileTransferService = fileTransferService;
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "CatTransferClients");
        
        
        var status = await _nodeService.GetStatusAsync();
        await Clients.Caller.SendAsync("InitialStatus", new
        {
            NodeId = status.NodeId.ToString(),
            NodeName = status.NodeName,
            IsRunning = status.IsRunning,
            ConnectedPeers = status.ConnectedPeers,
            ActiveTransfers = status.ActiveTransfers
        });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "CatTransferClients");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task RequestStatusUpdate()
    {
        var status = await _nodeService.GetStatusAsync();
        var peers = await _nodeService.GetConnectedPeersAsync();

        await Clients.Caller.SendAsync("StatusUpdate", new
        {
            Node = new
            {
                NodeId = status.NodeId.ToString(),
                NodeName = status.NodeName,
                IsRunning = status.IsRunning,
                ConnectedPeers = status.ConnectedPeers,
                ActiveTransfers = status.ActiveTransfers,
                TotalBytesTransferred = status.TotalBytesTransferred,
                UptimeSeconds = (int)status.Uptime.TotalSeconds
            },
            Peers = peers.Select(p => new
            {
                NodeId = p.NodeId.ToString(),
                Address = p.Address,
                Port = p.Port,
                ConnectedAt = p.ConnectedAt,
                IsConnected = p.IsConnected
            })
        });
    }

    public async Task SubscribeToTransfer(string transferId)
    {
        if (string.IsNullOrWhiteSpace(transferId))
        {
            await Clients.Caller.SendAsync("Error", new { Message = "Transfer ID cannot be empty" });
            return;
        }

        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Transfer_{transferId}");
            await Clients.Caller.SendAsync("SubscribedToTransfer", new { TransferId = transferId });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", new { Message = "Failed to subscribe to transfer", Error = ex.Message });
        }
    }

    public async Task UnsubscribeFromTransfer(string transferId)
    {
        if (string.IsNullOrWhiteSpace(transferId))
        {
            await Clients.Caller.SendAsync("Error", new { Message = "Transfer ID cannot be empty" });
            return;
        }

        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Transfer_{transferId}");
            await Clients.Caller.SendAsync("UnsubscribedFromTransfer", new { TransferId = transferId });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", new { Message = "Failed to unsubscribe from transfer", Error = ex.Message });
        }
    }
}

public interface ICatTransferNotificationService
{
    Task NotifyTransferProgress(string transferId, double progress, long bytesTransferred, long totalBytes);
    Task NotifyTransferCompleted(string transferId);
    Task NotifyTransferError(string transferId, string error);
    Task NotifyPeerConnected(string peerId, string address, int port);
    Task NotifyPeerDisconnected(string peerId);
    Task NotifyNodeStatusChanged(bool isRunning);
}

public class CatTransferNotificationService : ICatTransferNotificationService
{
    private readonly IHubContext<CatTransferHub> _hubContext;

    public CatTransferNotificationService(IHubContext<CatTransferHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyTransferProgress(string transferId, double progress, long bytesTransferred, long totalBytes)
    {
        await _hubContext.Clients.Group($"Transfer_{transferId}").SendAsync("TransferProgress", new
        {
            TransferId = transferId,
            Progress = progress,
            BytesTransferred = bytesTransferred,
            TotalBytes = totalBytes,
            Speed = CalculateSpeed(bytesTransferred), 
            EstimatedTimeRemaining = CalculateETA(progress, totalBytes, bytesTransferred)
        });

        
        await _hubContext.Clients.Group("CatTransferClients").SendAsync("TransferStatusChanged", new
        {
            TransferId = transferId,
            Status = "InProgress",
            Progress = progress
        });
    }

    public async Task NotifyTransferCompleted(string transferId)
    {
        await _hubContext.Clients.Group($"Transfer_{transferId}").SendAsync("TransferCompleted", new
        {
            TransferId = transferId,
            CompletedAt = DateTime.UtcNow
        });

        await _hubContext.Clients.Group("CatTransferClients").SendAsync("TransferStatusChanged", new
        {
            TransferId = transferId,
            Status = "Completed",
            Progress = 100.0
        });
    }

    public async Task NotifyTransferError(string transferId, string error)
    {
        await _hubContext.Clients.Group($"Transfer_{transferId}").SendAsync("TransferError", new
        {
            TransferId = transferId,
            Error = error,
            Timestamp = DateTime.UtcNow
        });

        await _hubContext.Clients.Group("CatTransferClients").SendAsync("TransferStatusChanged", new
        {
            TransferId = transferId,
            Status = "Error",
            Error = error
        });
    }

    public async Task NotifyPeerConnected(string peerId, string address, int port)
    {
        await _hubContext.Clients.Group("CatTransferClients").SendAsync("PeerConnected", new
        {
            PeerId = peerId,
            Address = address,
            Port = port,
            ConnectedAt = DateTime.UtcNow
        });
    }

    public async Task NotifyPeerDisconnected(string peerId)
    {
        await _hubContext.Clients.Group("CatTransferClients").SendAsync("PeerDisconnected", new
        {
            PeerId = peerId,
            DisconnectedAt = DateTime.UtcNow
        });
    }

    public async Task NotifyNodeStatusChanged(bool isRunning)
    {
        await _hubContext.Clients.Group("CatTransferClients").SendAsync("NodeStatusChanged", new
        {
            IsRunning = isRunning,
            Timestamp = DateTime.UtcNow
        });
    }

    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly Dictionary<string, TransferMetrics> _transferMetrics = new();

    private string CalculateSpeed(long bytesTransferred)
    {
        var elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
        if (elapsed <= 0) return "0 MB/s";
        
        var speedBytesPerSecond = bytesTransferred / elapsed;
        var speedMBPerSecond = speedBytesPerSecond / (1024.0 * 1024.0);
        
        return speedMBPerSecond switch
        {
            >= 1.0 => $"{speedMBPerSecond:F2} MB/s",
            >= 0.001 => $"{speedMBPerSecond * 1024:F2} KB/s",
            _ => $"{speedBytesPerSecond:F0} B/s"
        };
    }

    private TimeSpan CalculateETA(double progress, long totalBytes, long bytesTransferred)
    {
        if (progress <= 0 || progress >= 100) return TimeSpan.Zero;
        
        var elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
        if (elapsed <= 0) return TimeSpan.Zero;
        
        var remainingBytes = totalBytes - bytesTransferred;
        var currentSpeedBytesPerSecond = bytesTransferred / elapsed;
        
        if (currentSpeedBytesPerSecond <= 0) return TimeSpan.Zero;
        
        var estimatedSecondsRemaining = remainingBytes / currentSpeedBytesPerSecond;
        return TimeSpan.FromSeconds(estimatedSecondsRemaining);
    }

    private class TransferMetrics
    {
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public long InitialBytes { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
        public long LastBytes { get; set; }
    }
}
