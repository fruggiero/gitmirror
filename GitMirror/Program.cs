using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GitMirror.Configuration;
using GitMirror.Services;

namespace GitMirror;

static class Program
{
    static async Task Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        // Create host builder
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Bind configuration
                var gitMirrorConfig = new GitMirrorConfig();
                configuration.GetSection("GitMirror").Bind(gitMirrorConfig);
                services.AddSingleton(gitMirrorConfig);

                // Add services
                services.AddSingleton<GitService>();
                services.AddLogging(builder => builder.AddConsole());
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<GitService>>();
        var gitService = host.Services.GetRequiredService<GitService>();
        var config = host.Services.GetRequiredService<GitMirrorConfig>();

        logger.LogInformation("GitMirror application started");

        // Check command line arguments
        if (args.Length > 0)
        {
            switch (args[0].ToLower())
            {
                case "mirror":
                    logger.LogInformation("Performing one-time mirror operation");
                    var mirrorResult = await gitService.MirrorRepositoryAsync();
                    Environment.Exit(mirrorResult ? 0 : 1);
                    break;

                case "sync":
                    logger.LogInformation("Performing one-time sync operation");
                    var syncResult = await gitService.SyncRepositoryAsync();
                    Environment.Exit(syncResult ? 0 : 1);
                    break;

                case "daemon":
                    logger.LogInformation("Starting continuous sync daemon");
                    await RunDaemonAsync(gitService, config, logger);
                    break;

                default:
                    Console.WriteLine("Usage: GitMirror [mirror|sync|daemon]");
                    Console.WriteLine("  mirror  - Perform a one-time mirror operation (clone source and push to target)");
                    Console.WriteLine("  sync    - Perform a one-time sync operation (fetch updates and push to target)");
                    Console.WriteLine("  daemon  - Run continuously, syncing at specified intervals");
                    Environment.Exit(1);
                    break;
            }
        }
        else
        {
            // Default behavior - perform a sync operation
            logger.LogInformation("No command specified, performing sync operation");
            var result = await gitService.SyncRepositoryAsync();
            Environment.Exit(result ? 0 : 1);
        }
    }

    private static async Task RunDaemonAsync(GitService gitService, GitMirrorConfig config, ILogger logger)
    {
        using var cancellationToken = new CancellationTokenSource();
        
        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cancellationToken.Cancel();
            logger.LogInformation("Shutdown requested");
        };

        logger.LogInformation("Daemon started. Sync interval: {Interval} seconds", config.SyncInterval);

        while (!cancellationToken.Token.IsCancellationRequested)
        {
            try
            {
                await gitService.SyncRepositoryAsync();
                logger.LogInformation("Next sync in {Interval} seconds", config.SyncInterval);
                await Task.Delay(config.SyncInterval * 1000, cancellationToken.Token);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogInformation(ex, "Daemon shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in daemon loop");
                await Task.Delay(5000, cancellationToken.Token); // Wait 5 seconds before retrying
            }
        }

        logger.LogInformation("Daemon stopped");
    }
}
