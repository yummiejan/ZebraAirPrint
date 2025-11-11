# Build script for Zebra AirPrint Service Installer
# This script builds the service and creates the MSI installer

param(
    [string]$Configuration = "Release"
)

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Zebra AirPrint Service - Build Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build the service application
Write-Host "[1/3] Building service application..." -ForegroundColor Yellow
$publishPath = "publish"

dotnet publish ZebraAirPrintService.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -o $publishPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build service application" -ForegroundColor Red
    exit 1
}

Write-Host "Service built successfully" -ForegroundColor Green
Write-Host ""

# Step 2: Check WiX Toolset installation
Write-Host "[2/3] Checking WiX Toolset installation..." -ForegroundColor Yellow

$wixInstalled = $false
try {
    $wixVersion = dotnet tool list -g | Select-String "wix"
    if ($wixVersion) {
        Write-Host "WiX Toolset is installed" -ForegroundColor Green
        $wixInstalled = $true
    }
} catch {
    # Ignore error
}

if (-not $wixInstalled) {
    Write-Host "WiX Toolset not found. Installing..." -ForegroundColor Yellow
    dotnet tool install --global wix

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to install WiX Toolset" -ForegroundColor Red
        Write-Host "Please install WiX manually: dotnet tool install --global wix" -ForegroundColor Red
        exit 1
    }

    Write-Host "WiX Toolset installed successfully" -ForegroundColor Green
}

Write-Host ""

# Step 3: Build the MSI installer
Write-Host "[3/3] Building MSI installer..." -ForegroundColor Yellow

Set-Location Installer

dotnet build ZebraAirPrintInstaller.wixproj -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Set-Location ..
    Write-Host "Failed to build MSI installer" -ForegroundColor Red
    exit 1
}

Set-Location ..

Write-Host "MSI installer built successfully" -ForegroundColor Green
Write-Host ""

# Find the MSI file
$msiFile = Get-ChildItem -Path "Installer\bin\$Configuration" -Filter "*.msi" -Recurse | Select-Object -First 1

if ($msiFile) {
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "MSI Location: $($msiFile.FullName)" -ForegroundColor Green
    Write-Host "File Size: $([math]::Round($msiFile.Length / 1MB, 2)) MB" -ForegroundColor Green
    Write-Host ""
    Write-Host "To install, run:" -ForegroundColor Yellow
    Write-Host "  msiexec /i `"$($msiFile.FullName)`"" -ForegroundColor White
    Write-Host ""
    Write-Host "Or double-click the MSI file for GUI installation" -ForegroundColor Yellow
} else {
    Write-Host "Warning: Could not find generated MSI file" -ForegroundColor Yellow
}
