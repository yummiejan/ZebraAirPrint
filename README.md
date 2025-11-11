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
- .NET 8 Runtime
- Apple Bonjour Services
- Zebra ZD410 mit installiertem Windows-Treiber

## Installation

### Schritt 1: Voraussetzungen installieren

**1. .NET 8 Runtime installieren:**
```powershell
winget install Microsoft.DotNet.Runtime.8
```

**2. Bonjour Services installieren:**
- Download: [https://support.apple.com/kb/DL999](https://support.apple.com/kb/DL999)
- Alternative: Bonjour Print Services for Windows

### Schritt 2: Projekt bauen

```powershell
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false
```

Die kompilierte Anwendung befindet sich dann in:
```
bin\Release\net8.0-windows\win-x64\publish\
```

### Schritt 3: Service installieren

**Als Administrator ausführen:**

```powershell
.\install.ps1
```

Oder manuell:

```powershell
# Service erstellen
sc create "ZebraAirPrintService" binPath="C:\AirPrintService\ZebraAirPrintService.exe"

# Beschreibung setzen
sc description "ZebraAirPrintService" "AirPrint Server für Zebra ZD410 Etikettendrucker"

# Auto-Start konfigurieren
sc config "ZebraAirPrintService" start=auto

# Service starten
sc start "ZebraAirPrintService"
```

### Schritt 4: Firewall-Regeln konfigurieren

```powershell
# IPP-Port (TCP 631)
netsh advfirewall firewall add rule name="AirPrint IPP" dir=in action=allow protocol=TCP localport=631 profile=private

# mDNS-Port (UDP 5353)
netsh advfirewall firewall add rule name="AirPrint mDNS" dir=in action=allow protocol=UDP localport=5353 profile=private
```

## Konfiguration

Die Konfiguration erfolgt über die Datei `appsettings.json`:

```json
{
  "Printer": {
    "Name": "Zebra ZD410",
    "LabelWidth": 50.7,
    "LabelHeight": 30.6,
    "Resolution": 600
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
    "Path": "C:\\AirPrintService\\Logs",
    "RetentionDays": 30,
    "MinimumLevel": "Information"
  }
}
```

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
```
C:\AirPrintService\Logs\airprint-YYYY-MM-DD.txt
```

**Log-Beispiel:**
```
[2025-11-10 14:23:45] [INF] AirPrint Service is starting
[2025-11-10 14:23:46] [INF] Bonjour service started successfully
[2025-11-10 14:23:47] [INF] IPP server started successfully on port 631
[2025-11-10 14:24:12] [INF] Print job 1 queued: ContentType=image/urf, Size=45678 bytes
[2025-11-10 14:24:13] [INF] Print job 1 completed successfully
```

## Troubleshooting

### Drucker erscheint nicht auf dem iPad

**Lösung:**
1. Prüfen Sie, ob der Service läuft: `sc query "ZebraAirPrintService"`
2. Prüfen Sie die Logs in `C:\AirPrintService\Logs\`
3. Stellen Sie sicher, dass PC und iPad im gleichen Netzwerk sind
4. Prüfen Sie die Firewall-Regeln
5. Starten Sie Bonjour Services neu: `services.msc` → "Bonjour-Dienst"

### Port 631 ist bereits belegt

**Lösung:**
1. Ändern Sie den Port in `appsettings.json` (z.B. auf 8631)
2. Passen Sie die Firewall-Regel entsprechend an
3. Service neu starten

### Drucker druckt nicht (offline)

**Lösung:**
1. Prüfen Sie, ob der Zebra-Drucker in Windows als "Zebra ZD410" eingerichtet ist
2. Drucken Sie einen Windows-Testdruck, um zu prüfen, ob der Drucker funktioniert
3. Jobs werden automatisch wiederholt, wenn der Drucker wieder online ist

### Service startet nicht

**Fehler: "Access Denied" beim Starten**

**Lösung:**
```powershell
# URL ACL konfigurieren (als Administrator)
netsh http add urlacl url=http://+:631/ user=EVERYONE
```

## Deinstallation

```powershell
.\uninstall.ps1
```

Oder manuell:

```powershell
# Service stoppen und entfernen
sc stop "ZebraAirPrintService"
sc delete "ZebraAirPrintService"

# Firewall-Regeln entfernen
netsh advfirewall firewall delete rule name="AirPrint IPP"
netsh advfirewall firewall delete rule name="AirPrint mDNS"

# URL ACL entfernen (falls konfiguriert)
netsh http delete urlacl url=http://+:631/
```

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

## Nächste Schritte (Phase 2 - Optional)

Falls die Etiketten nicht korrekt formatiert werden:
- Implementierung von Bildverarbeitung (Cropping, Skalierung)
- PDF-Rendering mit PDFium.NET
- URF-Format-Decoder
- Automatische Format-Anpassung auf 50.7mm × 30.6mm

---

**Viel Erfolg mit dem AirPrint-Service!** 🖨️
