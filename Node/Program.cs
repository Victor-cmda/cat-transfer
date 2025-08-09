using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Serilog;
using Akka.Hosting;
using Akka.Actor;
using Node.Configuration;
using Node.Services;
using Node.Network;
using Node.Cli;
using Node.Api.Hubs;
using Application.Services;

namespace Node;

internal class Program
{
    static async Task Main(string[] args)
    {
        var configuration = LoadConfiguration();
        var nodeConfig = GetNodeConfiguration(configuration);
        
        if (ShouldRunAsApi(args))
        {
            await RunAsApi(args, configuration, nodeConfig);
        }
        else if (ShouldRunAsDaemon(args))
        {
            await RunAsDaemon(args, configuration, nodeConfig);
        }
        else
        {
            await RunAsConsole(args, configuration, nodeConfig);
        }
    }

    private static IConfiguration LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(Environment.GetCommandLineArgs())
            .Build();
    }

    private static NodeConfiguration GetNodeConfiguration(IConfiguration configuration)
    {
        var nodeConfig = new NodeConfiguration();
        configuration.GetSection("Node").Bind(nodeConfig);
        return nodeConfig;
    }

    private static bool ShouldRunAsApi(string[] args)
    {
        return args.Contains("--api") || args.Contains("--web");
    }

    private static bool ShouldRunAsDaemon(string[] args)
    {
        return args.Contains("--daemon") || args.Contains("--service");
    }

    private static async Task RunAsApi(string[] args, IConfiguration configuration, NodeConfiguration nodeConfig)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((context, services, loggerConfig) =>
            loggerConfig.WriteTo.Console()
                       .WriteTo.File("logs/node-.txt", rollingInterval: RollingInterval.Day));

        ConfigureApplicationServices(builder.Services, configuration, nodeConfig);

        builder.Services.AddControllers();
        builder.Services.AddSignalR();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("ReactPolicy", policy =>
            {
                policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseDeveloperExceptionPage();
        }

        app.UseHttpsRedirection();
        app.UseCors("ReactPolicy");
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<CatTransferHub>("/hub/transfers");

        var nodeService = app.Services.GetRequiredService<INodeService>();
        await nodeService.StartAsync();

        await app.RunAsync();
    }

    private static async Task RunAsDaemon(string[] args, IConfiguration configuration, NodeConfiguration nodeConfig)
    {
        var builder = Host.CreateDefaultBuilder(args);

        builder.UseSerilog((context, services, loggerConfig) =>
            loggerConfig.WriteTo.Console()
                       .WriteTo.File("logs/node-.txt", rollingInterval: RollingInterval.Day));

        builder.ConfigureServices(services =>
        {
            ConfigureApplicationServices(services, configuration, nodeConfig);
            services.AddHostedService<NodeDaemonService>();
        });

        var host = builder.Build();
        await host.RunAsync();
    }

    private static async Task RunAsConsole(string[] args, IConfiguration configuration, NodeConfiguration nodeConfig)
    {
        var builder = Host.CreateDefaultBuilder(args);

        builder.UseSerilog((context, services, loggerConfig) =>
            loggerConfig.WriteTo.Console()
                       .WriteTo.File("logs/node-.txt", rollingInterval: RollingInterval.Day));

        builder.ConfigureServices(services =>
        {
            ConfigureApplicationServices(services, configuration, nodeConfig);
        });

        var host = builder.Build();

        var cli = host.Services.GetRequiredService<CommandLineInterface>();
        var nodeService = host.Services.GetRequiredService<INodeService>();

        await nodeService.StartAsync();
        
        Console.WriteLine("Cat Transfer Node iniciado. Pressione 'q' para sair...");
        while (Console.ReadKey().KeyChar != 'q')
        {
            await Task.Delay(100);
        }
        
        await nodeService.StopAsync();
    }

    private static void ConfigureApplicationServices(IServiceCollection services, IConfiguration configuration, NodeConfiguration nodeConfig)
    {
        services.AddSingleton(nodeConfig);
        services.AddSingleton(configuration);

        services.AddTransient<Domain.Services.IChecksumService, Domain.Services.MultiAlgorithmChecksumService>();
        services.AddTransient<Domain.Services.IChunkingStrategy, Domain.Services.DefaultChunkingStrategy>();

        services.AddSingleton<Infrastructure.Storage.Interfaces.IMetadataStore, Infrastructure.Storage.Implementations.FileMetadataStore>();
        services.AddSingleton<Infrastructure.Storage.Interfaces.IFileRepository, Infrastructure.Storage.Implementations.LocalFileRepository>();
        services.AddSingleton<Infrastructure.Storage.Interfaces.IChunkStorage, Infrastructure.Storage.Implementations.LocalChunkStorage>();
        services.AddSingleton<Infrastructure.Storage.Interfaces.ITempFileManager, Infrastructure.Storage.Implementations.TempFileManager>();

        services.AddSingleton<IFileTransferService, Application.Services.FileTransferService>();

        services.AddSingleton<ActorSystem>(provider =>
        {
            var actorSystem = ActorSystem.Create("CatTransferActorSystem");
            return actorSystem;
        });
        services.AddSingleton<Application.Actors.ApplicationActorSystem>();
        services.AddSingleton<Application.Services.INetworkService, Application.Services.NetworkService>();

        services.AddSingleton<INodeService, NodeService>();
        services.AddSingleton<IP2PNetworkManager, P2PNetworkManager>();
    services.AddSingleton<IOutboundTransferOrchestrator, OutboundTransferOrchestrator>();
        services.AddSingleton<CommandLineInterface>();
        services.AddSingleton<ICatTransferNotificationService, CatTransferNotificationService>();
    }
}

public class NodeDaemonService : BackgroundService
{
    private readonly INodeService _nodeService;
    private readonly ILogger<NodeDaemonService> _logger;

    public NodeDaemonService(INodeService nodeService, ILogger<NodeDaemonService> logger)
    {
        _nodeService = nodeService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Node daemon service started");
        
        await _nodeService.StartAsync();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
        
        await _nodeService.StopAsync();
        _logger.LogInformation("Node daemon service stopped");
    }
}
