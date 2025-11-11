# AirPrint Server für Zebra ZD410 - Requirements Dokument

Version: 1.0
Datum: 10. November 2025
Projekt: ZebraAirPrintService

---

## 1. PROJEKTZIEL

Entwicklung eines Windows-Service, der als AirPrint-Server fungiert und Druckaufträge von einem iPad annimmt und an einen Zebra ZD410 Etikettendrucker weiterleitet.

---

## 2. TECHNISCHE RAHMENBEDINGUNGEN

### 2.1 Hardware-Umgebung

**Drucker:**
- Modell: Zebra ZD410
- Verbindung: Ethernet (statische IP)
- Etikettenformat: 50.7mm × 30.6mm

**Windows-PC:**
- Typ: Kassen-PC (permanent in Betrieb)
- Betriebssystem: Windows 10
- Verbindung: LAN
- Zebra-Treiber: Bereits installiert und funktionierend
- Zebra Setup Utility: Vorhanden

**iPad:**
- Verbindung: WLAN (gleiches Netzwerk wie PC)
- Anzahl: 1 Gerät
- Verwendung: Shopify Admin App mit SKUGen

### 2.2 Software-Umgebung

**Voraussetzungen:**
- .NET 8 Runtime
- Apple Bonjour Services
- Zebra Windows-Treiber

**Druck-Quelle:**
- SKUGen (Shopify App)
- Ausgabeformat: HTML mit inline CSS
- HTML-Definition: Bereits auf 50.7mm × 30.6mm gesetzt
- Browser-Rendering: Safari/Shopify App konvertiert zu PDF/URF

---

## 3. ARCHITEKTUR

### 3.1 Technologie-Stack

**Haupttechnologie:**
- Sprache: C# 
- Framework: .NET 8
- Service-Type: Windows Service

**Kern-Komponenten:**
- IPP-Server (Internet Printing Protocol)
- mDNS/Bonjour (Service Discovery)
- Windows Printing API (Druckertreiber-Zugriff)
- Logging-Framework (Serilog)
- Konfigurationsmanagement (appsettings.json)

**NuGet-Pakete:**
- TopShelf (Windows Service Wrapper)
- Serilog.Sinks.File (Strukturiertes Logging)
- Microsoft.Extensions.Configuration.Json (Config-Management)
- System.Drawing.Common (Druckeroperationen)
- Makaretu.Dns.Multicast (mDNS-Alternative falls nötig)

### 3.2 Projekt-Struktur

```
ZebraAirPrintService/
├── ZebraAirPrintService.csproj
├── Program.cs
├── appsettings.json
├── README.md
├── src/
│   ├── Services/
│   │   ├── IppServer.cs
│   │   ├── BonjourService.cs
│   │   ├── PrinterService.cs
│   │   └── QueueService.cs
│   ├── Models/
│   │   ├── PrintJob.cs
│   │   └── PrinterStatus.cs
│   └── Utils/
│       ├── Logger.cs
│       └── ConfigManager.cs
└── Logs/
```

---

## 4. FUNKTIONALE ANFORDERUNGEN

### 4.1 Phase 1 - MVP (Minimum Viable Product)

#### 4.1.1 Windows Service
- Start als Windows-Service
- Automatischer Start beim System-Boot
- Graceful Shutdown bei Service-Stop
- Automatischer Restart bei Crash
- Keine Admin-Rechte für Betrieb (nur für Installation)

#### 4.1.2 IPP-Server

**Netzwerk:**
- Port: 631 (Standard IPP-Port)
- Binding: Nur lokale IP-Adresse (keine externe Exposition)
- Protokoll: HTTP 1.1

**Unterstützte Formate:**
- application/pdf
- image/urf (Apple Unencoded Raster Format)
- image/jpeg
- image/png

**IPP-Operations:**
- Print-Job
- Get-Printer-Attributes
- Get-Jobs
- Cancel-Job

#### 4.1.3 Bonjour/mDNS Service-Announcement

**Service-Definition:**
- Service-Type: `_ipp._tcp.local.`
- Service-Name: "Zebra ZD410"
- Port: 631

**Printer-Attributes (kritisch):**
```
media-size-supported: om_small-label_50.7x30.6mm
media-default: om_small-label_50.7x30.6mm
media-size: 50.7mm x 30.6mm
printer-resolution: 600dpi
document-format-supported: application/pdf, image/urf, image/jpeg, image/png
urf-supported: W8H7,SRGB24,RS600
color-supported: false
sides-supported: one-sided
```

#### 4.1.4 Druckauftrags-Verarbeitung

