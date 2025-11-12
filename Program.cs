using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using ZebraAirPrintService.Interfaces;
using ZebraAirPrintService.Services;
using ZebraAirPrintService.Utils;

namespace ZebraAirPrintService;

/// <summary>
/// Main entry point for the AirPrint service
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Initial bootstrap logger (before configuration is loaded)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting Zebra AirPrint Service");

            var host = CreateHostBuilder(args).Build();

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Creates and configures the host builder
    /// </summary>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "ZebraAirPrintService";
            })
            .ConfigureAppConfiguration((context, config) =>
            {
                config
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Configure Serilog from configuration
                ConfigureSerilog(context.Configuration);

                // Register configuration manager
                services.AddSingleton<ConfigManager>();

                // Register utilities
                services.AddSingleton<IppParser>();

                // Register services as singletons
                services.AddSingleton<IPrinterService, PrinterService>();
                services.AddSingleton<IQueueService, QueueService>();
                services.AddSingleton<IIppServer, IppServer>();
                services.AddSingleton<IBonjourService, BonjourService>();

                // Register the main background service
                services.AddHostedService<AirPrintServiceWorker>();
            })
            .UseSerilog();

    /// <summary>
    /// Configures Serilog logging
    /// </summary>
    private static void ConfigureSerilog(IConfiguration configuration)
    {
        // Get logging configuration
        var configuredPath = configuration["Logging:Path"];
        var loggingPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "Logs")
            : Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppContext.BaseDirectory, configuredPath);

        var retentionDays = int.Parse(configuration["Logging:RetentionDays"] ?? "30");
        var minimumLevel = configuration["Logging:MinimumLevel"] ?? "Information";

        // Ensure log directory exists
        Directory.CreateDirectory(loggingPath);

        // Parse minimum level
        var logLevel = minimumLevel.ToLowerInvariant() switch
        {
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithMachineName()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(loggingPath, "airprint-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retentionDays,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                shared: true)
            .CreateLogger();

        Log.Information("Serilog configured: Path={LogPath}, RetentionDays={RetentionDays}, MinimumLevel={MinimumLevel}",
            loggingPath, retentionDays, minimumLevel);
    }
}
