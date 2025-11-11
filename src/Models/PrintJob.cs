namespace ZebraAirPrintService.Models;

/// <summary>
/// Represents a print job in the queue
/// </summary>
public class PrintJob
{
    /// <summary>
    /// Unique identifier for the job
    /// </summary>
    public int JobId { get; set; }

    /// <summary>
    /// Content type of the document (e.g., application/pdf, image/urf)
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Document data as byte array
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Current status of the job
    /// </summary>
    public PrintJobStatus Status { get; set; } = PrintJobStatus.Pending;

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Timestamp when the job was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Timestamp of last retry attempt
    /// </summary>
    public DateTime? LastRetryAttempt { get; set; }

    /// <summary>
    /// Error message if job failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Document name (optional)
    /// </summary>
    public string? DocumentName { get; set; }
}

/// <summary>
/// Status of a print job
/// </summary>
public enum PrintJobStatus
{
    /// <summary>
    /// Job is waiting to be processed
    /// </summary>
    Pending,

    /// <summary>
    /// Job is currently being processed
    /// </summary>
    Processing,

    /// <summary>
    /// Job completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Job failed to process
    /// </summary>
    Failed
}
