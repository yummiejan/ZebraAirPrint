using ZebraAirPrintService.Models;

namespace ZebraAirPrintService.Interfaces;

/// <summary>
/// Interface for printer operations
/// </summary>
public interface IPrinterService
{
    /// <summary>
    /// Prints a job to the Windows printer
    /// </summary>
    /// <param name="job">The print job to process</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> PrintAsync(PrintJob job);

    /// <summary>
    /// Gets the current status of the printer
    /// </summary>
    /// <returns>Printer status information</returns>
    Task<PrinterStatus> GetStatusAsync();

    /// <summary>
    /// Checks if the printer is available for printing
    /// </summary>
    /// <returns>True if printer is online and ready</returns>
    bool IsPrinterAvailable();
}
