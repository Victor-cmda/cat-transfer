using Microsoft.AspNetCore.Mvc;
using Application.Services;
using Microsoft.AspNetCore.Http;
using Domain.ValueObjects;
using Domain.Aggregates.FileTransfer;
using Domain.Enumerations;
using Node.Configuration;
using Node.Services;

namespace Node.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadsController : ControllerBase
{
    private readonly IFileTransferService _fileTransferService;
    private readonly IOutboundTransferOrchestrator _outbound;
    private readonly NodeConfiguration _nodeConfig;

    public UploadsController(
        IFileTransferService fileTransferService,
        IOutboundTransferOrchestrator outbound,
        NodeConfiguration nodeConfig)
    {
        _fileTransferService = fileTransferService;
        _outbound = outbound;
        _nodeConfig = nodeConfig;
    }

    [HttpPost]
    [RequestSizeLimit(1024L * 1024 * 1024)] // 1GB
    public async Task<ActionResult<TransferDto>> Upload(
        [FromQuery] string targetNodeId,
        [FromForm] IFormFile file,
        [FromQuery] int? chunkSize)
    {
        if (string.IsNullOrWhiteSpace(targetNodeId))
            return BadRequest(new { message = "targetNodeId é obrigatório" });
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Arquivo é obrigatório" });

        var preferredDir = "/app/test-files";
        string saveDir = Directory.Exists(preferredDir)
            ? preferredDir
            : Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(saveDir);

        var safeName = Path.GetFileName(file.FileName);
        var savePath = Path.Combine(saveDir, safeName);

        await using (var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            await file.CopyToAsync(fs);
        }

        var fileInfo = new FileInfo(savePath);
        var size = fileInfo.Length;

        byte[] checksum;
        using (var sha = System.Security.Cryptography.SHA256.Create())
        await using (var stream = System.IO.File.OpenRead(savePath))
        {
            checksum = await sha.ComputeHashAsync(stream);
        }

        var nodeChunkSize = Math.Clamp(chunkSize ?? _nodeConfig.Transfer.DefaultChunkSize, 4 * 1024, _nodeConfig.Transfer.MaxChunkSize);
        var meta = new FileMeta(
            name: safeName,
            size: new ByteSize(size),
            chunkSize: nodeChunkSize,
            hash: new Checksum(checksum, ChecksumAlgorithm.Sha256)
        );

        var fileId = new FileId(safeName);
        var target = new NodeId(targetNodeId);

        await _fileTransferService.StartTransferAsync(fileId, meta, target);
        _ = _outbound.SendFileAsync(fileId, target, meta, savePath);

        return Ok(new TransferDto
        {
            TransferId = fileId.ToString(),
            FileName = safeName,
            Status = "Started",
            Progress = 0,
            BytesTransferred = 0,
            TotalBytes = size
        });
    }
}
