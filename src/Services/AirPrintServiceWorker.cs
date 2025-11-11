using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZebraAirPrintService.Interfaces;

namespace ZebraAirPrintService.Services;

/// <summary>
/// Main background service that coordinates all AirPrint components
/// </summary>
public class AirPrintServiceWorker : BackgroundService
{
    private readonly ILogger<AirPrintServiceWorker> _logger;
    private readonly IIppServer _ippServer;
    private readonly IBonjourService _bonjourService;
    private readonly IQueueService _queueService;

    public AirPrintServiceWorker(
        ILogger<AirPrintServiceWorker> logger,
        IIppServer ippServer,
        IBonjourService bonjourService,
        IQueueService queueService)
    {
        _logger = logger;
        _ippServer = ippServer;
        _bonjourService = bonjourService;
        _queueService = queueService;
    }

    /// <summary>
    /// Main execution method for the background service
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("==============================================");
            _logger.LogInformation("AirPrint Service for Zebra ZD410 is starting");
            _logger.LogInformation("==============================================");

            // Start all services
            await StartServicesAsync(stoppingToken);

            _logger.LogInformation("==============================================");
            _logger.LogInformation("AirPrint Service is now running");
            _logger.LogInformation("Printer should be discoverable on the network");
            _logger.LogInformation("==============================================");

            // Keep running until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("AirPrint Service is shutting down...");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in AirPrint Service: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Starts all required services
    /// </summary>
    private async Task StartServicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 1. Start Bonjour service first (service advertisement)
            _logger.LogInformation("[1/3] Starting Bonjour/mDNS service...");
            await _bonjourService.StartAsync(cancellationToken);
            _logger.LogInformation("[1/3] Bonjour service started successfully");

            await Task.Delay(500, cancellationToken); // Small delay to ensure mDNS is ready

            // 2. Start IPP server (listens for print requests)
            _logger.LogInformation("[2/3] Starting IPP server...");
            var ippServerTask = Task.Run(() => _ippServer.StartAsync(cancellationToken), cancellationToken);
            await Task.Delay(1000, cancellationToken); // Give server time to start

            if (!_ippServer.IsRunning)
            {
                throw new Exception("IPP server failed to start");
            }

            _logger.LogInformation("[2/3] IPP server started successfully");

            // 3. Start queue processing (handles print jobs)
            _logger.LogInformation("[3/3] Starting queue processing service...");
            var queueTask = Task.Run(() => _queueService.StartProcessingAsync(cancellationToken), cancellationToken);
            _logger.LogInformation("[3/3] Queue processing service started successfully");

            // Wait for both background tasks
            _ = Task.WhenAll(ippServerTask, queueTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start services: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gracefully stops all services
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("==============================================");
            _logger.LogInformation("AirPrint Service is stopping...");
            _logger.LogInformation("==============================================");

            // Stop services in reverse order
            _logger.LogInformation("[1/3] Stopping queue processing...");
            _queueService.StopProcessing();
            _logger.LogInformation("[1/3] Queue processing stopped");

            _logger.LogInformation("[2/3] Stopping IPP server...");
            await _ippServer.StopAsync();
            _logger.LogInformation("[2/3] IPP server stopped");

            _logger.LogInformation("[3/3] Stopping Bonjour service...");
            await _bonjourService.StopAsync();
            _logger.LogInformation("[3/3] Bonjour service stopped");

            _logger.LogInformation("==============================================");
            _logger.LogInformation("AirPrint Service stopped successfully");
            _logger.LogInformation("==============================================");

            await base.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during service shutdown: {ErrorMessage}", ex.Message);
        }
    }
}
