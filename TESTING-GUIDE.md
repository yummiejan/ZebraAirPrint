# Testing Guide - Zebra AirPrint Service MSI Installer

## Build Status

**MSI Successfully Built!**
- Location: `Installer\bin\Release\ZebraAirPrintServiceSetup.msi`
- Size: 1.8 MB
- Platform: x64
- Default Installation: `C:\Program Files\ZebraAirPrintService`

---

## What to Do On-Site (Testing Checklist)

### Prerequisites

Before installing, ensure the target machine has:
- [ ] Windows 10/11 (64-bit)
- [ ] .NET 8 Runtime installed
- [ ] Bonjour Services for Windows installed
- [ ] Administrator rights

### Test 1: Fresh Installation with Default Path

1. **Double-click** `ZebraAirPrintServiceSetup.msi`
2. Click through the installation wizard
3. **Observe:** The default path should be `C:\Program Files\ZebraAirPrintService`
4. Complete the installation
5. **Verify Installation:**
   ```powershell
   # Check if service is running
   Get-Service ZebraAirPrintService

   # Check files exist
   dir "C:\Program Files\ZebraAirPrintService"

   # Should show:
   # - ZebraAirPrintService.exe
   # - appsettings.json
   # - Logs\ (directory)
   ```

6. **Test Service:**
   ```powershell
   # Stop service
   Stop-Service ZebraAirPrintService

   # Start service
   Start-Service ZebraAirPrintService

   # Check status
   Get-Service ZebraAirPrintService  # Should show "Running"
   ```

7. **Check Firewall Rules:**
   ```powershell
   # Check firewall rules were created
   Get-NetFirewallRule | Where-Object {$_.DisplayName -like "*Zebra AirPrint*"}
   ```

8. **Check Logs:**
   ```powershell
   # Check if logs are being created
   dir "C:\Program Files\ZebraAirPrintService\Logs"

   # View latest log
   Get-Content "C:\Program Files\ZebraAirPrintService\Logs\airprint-*.txt" -Tail 20
   ```

9. **Test Printing:**
   - Connect iPhone/iPad to same network
   - Try to print a test page
   - Check if print job appears in logs

---

### Test 2: Custom Installation Path

1. **Uninstall** the previous installation (if installed):
   ```powershell
   # Via GUI: Settings > Apps > Zebra AirPrint Service > Uninstall
   # OR via command line:
   msiexec /x ZebraAirPrintServiceSetup.msi
   ```

2. **Install to Custom Path:**
   ```powershell
   # Install to custom location
   msiexec /i ZebraAirPrintServiceSetup.msi INSTALLFOLDER="C:\CustomPath\ZebraAirPrint"
   ```

   **OR use GUI:**
   - Double-click MSI
   - In the wizard, click "Change" next to the installation folder
   - Browse to a custom location (e.g., `C:\CustomPath\ZebraAirPrint`)
   - Complete installation

3. **Verify Custom Path:**
   ```powershell
   # Check service is running
   Get-Service ZebraAirPrintService

   # Check files in custom location
   dir "C:\CustomPath\ZebraAirPrint"

   # Check logs in custom location
   dir "C:\CustomPath\ZebraAirPrint\Logs"
   ```

4. **Verify Logging Works at Custom Path:**
   ```powershell
   # Restart service to trigger log write
   Restart-Service ZebraAirPrintService

   # Check logs are created in custom path
   Get-Content "C:\CustomPath\ZebraAirPrint\Logs\airprint-*.txt" -Tail 10
   ```

---

### Test 3: Upgrade Scenario

1. **With service installed**, try to install again (simulate upgrade)
2. **Expected Behavior:**
   - Installer should detect existing installation
   - May prompt to upgrade or reinstall
   - Service should continue running after upgrade

---

### Test 4: Silent Installation (For Deployment)

```powershell
# Silent install with default path
msiexec /i ZebraAirPrintServiceSetup.msi /quiet /norestart

# Silent install with custom path
msiexec /i ZebraAirPrintServiceSetup.msi /quiet /norestart INSTALLFOLDER="C:\AirPrintService"

# Silent uninstall
msiexec /x ZebraAirPrintServiceSetup.msi /quiet /norestart

# Install with logging
msiexec /i ZebraAirPrintServiceSetup.msi /l*v install-log.txt
```

---

### Test 5: Configuration Changes

1. **Stop the service:**
   ```powershell
   Stop-Service ZebraAirPrintService
   ```

2. **Edit configuration** at `[InstallPath]\appsettings.json`:
   - Change printer name
   - Modify IPP port (test with 632 instead of 631)
   - Adjust logging level to "Debug"

3. **Start service:**
   ```powershell
   Start-Service ZebraAirPrintService
   ```

4. **Verify changes took effect** by checking logs

---

### Test 6: Service Recovery

