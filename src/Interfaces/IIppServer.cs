namespace ZebraAirPrintService.Interfaces;

/// <summary>
/// Interface for IPP server operations
/// </summary>
public interface IIppServer
{
    /// <summary>
    /// Starts the IPP server
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the IPP server
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Gets whether the server is running
    /// </summary>
    bool IsRunning { get; }
}
