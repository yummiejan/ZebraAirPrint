using Microsoft.Extensions.Configuration;
using ZebraAirPrintService.Models;

namespace ZebraAirPrintService.Utils;

/// <summary>
/// Manages application configuration
/// </summary>
public class ConfigManager
{
    private readonly IConfiguration _configuration;

    public ConfigManager(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the printer configuration
    /// </summary>
    public PrinterConfiguration GetPrinterConfig()
    {
        var config = new PrinterConfiguration();
        _configuration.GetSection("Printer").Bind(config);
        return config;
    }

    /// <summary>
    /// Gets the service configuration
    /// </summary>
    public ServiceConfiguration GetServiceConfig()
    {
        var config = new ServiceConfiguration();
        _configuration.GetSection("Service").Bind(config);
        return config;
    }

    /// <summary>
    /// Gets the queue configuration
    /// </summary>
    public QueueConfiguration GetQueueConfig()
    {
        var config = new QueueConfiguration();
        _configuration.GetSection("Queue").Bind(config);
        return config;
    }

    /// <summary>
    /// Gets the logging configuration
    /// </summary>
    public LoggingConfiguration GetLoggingConfig()
    {
        var config = new LoggingConfiguration();
        _configuration.GetSection("Logging").Bind(config);
        return config;
    }
}
