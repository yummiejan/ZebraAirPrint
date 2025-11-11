# Uninstall script for Zebra AirPrint Service
# This script completely removes the service and all its configurations

param(
    [switch]$Force
)

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Zebra AirPrint Service - Complete Uninstall" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Check for administrator privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script requires administrator privileges!" -ForegroundColor Red
    Write-Host "Please run PowerShell as Administrator and try again." -ForegroundColor Yellow
    exit 1
}

$serviceName = "ZebraAirPrintService"
$defaultInstallPath = "C:\Program Files\ZebraAirPrintService"
$cleanupSuccessful = $true

# Step 1: Stop and remove Windows Service
Write-Host "[1/7] Stopping and removing Windows Service..." -ForegroundColor Yellow
try {
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -eq 'Running') {
            Write-Host "  Stopping service..." -ForegroundColor Gray
            Stop-Service -Name $serviceName -Force -ErrorAction Stop
            Start-Sleep -Seconds 2
        }

        Write-Host "  Removing service..." -ForegroundColor Gray
        sc.exe delete $serviceName | Out-Null

        if ($LASTEXITCODE -eq 0) {
            Write-Host "  Service removed successfully" -ForegroundColor Green
        } else {
            Write-Host "  Warning: Service removal returned code $LASTEXITCODE" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  Service not found (already removed)" -ForegroundColor Gray
    }
} catch {
    Write-Host "  Error removing service: $($_.Exception.Message)" -ForegroundColor Red
    $cleanupSuccessful = $false
}

# Step 2: Remove Firewall Rules
Write-Host "[2/7] Removing firewall rules..." -ForegroundColor Yellow
try {
    $firewallRules = Get-NetFirewallRule -DisplayName "Zebra AirPrint*" -ErrorAction SilentlyContinue
    if ($firewallRules) {
        foreach ($rule in $firewallRules) {
            Write-Host "  Removing: $($rule.DisplayName)" -ForegroundColor Gray
            Remove-NetFirewallRule -Name $rule.Name -ErrorAction Stop
        }
        Write-Host "  Firewall rules removed" -ForegroundColor Green
    } else {
        Write-Host "  No firewall rules found" -ForegroundColor Gray
    }
} catch {
    Write-Host "  Error removing firewall rules: $($_.Exception.Message)" -ForegroundColor Red
    $cleanupSuccessful = $false
}

# Step 3: Remove URL ACL reservation
Write-Host "[3/7] Removing URL ACL reservation..." -ForegroundColor Yellow
try {
    $urlAcl = netsh http show urlacl | Select-String "http://\+:631/"
    if ($urlAcl) {
        Write-Host "  Removing http://+:631/ reservation..." -ForegroundColor Gray
        netsh http delete urlacl url=http://+:631/ | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  URL ACL removed" -ForegroundColor Green
        } else {
            Write-Host "  Warning: URL ACL removal returned code $LASTEXITCODE" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  No URL ACL reservation found" -ForegroundColor Gray
    }
} catch {
    Write-Host "  Error removing URL ACL: $($_.Exception.Message)" -ForegroundColor Red
    $cleanupSuccessful = $false
}

# Step 4: Find and remove installation directories
Write-Host "[4/7] Removing installation directories..." -ForegroundColor Yellow
$installPaths = @(
    $defaultInstallPath,
    "C:\AirPrintService"  # Old default from PowerShell script
)

foreach ($path in $installPaths) {
    if (Test-Path $path) {
        try {
            Write-Host "  Removing: $path" -ForegroundColor Gray
            Remove-Item -Path $path -Recurse -Force -ErrorAction Stop
            Write-Host "  Directory removed: $path" -ForegroundColor Green
        } catch {
            Write-Host "  Error removing $path : $($_.Exception.Message)" -ForegroundColor Red
            $cleanupSuccessful = $false
        }
    }
}

# Step 5: Remove registry entries
Write-Host "[5/7] Removing registry entries..." -ForegroundColor Yellow
$registryPaths = @(
    "HKCU:\Software\Your Company Name\Zebra AirPrint Service",
    "HKLM:\Software\ZebraAirPrintService"
)

foreach ($regPath in $registryPaths) {
    if (Test-Path $regPath) {
        try {
            Write-Host "  Removing: $regPath" -ForegroundColor Gray
            Remove-Item -Path $regPath -Recurse -Force -ErrorAction Stop
            Write-Host "  Registry entry removed" -ForegroundColor Green
        } catch {
            Write-Host "  Error removing registry: $($_.Exception.Message)" -ForegroundColor Red
            $cleanupSuccessful = $false
        }
    }
}

# Step 6: Try to uninstall via MSI (if installed via MSI)
Write-Host "[6/7] Checking for MSI installation..." -ForegroundColor Yellow
try {
    $msiProduct = Get-WmiObject -Class Win32_Product | Where-Object {
        $_.Name -like "*Zebra*AirPrint*" -or $_.Name -eq "Zebra AirPrint Service"
    }

    if ($msiProduct) {
        Write-Host "  Found MSI installation: $($msiProduct.Name)" -ForegroundColor Gray
        Write-Host "  Product Code: $($msiProduct.IdentifyingNumber)" -ForegroundColor Gray

        if ($Force) {
            Write-Host "  Uninstalling via MSI..." -ForegroundColor Gray
            $msiProduct.Uninstall() | Out-Null
            Write-Host "  MSI uninstalled" -ForegroundColor Green
        } else {
            Write-Host "  Use -Force to uninstall MSI package" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  No MSI installation found" -ForegroundColor Gray
    }
} catch {
    Write-Host "  Error checking MSI: $($_.Exception.Message)" -ForegroundColor Red
}

# Step 7: Clean up any orphaned processes
Write-Host "[7/7] Checking for orphaned processes..." -ForegroundColor Yellow
try {
    $processes = Get-Process -Name "ZebraAirPrintService" -ErrorAction SilentlyContinue
    if ($processes) {
        Write-Host "  Found running processes, terminating..." -ForegroundColor Gray
        $processes | Stop-Process -Force
        Write-Host "  Processes terminated" -ForegroundColor Green
    } else {
        Write-Host "  No orphaned processes found" -ForegroundColor Gray
    }
} catch {
    Write-Host "  Error checking processes: $($_.Exception.Message)" -ForegroundColor Red
}

# Summary
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
if ($cleanupSuccessful) {
    Write-Host "Uninstall completed successfully!" -ForegroundColor Green
} else {
    Write-Host "Uninstall completed with warnings/errors" -ForegroundColor Yellow
    Write-Host "Some components may not have been removed completely." -ForegroundColor Yellow
}
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Verification
Write-Host "Verification:" -ForegroundColor Cyan
Write-Host "  Service: " -NoNewline
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Write-Host "Still exists" -ForegroundColor Red
} else {
    Write-Host "Removed" -ForegroundColor Green
}

Write-Host "  Installation directory: " -NoNewline
if (Test-Path $defaultInstallPath) {
    Write-Host "Still exists" -ForegroundColor Red
} else {
    Write-Host "Removed" -ForegroundColor Green
}

Write-Host "  Firewall rules: " -NoNewline
$remainingRules = Get-NetFirewallRule -DisplayName "Zebra AirPrint*" -ErrorAction SilentlyContinue
if ($remainingRules) {
    Write-Host "Still exists ($($remainingRules.Count) rules)" -ForegroundColor Red
} else {
    Write-Host "Removed" -ForegroundColor Green
}

Write-Host ""
Write-Host "To reinstall, run the MSI installer." -ForegroundColor Yellow
