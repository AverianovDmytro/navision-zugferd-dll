# DLL registrieren und Variable anlegen in Navision 2017 (10.0)

Schritt-für-Schritt-Anleitung für `ZugferdNavision.dll` auf einem Navision 2017 (Version 10.0) Server.

---

## Voraussetzungen

| Anforderung | Details |
|---|---|
| .NET Framework | 4.6.2 oder höher (empfohlen: 4.7.2) |
| Navision-Version | Microsoft Dynamics NAV 2017 (10.0) |
| Berechtigungen | Administrator auf dem NAV-Server |
| Bitness | NAV 2017 ist **32-Bit** → es muss die 32-Bit-Version von `regasm.exe` verwendet werden |

---

## Schritt 1 — DLL auf den Server kopieren

Lege die fertig gebaute DLL in den NAV Add-Ins-Ordner:

```
C:\Program Files\Microsoft Dynamics NAV\100\Service\Add-Ins\ZugferdNavision.dll
```

> Der genaue Pfad hängt von der Installation ab. Der Ordner `Add-Ins` unterhalb des NAV-Dienstverzeichnisses ist der empfohlene Ablageort, da NAV ihn automatisch in den Assembly-Suchpfad aufnimmt.

**Benötigte Datei:** `ZugferdNavision.dll` (Build-Ausgabe aus `src\ZugferdNavision.Converter\bin\Release\`)

---

## Schritt 2 — DLL für COM registrieren

Die Registrierung muss **als Administrator** durchgeführt werden.

### Option A — PowerShell-Skript (empfohlen)

```powershell
.\scripts\Register-ZugferdNavision.ps1 -DllPath "C:\Program Files\Microsoft Dynamics NAV\100\Service\Add-Ins\ZugferdNavision.dll" -Platform x86
```

Das Skript prüft automatisch:
- ob es mit Administrator-Rechten läuft
- ob `regasm.exe` vorhanden ist
- und gibt eine eindeutige Erfolgs- oder Fehlermeldung aus

### Option B — Manuell per CMD (als Administrator)

**NAV 2017 ist 32-Bit** — unbedingt `Framework` (nicht `Framework64`) verwenden:

```cmd
C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe ^
    "C:\Program Files\Microsoft Dynamics NAV\100\Service\Add-Ins\ZugferdNavision.dll" ^
    /codebase /tlb
```

> **Wichtig:** `/codebase` sorgt dafür, dass der vollständige Pfad zur DLL in die Registry eingetragen wird.  
> **Wichtig:** `/tlb` erzeugt die Type Library (`.tlb`), die NAV benötigt, um den Typ im Automation-Picker anzuzeigen.

### Option C — Falls die PS-Ausführungsrichtlinie `.ps1` blockiert

```cmd
scripts\register.cmd
```

---

## Schritt 3 — Registrierung prüfen

```cmd
reg query "HKEY_CLASSES_ROOT\ZugferdNavision.ZugferdConverter"
```

Erwartete Ausgabe:

```
HKEY_CLASSES_ROOT\ZugferdNavision.ZugferdConverter
    (Standard)    REG_SZ    ZugferdNavision.ZugferdConverter

HKEY_CLASSES_ROOT\ZugferdNavision.ZugferdConverter\CLSID
    (Standard)    REG_SZ    {66D83865-A815-47C5-8376-568369E8B428}
```

Zusätzlich prüfen, ob der InprocServer32-Eintrag auf die DLL zeigt:

```cmd
reg query "HKEY_CLASSES_ROOT\CLSID\{66D83865-A815-47C5-8376-568369E8B428}\InprocServer32"
```

Der Wert `CodeBase` muss auf den vollständigen Pfad der DLL zeigen.

---

## Schritt 4 — NAV Development Environment neu starten

NAV liest die Automation-Typen beim Start ein. Nach einer Neuregistrierung muss die **NAV Development Environment vollständig geschlossen und neu gestartet** werden, bevor der neue Typ im Automation-Picker sichtbar ist.

---

## Schritt 5 — Automation-Variablen in einem CodeUnit anlegen

1. CodeUnit öffnen (oder neu anlegen)
2. **F9** drücken (oder Menü **View → C/AL Locals**) — das Fenster für lokale Variablen öffnet sich
3. Folgende Variablen eintragen:

| Name | DataType | SubType | Length |
|---|---|---|---|
| `Converter` | Automation | `'ZugferdNavision'.ZugferdConverter` | |
| `Result` | Automation | `'ZugferdNavision'.ConversionResult` | |
| `ApiUrl` | Text | | 250 |
| `TempFolder` | Text | | 250 |
| `PdfPath` | Text | | 250 |
| `XmlPath` | Text | | 250 |

### SubType korrekt auswählen

Im SubType-Feld auf `...` klicken → der Automation-Picker öffnet sich.

- Im Suchfeld **ZugferdNavision** eingeben
- In der Liste erscheinen:
  - `ZugferdNavision.ZugferdConverter` — Hauptklasse
  - `ZugferdNavision.ConversionResult` — Ergebnisobjekt
- Jeweils auswählen und mit OK bestätigen

> Falls der Typ **nicht in der Liste erscheint**: NAV Development Environment neu starten (Schritt 4) und sicherstellen, dass `regasm /codebase /tlb` als Administrator ausgeführt wurde.

---

## Schritt 6 — C/AL-Code schreiben

### 6.1 TryFunction-Wrapper anlegen

Da NAV 2017 keine inline-`TRY`-Blöcke für COM-Aufrufe unterstützt, muss eine separate lokale Funktion mit der Eigenschaft **TryFunction** erstellt werden.

Neue Funktion `TRY_ConvertToZugferd` anlegen und in den **Eigenschaften (F4)** die Eigenschaft `TryFunction` auf `Yes` setzen:

```pascal
[TryFunction]
LOCAL PROCEDURE TRY_ConvertToZugferd@1(
  VAR Converter@1000 : Automation "'ZugferdNavision'.ZugferdConverter";
  VAR Result@1001    : Automation "'ZugferdNavision'.ConversionResult";
  ApiUrl@1002        : Text[250];
  PdfPath@1003       : Text[250];
  XmlPath@1004       : Text[250];
  TempFolder@1005    : Text[250]);
BEGIN
  CREATE(Converter);
  // Parameter: apiUrl, pdfPath, xmlPath, profile, apiKey, outputDirectory
  Result := Converter.ConvertToZugferd(ApiUrl, PdfPath, XmlPath, 'BASIC', '', TempFolder);
END;
```

### 6.2 Hauptcode im CodeUnit

```pascal
// 1. Pfade vorbereiten
ApiUrl     := 'https://your-api-host/convert';
TempFolder := 'C:\Temp\zugferd_' + DELCHR(FORMAT(CREATEGUID()), '=', '{}');
PdfPath    := TempFolder + '\invoice.pdf';
XmlPath    := TempFolder + '\invoice.xml';
SHELL('cmd /c mkdir "' + TempFolder + '"');

// 2. Rechnung als PDF und XML exportieren
// REPORT.SAVEASPDF(Report::"Sales Invoice", PdfPath, ...);
// XMLPort.EXPORT(XMLport::"ZUGFeRD Invoice", XmlPath, ...);

// 3. DLL aufrufen
IF NOT TRY_ConvertToZugferd(Converter, Result, ApiUrl, PdfPath, XmlPath, TempFolder) THEN BEGIN
  ERROR('ZUGFeRD-Konvertierung fehlgeschlagen: %1', GETLASTERRORTEXT);
  EXIT;
END;

// 4. Validierungswarnungen prüfen
IF Result.HasXmlErrors THEN
  MESSAGE('XML-Validierungsfehler:\n%1', Result.XmlValidationErrors);
IF Result.HasPdfErrors THEN
  MESSAGE('PDF-Validierungsfehler:\n%1', Result.PdfValidationErrors);
IF Result.HasPdfA3Errors THEN
  MESSAGE('PDF/A-3-Validierungsfehler:\n%1', Result.PdfA3ValidationErrors);

// 5. Ergebnis verwenden (anhängen, mailen, archivieren usw.)
MESSAGE('ZUGFeRD-PDF gespeichert unter: %1', Result.OutputPath);
// z. B. DOWNLOAD(Result.OutputPath, '', '', '', LocalFileName);

// 6. Aufräumen
CLEAR(Converter);
SHELL('cmd /c rmdir /s /q "' + TempFolder + '"');
```

---

## Optionale Einstellungen

### API-Key übergeben

Falls der API-Endpunkt eine Authentifizierung erfordert:

```pascal
Result := Converter.ConvertToZugferd(ApiUrl, PdfPath, XmlPath, 'BASIC', 'mein-api-key');
```

Die DLL sendet den Key automatisch als `X-Api-Key`-Header.

### ZUGFeRD-Profil wählen

Der vierte Parameter bestimmt das Konformitätsniveau. Gültige Werte:

| Wert | Beschreibung |
|---|---|
| `'MINIMUM'` | Minimalanforderungen |
| `'BASIC WL'` | Basic ohne Positionsdaten |
| `'BASIC'` | Standard (Standardwert) |
| `'EN16931'` | Europäische Norm |
| `'EXTENDED'` | Erweitertes Profil |
| `'XRECHNUNG'` | Deutsche X-Rechnung |

```pascal
Result := Converter.ConvertToZugferd(ApiUrl, PdfPath, XmlPath, 'XRECHNUNG', '');
```

### HTTP-Timeout anpassen

Bei großen PDF-Dateien kann der Standardwert von 60 Sekunden zu kurz sein:

```pascal
CREATE(Converter);
Converter.TimeoutSeconds := 120;  // 2 Minuten
Result := Converter.ConvertToZugferd(ApiUrl, PdfPath, XmlPath, 'BASIC', '', TempFolder);
```

---

## Deregistrierung

```powershell
.\scripts\Register-ZugferdNavision.ps1 -Unregister
```

oder manuell:

```cmd
C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe ^
    "C:\Program Files\Microsoft Dynamics NAV\100\Service\Add-Ins\ZugferdNavision.dll" ^
    /unregister
```

---

## Fehlerbehebung

| Symptom | Ursache | Lösung |
|---|---|---|
| Typ nicht im Automation-Picker sichtbar | DLL nicht registriert oder `/tlb` fehlt | `regasm /codebase /tlb` als Administrator ausführen; NAV neu starten |
| "Class not registered" zur Laufzeit | `regasm` nicht als Administrator ausgeführt | Erneut als Administrator registrieren |
| "Cannot create ActiveX component" | DLL-Bitness passt nicht zu NAV | NAV 2017 ist 32-Bit → `Framework` (nicht `Framework64`) verwenden |
| Methode nicht sichtbar in C/AL | `[ClassInterface(AutoDual)]` fehlt in der DLL | DLL neu bauen und registrieren |
| "File not found" beim Laden | `/codebase` beim `regasm`-Aufruf vergessen | Stets `/codebase` angeben |
| Timeout beim API-Aufruf | 60-Sekunden-Standard zu kurz | `Converter.TimeoutSeconds := 120;` vor dem Aufruf setzen |
| `reg query` zeigt `ZugferdNavision.Converter` statt `ZugferdNavision.ZugferdConverter` | Alte DLL-Version registriert | Alte Version deregistrieren, neue Version mit korrektem ProgID registrieren |

---

## Technische Referenz

| Eigenschaft | Wert |
|---|---|
| Assembly-Name | `ZugferdNavision` |
| ProgID (Converter) | `ZugferdNavision.ZugferdConverter` |
| CLSID (Converter) | `{66D83865-A815-47C5-8376-568369E8B428}` |
| ProgID (Result) | `ZugferdNavision.ConversionResult` |
| CLSID (Result) | `{5D6258F6-7C4B-4F62-9D7A-4143C1D5211A}` |
| Assembly-GUID | `{4F52A864-903F-4208-8C85-F052A03DEB95}` |
| Ziel-Framework | .NET Framework 4.7.2 (x86) |
| RegAsm-Pfad (32-Bit) | `C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe` |
| RegAsm-Pfad (64-Bit) | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm.exe` |
