# Developer Guide: ZUGFeRD DLL for Microsoft Navision

## Overview

Build a .NET 4.6 class library (DLL) that Microsoft Navision can call to convert a standard PDF invoice into a ZUGFeRD-compliant PDF/A-3 file. Navision generates the PDF and XML invoice data locally, then delegates the conversion to this DLL, which calls the external REST API and returns the output file path along with any validation warnings.

---

## Background and Constraints

- **Navision version**: Classic Microsoft Navision running on .NET 4.6.
- **Problem**: Navision cannot make multipart HTTP requests natively (no stream library access).
- **Solution**: A thin DLL wrapper that reads two local files, POSTs them to the conversion API, saves the response, and returns the output path.

---

## Step 1 — Define the Public API of the DLL

The DLL exposes a result class and a single static method, both COM-visible:

### `ConversionResult` class

```csharp
[ComVisible(true)]
public class ConversionResult
{
    public string OutputPath { get; set; }             // Absolute path to the saved ZUGFeRD PDF
    public string XmlValidationErrors { get; set; }    // Raw JSON array string, or null if no issues
    public string PdfValidationErrors { get; set; }    // Raw JSON array string, or null if no issues
    public string PdfA3ValidationErrors { get; set; }  // Raw JSON array string, or null if no issues
    public bool HasXmlErrors   => !string.IsNullOrEmpty(XmlValidationErrors);
    public bool HasPdfErrors   => !string.IsNullOrEmpty(PdfValidationErrors);
    public bool HasPdfA3Errors => !string.IsNullOrEmpty(PdfA3ValidationErrors);
}
```

### `ConvertToZugferd` method

```csharp
public static ConversionResult ConvertToZugferd(string apiUrl, string pdfFilePath, string xmlFilePath)
```

| Parameter     | Type   | Description                                   |
|---------------|--------|-----------------------------------------------|
| `apiUrl`      | string | Full URL of the conversion endpoint (POST)    |
| `pdfFilePath` | string | Absolute path to the invoice PDF on disk      |
| `xmlFilePath` | string | Absolute path to the ZUGFeRD XML file on disk |

**Return value**: A `ConversionResult` with the output path and any validation warnings.  
**On error**: Throw a descriptive exception (Navision will surface the message to the user).

---

## Step 2 — Understand the REST API Contract

See `openapi/convert.yaml` for the full spec. Summary:

- **Method**: `POST`
- **Content-Type**: `multipart/form-data`
- **Fields**:
  | Field     | Type   | Required | Description                              |
  |-----------|--------|----------|------------------------------------------|
  | `file`    | binary | Yes      | The source PDF file                      |
  | `xmlFile` | binary | No       | The ZUGFeRD XML to embed                 |
  | `profile` | string | No       | Conformance level: `BASIC`, `COMFORT`, or `EXTENDED` (default: `BASIC`) |
- **Success response**: `200 OK`
  - Body is always the converted PDF binary (`application/pdf`)
  - Validation results are returned as **response headers** (the body is always present regardless):

  | Header                      | Present when                                | Content                         |
  |-----------------------------|---------------------------------------------|---------------------------------|
  | `X-XML-Validation-Errors`   | XML file has XSD validation issues          | JSON array of `ValidationError` |
  | `X-PDF-Validation-Errors`   | Converted PDF has PDF/A-3 compliance issues | JSON array of `ValidationError` |
  | `X-PDFA3-Validation-Errors` | PDF/A-3 specific conformance issues         | JSON array of `ValidationError` |

- **Error responses**: `400` / `500` — JSON body with an `ErrorResponse` object; `429` — rate limit exceeded

---

## Step 3 — Implement the DLL

### 3.1 Project setup

1. Create a new **Class Library (.NET Framework 4.6)** project in Visual Studio.
2. Target framework: `.NET Framework 4.6`.
3. Add a reference to `System.Net.Http` (available in .NET 4.6).
4. If Navision calls the DLL via COM, decorate the assembly and class with `[ComVisible(true)]`.

### 3.2 Core implementation outline

