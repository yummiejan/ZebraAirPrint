namespace ZebraAirPrintService.Models;

/// <summary>
/// Configuration for the printer
/// </summary>
public class PrinterConfiguration
{
    /// <summary>
    /// Name of the Windows printer
    /// </summary>
    public string Name { get; set; } = "Zebra ZD410";

    /// <summary>
    /// Label width in millimeters
    /// </summary>
    public double LabelWidth { get; set; } = 50.7;

    /// <summary>
    /// Label height in millimeters
    /// </summary>
    public double LabelHeight { get; set; } = 30.6;

    /// <summary>
    /// Printer resolution in DPI
    /// </summary>
    public int Resolution { get; set; } = 203;

    /// <summary>
    /// Connection type: "WindowsPrinter" (uses Windows print spooler) or "DirectIP" (direct TCP/IP connection)
    /// </summary>
    public string ConnectionType { get; set; } = "WindowsPrinter";

    /// <summary>
    /// IP address of the printer (only used when ConnectionType is "DirectIP")
    /// </summary>
    public string IpAddress { get; set; } = "";

    /// <summary>
    /// Port for direct connection (typically 9100 for Zebra printers)
    /// </summary>
    public int Port { get; set; } = 9100;

    /// <summary>
    /// Gets the label width in pixels (at specified resolution)
    /// </summary>
    public int LabelWidthPixels => (int)(LabelWidth / 25.4 * Resolution);

    /// <summary>
    /// Gets the label height in pixels (at specified resolution)
    /// </summary>
    public int LabelHeightPixels => (int)(LabelHeight / 25.4 * Resolution);
}

/// <summary>
/// Configuration for the service
/// </summary>
public class ServiceConfiguration
{
    /// <summary>
    /// Service name
    /// </summary>
    public string Name { get; set; } = "Zebra AirPrint Service";

    /// <summary>
    /// IPP server port
    /// </summary>
    public int IppPort { get; set; } = 631;

    /// <summary>
    /// Bonjour service name (advertised to clients)
    /// </summary>
    public string BonjourServiceName { get; set; } = "Zebra ZD410";
}

/// <summary>
/// Configuration for the print queue
/// </summary>
public class QueueConfiguration
{
    /// <summary>
    /// Maximum number of jobs in queue
    /// </summary>
    public int MaxJobs { get; set; } = 50;

    /// <summary>
    /// Retry interval in seconds
    /// </summary>
    public int RetryIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Enable exponential backoff for retries
    /// </summary>
    public bool ExponentialBackoffEnabled { get; set; } = true;

    /// <summary>
    /// Maximum backoff time in seconds
    /// </summary>
    public int MaxBackoffSeconds { get; set; } = 60;
}

/// <summary>
/// Configuration for logging
/// </summary>
public class LoggingConfiguration
{
    /// <summary>
    /// Path to log files
    /// </summary>
    public string Path { get; set; } = "C:\\AirPrintService\\Logs";

    /// <summary>
    /// Number of days to retain logs
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Minimum log level (Information, Warning, Error)
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";
}
