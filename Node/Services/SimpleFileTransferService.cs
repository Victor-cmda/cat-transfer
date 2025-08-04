using Application.Services;
using Application.Messages;
using Domain.ValueObjects;
using Domain.Aggregates.FileTransfer;
using Domain.Enumerations;
using Microsoft.Extensions.Logging;

namespace Node.Services;

public class SimpleFileTransferService : IFileTransferService
{
    private readonly ILogger<SimpleFileTransferService> _logger;
    private readonly Dictionary<FileId, SimpleTransferInfo> _activeTransfers;

    public SimpleFileTransferService(ILogger<SimpleFileTransferService> logger)
    {
        _logger = logger;
        _activeTransfers = new Dictionary<FileId, SimpleTransferInfo>();
    }

    public async Task<FileTransferStarted> StartTransferAsync(FileId fileId, FileMeta meta, NodeId? initiatorNode = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando transferência para arquivo {FileId}", fileId);
        
        _activeTransfers[fileId] = new SimpleTransferInfo
        {
            FileId = fileId,
            Status = TransferStatus.InProgress,
            Progress = 0,
            StartTime = DateTime.UtcNow,
            InitiatorNode = initiatorNode,
            TotalBytes = meta.Size
        };

        await Task.Delay(100, cancellationToken);

        return new FileTransferStarted(
            fileId,
            initiatorNode ?? NodeId.NewGuid(),
            DateTime.UtcNow
        );
    }

    public async Task<FileTransferPaused> PauseTransferAsync(FileId fileId, NodeId requestingNode, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Pausando transferência para arquivo {FileId}", fileId);

        if (_activeTransfers.ContainsKey(fileId))
        {
            _activeTransfers[fileId].Status = TransferStatus.Paused;
        }

        await Task.Delay(50, cancellationToken);

        return new FileTransferPaused(
            fileId,
            requestingNode,
            DateTime.UtcNow
        );
    }

    public async Task<FileTransferResumed> ResumeTransferAsync(FileId fileId, NodeId requestingNode, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retomando transferência para arquivo {FileId}", fileId);

        if (_activeTransfers.ContainsKey(fileId))
        {
            _activeTransfers[fileId].Status = TransferStatus.InProgress;
        }

        await Task.Delay(50, cancellationToken);

        return new FileTransferResumed(
            fileId,
            requestingNode,
            DateTime.UtcNow
        );
    }

    public async Task<FileTransferCancelled> CancelTransferAsync(FileId fileId, NodeId requestingNode, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cancelando transferência para arquivo {FileId}", fileId);

        if (_activeTransfers.ContainsKey(fileId))
        {
            _activeTransfers.Remove(fileId);
        }

        await Task.Delay(50, cancellationToken);

        return new FileTransferCancelled(
            fileId,
            requestingNode,
            DateTime.UtcNow
        );
    }

    public async Task<FileTransferStatusResponse> GetTransferStatusAsync(FileId fileId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);

        if (_activeTransfers.TryGetValue(fileId, out var transfer))
        {
            return new FileTransferStatusResponse(
                fileId,
                transfer.Status,
                transfer.Progress,
                new ByteSize((long)(transfer.TotalBytes.bytes * transfer.Progress / 100.0)),
                transfer.TotalBytes,
                DateTime.UtcNow - transfer.StartTime,
                new[] { transfer.InitiatorNode ?? NodeId.NewGuid() }
            );
        }

        return new FileTransferStatusResponse(
            fileId,
            TransferStatus.Failed,
            0,
            new ByteSize(0),
            new ByteSize(0),
            TimeSpan.Zero,
            Array.Empty<NodeId>()
        );
    }

    public async Task<ActiveTransfersResponse> GetActiveTransfersAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);

        var activeTransferResponses = new List<FileTransferStatusResponse>();

        foreach (var transfer in _activeTransfers.Values.Where(t => t.Status == TransferStatus.InProgress))
        {
            activeTransferResponses.Add(new FileTransferStatusResponse(
                transfer.FileId,
                transfer.Status,
                transfer.Progress,
                new ByteSize((long)(transfer.TotalBytes.bytes * transfer.Progress / 100.0)),
                transfer.TotalBytes,
                DateTime.UtcNow - transfer.StartTime,
                new[] { transfer.InitiatorNode ?? NodeId.NewGuid() }
            ));
        }

        return new ActiveTransfersResponse(
            activeTransferResponses,
            activeTransferResponses.Count
        );
    }
}

internal class SimpleTransferInfo
{
    public FileId FileId { get; set; }
    public TransferStatus Status { get; set; }
    public double Progress { get; set; }
    public DateTime StartTime { get; set; }
    public NodeId? InitiatorNode { get; set; }
    public ByteSize TotalBytes { get; set; }
}
