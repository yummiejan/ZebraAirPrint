# Uninstallation script for Zebra AirPrint Service
# Must be run as Administrator

param(
    [string]$InstallPath = "C:\AirPrintService",
    [switch]$RemoveFiles = $false
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Zebra AirPrint Service - Uninstallation" -ForegroundColor Cyan
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

Write-Host "Step 1/5: Stopping Windows Service..." -ForegroundColor Green
$service = Get-Service -Name "ZebraAirPrintService" -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -eq "Running") {
        Write-Host "  - Stopping service..." -ForegroundColor Gray
        Stop-Service -Name "ZebraAirPrintService" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
        Write-Host "  - Service stopped" -ForegroundColor Gray
    }
    else {
        Write-Host "  - Service is not running" -ForegroundColor Gray
    }
}
else {
    Write-Host "  - Service not found" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Step 2/5: Removing Windows Service..." -ForegroundColor Green
if ($service) {
    sc.exe delete "ZebraAirPrintService" | Out-Null
    Start-Sleep -Seconds 2
    Write-Host "  - Service removed" -ForegroundColor Gray
}
else {
    Write-Host "  - Service not installed" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Step 3/5: Removing firewall rules..." -ForegroundColor Green
netsh advfirewall firewall delete rule name="AirPrint IPP" 2>$null | Out-Null
Write-Host "  - Removed: AirPrint IPP" -ForegroundColor Gray

netsh advfirewall firewall delete rule name="AirPrint mDNS" 2>$null | Out-Null
Write-Host "  - Removed: AirPrint mDNS" -ForegroundColor Gray

Write-Host ""
Write-Host "Step 4/5: Removing URL ACL..." -ForegroundColor Green
netsh http delete urlacl url=http://+:631/ 2>$null | Out-Null
Write-Host "  - URL ACL removed" -ForegroundColor Gray

Write-Host ""
Write-Host "Step 5/5: Cleaning up files..." -ForegroundColor Green
if ($RemoveFiles) {
    if (Test-Path $InstallPath) {
        Write-Host "  - Removing installation directory: $InstallPath" -ForegroundColor Gray
        Remove-Item -Path $InstallPath -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  - Files removed" -ForegroundColor Gray
    }
    else {
        Write-Host "  - Installation directory not found" -ForegroundColor Gray
    }
}
else {
    Write-Host "  - Keeping files in: $InstallPath" -ForegroundColor Yellow
    Write-Host "  - To remove files, run: .\uninstall.ps1 -RemoveFiles" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Uninstallation completed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not $RemoveFiles) {
    Write-Host "Note: Installation files are still present at: $InstallPath" -ForegroundColor Yellow
    Write-Host "To remove them, run: .\uninstall.ps1 -RemoveFiles" -ForegroundColor Yellow
    Write-Host ""
}
