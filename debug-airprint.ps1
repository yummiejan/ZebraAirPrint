# AirPrint Service Debugging Script
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  AirPrint Service Debugging Tool" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 1. Check if service is running
Write-Host "[1/7] Checking service status..." -ForegroundColor Yellow
$service = Get-Service "Zebra AirPrint Service" -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -eq "Running") {
        Write-Host "  OK - Service is RUNNING" -ForegroundColor Green
    } else {
        Write-Host "  ERROR - Service is $($service.Status)" -ForegroundColor Red
    }
} else {
    Write-Host "  ERROR - Service not found" -ForegroundColor Red
}
Write-Host ""

# 2. Check IPP port 631
Write-Host "[2/7] Checking IPP port 631..." -ForegroundColor Yellow
$port631 = Get-NetTCPConnection -LocalPort 631 -State Listen -ErrorAction SilentlyContinue
if ($port631) {
    Write-Host "  OK - Port 631 is LISTENING" -ForegroundColor Green
} else {
    Write-Host "  ERROR - Port 631 is NOT listening" -ForegroundColor Red
}
Write-Host ""

# 3. Check mDNS port 5353
Write-Host "[3/7] Checking mDNS port 5353..." -ForegroundColor Yellow
$port5353 = Get-NetUDPEndpoint -LocalPort 5353 -ErrorAction SilentlyContinue
if ($port5353) {
    $count = ($port5353 | Measure-Object).Count
    Write-Host "  OK - Port 5353 is ACTIVE ($count endpoints)" -ForegroundColor Green
} else {
    Write-Host "  ERROR - Port 5353 is NOT active" -ForegroundColor Red
}
Write-Host ""

# 4. Check Windows Firewall
Write-Host "[4/7] Checking Windows Firewall..." -ForegroundColor Yellow
$fwRules = Get-NetFirewallRule | Where-Object {
    ($_.DisplayName -like "*631*" -or $_.DisplayName -like "*AirPrint*") -and $_.Enabled -eq $true
}
if ($fwRules) {
    Write-Host "  OK - Firewall rules found" -ForegroundColor Green
} else {
    Write-Host "  WARNING - No firewall rules found" -ForegroundColor Yellow
}
Write-Host ""

# 5. Get network interfaces
Write-Host "[5/7] Checking network interfaces..." -ForegroundColor Yellow
$interfaces = Get-NetIPAddress -AddressFamily IPv4 | Where-Object {
    $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*"
}
if ($interfaces) {
    Write-Host "  Network interfaces:" -ForegroundColor Green
    foreach ($iface in $interfaces) {
        Write-Host "    - $($iface.IPAddress)" -ForegroundColor Gray
    }
} else {
    Write-Host "  ERROR - No valid network interfaces" -ForegroundColor Red
}
Write-Host ""

# 6. Check logs
Write-Host "[6/7] Checking service logs..." -ForegroundColor Yellow
$logPath = "Logs"
if (Test-Path $logPath) {
    $latestLog = Get-ChildItem $logPath -Filter "zebraairprint-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latestLog) {
        Write-Host "  Log file: $($latestLog.Name)" -ForegroundColor Green
        $logContent = Get-Content $latestLog.FullName -Tail 30

        $bonjourOK = $logContent | Select-String "Bonjour service started"
        $universalOK = $logContent | Select-String "universal subtype"
        $ippOK = $logContent | Select-String "IPP server started"

        if ($bonjourOK) { Write-Host "    OK - Bonjour started" -ForegroundColor Green }
        else { Write-Host "    ERROR - Bonjour NOT started" -ForegroundColor Red }

        if ($universalOK) { Write-Host "    OK - Universal subtype advertised" -ForegroundColor Green }
        else { Write-Host "    WARNING - Universal subtype not found in logs" -ForegroundColor Yellow }

        if ($ippOK) { Write-Host "    OK - IPP server started" -ForegroundColor Green }
        else { Write-Host "    ERROR - IPP NOT started" -ForegroundColor Red }

        Write-Host ""
        Write-Host "  Last 15 log lines:" -ForegroundColor Cyan
        $logContent[-15..-1] | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    } else {
        Write-Host "  ERROR - No log files found" -ForegroundColor Red
    }
} else {
    Write-Host "  ERROR - Log directory not found" -ForegroundColor Red
}
Write-Host ""

# 7. Test IPP endpoint
Write-Host "[7/7] Testing IPP endpoint..." -ForegroundColor Yellow
try {
    $null = Invoke-WebRequest -Uri "http://localhost:631/" -Method GET -TimeoutSec 3 -ErrorAction Stop
    Write-Host "  OK - IPP endpoint responding" -ForegroundColor Green
} catch {
    if ($_.Exception.Message -like "*405*") {
        Write-Host "  OK - IPP active (405 is normal for GET)" -ForegroundColor Green
    } else {
        Write-Host "  ERROR - IPP not responding: $($_.Exception.Message)" -ForegroundColor Red
    }
}
Write-Host ""

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  iPhone Troubleshooting Checklist" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "- iPhone on SAME Wi-Fi as this PC" -ForegroundColor White
Write-Host "- NOT on guest network" -ForegroundColor White
Write-Host "- NO VPN active" -ForegroundColor White
Write-Host "- Private Wi-Fi Address OFF (Settings > Wi-Fi > (i) > Private Address)" -ForegroundColor White
Write-Host "- Try Airplane mode ON/OFF" -ForegroundColor White
Write-Host ""
