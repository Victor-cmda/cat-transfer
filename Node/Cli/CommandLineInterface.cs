using Node.Services;
using Domain.ValueObjects;
using Application.Services;
using Microsoft.Extensions.Logging;

namespace Node.Cli;

public class CommandLineInterface
{
    private readonly INodeService _nodeService;
    private readonly IFileTransferService _fileTransferService;
    private readonly INetworkService _networkService;
    private readonly ILogger<CommandLineInterface> _logger;
    private bool _isRunning;

    public CommandLineInterface(
        INodeService nodeService,
        IFileTransferService fileTransferService,
        INetworkService networkService,
        ILogger<CommandLineInterface> logger)
    {
        _nodeService = nodeService;
        _fileTransferService = fileTransferService;
        _networkService = networkService;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        _isRunning = true;
        
        Console.WriteLine("=== Cat Transfer Node ===");
        Console.WriteLine("Digite 'help' para ver os comandos disponíveis");
        Console.WriteLine();

        while (_isRunning)
        {
            Console.Write("cat-transfer> ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            try
            {
                await ProcessCommandAsync(input);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
                _logger.LogError(ex, "Erro ao processar comando: {Command}", input);
            }
        }
    }

    private async Task ProcessCommandAsync(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();

        switch (command)
        {
            case "help":
                ShowHelp();
                break;

            case "start":
                await StartNodeAsync();
                break;

            case "stop":
                await StopNodeAsync();
                break;

            case "status":
                await ShowStatusAsync();
                break;

            case "connect":
                if (parts.Length >= 3)
                    await ConnectToPeerAsync(parts[1], parts[2]);
                else
                    Console.WriteLine("Uso: connect <endereço> <porta>");
                break;

            case "disconnect":
                if (parts.Length >= 2 && Guid.TryParse(parts[1], out var peerId))
                    await DisconnectFromPeerAsync(new NodeId(peerId.ToString()));
                else
                    Console.WriteLine("Uso: disconnect <peer-id>");
                break;

            case "peers":
                await ShowPeersAsync();
                break;

            case "transfer":
                if (parts.Length >= 3)
                    await StartTransferAsync(parts[1], parts[2]);
                else
                    Console.WriteLine("Uso: transfer <arquivo> <peer-id>");
                break;

            case "transfers":
                await ShowTransfersAsync();
                break;

            case "pause":
                if (parts.Length >= 2 && Guid.TryParse(parts[1], out var pauseId))
                    await PauseTransferAsync(new FileId(pauseId.ToString()));
                else
                    Console.WriteLine("Uso: pause <transfer-id>");
                break;

            case "resume":
                if (parts.Length >= 2 && Guid.TryParse(parts[1], out var resumeId))
                    await ResumeTransferAsync(new FileId(resumeId.ToString()));
                else
                    Console.WriteLine("Uso: resume <transfer-id>");
                break;

            case "cancel":
                if (parts.Length >= 2 && Guid.TryParse(parts[1], out var cancelId))
                    await CancelTransferAsync(new FileId(cancelId.ToString()));
                else
                    Console.WriteLine("Uso: cancel <transfer-id>");
                break;

            case "clear":
                Console.Clear();
                break;

            case "exit":
            case "quit":
                await ExitAsync();
                break;

            default:
                Console.WriteLine($"Comando desconhecido: {command}. Digite 'help' para ver os comandos disponíveis.");
                break;
        }
    }

    private void ShowHelp()
    {
        Console.WriteLine("Comandos disponíveis:");
        Console.WriteLine("  help                     - Mostra esta ajuda");
        Console.WriteLine("  start                    - Inicia o nó");
        Console.WriteLine("  stop                     - Para o nó");
        Console.WriteLine("  status                   - Mostra o status do nó");
        Console.WriteLine("  connect <endereço> <porta> - Conecta a um peer");
        Console.WriteLine("  disconnect <peer-id>     - Desconecta de um peer");
        Console.WriteLine("  peers                    - Lista peers conectados");
        Console.WriteLine("  transfer <arquivo> <peer-id> - Inicia transferência");
        Console.WriteLine("  transfers                - Lista transferências ativas");
        Console.WriteLine("  pause <transfer-id>      - Pausa uma transferência");
        Console.WriteLine("  resume <transfer-id>     - Retoma uma transferência");
        Console.WriteLine("  cancel <transfer-id>     - Cancela uma transferência");
        Console.WriteLine("  clear                    - Limpa a tela");
        Console.WriteLine("  exit, quit               - Sai do programa");
    }

    private async Task StartNodeAsync()
    {
        Console.WriteLine("Iniciando nó...");
        await _nodeService.StartAsync();
        Console.WriteLine("Nó iniciado com sucesso!");
    }

    private async Task StopNodeAsync()
    {
        Console.WriteLine("Parando nó...");
        await _nodeService.StopAsync();
        Console.WriteLine("Nó parado com sucesso!");
    }

    private async Task ShowStatusAsync()
    {
        var status = await _nodeService.GetStatusAsync();
        
        Console.WriteLine("=== Status do Nó ===");
        Console.WriteLine($"ID: {status.NodeId}");
        Console.WriteLine($"Nome: {status.NodeName}");
        Console.WriteLine($"Status: {(status.IsRunning ? "Ativo" : "Inativo")}");
        Console.WriteLine($"Peers Conectados: {status.ConnectedPeers}");
        Console.WriteLine($"Transferências Ativas: {status.ActiveTransfers}");
        Console.WriteLine($"Total Transferido: {FormatBytes(status.TotalBytesTransferred)}");
        Console.WriteLine($"Tempo de Atividade: {status.Uptime:hh\\:mm\\:ss}");
    }

    private async Task ConnectToPeerAsync(string address, string portStr)
    {
        if (!int.TryParse(portStr, out var port))
        {
            Console.WriteLine("Porta inválida");
            return;
        }

        Console.WriteLine($"Conectando ao peer {address}:{port}...");
        await _nodeService.ConnectToPeerAsync(address, port);
        Console.WriteLine("Conectado com sucesso!");
    }

    private async Task DisconnectFromPeerAsync(NodeId peerId)
    {
        Console.WriteLine($"Desconectando do peer {peerId}...");
        await _nodeService.DisconnectFromPeerAsync(peerId);
        Console.WriteLine("Desconectado com sucesso!");
    }

    private async Task ShowPeersAsync()
    {
        var peers = await _nodeService.GetConnectedPeersAsync();
        
        Console.WriteLine("=== Peers Conectados ===");
        if (!peers.Any())
        {
            Console.WriteLine("Nenhum peer conectado");
            return;
        }

        foreach (var peer in peers)
        {
            var duration = DateTime.UtcNow - peer.ConnectedAt;
            Console.WriteLine($"ID: {peer.NodeId}");
            Console.WriteLine($"Endereço: {peer.Address}:{peer.Port}");
            Console.WriteLine($"Conectado há: {duration:hh\\:mm\\:ss}");
            Console.WriteLine("---");
        }
    }

    private async Task StartTransferAsync(string filePath, string peerIdStr)
    {
        if (!Guid.TryParse(peerIdStr, out var peerGuid))
        {
            Console.WriteLine("ID do peer inválido");
            return;
        }

        if (!File.Exists(filePath))
        {
            Console.WriteLine("Arquivo não encontrado");
            return;
        }

        var fileInfo = new FileInfo(filePath);
        var fileId = new FileId(Guid.NewGuid().ToString());
        var peerId = new NodeId(peerGuid.ToString());
        var fileMeta = new Domain.Aggregates.FileTransfer.FileMeta(
            fileInfo.Name,
            new Domain.ValueObjects.ByteSize(fileInfo.Length),
            64 * 1024, 
            new Domain.ValueObjects.Checksum(new byte[32], Domain.Enumerations.ChecksumAlgorithm.Sha256));

        Console.WriteLine($"Iniciando transferência do arquivo {filePath}...");
        var response = await _fileTransferService.StartTransferAsync(fileId, fileMeta, peerId);
        
        if (response != null)
        {
            Console.WriteLine($"Transferência iniciada com ID: {fileId}");
        }
        else
        {
            Console.WriteLine("Erro ao iniciar transferência");
        }
    }

    private async Task ShowTransfersAsync()
    {
        var transfers = await _fileTransferService.GetActiveTransfersAsync();
        
        Console.WriteLine("=== Transferências ===");
        if (transfers == null)
        {
            Console.WriteLine("Nenhuma transferência ativa");
            return;
        }

        
        Console.WriteLine("Lista de transferências (implementação pendente)");
    }

    private async Task PauseTransferAsync(FileId transferId)
    {
        Console.WriteLine($"Pausando transferência {transferId}...");
        var nodeId = new NodeId(Guid.NewGuid().ToString()); 
        await _fileTransferService.PauseTransferAsync(transferId, nodeId);
        Console.WriteLine("Transferência pausada!");
    }

    private async Task ResumeTransferAsync(FileId transferId)
    {
        Console.WriteLine($"Retomando transferência {transferId}...");
        var nodeId = new NodeId(Guid.NewGuid().ToString()); 
        await _fileTransferService.ResumeTransferAsync(transferId, nodeId);
        Console.WriteLine("Transferência retomada!");
    }

    private async Task CancelTransferAsync(FileId transferId)
    {
        Console.WriteLine($"Cancelando transferência {transferId}...");
        var nodeId = new NodeId(Guid.NewGuid().ToString()); 
        await _fileTransferService.CancelTransferAsync(transferId, nodeId);
        Console.WriteLine("Transferência cancelada!");
    }

    private async Task ExitAsync()
    {
        Console.WriteLine("Saindo...");
        
        if (await _nodeService.IsRunningAsync())
        {
            Console.WriteLine("Parando nó...");
            await _nodeService.StopAsync();
        }

        _isRunning = false;
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        
        return $"{number:n1} {suffixes[counter]}";
    }
}
