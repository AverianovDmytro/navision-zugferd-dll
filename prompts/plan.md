# Implementation Plan: ZUGFeRD DLL for Microsoft Navision

## Gaps identified in the requirements

Before planning the work, here are the issues found in the current requirements that the implementation must resolve:

| # | Gap | Impact |
|---|-----|--------|
| 1 | `profile` is hardcoded to `"BASIC"` | Navision cannot request `COMFORT` or `EXTENDED` |
| 2 | Output path uses a fixed `_zugferd` suffix | Concurrent calls for the same invoice will overwrite each other |
| 3 | `FileNotFoundException` is never explicitly thrown | `ReadAllBytes` will throw with a cryptic message instead of a clear one |
| 4 | `IEnumerable<string>` is used without `using System.Collections.Generic` | Will not compile |
| 5 | `ZugferdConverter` method is `static` | COM clients cannot call static methods; the class needs an instance method |
| 6 | Platform target not specified | Classic Navision is 32-bit; building `Any CPU` may cause a load failure |
| 7 | No assembly-level `[ComVisible(true)]`, GUID, or strong name | COM registration will be incomplete or unreliable |
| 8 | No authentication/API key support | Will fail against any protected API endpoint |
| 9 | No test project | No way to verify behaviour without a live Navision instance |
| 10 | No Navision C/AL code sample | Integration step is described but not demonstrated |

---

## Phase 1 — Project Scaffold

**Goal**: Create a compilable, correctly structured Visual Studio solution.

### Tasks

1. **Create the solution**
   - Solution name: `ZugferdNavision`
   - Project name: `ZugferdNavision.Converter` (Class Library, .NET Framework 4.6)
   - Second project: `ZugferdNavision.Tests` (Unit Test Project, .NET Framework 4.6, MSTest or NUnit)

2. **Configure the main project**
   - Target framework: `.NET Framework 4.6`
   - Platform target: **x86** (matches 32-bit Navision process)
   - Output type: Class Library
   - Assembly name: `ZugferdNavision.Converter`
   - Root namespace: `ZugferdNavision`

3. **Set assembly metadata** (`Properties/AssemblyInfo.cs`)
   ```csharp
   [assembly: ComVisible(true)]
   [assembly: Guid("/* generate a new GUID */")]
   [assembly: AssemblyVersion("1.0.0.0")]
   ```

4. **Add NuGet references**
   - `System.Net.Http` (if not already in the GAC for .NET 4.6)
   - No other third-party dependencies — keep the DLL self-contained

5. **File layout**
   ```
   ZugferdNavision/
   ├── ZugferdNavision.sln
   ├── src/
   │   └── ZugferdNavision.Converter/
   │       ├── ZugferdNavision.Converter.csproj
   │       ├── Properties/AssemblyInfo.cs
   │       ├── ConversionResult.cs
   │       └── ZugferdConverter.cs
   └── tests/
       └── ZugferdNavision.Tests/
           ├── ZugferdNavision.Tests.csproj
           └── ZugferdConverterTests.cs
   ```

---

## Phase 2 — Core Implementation

**Goal**: Implement the two public classes exactly as specified, with all gaps fixed.

### Task 2.1 — `ConversionResult.cs`

```csharp
using System.Runtime.InteropServices;

namespace ZugferdNavision
{
    [ComVisible(true)]
    [Guid("/* generate a new GUID */")]
    public class ConversionResult
    {
        public string OutputPath { get; set; }
        public string XmlValidationErrors { get; set; }
        public string PdfValidationErrors { get; set; }
        public string PdfA3ValidationErrors { get; set; }
        public bool HasXmlErrors   => !string.IsNullOrEmpty(XmlValidationErrors);
        public bool HasPdfErrors   => !string.IsNullOrEmpty(PdfValidationErrors);
        public bool HasPdfA3Errors => !string.IsNullOrEmpty(PdfA3ValidationErrors);
    }
}
```

### Task 2.2 — `ZugferdConverter.cs`

