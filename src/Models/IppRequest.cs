namespace ZebraAirPrintService.Models;

/// <summary>
/// Represents an IPP (Internet Printing Protocol) request
/// </summary>
public class IppRequest
{
    /// <summary>
    /// IPP operation code
    /// </summary>
    public IppOperation Operation { get; set; }

    /// <summary>
    /// Request ID (for response matching)
    /// </summary>
    public int RequestId { get; set; }

    /// <summary>
    /// IPP version (major.minor)
    /// </summary>
    public (byte Major, byte Minor) Version { get; set; } = (1, 1);

    /// <summary>
    /// IPP attributes grouped by tag
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> Attributes { get; set; } = new();

    /// <summary>
    /// Document data (for Print-Job operations)
    /// </summary>
    public byte[]? DocumentData { get; set; }

    /// <summary>
    /// Content type of the document
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Document name (from job-name attribute)
    /// </summary>
    public string? DocumentName { get; set; }
}

/// <summary>
/// IPP operation codes
/// </summary>
public enum IppOperation : short
{
    /// <summary>
    /// Print a document
    /// </summary>
    PrintJob = 0x0002,

    /// <summary>
    /// Get printer attributes
    /// </summary>
    GetPrinterAttributes = 0x000B,

    /// <summary>
    /// Get list of jobs
    /// </summary>
    GetJobs = 0x000A,

    /// <summary>
    /// Cancel a job
    /// </summary>
    CancelJob = 0x0008,

    /// <summary>
    /// Validate job (dry-run)
    /// </summary>
    ValidateJob = 0x0004
}

/// <summary>
/// IPP status codes for responses
/// </summary>
public enum IppStatusCode : short
{
    /// <summary>
    /// Request was successful
    /// </summary>
    SuccessfulOk = 0x0000,

    /// <summary>
    /// Client error - bad request
    /// </summary>
    ClientErrorBadRequest = 0x0400,

    /// <summary>
    /// Client error - not found
    /// </summary>
    ClientErrorNotFound = 0x0404,

    /// <summary>
    /// Server error - internal error
    /// </summary>
    ServerErrorInternalError = 0x0500,

    /// <summary>
    /// Server error - operation not supported
    /// </summary>
    ServerErrorOperationNotSupported = 0x0501
}