**Workflow:**
1. IPP-Request empfangen
2. Request-Header parsen (Content-Type, Content-Length)
3. Job-Daten in Memory-Buffer laden
4. Content-Type und Dokumentgröße loggen
5. Job an Windows-Druckerwarteschlange senden (Drucker: "Zebra ZD410")
6. Job-ID zurückgeben
7. Status-Update an Client senden

**Fehlerbehandlung:**
- Bei Fehler: Job in interne Queue
- Retry-Mechanismus mit konfigurierbare Intervallen
- Status-Tracking pro Job

#### 4.1.5 Queue-Management

**Queue-Funktionalität:**
- Max. Kapazität: 50 Jobs (konfigurierbar)
- Persistierung: Im Memory (kein Disk-Storage)
- FIFO-Prinzip (First In, First Out)
- Bei Überlauf: Älteste Jobs verwerfen

**Retry-Logik:**
- Intervall: 30 Sekunden (konfigurierbar)
- Max. Versuche: Unbegrenzt solange Queue nicht voll
- Exponential Backoff bei Netzwerkfehlern: 1s, 2s, 4s, 8s, max. 60s

#### 4.1.6 Logging

**Log-Framework:**
- Serilog mit File-Sink
- Format: JSON oder strukturiert lesbar
- Rotation: Täglich
- Retention: 30 Tage

**Log-Pfad:**
```
C:\AirPrintService\Logs\airprint-{Date}.txt
```

**Zu loggende Events:**

**INFO-Level:**
- Service gestartet/gestoppt
- Druckauftrag empfangen (Content-Type, Größe, Format)
- Job erfolgreich an Drucker gesendet
- Job erfolgreich gedruckt

**WARNING-Level:**
- Drucker offline (Job in Queue)
- Queue fast voll (> 80%)
- Unbekanntes Format empfangen

**ERROR-Level:**
- Netzwerkfehler
- Drucker-Fehler
- Parsing-Fehler
- Service-Crash

**Log-Format Beispiel:**
```
[2025-11-10 14:23:45] [INFO] Service started
[2025-11-10 14:24:12] [INFO] Print job received: ContentType=image/urf, Size=50.7x30.6mm, JobId=12345
[2025-11-10 14:24:13] [INFO] Job forwarded to Zebra ZD410: JobId=12345
[2025-11-10 14:24:15] [SUCCESS] Print completed: JobId=12345
[2025-11-10 14:25:01] [ERROR] Printer offline: JobId=12346 queued for retry
```

#### 4.1.7 Konfiguration

**Config-Datei: appsettings.json**

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
    "RetryIntervalSeconds": 30
  },
  "Logging": {
    "Path": "C:\\AirPrintService\\Logs",
    "RetentionDays": 30
  }
}
```

---

### 4.2 Phase 2 - Erweiterte Funktionen (Optional)

**Implementierung nur falls MVP-Test zeigt, dass Zebra-Treiber Format nicht automatisch anpasst.**

#### 4.2.1 Bildverarbeitung

**Workflow:**
1. Eingehende Datei (PDF/URF/PNG) in Bitmap konvertieren
2. Content-Detection: Nicht-weiße Pixel finden
3. Automatisches Cropping auf relevanten Inhalt
4. Skalierung auf exaktes Etikettenformat
5. Ausgabe: 1196 × 723 Pixel (50.7mm × 30.6mm @ 600 DPI)
6. An Zebra-Treiber senden

**Zusätzliche Libraries:**
- PDFium.NET (PDF-Rendering)
- SkiaSharp oder System.Drawing (Bildverarbeitung)
- URF-Decoder (falls URF-Format verwendet wird)

---

## 5. USER INTERFACE (Optional)

### 5.1 System Tray Icon

**Status-Indikation:**
- Grün: Service läuft, Drucker online
- Gelb: Jobs in Queue
- Rot: Drucker offline oder Fehler

**Kontextmenü (Rechtsklick):**
- Status anzeigen
- Queue anzeigen (Anzahl Jobs)
- Logs öffnen
- Service neu starten
- Beenden

### 5.2 Status-Fenster (Optional WPF GUI)

**Minimales Fenster mit Informationen:**
- Service-Status (Running/Stopped)
- Drucker-Status (Online/Offline)
- Aktuelle Queue-Größe
- Letzte Aktivität (Timestamp)
- Buttons: "Queue anzeigen", "Logs öffnen"

---

## 6. NICHT-FUNKTIONALE ANFORDERUNGEN

### 6.1 Performance

**Zielwerte:**
- Service-Startzeit: < 5 Sekunden
- Druckauftrags-Verarbeitung: < 2 Sekunden
- Memory-Verbrauch (Idle): < 100 MB
- Memory-Verbrauch (unter Last): < 250 MB

### 6.2 Stabilität

**Anforderungen:**
- Uptime-Ziel: 99.9%
- Maximale Downtime pro Jahr: 8 Stunden
- Automatische Recovery bei Crashes
- Keine Memory-Leaks bei Langzeitbetrieb
- Graceful Handling aller Fehlerszenarien

### 6.3 Security

**Sicherheitsmaßnahmen:**
- Keine externe Netzwerk-Exposition
- Firewall-Regel: Port 631 nur für lokales Netzwerk
- mDNS funktioniert nur im lokalen Subnetz
- Keine unverschlüsselten Credentials in Config
- Keine Admin-Rechte für Service-Betrieb

### 6.4 Wartbarkeit

**Code-Qualität:**
- XML-Dokumentation für alle öffentlichen Methoden
- Keine hardcodierten Werte
- Alle Parameter in Config-Datei
- Strukturierte Error-Messages
- Klare Logging-Ausgaben

---

## 7. INSTALLATION & DEPLOYMENT

### 7.1 Voraussetzungen installieren

**Bonjour Services:**
```
Download: https://support.apple.com/kb/DL999
Alternative: Bonjour Print Services for Windows
Installation: Standard-Installation durchführen
```

**.NET 8 Runtime:**
```powershell
winget install Microsoft.DotNet.Runtime.8
```

### 7.2 Service installieren

**Als Administrator ausführen:**

```powershell
# Service erstellen
sc create "ZebraAirPrintService" binPath="C:\AirPrintService\ZebraAirPrint.exe"