Fix all gaps from Phase 0 and implement the full method:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace ZugferdNavision
{
    [ComVisible(true)]
    [Guid("/* generate a new GUID */")]
    public class ZugferdConverter
    {
        // Instance method — COM clients cannot call static methods
        public ConversionResult ConvertToZugferd(
            string apiUrl,
            string pdfFilePath,
            string xmlFilePath,
            string profile = "BASIC")
        {
            if (!File.Exists(pdfFilePath))
                throw new FileNotFoundException("PDF file not found", pdfFilePath);
            if (!File.Exists(xmlFilePath))
                throw new FileNotFoundException("XML file not found", xmlFilePath);

            byte[] pdfBytes = File.ReadAllBytes(pdfFilePath);
            byte[] xmlBytes = File.ReadAllBytes(xmlFilePath);

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
            using (var content = new MultipartFormDataContent())
            {
                content.Add(new ByteArrayContent(pdfBytes), "file",    Path.GetFileName(pdfFilePath));
                content.Add(new ByteArrayContent(xmlBytes), "xmlFile", Path.GetFileName(xmlFilePath));
                content.Add(new StringContent(profile),    "profile");

                HttpResponseMessage response = client
                    .PostAsync(apiUrl, content)
                    .GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    string error = response.Content
                        .ReadAsStringAsync().GetAwaiter().GetResult();
                    throw new Exception(
                        $"API error {(int)response.StatusCode}: {error}");
                }

                byte[] resultPdf = response.Content
                    .ReadAsByteArrayAsync().GetAwaiter().GetResult();

                // Unique output path — avoids collision on concurrent calls
                string outputPath = Path.Combine(
                    Path.GetTempPath(),
                    $"{Path.GetFileNameWithoutExtension(pdfFilePath)}_zugferd_{Guid.NewGuid():N}.pdf"
                );
                File.WriteAllBytes(outputPath, resultPdf);

                return new ConversionResult
                {
                    OutputPath            = outputPath,
                    XmlValidationErrors   = GetHeader(response, "X-XML-Validation-Errors"),
                    PdfValidationErrors   = GetHeader(response, "X-PDF-Validation-Errors"),
                    PdfA3ValidationErrors = GetHeader(response, "X-PDFA3-Validation-Errors")
                };
            }
        }

        private static string GetHeader(HttpResponseMessage response, string name)
        {
            IEnumerable<string> values;
            return response.Headers.TryGetValues(name, out values)
                ? string.Join("", values)
                : null;
        }
    }
}
```

**Changes from the draft in requirements.md:**
- `profile` promoted to a method parameter with default `"BASIC"`
- Explicit `File.Exists` guards with clear `FileNotFoundException` messages
- Instance method instead of `static` (required for COM)
- `Guid.NewGuid()` in output path prevents overwrite on concurrent calls
- `using System.Collections.Generic` added

---

## Phase 3 — Authentication Support

**Goal**: Allow callers to pass an API key or Bearer token for protected endpoints.

Add an optional `apiKey` parameter:

```csharp
public ConversionResult ConvertToZugferd(
    string apiUrl,
    string pdfFilePath,
    string xmlFilePath,
    string profile = "BASIC",
    string apiKey  = null)