```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;

[ComVisible(true)]
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

[ComVisible(true)]
public class ZugferdConverter
{
    public static ConversionResult ConvertToZugferd(string apiUrl, string pdfFilePath, string xmlFilePath)
    {
        // 1. Read both files from disk
        byte[] pdfBytes = File.ReadAllBytes(pdfFilePath);
        byte[] xmlBytes = File.ReadAllBytes(xmlFilePath);

        // 2. Build multipart/form-data request
        using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
        using (var content = new MultipartFormDataContent())
        {
            content.Add(new ByteArrayContent(pdfBytes), "file", Path.GetFileName(pdfFilePath));
            content.Add(new ByteArrayContent(xmlBytes), "xmlFile", Path.GetFileName(xmlFilePath));
            content.Add(new StringContent("BASIC"), "profile");

            // 3. POST to the API
            HttpResponseMessage response = client.PostAsync(apiUrl, content).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                string error = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                throw new Exception($"API error {(int)response.StatusCode}: {error}");
            }

            // 4. Save the returned PDF
            byte[] resultPdf = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            string outputPath = Path.Combine(
                Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(pdfFilePath) + "_zugferd.pdf"
            );
            File.WriteAllBytes(outputPath, resultPdf);

            // 5. Extract validation headers (present only when issues were found)
            string xmlErrors   = GetHeader(response, "X-XML-Validation-Errors");
            string pdfErrors   = GetHeader(response, "X-PDF-Validation-Errors");
            string pdfA3Errors = GetHeader(response, "X-PDFA3-Validation-Errors");

            return new ConversionResult
            {
                OutputPath           = outputPath,
                XmlValidationErrors  = xmlErrors,
                PdfValidationErrors  = pdfErrors,
                PdfA3ValidationErrors = pdfA3Errors
            };
        }
    }

    private static string GetHeader(HttpResponseMessage response, string name)
    {
        IEnumerable<string> values;
        if (response.Headers.TryGetValues(name, out values))
            return string.Join("", values);
        return null;
    }
}
```

### 3.3 Key implementation notes

- Use `.GetAwaiter().GetResult()` to run async calls synchronously — Navision C/AL cannot await.
- `HttpClient.Timeout` is set to 60 seconds; adjust if large files cause timeouts.
- Do not reuse a static `HttpClient` across calls unless you verify thread-safety with Navision's call pattern.
- The output file name must not collide with the input; a `_zugferd` suffix is sufficient.
- Validation headers are absent (not empty) when there are no issues — `TryGetValues` correctly returns `null` in that case.
- The body PDF is always saved regardless of whether validation headers are present.

---

## Step 4 — Error Handling

Handle and surface these cases explicitly:

| Scenario                   | Action                                                  |
|----------------------------|---------------------------------------------------------|
| Input file not found       | Throw `FileNotFoundException` with the missing path     |
| API returns `400`          | Throw with the JSON error body included in the message  |
| API returns `429`          | Throw with a "rate limit exceeded, retry later" message |
| API returns `500`          | Throw with the JSON error body                          |
| Network timeout            | Let `HttpClient` timeout propagate naturally            |
| Output directory not found | Use `Path.GetTempPath()` — always writable              |

---

## Step 5 — Build and Deploy

1. Build the project in **Release** mode targeting **Any CPU**.
2. Copy the output DLL (and its dependencies, especially `System.Net.Http.dll` if not in the GAC) to the Navision server's accessible path.
3. Register the DLL with Navision:
   - If using COM: run `regasm /codebase ZugferdConverter.dll` on the server.
   - If using direct .NET interop: reference the DLL path in the Navision automation variable.

---

## Step 6 — Navision Integration

In Navision C/AL, the call pattern is:

1. Export the invoice as `C:\Temp\invoice.pdf` and `C:\Temp\invoice.xml`.
2. Create an Automation variable for `ZugferdConverter` and a second one for `ConversionResult`.
3. Call `ConvertToZugferd(apiUrl, 'C:\Temp\invoice.pdf', 'C:\Temp\invoice.xml')` and assign the result.
4. Read `Result.OutputPath` to get the converted file.
5. Check `Result.HasXmlErrors`, `Result.HasPdfErrors`, and `Result.HasPdfA3Errors`; if true, read the JSON string from the corresponding property and log or display it as a warning.
6. Attach or send the ZUGFeRD PDF as needed.
7. Delete the temp input files after the call completes.

> **Note**: Validation errors on a `200 OK` are warnings — the converted file is still returned and usable. It is up to the Navision code to decide whether to block the flow or only log them.

---

## Quick Reference

| Item                     | Value                                         |
|--------------------------|-----------------------------------------------|
| Target framework         | .NET Framework 4.6                            |
| HTTP method              | POST multipart/form-data                      |
| API spec                 | `openapi/convert.yaml`                        |
| Input files              | PDF invoice + ZUGFeRD XML                     |
| Return type              | `ConversionResult` (COM-visible class)        |
| `OutputPath`             | Absolute path to ZUGFeRD PDF/A-3 file         |
| `XmlValidationErrors`    | JSON array string, or `null` if none          |
| `PdfValidationErrors`    | JSON array string, or `null` if none          |
| `PdfA3ValidationErrors`  | JSON array string, or `null` if none          |
| Default profile          | `BASIC`                                       |
| Temp directory           | `Path.GetTempPath()` (e.g., `C:\Temp\`)       |
