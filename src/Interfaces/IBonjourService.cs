namespace ZebraAirPrintService.Interfaces;

/// <summary>
/// Interface for Bonjour/mDNS service advertisement
/// </summary>
public interface IBonjourService
{
    /// <summary>
    /// Starts advertising the printer service via mDNS
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops advertising the printer service
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Gets whether the service is currently advertising
    /// </summary>
    bool IsAdvertising { get; }
}
