using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using ZebraAirPrintService.Interfaces;
using ZebraAirPrintService.Models;
using ZebraAirPrintService.Utils;

namespace ZebraAirPrintService.Services;

/// <summary>
/// Service for advertising the printer via Bonjour/mDNS
/// </summary>
public class BonjourService : IBonjourService
{
    private readonly ILogger<BonjourService> _logger;
    private readonly ConfigManager _configManager;
    private readonly ServiceConfiguration _serviceConfig;
    private readonly PrinterConfiguration _printerConfig;

    private ServiceDiscovery? _serviceDiscovery;
    private ServiceProfile? _serviceProfile;

    public bool IsAdvertising { get; private set; }

    public BonjourService(ILogger<BonjourService> logger, ConfigManager configManager)
    {
        _logger = logger;
        _configManager = configManager;
        _serviceConfig = _configManager.GetServiceConfig();
        _printerConfig = _configManager.GetPrinterConfig();
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting Bonjour/mDNS service advertisement");

            // Create service profile
            _serviceProfile = CreateServiceProfile();

            // Create and start service discovery
            _serviceDiscovery = new ServiceDiscovery();
            _serviceDiscovery.Advertise(_serviceProfile);

            IsAdvertising = true;

            _logger.LogInformation(
                "Bonjour service started: {ServiceName} on port {Port}",
                _serviceProfile.InstanceName,
                _serviceConfig.IppPort);

            _logger.LogInformation("mDNS advertisement active - printer should now be discoverable on the network");

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Bonjour service: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        try
        {
            _logger.LogInformation("Stopping Bonjour/mDNS service advertisement");

            if (_serviceProfile != null && _serviceDiscovery != null)
            {
                _serviceDiscovery.Unadvertise(_serviceProfile);
            }

            _serviceDiscovery?.Dispose();
            _serviceDiscovery = null;
            _serviceProfile = null;

            IsAdvertising = false;

            _logger.LogInformation("Bonjour service stopped");

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Bonjour service: {ErrorMessage}", ex.Message);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Creates the service profile for IPP printer advertisement
    /// </summary>
    private ServiceProfile CreateServiceProfile()
    {
        // Get local IP address
        var localIp = GetLocalIPAddress();

        _logger.LogInformation("Local IP address: {IpAddress}", localIp);

        // Create service profile
        var profile = new ServiceProfile(
            instanceName: _serviceConfig.BonjourServiceName,
            serviceName: "_ipp._tcp",
            port: (ushort)_serviceConfig.IppPort,
            addresses: new[] { localIp });

        // Add TXT records with printer attributes
        var txtRecords = BuildTxtRecords();
        foreach (var record in txtRecords)
        {
            profile.AddProperty(record.Key, record.Value);
            _logger.LogDebug("TXT Record: {Key}={Value}", record.Key, record.Value);
        }

        return profile;
    }

    /// <summary>
    /// Builds TXT records for the mDNS advertisement
    /// </summary>
    private Dictionary<string, string> BuildTxtRecords()
    {
        return new Dictionary<string, string>
        {
            // Required AirPrint attributes
            ["txtvers"] = "1",
            ["qtotal"] = "1",
            ["rp"] = "ipp/print",
            ["ty"] = _printerConfig.Name,
            ["adminurl"] = $"http://localhost:{_serviceConfig.IppPort}/",
            ["note"] = "AirPrint Server for Zebra ZD410",

            // Document formats
            ["pdl"] = "application/pdf,image/urf,image/jpeg,image/png",

            // URF (Unencoded Raster Format) capabilities
            // W8H7 = 8 inches wide, 7 inches high (approximation)
            // SRGB24 = 24-bit sRGB color
            // RS203 = 203 DPI resolution (Zebra ZD410)
            ["URF"] = $"W8H7,SRGB24,RS{_printerConfig.Resolution}",

            // Color support
            ["Color"] = "F", // F = False (monochrome)

            // Duplex support
            ["Duplex"] = "S", // S = Simplex (one-sided)

            // Media size (label dimensions)
            ["media"] = "om_small-label_50.7x30.6mm",

            // Print quality
            ["print-quality-supported"] = "3,4,5",
            ["print-quality-default"] = "4",

            // Fax support
            ["Fax"] = "F",

            // Scan support
            ["Scan"] = "F",

            // Priority
            ["priority"] = "50",

            // Product
            ["product"] = $"({_printerConfig.Name})",

            // USB MFG and MDL (for compatibility)
            ["usb_MFG"] = "Zebra",
            ["usb_MDL"] = "ZD410",

            // Kind (label printer)
            ["kind"] = "label"
        };
    }

    /// <summary>
    /// Gets the local IP address of the machine
    /// </summary>
    private IPAddress GetLocalIPAddress()
    {
        try
        {
            // Get all network interfaces
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            foreach (var ni in interfaces)
            {
                var props = ni.GetIPProperties();
                var addresses = props.UnicastAddresses
                    .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                                  !IPAddress.IsLoopback(addr.Address))
                    .Select(addr => addr.Address)
                    .ToList();

                if (addresses.Any())
                {
                    // Prefer non-169.254.x.x addresses (APIPA)
                    var nonApipa = addresses.FirstOrDefault(addr => !addr.ToString().StartsWith("169.254"));
                    return nonApipa ?? addresses.First();
                }
            }

            // Fallback to localhost if no suitable address found
            _logger.LogWarning("No suitable network interface found, using localhost");
            return IPAddress.Loopback;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting local IP address, using localhost: {ErrorMessage}", ex.Message);
            return IPAddress.Loopback;
        }
    }
}