```

Inside the method, before `PostAsync`:

```csharp
if (!string.IsNullOrEmpty(apiKey))
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
```

If Bearer tokens are needed instead, use:

```csharp
client.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
```

> Confirm the API's authentication scheme before choosing between these two.

---

## Phase 4 — Testing

**Goal**: Verify all behaviour without a live Navision instance or production API.

### Test cases to implement in `ZugferdConverterTests.cs`

| # | Scenario | Method | How to test |
|---|----------|--------|-------------|
| 1 | PDF file not found | `ConvertToZugferd` | Pass a non-existent path, assert `FileNotFoundException` |
| 2 | XML file not found | `ConvertToZugferd` | Pass a non-existent XML path, assert `FileNotFoundException` |
| 3 | API returns 400 | `ConvertToZugferd` | Mock `HttpClient`, return 400, assert `Exception` with status code |
| 4 | API returns 429 | `ConvertToZugferd` | Mock `HttpClient`, return 429, assert `Exception` |
| 5 | API returns 500 | `ConvertToZugferd` | Mock `HttpClient`, return 500, assert `Exception` |
| 6 | Successful conversion, no validation errors | `ConvertToZugferd` | Mock 200 with PDF body, assert `OutputPath` exists, all error fields null |
| 7 | Successful conversion, all three validation headers present | `ConvertToZugferd` | Mock 200 with headers set, assert all three error fields non-null |
| 8 | Concurrent calls for same input file | `ConvertToZugferd` | Call twice in parallel, assert output paths are different |
| 9 | `HasXmlErrors` / `HasPdfErrors` / `HasPdfA3Errors` flags | `ConversionResult` | Unit test the bool properties directly |

**Mocking `HttpClient`**: Use `HttpMessageHandler` subclass or install `RichardSzalay.MockHttp` NuGet package.

### Integration smoke test (manual)

1. Point `apiUrl` at the real conversion service.
2. Supply a real invoice PDF and a valid ZUGFeRD XML file.
3. Assert `OutputPath` file exists, is non-empty, and opens as a valid PDF.
4. Optionally run a PDF/A-3 validator against the output.

---

## Phase 5 — Build & COM Registration

**Goal**: Produce a deployable DLL and register it on the Navision server.

### Build steps

1. Build in **Release | x86** configuration.
2. Verify output: `bin\x86\Release\ZugferdNavision.Converter.dll`
3. Sign the assembly with a strong name key (`sn -k ZugferdNavision.snk`) if the Navision server requires signed COM components.

### Deployment checklist

```
[ ] Copy ZugferdNavision.Converter.dll to the server (e.g. C:\NavAddins\)
[ ] Copy System.Net.Http.dll to the same folder if not present in the GAC
[ ] Open an elevated command prompt on the server
[ ] Run: C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe
         /codebase C:\NavAddins\ZugferdNavision.Converter.dll
[ ] Verify: RegAsm reports "Types registered successfully"
[ ] Confirm GUID appears in HKEY_CLASSES_ROOT\CLSID in the registry
```

> Use the 32-bit `Framework\v4.0.30319` path (not `Framework64`) to match Navision's process.

---

## Phase 6 — Navision C/AL Integration

**Goal**: Provide a ready-to-paste C/AL code block.

### Variables

| Name | Type | SubType |
|------|------|---------|
| `Converter` | Automation | `'ZugferdNavision'.ZugferdConverter` |
| `Result` | Automation | `'ZugferdNavision'.ConversionResult` |
| `PdfPath` | Text | |
| `XmlPath` | Text | |
| `ApiUrl` | Text | |

### C/AL code

```pascal
ApiUrl  := 'https://your-api-host/convert';
PdfPath := 'C:\Temp\invoice.pdf';
XmlPath := 'C:\Temp\invoice.xml';

CREATE(Converter);
Result := Converter.ConvertToZugferd(ApiUrl, PdfPath, XmlPath, 'BASIC', '');

IF Result.HasXmlErrors THEN
  MESSAGE('XML validation warnings: %1', Result.XmlValidationErrors);

IF Result.HasPdfErrors THEN
  MESSAGE('PDF validation warnings: %1', Result.PdfValidationErrors);

IF Result.HasPdfA3Errors THEN
  MESSAGE('PDF/A-3 validation warnings: %1', Result.PdfA3ValidationErrors);

// Result.OutputPath now holds the ZUGFeRD PDF — attach or send as needed
MESSAGE('ZUGFeRD PDF saved to: %1', Result.OutputPath);

CLEAR(Converter);

// Clean up temp files
ERASE(PdfPath);
ERASE(XmlPath);
```

> Wrap the entire block in error handling (`IF NOT TRY ... THEN ERROR(GETLASTERRORTEXT)`) so API failures surface a readable message in Navision.

---

## Delivery Checklist

```
[ ] Phase 1: Solution and projects created, platform target x86, AssemblyInfo configured
[ ] Phase 2: ConversionResult.cs and ZugferdConverter.cs implemented and compile clean
[ ] Phase 3: apiKey parameter added and documented
[ ] Phase 4: All 9 unit tests passing; manual smoke test against real API completed
[ ] Phase 5: DLL built in Release|x86, deployed and registered on Navision server
[ ] Phase 6: C/AL code tested end-to-end against a real invoice in Navision
```
