# Calling ZugferdNavision.Converter from a Navision 2017 CodeUnit

## Prerequisites

The DLL must be registered on the Navision server before these variables work:

```
C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe /codebase /tlb C:\NavAddins\ZugferdNavision.dll
```

> The output file is `ZugferdNavision.dll` (not `ZugferdNavision.Converter.dll`).
> Use the PowerShell helper for a one-step build + register: `scripts\Register-ZugferdNavision.ps1`

---

## Variable declarations

Open the CodeUnit, press **F9** (or use the **View > C/AL Locals** menu), and declare:

| Name         | DataType   | SubType                                    | Length |
|--------------|------------|--------------------------------------------|--------|
| `Converter`  | Automation | `'ZugferdNavision'.ZugferdConverter`       |        |
| `Result`     | Automation | `'ZugferdNavision'.ConversionResult`       |        |
| `ApiUrl`     | Text       |                                            | 250    |
| `TempFolder` | Text       |                                            | 250    |
| `PdfPath`    | Text       |                                            | 250    |
| `XmlPath`    | Text       |                                            | 250    |

> The SubType string must match the **ProgID** that RegAsm registers.
> Format: `'<AssemblyTitle>'.<ClassName>` — both come from `AssemblyInfo.cs` and the class name.

---

## C/AL code

```pascal
// ---------------------------------------------------------------
// 1. Create a unique temp folder for this invoice
//    Every conversion gets its own folder — no cross-invoice
//    file conflicts even when multiple users run concurrently.
// ---------------------------------------------------------------
ApiUrl     := 'https://your-api-host/convert';
TempFolder := 'C:\Temp\zugferd_' + DELCHR(FORMAT(CREATEGUID()), '=', '{}');
PdfPath    := TempFolder + '\invoice.pdf';
XmlPath    := TempFolder + '\invoice.xml';
SHELL('cmd /c mkdir "' + TempFolder + '"');

// ---------------------------------------------------------------
// 2. Export the invoice to disk (adapt to your report/codeunit)
// ---------------------------------------------------------------
// REPORT.SAVEASPDF(Report::"Sales Invoice", PdfPath, ...);
// XMLPort.EXPORT(XMLport::"ZUGFeRD Invoice", XmlPath, ...);

// ---------------------------------------------------------------
// 3. Call the DLL — wrap in error handling so failures surface
//    as a readable Navision dialog instead of a hard crash
// ---------------------------------------------------------------
IF NOT TRY_ConvertToZugferd(Converter, Result, ApiUrl, PdfPath, XmlPath, TempFolder) THEN BEGIN
  ERROR('ZUGFeRD conversion failed: %1', GETLASTERRORTEXT);
  EXIT;
END;

// ---------------------------------------------------------------
// 4. Check validation warnings (200 OK is still returned even
//    when warnings are present — decide whether to block or log)
// ---------------------------------------------------------------
IF Result.HasXmlErrors THEN
  MESSAGE('XML validation warnings:\n%1', Result.XmlValidationErrors);

IF Result.HasPdfErrors THEN
  MESSAGE('PDF validation warnings:\n%1', Result.PdfValidationErrors);

IF Result.HasPdfA3Errors THEN
  MESSAGE('PDF/A-3 validation warnings:\n%1', Result.PdfA3ValidationErrors);

// ---------------------------------------------------------------
// 5. Use the converted file (attach, email, archive, etc.)
// ---------------------------------------------------------------
MESSAGE('ZUGFeRD PDF saved to: %1', Result.OutputPath);
// e.g. DOWNLOAD(Result.OutputPath, '', '', '', LocalFileName);

// ---------------------------------------------------------------
// 6. Clean up — delete the entire folder (input + output)
// ---------------------------------------------------------------
CLEAR(Converter);
SHELL('cmd /c rmdir /s /q "' + TempFolder + '"');
```

---

## TRY function wrapper

Navision 2017 does not support inline `TRY` blocks for COM calls.
Add a separate local function `TRY_ConvertToZugferd` marked as **TryFunction**:

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
  // Arguments: apiUrl, pdfPath, xmlPath, profile, apiKey, outputDirectory
  Result := Converter.ConvertToZugferd(ApiUrl, PdfPath, XmlPath, 'BASIC', '', TempFolder);
  // The converted PDF is written into TempFolder — same folder as the inputs.
  // Pass an API key as the 5th argument instead of '' if the endpoint requires one.
END;
```

> Mark the function as **TryFunction** in the **Properties** pane (F4) so that
> any exception thrown by the DLL is caught and turned into a boolean return value
> rather than aborting the codeunit.

---

## Passing an API key

If the API endpoint requires authentication, pass the key as the fifth argument:

```pascal
Result := Converter.ConvertToZugferd(ApiUrl, PdfPath, XmlPath, 'BASIC', 'your-api-key-here');
```

The DLL adds it as an `X-Api-Key` request header automatically.

---

## Choosing a ZUGFeRD profile

The fourth argument selects the conformance level.
Valid values are `'MINIMUM'`, `'BASIC WL'`, `'BASIC'`, `'EN16931'`, `'EXTENDED'`, and `'XRECHNUNG'`:

```pascal
Result := Converter.ConvertToZugferd(ApiUrl, PdfPath, XmlPath, 'EXTENDED', '');
```

An empty string or omitted value defaults to `'BASIC'`. Any other value raises a COM exception
with a message listing the valid profiles.

---

## Adjusting the HTTP timeout

For large PDF files the default 60-second timeout may be too short.
Set `TimeoutSeconds` before calling `ConvertToZugferd`:

```pascal
CREATE(Converter);
Converter.TimeoutSeconds := 120;   // 2-minute timeout
Result := Converter.ConvertToZugferd(ApiUrl, PdfPath, XmlPath, 'BASIC', '', TempFolder);
```

---

## Registering with a specific server path

If multiple DLL versions exist on the server, pin the exact path in the CodeUnit
variable SubType by using the full ProgID including version, or ensure only one
version is registered under `HKEY_CLASSES_ROOT\ZugferdNavision.ZugferdConverter`.
