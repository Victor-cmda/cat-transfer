using System.Text;
using System.Text.Json;
using Domain.ValueObjects;
using Domain.Aggregates.FileTransfer;
using Microsoft.Extensions.Logging;
using Node.Network;

namespace Node.Services;

public interface IOutboundTransferOrchestrator
{
    Task SendFileAsync(FileId fileId, NodeId targetNode, FileMeta meta, string sourcePath, CancellationToken ct = default);
}

internal record FileChunkEnvelope(
    string Type,
    string FileId,
    long Offset,
    string SourceNodeId,
    byte[] Data
);

internal record FileInitEnvelope(
    string Type,
    string FileId,
    string FileName,
    long FileSize,
    int ChunkSize,
    string SourceNodeId,
    byte[] Checksum,
    Domain.Enumerations.ChecksumAlgorithm ChecksumAlgorithm
);

public class OutboundTransferOrchestrator : IOutboundTransferOrchestrator
{
    private readonly ILogger<OutboundTransferOrchestrator> _logger;
    private readonly IP2PNetworkManager _network;
    private readonly Application.Actors.ApplicationActorSystem _actorSystem;
    private readonly Node.Configuration.NodeConfiguration _nodeConfig;

    public OutboundTransferOrchestrator(ILogger<OutboundTransferOrchestrator> logger, IP2PNetworkManager network, Node.Configuration.NodeConfiguration nodeConfig, Application.Actors.ApplicationActorSystem actorSystem)
    {
        _logger = logger;
        _network = network;
        _nodeConfig = nodeConfig;
        _actorSystem = actorSystem;
    }

    public async Task SendFileAsync(FileId fileId, NodeId targetNode, FileMeta meta, string sourcePath, CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
        {
            _logger.LogWarning("Arquivo não encontrado para envio: {Path}", sourcePath);
            return;
        }

        try
        {
            // Envia metadados iniciais
            var init = new FileInitEnvelope(
                Type: "file_init",
                FileId: fileId.ToString(),
                FileName: meta.Name,
                FileSize: meta.Size.bytes,
                ChunkSize: meta.ChunkSize,
                SourceNodeId: _nodeConfig.NodeId.ToString(),
                Checksum: meta.Hash.value,
                ChecksumAlgorithm: meta.Hash.algorithm
            );
            await _network.SendMessageToPeerAsync(targetNode, init);

            using var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[meta.ChunkSize];
            long offset = 0;
            int read;
            while ((read = await fs.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                var data = buffer;
                if (read != buffer.Length)
                {
                    data = new byte[read];
                    Buffer.BlockCopy(buffer, 0, data, 0, read);
                }

                var envelope = new FileChunkEnvelope(
                    Type: "file_chunk",
                    FileId: fileId.ToString(),
                    Offset: offset,
                    SourceNodeId: _nodeConfig.NodeId.ToString(),
                    Data: data
                );

                await _network.SendMessageToPeerAsync(targetNode, envelope);
                offset += read;

                // Notifica progresso de envio para o supervisor (para UI do remetente)
                try
                {
                    _actorSystem.FileTransferSupervisor.Tell(
                        new Application.Messages.OutboundChunkSentNotice(fileId, offset),
                        Akka.Actor.ActorRefs.NoSender);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Falha ao notificar progresso outbound");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar arquivo {FileId} para {Target}", fileId, targetNode);
        }
    }
}
