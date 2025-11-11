using System.Net;
using Microsoft.Extensions.Logging;
using ZebraAirPrintService.Interfaces;
using ZebraAirPrintService.Models;
using ZebraAirPrintService.Utils;

namespace ZebraAirPrintService.Services;

/// <summary>
/// IPP (Internet Printing Protocol) server implementation
/// </summary>
public class IppServer : IIppServer
{
    private readonly ILogger<IppServer> _logger;
    private readonly IQueueService _queueService;
    private readonly IPrinterService _printerService;
    private readonly ConfigManager _configManager;
    private readonly IppParser _ippParser;
    private readonly ServiceConfiguration _serviceConfig;
    private readonly PrinterConfiguration _printerConfig;

    private HttpListener? _listener;
    private CancellationTokenSource? _listenerCts;

    public bool IsRunning => _listener?.IsListening ?? false;

    public IppServer(
        ILogger<IppServer> logger,
        IQueueService queueService,
        IPrinterService printerService,
        ConfigManager configManager,
        IppParser ippParser)
    {
        _logger = logger;
        _queueService = queueService;
        _printerService = printerService;
        _configManager = configManager;
        _ippParser = ippParser;
        _serviceConfig = _configManager.GetServiceConfig();
        _printerConfig = _configManager.GetPrinterConfig();
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{_serviceConfig.IppPort}/");
            _listener.Prefixes.Add($"http://+:{_serviceConfig.IppPort}/ipp/");
            _listener.Prefixes.Add($"http://+:{_serviceConfig.IppPort}/ipp/print/");

            _logger.LogInformation("Starting IPP server on port {Port}", _serviceConfig.IppPort);
            _listener.Start();

            _logger.LogInformation("IPP server started successfully on port {Port}", _serviceConfig.IppPort);

            _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Start accepting requests
            await AcceptRequestsAsync(_listenerCts.Token);
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5) // Access Denied
        {
            _logger.LogError(
                "Failed to start IPP server: Access denied. " +
                "Please run the service with administrator privileges or configure URL ACL using: " +
                "netacl http add urlacl url=http://+:{Port}/ user=DOMAIN\\username",
                _serviceConfig.IppPort);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start IPP server: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        _logger.LogInformation("Stopping IPP server");

        _listenerCts?.Cancel();
        _listener?.Stop();
        _listener?.Close();

        _logger.LogInformation("IPP server stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Main loop for accepting and processing requests
    /// </summary>
    private async Task AcceptRequestsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("IPP server is now accepting requests");

        while (!cancellationToken.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();

                // Handle request asynchronously (fire and forget with error handling)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleRequestAsync(context);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling IPP request: {ErrorMessage}", ex.Message);
                    }
                }, cancellationToken);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting IPP request: {ErrorMessage}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        _logger.LogInformation("IPP server stopped accepting requests");
    }

    /// <summary>
    /// Handles a single IPP request
    /// </summary>
    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            _logger.LogInformation(
                "Received {Method} request from {RemoteEndPoint}: {Url}",
                request.HttpMethod, request.RemoteEndPoint, request.Url);

            // Only handle POST requests (IPP uses POST)
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = 405; // Method Not Allowed
                response.Close();
                return;
            }

            // Parse IPP request
            var ippRequest = _ippParser.ParseRequest(request.InputStream);

            _logger.LogInformation(
                "IPP Request: Operation={Operation}, RequestId={RequestId}, ContentType={ContentType}",
                ippRequest.Operation, ippRequest.RequestId, ippRequest.ContentType ?? "N/A");

            // Handle operation
            byte[] ippResponse = ippRequest.Operation switch
            {
                IppOperation.PrintJob => await HandlePrintJobAsync(ippRequest),
                IppOperation.GetPrinterAttributes => HandleGetPrinterAttributes(ippRequest),
                IppOperation.GetJobs => HandleGetJobs(ippRequest),
                IppOperation.CancelJob => HandleCancelJob(ippRequest),
                IppOperation.ValidateJob => HandleValidateJob(ippRequest),
                _ => _ippParser.CreateErrorResponse(
                    ippRequest.RequestId,
                    IppStatusCode.ServerErrorOperationNotSupported,
                    $"Operation {ippRequest.Operation} is not supported")
            };

            // Send response
            response.ContentType = "application/ipp";
            response.StatusCode = 200;
            response.ContentLength64 = ippResponse.Length;
            await response.OutputStream.WriteAsync(ippResponse);
            response.Close();

            _logger.LogInformation(
                "IPP Response sent: Operation={Operation}, ResponseSize={Size} bytes",
                ippRequest.Operation, ippResponse.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing IPP request: {ErrorMessage}", ex.Message);

            try
            {
                response.StatusCode = 500;
                response.Close();
            }
            catch
            {
                // Ignore errors when closing response
            }
        }
    }

    /// <summary>
    /// Handles Print-Job operation
    /// </summary>
    private async Task<byte[]> HandlePrintJobAsync(IppRequest ippRequest)
    {
        try
        {
            if (ippRequest.DocumentData == null || ippRequest.DocumentData.Length == 0)
            {
                _logger.LogWarning("Print-Job request has no document data");
                return _ippParser.CreateErrorResponse(
                    ippRequest.RequestId,
                    IppStatusCode.ClientErrorBadRequest,
                    "No document data provided");
            }

            // Create print job
            var printJob = new PrintJob
            {
                ContentType = ippRequest.ContentType ?? "application/octet-stream",
                Data = ippRequest.DocumentData,
                DocumentName = ippRequest.DocumentName,
                Timestamp = DateTime.Now
            };

            // Add to queue
            _queueService.Enqueue(printJob);

            _logger.LogInformation(
                "Print job {JobId} queued: ContentType={ContentType}, Size={Size} bytes",
                printJob.JobId, printJob.ContentType, printJob.Data.Length);

            // Return success response
            return _ippParser.CreateSuccessResponse(ippRequest.RequestId, printJob.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Print-Job: {ErrorMessage}", ex.Message);
            return _ippParser.CreateErrorResponse(
                ippRequest.RequestId,
                IppStatusCode.ServerErrorInternalError,
                ex.Message);
        }
    }

    /// <summary>
    /// Handles Get-Printer-Attributes operation
    /// </summary>
    private byte[] HandleGetPrinterAttributes(IppRequest ippRequest)
    {
        try
        {
            var attributes = new Dictionary<string, object>
            {
                ["printer-uri-supported"] = $"ipp://localhost:{_serviceConfig.IppPort}/ipp/print",
                ["printer-name"] = _printerConfig.Name,
                ["printer-state"] = _printerService.IsPrinterAvailable() ? 3 : 5, // 3=idle, 5=stopped
                ["printer-state-reasons"] = "none",
                ["printer-is-accepting-jobs"] = true,
                ["queued-job-count"] = _queueService.GetQueueCount(),

                // Document formats
                ["document-format-supported"] = new[]
                {
                    "application/pdf",
                    "image/urf",
                    "image/jpeg",
                    "image/png"
                },

                // Media (label size)
                ["media-supported"] = new[] { "om_small-label_50.7x30.6mm" },
                ["media-default"] = "om_small-label_50.7x30.6mm",
                ["media-size-supported"] = $"{_printerConfig.LabelWidth}x{_printerConfig.LabelHeight}mm",

                // Resolution
                ["printer-resolution-supported"] = $"{_printerConfig.Resolution}dpi",
                ["printer-resolution-default"] = $"{_printerConfig.Resolution}dpi",

                // Color
                ["color-supported"] = false,

                // URF capabilities (Apple format)
                ["urf-supported"] = new[] { "W8H7", "SRGB24", $"RS{_printerConfig.Resolution}" },

                // Sides
                ["sides-supported"] = "one-sided",
                ["sides-default"] = "one-sided",

                // IPP versions
                ["ipp-versions-supported"] = new[] { "1.0", "1.1", "2.0" },

                // Operations
                ["operations-supported"] = new[]
                {
                    (int)IppOperation.PrintJob,
                    (int)IppOperation.ValidateJob,
                    (int)IppOperation.CancelJob,
                    (int)IppOperation.GetJobs,
                    (int)IppOperation.GetPrinterAttributes
                }
            };

            return _ippParser.CreateGetPrinterAttributesResponse(ippRequest.RequestId, attributes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Get-Printer-Attributes: {ErrorMessage}", ex.Message);
            return _ippParser.CreateErrorResponse(
                ippRequest.RequestId,
                IppStatusCode.ServerErrorInternalError,
                ex.Message);
        }
    }

    /// <summary>
    /// Handles Get-Jobs operation
    /// </summary>
    private byte[] HandleGetJobs(IppRequest ippRequest)
    {
        try
        {
            // For simplicity, return success with no jobs
            // In a full implementation, we would return job details from the queue
            var attributes = new Dictionary<string, object>
            {
                ["job-count"] = _queueService.GetQueueCount()
            };

            return _ippParser.CreateSuccessResponse(ippRequest.RequestId, 0, attributes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Get-Jobs: {ErrorMessage}", ex.Message);
            return _ippParser.CreateErrorResponse(
                ippRequest.RequestId,
                IppStatusCode.ServerErrorInternalError,
                ex.Message);
        }
    }

    /// <summary>
    /// Handles Cancel-Job operation
    /// </summary>
    private byte[] HandleCancelJob(IppRequest ippRequest)
    {
        try
        {
            // For MVP, we don't support job cancellation
            // Return "not supported" for now
            _logger.LogWarning("Cancel-Job requested but not implemented");

            return _ippParser.CreateErrorResponse(
                ippRequest.RequestId,
                IppStatusCode.ServerErrorOperationNotSupported,
                "Job cancellation is not supported in this version");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Cancel-Job: {ErrorMessage}", ex.Message);
            return _ippParser.CreateErrorResponse(
                ippRequest.RequestId,
                IppStatusCode.ServerErrorInternalError,
                ex.Message);
        }
    }

    /// <summary>
    /// Handles Validate-Job operation (dry-run)
    /// </summary>
    private byte[] HandleValidateJob(IppRequest ippRequest)
    {
        try
        {
            // Always return success - we accept all jobs
            return _ippParser.CreateSuccessResponse(ippRequest.RequestId, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Validate-Job: {ErrorMessage}", ex.Message);
            return _ippParser.CreateErrorResponse(
                ippRequest.RequestId,
                IppStatusCode.ServerErrorInternalError,
                ex.Message);
        }
    }
}
