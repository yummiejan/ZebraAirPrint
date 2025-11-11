namespace ZebraAirPrintService.Models;

/// <summary>
/// Represents the current status of the printer
/// </summary>
public class PrinterStatus
{
    /// <summary>
    /// Indicates whether the printer is online and available
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Number of jobs currently in the queue
    /// </summary>
    public int JobCount { get; set; }

    /// <summary>
    /// Last error message (if any)
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Timestamp of last status check
    /// </summary>
    public DateTime LastChecked { get; set; } = DateTime.Now;

    /// <summary>
    /// Printer name
    /// </summary>
    public string PrinterName { get; set; } = string.Empty;
}
