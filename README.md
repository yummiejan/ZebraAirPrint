# Zebra AirPrint Service

Windows-Service, der einen Zebra ZD410 Etikettendrucker als AirPrint-Drucker im Netzwerk verfügbar macht.

## Features

✅ **AirPrint-Unterstützung** - Drucken vom iPad/iPhone ohne zusätzliche Apps
✅ **Automatische Service Discovery** - Drucker wird automatisch im Netzwerk gefunden
✅ **Queue-Management** - Jobs werden bei Drucker-Ausfall in eine Warteschlange gestellt
✅ **Retry-Logik** - Automatische Wiederholungsversuche mit exponentiellen Backoff
✅ **Strukturiertes Logging** - Vollständiges Logging aller Aktivitäten
✅ **Windows Service** - Läuft automatisch beim Systemstart

## Systemvoraussetzungen

- Windows 10 oder höher
- .NET 8 SDK (für Build)
- .NET 8 Runtime (für Ausführung)
- Apple Bonjour Services
- Zebra ZD410 mit Zebra Setup Utilities

## Installation

### Schritt 1: Voraussetzungen installieren

**1. .NET 8 SDK installieren:**
```powershell
winget install Microsoft.DotNet.SDK.8
```

**2. Bonjour Services installieren:**
- Download: [https://support.apple.com/kb/DL999](https://support.apple.com/kb/DL999)
- Alternative: Bonjour Print Services for Windows

**3. Zebra Setup Utilities installieren:**
- Download: [https://www.zebra.com/gb/en/support-downloads/software/printer-software/printer-setup-utilities.html](https://www.zebra.com/gb/en/support-downloads/software/printer-software/printer-setup-utilities.html)
- Drucker als "Zebra ZD410" in Windows einrichten

### Schritt 2: Installer bauen

```powershell
.\build-installer.ps1
```

Der MSI-Installer wird erstellt in:
```
Installer\bin\Release\ZebraAirPrintInstaller.msi
```

### Schritt 3: Service installieren

**Doppelklick auf die MSI-Datei** oder:

```powershell
msiexec /i "Installer\bin\Release\ZebraAirPrintInstaller.msi"
```

Der Installer:
- ✅ Installiert den Service nach `C:\Program Files\Zebra AirPrint Service\`
- ✅ Konfiguriert Firewall-Regeln automatisch
- ✅ Startet den Service automatisch
- ✅ Richtet Auto-Start beim Systemstart ein

## Konfiguration

Die Konfiguration erfolgt über die Datei `appsettings.json`:

```json
{
  "Printer": {
    "Name": "Zebra ZD410",
    "LabelWidth": 50.7,
    "LabelHeight": 30.6,
    "Resolution": 203,
    "ConnectionType": "WindowsPrinter",
    "IpAddress": "",
    "Port": 9100
  },
  "Service": {
    "Name": "Zebra AirPrint Service",
    "IppPort": 631,
    "BonjourServiceName": "Zebra ZD410"
  },
  "Queue": {
    "MaxJobs": 50,
    "RetryIntervalSeconds": 30,
    "ExponentialBackoffEnabled": true,
    "MaxBackoffSeconds": 60
  },
  "Logging": {
    "Path": "Logs",
    "RetentionDays": 30,
    "MinimumLevel": "Information"
  }
}
```

### Drucker-Konfiguration

**ConnectionType**: Zwei Modi verfügbar:

**1. WindowsPrinter (empfohlen):**
```json
"ConnectionType": "WindowsPrinter"
```
- Nutzt den Windows Print Spooler
- Funktioniert mit USB oder Netzwerk-Druckern
- Keine IP-Adresse erforderlich

**2. DirectIP (für direkte TCP/IP-Verbindung):**
```json
"ConnectionType": "DirectIP",
"IpAddress": "192.168.1.100",
"Port": 9100
```
- Direkte Verbindung zum Drucker (Port 9100)
- Schneller, aber erfordert statische IP
- Nur für Netzwerk-Drucker

## Verwendung

### Service-Verwaltung

**Status prüfen:**
```powershell
sc query "ZebraAirPrintService"
```

**Service stoppen:**
```powershell
sc stop "ZebraAirPrintService"
```

**Service starten:**
```powershell
sc start "ZebraAirPrintService"
```

**Service neu starten:**
```powershell
sc stop "ZebraAirPrintService"
sc start "ZebraAirPrintService"
```

### Vom iPad drucken

1. Öffnen Sie auf dem iPad: **Einstellungen → Allgemein → Drucker & Scanner**
2. Warten Sie ca. 10 Sekunden
3. Der Drucker "Zebra ZD410" sollte automatisch erscheinen
4. In jeder App mit Druckfunktion: Tippen Sie auf **Teilen → Drucken**
5. Wählen Sie "Zebra ZD410" als Drucker
6. Drucken Sie!

## Logs

Logs werden automatisch geschrieben in:
- **Service-Installation:** `C:\AirPrintService\Logs\airprint-YYYY-MM-DD.txt`

**Log-Beispiel:**
```
[2025-11-10 14:23:45] [INF] AirPrint Service is starting
[2025-11-10 14:23:46] [INF] Bonjour service started successfully
[2025-11-10 14:23:47] [INF] IPP server started successfully on port 631
[2025-11-10 14:24:12] [INF] Print job 1 queued: ContentType=image/urf, Size=45678 bytes
[2025-11-10 14:24:13] [INF] Print job 1 completed successfully
```

## Deinstallation

### Option 1: Windows Systemsteuerung

1. **Einstellungen → Apps → Apps & Features**
2. Suche nach "Zebra AirPrint Service"
3. Klick auf **Deinstallieren**

### Option 2: MSI-Installer

```powershell
msiexec /x "Installer\bin\Release\ZebraAirPrintInstaller.msi"
```

### Option 3: Uninstall-Script

```powershell
.\uninstall-service.ps1
```

Der Deinstaller entfernt automatisch:
- ✅ Windows Service
- ✅ Firewall-Regeln
- ✅ URL ACL Konfiguration
- ✅ Installationsdateien

## Architektur

```
Program.cs (Host + DI)
    └── AirPrintServiceWorker (BackgroundService)
        ├── IppServer (HttpListener auf Port 631)
        │   ├── IppParser (Request/Response Handling)
        │   └── QueueService (Job-Queue mit Retry)
        │       └── PrinterService (Windows Printing API)
        └── BonjourService (mDNS Advertisement)
```

## Unterstützte Formate

- ✅ `application/pdf` - PDF-Dokumente
- ✅ `image/urf` - Apple Unencoded Raster Format
- ✅ `image/jpeg` - JPEG-Bilder
- ✅ `image/png` - PNG-Bilder

## Support & Entwicklung

**Projekt:** ZebraAirPrintService
**Version:** 1.0.0 (Phase 1 MVP)
**Framework:** .NET 8
**Lizenz:** MIT

---

**Viel Erfolg mit dem AirPrint-Service!** 🖨️