# Beschreibung setzen
sc description "ZebraAirPrintService" "AirPrint Server für Zebra ZD410 Etikettendrucker"

# Auto-Start konfigurieren
sc config "ZebraAirPrintService" start=auto

# Service starten
sc start "ZebraAirPrintService"
```

### 7.3 Firewall-Regeln

**IPP-Port (TCP 631):**
```powershell
netsh advfirewall firewall add rule name="AirPrint IPP" dir=in action=allow protocol=TCP localport=631 profile=private
```

**mDNS-Port (UDP 5353):**
```powershell
netsh advfirewall firewall add rule name="AirPrint mDNS" dir=in action=allow protocol=UDP localport=5353 profile=private
```

### 7.4 Verifizierung

**Service-Status prüfen:**
```powershell
sc query "ZebraAirPrintService"
```

**Logs prüfen:**
```
C:\AirPrintService\Logs\airprint-[Datum].txt
```

---

## 8. TEST-PLAN

### 8.1 Phase 1 - MVP Tests

#### Test 1: Service-Discovery

**Ziel:** Prüfen ob iPad den Drucker findet

**Schritte:**
1. Service auf Windows-PC starten
2. Auf iPad: Einstellungen → Allgemein → Drucker & Scanner öffnen
3. Warten (ca. 10 Sekunden)

**Erwartetes Ergebnis:**
- "Zebra ZD410" erscheint in der Drucker-Liste
- Format wird angezeigt als "50.7mm × 30.6mm" oder "Label"

**Fehlerfall:**
- Falls Drucker nicht erscheint: Bonjour-Service prüfen
- Logs auf Windows-PC prüfen

---

#### Test 2: Druckvorschau-Format

**Ziel:** Prüfen welches Format iPad sendet

**Schritte:**
1. SKUGen in Shopify Admin öffnen
2. Produktetikett generieren
3. "Drucken" antippen
4. Druckvorschau beobachten

**Zu prüfen:**
- Zeigt Vorschau Etikettenformat (50.7 × 30.6mm)?
- Oder zeigt sie A4-Dokument?

**Log prüfen:**
```
C:\AirPrintService\Logs\airprint-[heute].txt
```
- Content-Type checken (PDF, URF, PNG?)
- Dokumentgröße checken

**Erwartetes Ergebnis:**
- Log zeigt "Size=50.7x30.6mm" → Perfekt!
- Log zeigt "Size=210x297mm" → Phase 2 nötig

---

#### Test 3: Echter Druckvorgang

**Ziel:** Etikett tatsächlich drucken

**Schritte:**
1. In Druckvorschau: "Drucken" antippen
2. Warten auf Druckergebnis
3. Etikett aus Drucker nehmen

**Zu prüfen:**
- Wird Etikett gedruckt?
- Ist Format korrekt (passt auf 50.7 × 30.6mm)?
- Ist Barcode lesbar?
- Ist Text vollständig und scharf?

**Logs prüfen:**
- Gibt es Fehler oder Warnungen?
- Wurde Job erfolgreich abgeschlossen?

**Entscheidung nach Test 3:**
- Falls Etikett perfekt: MVP erfolgreich, Projekt abgeschlossen
- Falls Etikett falsch formatiert: Phase 2 implementieren

---

### 8.2 Phase 2 - Format-Anpassung Tests (falls nötig)

#### Test 4: Skalierung

**Nach Implementierung von Phase 2:**
1. Test 2 und Test 3 wiederholen
2. Verschiedene Etiketten-Layouts testen
3. Prüfen ob Skalierung konsistent funktioniert

---

### 8.3 Stress-Tests

#### Test 5: Queue-Funktionalität

**Drucker offline:**
1. Zebra ZD410 ausschalten
2. Vom iPad 5 Etiketten drucken
3. Log prüfen: Jobs in Queue?
4. Drucker einschalten
5. Prüfen: Werden alle 5 Etiketten gedruckt?

**Erwartetes Ergebnis:**
- Alle Jobs werden nacheinander gedruckt
- Keine Jobs gehen verloren

---

#### Test 6: Langzeit-Stabilität

**24-Stunden-Test:**
1. Service starten
2. Alle 30 Minuten ein Etikett drucken
3. Nach 24 Stunden prüfen:
   - Läuft Service noch?
   - Memory-Verbrauch stabil?
   - Alle Druckvorgänge erfolgreich?

---

## 9. OFFENE FRAGEN & ANNAHMEN

### 9.1 Bestätigte Annahmen

- Windows 10 mit Zebra-Treibern auf Kassen-PC
- Drucker hat statische IP-Adresse
- PC kann Bonjour Services installieren
- Etikettenformat 50.7mm × 30.6mm ist korrekt
- SKUGen generiert HTML mit korrektem Format

### 9.2 Unklare Punkte (durch MVP-Test zu klären)

**Kritische Fragen:**
1. Passt Zebra-Treiber Format automatisch an?
2. Welches Format sendet iPad (PDF, URF, PNG, JPEG)?
3. Respektiert Safari/Shopify-App das annoncierte Etikettenformat?
4. Wird A4 gesendet oder bereits 50.7 × 30.6mm?

**Diese Fragen werden durch Test 2 & 3 beantwortet.**

---

## 10. SUCCESS-KRITERIEN

### 10.1 MVP erfolgreich wenn:

- iPad findet Drucker in AirPrint-Liste
- Etikett wird gedruckt (Format zunächst egal)
- Service läuft stabil über 24 Stunden
- Logging funktioniert vollständig
- Keine Crashes oder unbehandelte Exceptions

### 10.2 Projekt erfolgreich wenn:

- Etikett wird korrekt formatiert gedruckt (50.7 × 30.6mm)
- Service läuft ohne manuelle Eingriffe
- Fehlerbehandlung funktioniert (Drucker offline → Queue → Retry)
- Kann ohne technisches Wissen bedient werden
- Dokumentation vollständig

---

## 11. DELIVERABLES

### 11.1 Auslieferbare Komponenten

**Executable:**
- ZebraAirPrint.exe (Single-File-Publish wenn möglich)

**Konfiguration:**
- appsettings.json (mit Dokumentation)

**Installer:**
- install.ps1 (PowerShell-Script für automatische Installation)
- uninstall.ps1 (PowerShell-Script für Deinstallation)

**Dokumentation:**
- README.md (Setup-Anleitung)
- TROUBLESHOOTING.md (Fehlerbehandlung)

**Logs:**
- Werden automatisch in C:\AirPrintService\Logs erstellt

---

## 12. ZEITPLAN & MEILENSTEINE

### 12.1 Phase 1 - MVP

**Geschätzte Entwicklungszeit: 2-3 Tage**

**Meilensteine:**
1. Projekt-Setup & Dependencies (0.5 Tage)
2. IPP-Server Implementation (1 Tag)
3. Bonjour/mDNS Integration (0.5 Tage)
4. Drucker-Integration (0.5 Tage)
5. Logging & Error-Handling (0.5 Tage)
6. Testing & Bugfixes (1 Tag)

### 12.2 Phase 2 - Format-Anpassung (optional)

**Nur falls MVP-Test fehlschlägt**

**Geschätzte Entwicklungszeit: 1-2 Tage**

**Meilensteine:**
1. Image-Processing Library Integration (0.5 Tage)
2. PDF/URF Decoding (0.5 Tage)
3. Skalierungs-Logik (0.5 Tage)
4. Testing & Optimierung (0.5 Tage)

---

Ende des Dokuments.