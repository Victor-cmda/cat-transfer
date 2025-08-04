using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Node.Services;
using Node.Api.Hubs;
using Application.Services;
using Domain.ValueObjects;

namespace Node.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NodeController : ControllerBase
{
    private readonly INodeService _nodeService;
    private readonly IHubContext<CatTransferHub> _hubContext;

    public NodeController(INodeService nodeService, IHubContext<CatTransferHub> hubContext)
    {
        _nodeService = nodeService;
        _hubContext = hubContext;
    }

    [HttpGet("status")]
    public async Task<ActionResult<NodeStatusDto>> GetStatus()
    {
        var status = await _nodeService.GetStatusAsync();
        return Ok(new NodeStatusDto
        {
            NodeId = status.NodeId.ToString(),
            NodeName = status.NodeName,
            IsRunning = status.IsRunning,
            ConnectedPeers = status.ConnectedPeers,
            ActiveTransfers = status.ActiveTransfers,
            TotalBytesTransferred = status.TotalBytesTransferred,
            UptimeSeconds = (int)status.Uptime.TotalSeconds
        });
    }

    [HttpPost("start")]
    public async Task<ActionResult> Start()
    {
        await _nodeService.StartAsync();


        await _hubContext.Clients.All.SendAsync("NodeStatusChanged", new { Status = "Started" });

        return Ok(new { message = "Nó iniciado com sucesso" });
    }

    [HttpPost("stop")]
    public async Task<ActionResult> Stop()
    {
        await _nodeService.StopAsync();


        await _hubContext.Clients.All.SendAsync("NodeStatusChanged", new { Status = "Stopped" });

        return Ok(new { message = "Nó parado com sucesso" });
    }
}

[ApiController]
[Route("api/[controller]")]
public class PeersController : ControllerBase
{
    private readonly INodeService _nodeService;
    private readonly IHubContext<CatTransferHub> _hubContext;

    public PeersController(INodeService nodeService, IHubContext<CatTransferHub> hubContext)
    {
        _nodeService = nodeService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PeerDto>>> GetConnectedPeers()
    {
        var peers = await _nodeService.GetConnectedPeersAsync();
        var peerDtos = peers.Select(p => new PeerDto
        {
            NodeId = p.NodeId.ToString(),
            Address = p.Address,
            Port = p.Port,
            ConnectedAt = p.ConnectedAt,
            IsConnected = p.IsConnected
        });

        return Ok(peerDtos);
    }

    [HttpPost("connect")]
    public async Task<ActionResult> ConnectToPeer([FromBody] ConnectPeerRequest request)
    {
        await _nodeService.ConnectToPeerAsync(request.Address, request.Port);


        await _hubContext.Clients.All.SendAsync("PeerConnecting", new
        {
            Address = request.Address,
            Port = request.Port
        });

        return Ok(new { message = $"Conectando ao peer {request.Address}:{request.Port}" });
    }

    [HttpDelete("{peerId}")]
    public async Task<ActionResult> DisconnectFromPeer(string peerId)
    {
        await _nodeService.DisconnectFromPeerAsync(new NodeId(peerId));


        await _hubContext.Clients.All.SendAsync("PeerDisconnected", new { PeerId = peerId });

        return Ok(new { message = $"Desconectado do peer {peerId}" });
    }
}

[ApiController]
[Route("api/[controller]")]
public class TransfersController : ControllerBase
{
    private readonly IFileTransferService _fileTransferService;
    private readonly IHubContext<CatTransferHub> _hubContext;

    public TransfersController(IFileTransferService fileTransferService, IHubContext<CatTransferHub> hubContext)
    {
        _fileTransferService = fileTransferService;
        _hubContext = hubContext;
    }

    [HttpPost]
    public async Task<ActionResult<TransferDto>> StartTransfer([FromBody] StartTransferRequest request)
    {
        var fileId = new FileId(request.FileId);
        var targetNodeId = new NodeId(request.TargetNodeId);


        var fileMeta = new Domain.Aggregates.FileTransfer.FileMeta(
            request.FileName,
            new Domain.ValueObjects.ByteSize(request.FileSize),
            64 * 1024,
            new Domain.ValueObjects.Checksum(new byte[32], Domain.Enumerations.ChecksumAlgorithm.Sha256)
        );

        var result = await _fileTransferService.StartTransferAsync(fileId, fileMeta, targetNodeId);


        await _hubContext.Clients.All.SendAsync("TransferStarted", new
        {
            TransferId = fileId.ToString(),
            FileName = request.FileName,
            TargetNodeId = request.TargetNodeId
        });

        return Ok(new TransferDto
        {
            TransferId = fileId.ToString(),
            FileName = request.FileName,
            Status = "Started",
            Progress = 0
        });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransferDto>>> GetActiveTransfers()
    {
        var transfers = await _fileTransferService.GetActiveTransfersAsync();
        var transferDtos = transfers.ActiveTransfers.Select(t => new TransferDto
        {
            TransferId = t.FileId.ToString(),
            FileName = $"file-{t.FileId}",
            Status = t.Status.ToString(),
            Progress = (int)(t.CompletionPercentage * 100),
            BytesTransferred = t.TransferredBytes.bytes,
            TotalBytes = t.TotalBytes.bytes
        }).ToList();

        return Ok(transferDtos);
    }

    [HttpPost("{transferId}/pause")]
    public async Task<ActionResult> PauseTransfer(string transferId)
    {
        var fileId = new FileId(transferId);
        var nodeId = new NodeId(Guid.NewGuid().ToString());

        await _fileTransferService.PauseTransferAsync(fileId, nodeId);


        await _hubContext.Clients.All.SendAsync("TransferPaused", new { TransferId = transferId });

        return Ok(new { message = $"Transferência {transferId} pausada" });
    }

    [HttpPost("{transferId}/resume")]
    public async Task<ActionResult> ResumeTransfer(string transferId)
    {
        var fileId = new FileId(transferId);
        var nodeId = new NodeId(Guid.NewGuid().ToString());

        await _fileTransferService.ResumeTransferAsync(fileId, nodeId);


        await _hubContext.Clients.All.SendAsync("TransferResumed", new { TransferId = transferId });

        return Ok(new { message = $"Transferência {transferId} retomada" });
    }

    [HttpDelete("{transferId}")]
    public async Task<ActionResult> CancelTransfer(string transferId)
    {
        var fileId = new FileId(transferId);
        var nodeId = new NodeId(Guid.NewGuid().ToString());

        await _fileTransferService.CancelTransferAsync(fileId, nodeId);


        await _hubContext.Clients.All.SendAsync("TransferCancelled", new { TransferId = transferId });

        return Ok(new { message = $"Transferência {transferId} cancelada" });
    }
}


public record NodeStatusDto
{
    public string NodeId { get; init; } = string.Empty;
    public string NodeName { get; init; } = string.Empty;
    public bool IsRunning { get; init; }
    public int ConnectedPeers { get; init; }
    public int ActiveTransfers { get; init; }
    public long TotalBytesTransferred { get; init; }
    public int UptimeSeconds { get; init; }
}

public record PeerDto
{
    public string NodeId { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public int Port { get; init; }
    public DateTime ConnectedAt { get; init; }
    public bool IsConnected { get; init; }
}

public record TransferDto
{
    public string TransferId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public double Progress { get; init; }
    public long BytesTransferred { get; init; }
    public long TotalBytes { get; init; }
}

public record ConnectPeerRequest(string Address, int Port);

public record StartTransferRequest(
    string FileId,
    string FileName,
    long FileSize,
    string TargetNodeId);
