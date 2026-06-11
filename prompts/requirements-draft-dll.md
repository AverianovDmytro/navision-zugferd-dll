# Prompt: COM-fähige DLL für Microsoft Dynamics NAV 2017 (Version 10.0) erstellen

## Aufgabe

Erstelle eine vollständige, COM-registrierbare .NET-Assembly (`ZugferdNavision.dll`) für Microsoft Dynamics NAV 2017 (Version 10.0.30699). Die DLL soll in NAV unter **Automation** als `ZugferdNavision.ZugferdConverter` sichtbar sein und ZUGFeRD-PDFs über eine HTTP-API erzeugen.

---

## Technische Anforderungen

### Zielplattform
- **Framework:** .NET Framework **4.6.2** (oder 4.6.2 als Minimum)
- **Plattform:** `x86` oder `AnyCPU` mit `Prefer 32-bit` aktiviert
- **Ausgabe:** Class Library (`.dll`)
- **COM-Interop:** Muss in Windows-Registry registrierbar sein via `regasm.exe /codebase /tlb`

### Projektdatei (.csproj) Anforderungen
```xml
<TargetFramework>net472</TargetFramework>
<PlatformTarget>x86</PlatformTarget>  <!-- oder AnyCPU mit Prefer32Bit=true -->
<RegisterForComInterop>true</RegisterForComInterop>
<GenerateAssemblyInfo>false</GenerateAssemblyInfo>
```

### AssemblyInfo.cs — PFLICHT
```csharp
using System.Runtime.InteropServices;

[assembly: ComVisible(true)]
[assembly: Guid("ASSEMBLY-GUID-HIER-EINTRAGEN")]
```

---

## Klassen-Design

### ZugferdConverter — Hauptklasse

```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace ZugferdNavision
{
    [ComVisible(true)]
    [Guid("66D83865-A815-47C5-8376-568369E8B428")]
    [ProgId("ZugferdNavision.ZugferdConverter")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class ZugferdConverter
    {
        // Parameterloser Konstruktor ist PFLICHT für COM
        public ZugferdConverter() { }

        public ConversionResult ConvertToZugferd(
            string apiUrl,
            string pdfFilePath,
            string xmlFilePath,
            string profile = "BASIC",
            string apiKey = null,
            string outputDirectory = null)
        {
            // ... Implementierung (HTTP MultipartFormData)
        }
    }
}
```

**Wichtig:**
- `[ProgId("ZugferdNavision.ZugferdConverter")]` — genau dieser Name erscheint in NAV unter Automation
- `[ClassInterface(ClassInterfaceType.AutoDual)]` — exponiert alle public-Methoden als COM-Dispatch
- Parameterloser Konstruktor ist für COM-Aktivierung zwingend erforderlich

### ConversionResult — Ergebnisklasse

```csharp
[ComVisible(true)]
[Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]  // neue GUID generieren!
[ClassInterface(ClassInterfaceType.AutoDual)]
public class ConversionResult
{
    public string OutputPath { get; set; }
    public string XmlValidationErrors { get; set; }
    public string PdfValidationErrors { get; set; }
    public string PdfA3ValidationErrors { get; set; }
}
```

**Wichtig:** Jede Klasse die aus NAV zugänglich sein soll braucht eigene GUID und COM-Attribute.

---

## HTTP-Implementierung (kompatibel mit .NET 4.x)

```csharp
// .NET 4.x: kein async/await in COM-Methoden!
// Verwende .GetAwaiter().GetResult() oder WebClient als Alternative

using (var client = new HttpClient())
using (var content = new MultipartFormDataContent())
{
    client.Timeout = TimeSpan.FromSeconds(60);

    if (!string.IsNullOrEmpty(apiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

    content.Add(new ByteArrayContent(File.ReadAllBytes(pdfFilePath)), "file", Path.GetFileName(pdfFilePath));
    content.Add(new ByteArrayContent(File.ReadAllBytes(xmlFilePath)), "xmlFile", Path.GetFileName(xmlFilePath));
    content.Add(new StringContent(profile), "profile");

    var response = client.PostAsync(apiUrl, content).GetAwaiter().GetResult();

    if (!response.IsSuccessStatusCode)
    {
        string error = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        throw new Exception(string.Format("API error {0}: {1}", (int)response.StatusCode, error));
    }

    // ... Datei speichern, ConversionResult zurückgeben
}
```

---

## Registrierung in Windows

### Schritt 1: Kompilieren
```
msbuild ZugferdNavision.csproj /p:Configuration=Release /p:Platform=x86
```

### Schritt 2: Als Administrator registrieren
```cmd
:: 32-bit .NET Framework (für NAV 2017 typisch)
C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe ^
    "C:\Pfad\zur\ZugferdNavision.dll" /codebase /tlb

:: Falls 64-bit NAV:
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm.exe ^
    "C:\Pfad\zur\ZugferdNavision.dll" /codebase /tlb
```

### Schritt 3: Verifizieren
```cmd
:: ProgId in Registry prüfen:
reg query "HKEY_CLASSES_ROOT\ZugferdNavision.ZugferdConverter"
```

---

## Verwendung in NAV 2017 (C/AL Code)

### Variablen
| Name | DataType | Subtype |
|------|----------|---------|
| Converter | Automation | `ZugferdNavision.ZugferdConverter`.`ZugferdConverter` |
| Result | Automation | `ZugferdNavision.ZugferdConverter`.`ConversionResult` |

### C/AL Code
```pascal
CREATE(Converter);
Result := Converter.ConvertToZugferd(
    'https://api.example.com/convert',  // apiUrl
    'C:\Temp\invoice.pdf',              // pdfFilePath
    'C:\Temp\invoice.xml',              // xmlFilePath
    'BASIC',                            // profile
    '',                                 // apiKey (optional)
    'C:\Temp\Output'                    // outputDirectory (optional)
);
MESSAGE('Gespeichert unter: %1', Result.OutputPath);
```

---

## Häufige Fehler & Lösungen

| Fehler | Ursache | Lösung |
|--------|---------|--------|
| Typ erscheint nicht in Automation | Fehlende `[ProgId]` oder `[ClassInterface]` | Attribute hinzufügen, neu registrieren |
| "Class not registered" | `regasm` nicht als Admin ausgeführt | Als Administrator ausführen |
| "Cannot create ActiveX component" | Falsche Bitness (32/64) | DLL und regasm müssen gleiche Bitness haben |
| Methode nicht sichtbar in NAV | `[ClassInterface(ClassInterfaceType.None)]` gesetzt | Auf `AutoDual` ändern |
| "File not found" bei HttpClient | .NET Framework nicht installiert | Ziel-Framework prüfen |

---

## Zusammenfassung der Änderungen gegenüber Original

1. `[ProgId("ZugferdNavision.ZugferdConverter")]` hinzufügen
2. `[ClassInterface(ClassInterfaceType.AutoDual)]` hinzufügen
3. `ConversionResult`-Klasse mit `[ComVisible(true)]`, `[Guid(...)]`, `[ClassInterface(ClassInterfaceType.AutoDual)]` dekorieren
4. `AssemblyInfo.cs` mit `[assembly: ComVisible(true)]` erstellen
5. Projekt auf `net472` und `x86` umstellen
6. Mit `regasm /codebase /tlb` als Administrator registrieren