1. **Simulate service crash:**
   ```powershell
   # Force kill the service process
   $process = Get-Process -Name "ZebraAirPrintService" -ErrorAction SilentlyContinue
   if ($process) { Stop-Process -Id $process.Id -Force }
   ```

2. **Wait 5-10 seconds**

3. **Verify auto-restart:**
   ```powershell
   Get-Service ZebraAirPrintService  # Should show "Running" again
   ```

---

### Test 7: Network Connectivity

1. **From iPhone/iPad:**
   - Open any app that supports printing
   - Tap Share > Print
   - Look for "Zebra ZD410" printer (or configured name)
   - Try to print a test page

2. **Check logs** for connection attempts

3. **Verify mDNS is working:**
   ```powershell
   # Install Bonjour Browser (optional) or use dns-sd command
   dns-sd -B _ipp._tcp
   # Should show "Zebra ZD410" service
   ```

---

## Common Issues & Troubleshooting

### Issue: Service won't start

**Check:**
```powershell
# View service status details
Get-Service ZebraAirPrintService | Format-List *

# Check Windows Event Viewer
Get-EventLog -LogName Application -Source "ZebraAirPrintService" -Newest 10
```

**Possible causes:**
- .NET 8 Runtime not installed
- Port 631 already in use
- Bonjour Services not running
- Firewall blocking connections

---

### Issue: Can't see printer from iOS device

**Check:**
1. Are both devices on same network/VLAN?
2. Is Bonjour Service running?
   ```powershell
   Get-Service Bonjour*
   ```
3. Is firewall rule active?
   ```powershell
   Get-NetFirewallRule -DisplayName "Zebra AirPrint*"
   ```
4. Is service listening on port 631?
   ```powershell
   netstat -ano | findstr ":631"
   ```

---

### Issue: Logs are not being created

**Check:**
1. Does Logs directory exist?
   ```powershell
   Test-Path "[InstallPath]\Logs"
   ```
2. Does service have write permissions?
3. Check appsettings.json - Path should be "Logs" (relative)

---

## Uninstallation Test

### GUI Uninstall:
1. Open Settings > Apps & Features
2. Find "Zebra AirPrint Service"
3. Click Uninstall
4. **Verify:**
   - Service removed: `Get-Service ZebraAirPrintService` (should error)
   - Files removed: Installation directory should be deleted
   - Firewall rules removed

### Silent Uninstall:
```powershell
msiexec /x ZebraAirPrintServiceSetup.msi /quiet
```

---

## Expected Test Results

After successful installation:

- ✅ Service "ZebraAirPrintService" is **Running**
- ✅ Files exist in installation directory
- ✅ Logs directory created and contains log files
- ✅ Firewall rules created for ports 631 (TCP) and 5353 (UDP)
- ✅ Service auto-restarts on failure
- ✅ Service starts automatically on boot
- ✅ Printer visible from iOS devices on same network
- ✅ Configuration changes take effect after service restart
- ✅ Uninstallation removes all files, service, and firewall rules

---

## Production Deployment Notes

Once testing is complete:

1. **Update these values** in [Product.wxs](Installer/Product.wxs:8-9):
   - Line 8: Change "Your Company Name" to your actual company name
   - Line 9: Generate a new UpgradeCode GUID:
     ```powershell
     [guid]::NewGuid()
     ```

2. **Update version** in [Product.wxs](Installer/Product.wxs:7) when releasing updates

3. **Sign the MSI** for production:
   ```powershell
   signtool sign /f "YourCertificate.pfx" /p "password" /t http://timestamp.digicert.com ZebraAirPrintServiceSetup.msi
   ```

4. **Test on clean Windows VM** before mass deployment

---

## Quick Reference Commands

```powershell
# Check service status
Get-Service ZebraAirPrintService

# Start/Stop/Restart
Start-Service ZebraAirPrintService
Stop-Service ZebraAirPrintService
Restart-Service ZebraAirPrintService

# View logs
Get-Content "C:\Program Files\ZebraAirPrintService\Logs\airprint-*.txt" -Tail 50

# Check what's listening on port 631
netstat -ano | findstr ":631"

# Check firewall rules
Get-NetFirewallRule | Where-Object {$_.DisplayName -like "*Zebra*"}

# Rebuild MSI (from project root)
.\build-installer.ps1
```

---

## Support Information

If you encounter issues during testing:

1. Collect logs from `[InstallPath]\Logs\`
2. Check Windows Event Viewer (Application log)
3. Run installer with verbose logging:
   ```powershell
   msiexec /i ZebraAirPrintServiceSetup.msi /l*v install-debug.log
   ```
4. Check service configuration:
   ```powershell
   Get-WmiObject Win32_Service | Where-Object {$_.Name -eq "ZebraAirPrintService"} | Format-List *
   ```

---

**Happy Testing!** 🎯
