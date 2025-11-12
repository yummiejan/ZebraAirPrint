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

            // Create service profile with _universal subtype for iOS compatibility
            _serviceProfile = CreateServiceProfile();

            // CRITICAL: Add _universal subtype for iOS discovery
            // iOS devices browse specifically for _universal._sub._ipp._tcp services
            _serviceProfile.Subtypes.Add("_universal");

            // Create and start service discovery
            _serviceDiscovery = new ServiceDiscovery();
            _serviceDiscovery.Advertise(_serviceProfile);

            _logger.LogInformation("Advertised _ipp._tcp service with _universal subtype");

            IsAdvertising = true;

            _logger.LogInformation(
                "Bonjour service started: {ServiceName} on port {Port}",
                _serviceProfile.InstanceName,
                _serviceConfig.IppPort);

            _logger.LogInformation("mDNS advertisement active - printer should now be discoverable on iOS devices");

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

        // Create service profile for AirPrint
        var profile = new ServiceProfile(
            instanceName: _serviceConfig.BonjourServiceName,
            serviceName: "_ipp._tcp",
            port: (ushort)_serviceConfig.IppPort,
            addresses: new[] { localIp });

        // Add TXT records with printer attributes
        var txtRecords = BuildTxtRecords();

        _logger.LogInformation("Adding TXT records to mDNS advertisement:");
        foreach (var record in txtRecords)
        {
            profile.AddProperty(record.Key, record.Value);
            _logger.LogInformation("  {Key} = {Value}", record.Key, record.Value);
        }

        return profile;
    }

    /// <summary>
    /// Builds TXT records for the mDNS advertisement
    /// Based on Apple AirPrint specification and iOS compatibility requirements
    /// </summary>
    private Dictionary<string, string> BuildTxtRecords()
    {
        return new Dictionary<string, string>
        {
            // REQUIRED - TXT record format version (must be first)
            ["txtvers"] = "1",

            // REQUIRED - Number of print queues
            ["qtotal"] = "1",

            // REQUIRED - Resource path for IPP endpoint (no leading slash)
            ["rp"] = "ipp/print",

            // REQUIRED - Printer make and model
            ["ty"] = _printerConfig.Name,

            // REQUIRED - Product identifier with parentheses
            ["product"] = $"({_printerConfig.Name})",

            // REQUIRED - Location/description
            ["note"] = "AirPrint Server for Zebra ZD410",

            // REQUIRED - Document formats (MUST include image/urf for iOS)
            ["pdl"] = "application/pdf,image/jpeg,image/urf",

            // REQUIRED - URF (Universal Raster Format) capabilities for iOS 6+
            // V1.4 = URF version 1.4
            // W8 = 8-bit grayscale
            // SRGB24 = 24-bit sRGB color
            // RS203 = 203 DPI resolution (Zebra ZD410)
            // DM1 = Simplex only (no duplex)
            // CP1 = 1 copy per job
            // PQ3-4-5 = Print quality: draft, normal, high
            ["URF"] = $"V1.4,W8,SRGB24,RS{_printerConfig.Resolution},DM1,CP1,PQ3-4-5",

            // Recommended - Printer capabilities
            ["Color"] = "F", // F = False (monochrome)
            ["Duplex"] = "F", // F = False (no duplex support)
            ["Copies"] = "T" // T = True (supports multiple copies)
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
