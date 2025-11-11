using System.Drawing;
using System.Drawing.Printing;
using Microsoft.Extensions.Logging;
using ZebraAirPrintService.Interfaces;
using ZebraAirPrintService.Models;
using ZebraAirPrintService.Utils;

namespace ZebraAirPrintService.Services;

/// <summary>
/// Service for interacting with Windows printers
/// </summary>
public class PrinterService : IPrinterService
{
    private readonly ILogger<PrinterService> _logger;
    private readonly ConfigManager _configManager;
    private readonly PrinterConfiguration _printerConfig;

    public PrinterService(ILogger<PrinterService> logger, ConfigManager configManager)
    {
        _logger = logger;
        _configManager = configManager;
        _printerConfig = _configManager.GetPrinterConfig();
    }

    /// <inheritdoc />
    public async Task<bool> PrintAsync(PrintJob job)
    {
        try
        {
            _logger.LogInformation(
                "Starting print job {JobId}: ContentType={ContentType}, Size={Size} bytes, DocumentName={DocumentName}",
                job.JobId, job.ContentType, job.Data.Length, job.DocumentName ?? "Unnamed");

            // Check if printer is available
            if (!IsPrinterAvailable())
            {
                _logger.LogWarning("Printer {PrinterName} is not available", _printerConfig.Name);
                return false;
            }

            // Print the job on a background thread (Windows Printing API is synchronous)
            await Task.Run(() => SendToWindowsSpooler(job));

            _logger.LogInformation("Print job {JobId} sent to Windows spooler successfully", job.JobId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to print job {JobId}: {ErrorMessage}", job.JobId, ex.Message);
            job.ErrorMessage = ex.Message;
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<PrinterStatus> GetStatusAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var printerSettings = new PrinterSettings
                {
                    PrinterName = _printerConfig.Name
                };

                var status = new PrinterStatus
                {
                    PrinterName = _printerConfig.Name,
                    IsOnline = printerSettings.IsValid,
                    LastChecked = DateTime.Now
                };

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get printer status: {ErrorMessage}", ex.Message);
                return new PrinterStatus
                {
                    PrinterName = _printerConfig.Name,
                    IsOnline = false,
                    LastError = ex.Message,
                    LastChecked = DateTime.Now
                };
            }
        });
    }

    /// <inheritdoc />
    public bool IsPrinterAvailable()
    {
        try
        {
            var printerSettings = new PrinterSettings
            {
                PrinterName = _printerConfig.Name
            };

            return printerSettings.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking printer availability: {ErrorMessage}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Sends the print job to the Windows print spooler
    /// </summary>
    private void SendToWindowsSpooler(PrintJob job)
    {
        var printDocument = new PrintDocument
        {
            PrinterSettings = new PrinterSettings
            {
                PrinterName = _printerConfig.Name
            },
            DocumentName = job.DocumentName ?? $"Job-{job.JobId}"
        };

        // Create a temporary file for the document data
        string tempFile = Path.Combine(Path.GetTempPath(), $"airprint_{job.JobId}_{Guid.NewGuid()}.dat");

        try
        {
            // Write document data to temp file
            File.WriteAllBytes(tempFile, job.Data);

            // Determine how to handle the document based on content type
            if (job.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            {
                // For PDF files, send directly to printer using raw data
                SendRawDataToPrinter(job.Data, printDocument.PrinterSettings.PrinterName, printDocument.DocumentName);
            }
            else if (job.ContentType.Contains("image", StringComparison.OrdinalIgnoreCase))
            {
                // For images, use PrintDocument to render
                PrintImageDocument(job.Data, printDocument);
            }
            else
            {
                // For other formats, try raw printing
                _logger.LogWarning("Unknown content type {ContentType}, attempting raw print", job.ContentType);
                SendRawDataToPrinter(job.Data, printDocument.PrinterSettings.PrinterName, printDocument.DocumentName);
            }

            _logger.LogInformation("Print job {JobId} completed successfully", job.JobId);
        }
        finally
        {
            // Clean up temp file
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp file {TempFile}", tempFile);
            }
        }
    }

    /// <summary>
    /// Prints an image document
    /// </summary>
    private void PrintImageDocument(byte[] imageData, PrintDocument printDocument)
    {
        using var ms = new MemoryStream(imageData);
        using var image = Image.FromStream(ms);

        printDocument.PrintPage += (sender, e) =>
        {
            if (e.Graphics != null)
            {
                // Calculate scaling to fit label size
                float scaleX = e.PageBounds.Width / (float)image.Width;
                float scaleY = e.PageBounds.Height / (float)image.Height;
                float scale = Math.Min(scaleX, scaleY);

                int scaledWidth = (int)(image.Width * scale);
                int scaledHeight = (int)(image.Height * scale);

                // Center the image
                int x = (e.PageBounds.Width - scaledWidth) / 2;
                int y = (e.PageBounds.Height - scaledHeight) / 2;

                e.Graphics.DrawImage(image, x, y, scaledWidth, scaledHeight);
            }
        };

        printDocument.Print();
    }

    /// <summary>
    /// Sends raw data to the printer (for PDF and other formats)
    /// </summary>
    private void SendRawDataToPrinter(byte[] data, string printerName, string documentName)
    {
        // Use Windows RAW printing for PDF and other formats
        // This requires P/Invoke to Win32 API
        RawPrinterHelper.SendBytesToPrinter(printerName, data, documentName);
    }
}

/// <summary>
/// Helper class for raw printer access via Win32 API
/// </summary>
internal static class RawPrinterHelper
{
    // Win32 API declarations
    [System.Runtime.InteropServices.DllImport("winspool.drv", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [System.Runtime.InteropServices.DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOC_INFO_1 pDocInfo);

    [System.Runtime.InteropServices.DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [System.Runtime.InteropServices.DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private struct DOC_INFO_1
    {
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
        public string pDocName;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
        public string? pOutputFile;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
        public string? pDataType;
    }

    /// <summary>
    /// Sends raw bytes to a printer
    /// </summary>
    public static bool SendBytesToPrinter(string printerName, byte[] bytes, string documentName)
    {
        IntPtr hPrinter = IntPtr.Zero;
        DOC_INFO_1 docInfo = new DOC_INFO_1
        {
            pDocName = documentName,
            pOutputFile = null,
            pDataType = "RAW"
        };

        try
        {
            // Open printer
            if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
            {
                return false;
            }

            // Start document
            if (!StartDocPrinter(hPrinter, 1, ref docInfo))
            {
                return false;
            }

            // Start page
            if (!StartPagePrinter(hPrinter))
            {
                EndDocPrinter(hPrinter);
                return false;
            }

            // Write data
            int bytesWritten;
            bool success = WritePrinter(hPrinter, bytes, bytes.Length, out bytesWritten);

            // End page and document
            EndPagePrinter(hPrinter);
            EndDocPrinter(hPrinter);

            return success && bytesWritten == bytes.Length;
        }
        finally
        {
            if (hPrinter != IntPtr.Zero)
            {
                ClosePrinter(hPrinter);
            }
        }
    }
}
