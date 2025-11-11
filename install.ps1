# Installation script for Zebra AirPrint Service
# Must be run as Administrator

param(
    [string]$InstallPath = "C:\AirPrintService",
    [string]$SourcePath = "publish"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Zebra AirPrint Service - Installation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Please right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

Write-Host "Step 1/7: Checking prerequisites..." -ForegroundColor Green

# Check if .NET 8 Runtime is installed
Write-Host "  - Checking .NET 8 Runtime..."
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "  WARNING: .NET 8 Runtime not found!" -ForegroundColor Yellow
    Write-Host "  Please install: winget install Microsoft.DotNet.Runtime.8" -ForegroundColor Yellow
}
else {
    Write-Host "  - .NET Runtime found: $dotnetVersion" -ForegroundColor Gray
}

# Check if Bonjour is installed
Write-Host "  - Checking Bonjour Services..."
$bonjourService = Get-Service -Name "Bonjour Service" -ErrorAction SilentlyContinue
if (-not $bonjourService) {
    Write-Host "  WARNING: Bonjour Service not found!" -ForegroundColor Yellow
    Write-Host "  Please download from: https://support.apple.com/kb/DL999" -ForegroundColor Yellow
}
else {
    Write-Host "  - Bonjour Service found" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Step 2/7: Creating installation directory..." -ForegroundColor Green
if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    Write-Host "  - Created: $InstallPath" -ForegroundColor Gray
}
else {
    Write-Host "  - Directory already exists: $InstallPath" -ForegroundColor Gray
}

# Create Logs directory
$logsPath = "$InstallPath\Logs"
if (-not (Test-Path $logsPath)) {
    New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
    Write-Host "  - Created: $logsPath" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Step 3/7: Copying files..." -ForegroundColor Green
if (Test-Path $SourcePath) {
    Copy-Item -Path "$SourcePath\*" -Destination $InstallPath -Recurse -Force
    Write-Host "  - Files copied to $InstallPath" -ForegroundColor Gray
}
else {
    Write-Host "  ERROR: Source path not found: $SourcePath" -ForegroundColor Red
    Write-Host "  Please build the project first: dotnet publish -c Release -r win-x64" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Step 4/7: Configuring firewall rules..." -ForegroundColor Green

# IPP Port (631)
Write-Host "  - Adding firewall rule for IPP (TCP 631)..."
netsh advfirewall firewall delete rule name="AirPrint IPP" 2>$null | Out-Null
netsh advfirewall firewall add rule name="AirPrint IPP" dir=in action=allow protocol=TCP localport=631 profile=private | Out-Null
Write-Host "    Added: AirPrint IPP" -ForegroundColor Gray

# mDNS Port (5353)
Write-Host "  - Adding firewall rule for mDNS (UDP 5353)..."
netsh advfirewall firewall delete rule name="AirPrint mDNS" 2>$null | Out-Null
netsh advfirewall firewall add rule name="AirPrint mDNS" dir=in action=allow protocol=UDP localport=5353 profile=private | Out-Null
Write-Host "    Added: AirPrint mDNS" -ForegroundColor Gray

Write-Host ""
Write-Host "Step 5/7: Configuring URL ACL..." -ForegroundColor Green
netsh http delete urlacl url=http://+:631/ 2>$null | Out-Null
netsh http add urlacl url=http://+:631/ user=EVERYONE | Out-Null
Write-Host "  - URL ACL configured for port 631" -ForegroundColor Gray

Write-Host ""
Write-Host "Step 6/7: Installing Windows Service..." -ForegroundColor Green

# Stop and delete existing service if it exists
$existingService = Get-Service -Name "ZebraAirPrintService" -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "  - Stopping existing service..." -ForegroundColor Gray
    Stop-Service -Name "ZebraAirPrintService" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    Write-Host "  - Removing existing service..." -ForegroundColor Gray
    sc.exe delete "ZebraAirPrintService" | Out-Null
    Start-Sleep -Seconds 2
}

# Create new service
Write-Host "  - Creating Windows Service..." -ForegroundColor Gray
$exePath = Join-Path $InstallPath "ZebraAirPrintService.exe"
sc.exe create "ZebraAirPrintService" binPath= $exePath start= auto | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "  - Service created successfully" -ForegroundColor Gray

    # Set service description
    sc.exe description "ZebraAirPrintService" "AirPrint Server für Zebra ZD410 Etikettendrucker" | Out-Null

    # Configure service recovery options (restart on failure)
    sc.exe failure "ZebraAirPrintService" reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
}
else {
    Write-Host "  ERROR: Failed to create service!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 7/7: Starting service..." -ForegroundColor Green
sc.exe start "ZebraAirPrintService" | Out-Null
Start-Sleep -Seconds 3

$service = Get-Service -Name "ZebraAirPrintService" -ErrorAction SilentlyContinue
if ($service -and $service.Status -eq "Running") {
    Write-Host "  - Service started successfully!" -ForegroundColor Gray
}
else {
    Write-Host "  WARNING: Service may not have started correctly" -ForegroundColor Yellow
    Write-Host "  Check logs at: $logsPath" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Installation completed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Service Information:" -ForegroundColor White
Write-Host "  - Service Name: ZebraAirPrintService" -ForegroundColor Gray
Write-Host "  - Installation Path: $InstallPath" -ForegroundColor Gray
Write-Host "  - Log Path: $logsPath" -ForegroundColor Gray
Write-Host "  - IPP Port: 631" -ForegroundColor Gray
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor White
Write-Host "  1. Check service status: sc query ZebraAirPrintService" -ForegroundColor Gray
Write-Host "  2. View logs in: $logsPath" -ForegroundColor Gray
Write-Host "  3. On iPad: Settings → General → Printer & Scanners" -ForegroundColor Gray
Write-Host "  4. Wait ~10 seconds for 'Zebra ZD410' to appear" -ForegroundColor Gray
Write-Host ""
Write-Host "To uninstall: Run .\uninstall.ps1" -ForegroundColor Yellow
Write-Host ""
