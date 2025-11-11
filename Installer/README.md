# Zebra AirPrint Service - MSI Installer

This directory contains the WiX installer project for creating a professional MSI installer for the Zebra AirPrint Service.

## Features

- **GUI Installation**: Users can choose the installation directory through a standard Windows installer dialog
- **Default Path**: Installs to `C:\Program Files\ZebraAirPrintService` by default
- **Windows Service**: Automatically registers and starts the service
- **Firewall Rules**: Configures Windows Firewall for IPP (port 631) and mDNS (port 5353)
- **URL ACL**: Configures HTTP.SYS URL reservations
- **Logging**: Creates a `Logs` subdirectory in the chosen installation path
- **Auto-start**: Service is configured to start automatically on boot
- **Recovery**: Service automatically restarts on failure

## Prerequisites

- .NET 8 Runtime (will be checked during installation)
- WiX Toolset v5 (automatically installed by build script)
- Bonjour Services for Windows (required for mDNS)

## Building the Installer

Run the build script from the project root:

```powershell
.\build-installer.ps1
```

This will:
1. Build the service application
2. Install WiX Toolset if needed
3. Generate the MSI installer

The MSI will be created in `Installer\bin\Release\`

## Installation

### GUI Installation (Recommended)

1. Double-click the MSI file
2. Follow the installation wizard
3. Choose your installation directory (or use the default)
4. Click Install

### Silent Installation

```powershell
msiexec /i ZebraAirPrintServiceSetup.msi /quiet
```

### Custom Installation Path

```powershell
msiexec /i ZebraAirPrintServiceSetup.msi INSTALLFOLDER="C:\CustomPath\ZebraAirPrint"
```

## Uninstallation

### GUI Uninstall

1. Open "Add or Remove Programs"
2. Find "Zebra AirPrint Service"
3. Click Uninstall

### Silent Uninstall

```powershell
msiexec /x ZebraAirPrintServiceSetup.msi /quiet
```

## What Gets Installed

```
C:\Program Files\ZebraAirPrintService\
├── ZebraAirPrintService.exe    (Main service executable)
├── appsettings.json            (Configuration file)
└── Logs\                       (Log files directory)
    └── airprint-YYYY-MM-DD.txt
```

## Configuration

After installation, you can modify settings in:
```
[InstallFolder]\appsettings.json
```

**Note**: The logging path is relative (`Logs`) and will automatically use the installation directory.

## Service Management

The service is managed through Windows Services:

```powershell
# Start service
Start-Service ZebraAirPrintService

# Stop service
Stop-Service ZebraAirPrintService

# Restart service
Restart-Service ZebraAirPrintService

# Check status
Get-Service ZebraAirPrintService
```

## Troubleshooting

### Build Issues

If WiX build fails, ensure you have the latest .NET SDK:
```powershell
dotnet --version  # Should be 8.0 or higher
```

### Installation Issues

Check the MSI installation log:
```powershell
msiexec /i ZebraAirPrintServiceSetup.msi /l*v install.log
```

## Technical Details

- **Installer Type**: Windows Installer (MSI) using WiX Toolset v5
- **Platform**: x64 Windows
- **Service Account**: LocalSystem
- **Upgrade Code**: 12345678-1234-1234-1234-123456789012 (change this for your deployment)

## Customization

### Changing Product Information

Edit `Product.wxs` and modify the variables:
```xml
<?define ProductName = "Zebra AirPrint Service" ?>
<?define ProductVersion = "1.0.0.0" ?>
<?define Manufacturer = "Your Company Name" ?>
<?define UpgradeCode = "..." ?>
```

**Important**: Generate a new GUID for `UpgradeCode` using:
```powershell
[guid]::NewGuid()
```

### Changing GUIDs

Each Component in `Product.wxs` has a GUID. If you modify components, generate new GUIDs:
```powershell
[guid]::NewGuid()
```
