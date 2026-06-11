# Task List: ZUGFeRD DLL for Microsoft Navision

## Phase 1 — Project Scaffold

- [x] 1.1 Create `ZugferdNavision` solution with two projects:
  - [x] 1.1.1 `ZugferdNavision.Converter` — Class Library, .NET Framework 4.6
  - [x] 1.1.2 `ZugferdNavision.Tests` — Unit Test Project, .NET Framework 4.6
- [x] 1.2 Set `ZugferdNavision.Converter` platform target to **x86**
- [x] 1.3 Set assembly name to `ZugferdNavision.Converter` and root namespace to `ZugferdNavision`
- [x] 1.4 Configure `Properties/AssemblyInfo.cs`:
  - [x] 1.4.1 Add `[assembly: ComVisible(true)]`
  - [x] 1.4.2 Generate and set `[assembly: Guid("4F52A864-903F-4208-8C85-F052A03DEB95")]`
  - [x] 1.4.3 Set `[assembly: AssemblyVersion("1.0.0.0")]`
- [x] 1.5 Add `System.Net.Http` reference to `ZugferdNavision.Converter`
- [x] 1.6 Create the folder layout:
  - [x] 1.6.1 `src/ZugferdNavision.Converter/` with `ConversionResult.cs` and `ZugferdConverter.cs`
  - [x] 1.6.2 `tests/ZugferdNavision.Tests/` with `ZugferdConverterTests.cs`

---

## Phase 2 — Core Implementation

- [x] 2.1 Implement `ConversionResult.cs`:
  - [x] 2.1.1 Add `[ComVisible(true)]` and `[Guid("5D6258F6-7C4B-4F62-9D7A-4143C1D5211A")]` attributes
  - [x] 2.1.2 Add `OutputPath` string property
  - [x] 2.1.3 Add `XmlValidationErrors` string property
  - [x] 2.1.4 Add `PdfValidationErrors` string property
  - [x] 2.1.5 Add `PdfA3ValidationErrors` string property
  - [x] 2.1.6 Add `HasXmlErrors` bool property
  - [x] 2.1.7 Add `HasPdfErrors` bool property
  - [x] 2.1.8 Add `HasPdfA3Errors` bool property
- [x] 2.2 Implement `ZugferdConverter.cs`:
  - [x] 2.2.1 Add `[ComVisible(true)]` and `[Guid("66D83865-A815-47C5-8376-568369E8B428")]` attributes
  - [x] 2.2.2 Implement `ConvertToZugferd` as an **instance method** (not static)
  - [x] 2.2.3 Add `profile` parameter with default value `"BASIC"`
  - [x] 2.2.4 Add explicit `File.Exists` guard for `pdfFilePath` — throw `FileNotFoundException`
  - [x] 2.2.5 Add explicit `File.Exists` guard for `xmlFilePath` — throw `FileNotFoundException`
  - [x] 2.2.6 Read both input files with `File.ReadAllBytes`
  - [x] 2.2.7 Build `MultipartFormDataContent` with `file`, `xmlFile`, and `profile` fields
  - [x] 2.2.8 Set `HttpClient.Timeout` to 60 seconds
  - [x] 2.2.9 Call `PostAsync` synchronously via `.GetAwaiter().GetResult()`
  - [x] 2.2.10 On non-2xx response: read body and throw `Exception` with status code and message
  - [x] 2.2.11 Save response PDF to a unique path using `Guid.NewGuid()` suffix (prevents collision)
  - [x] 2.2.12 Implement private `GetHeader` helper using `TryGetValues`, returning `null` when absent
  - [x] 2.2.13 Read `X-XML-Validation-Errors` header into `ConversionResult`
  - [x] 2.2.14 Read `X-PDF-Validation-Errors` header into `ConversionResult`
  - [x] 2.2.15 Read `X-PDFA3-Validation-Errors` header into `ConversionResult`
  - [x] 2.2.16 Add `using System.Collections.Generic` (required for `IEnumerable<string>`)
- [ ] 2.3 Verify the project compiles clean in Release|x86 with no warnings
      *(requires .NET SDK or Visual Studio — run `dotnet build` or build in VS)*

---

## Phase 3 — Authentication Support

- [x] 3.1 Add optional `apiKey` parameter to `ConvertToZugferd` (default `null`)
- [x] 3.2 When `apiKey` is non-empty, add it to the request as `X-Api-Key` header
- [ ] 3.3 Confirm with API owner whether Bearer token or `X-Api-Key` header is required and adjust accordingly
- [ ] 3.4 Verify the project still compiles clean after the parameter addition
      *(requires .NET SDK or Visual Studio)*

---

## Phase 4 — Testing

- [x] 4.1 Write unit test: PDF file not found → `FileNotFoundException`
- [x] 4.2 Write unit test: XML file not found → `FileNotFoundException`
- [x] 4.3 Write unit test: API returns 400 → `Exception` containing status code
- [x] 4.4 Write unit test: API returns 429 → `Exception`
- [x] 4.5 Write unit test: API returns 500 → `Exception`
- [x] 4.6 Write unit test: 200 OK, no validation headers → all error fields `null`, `OutputPath` file exists
- [x] 4.7 Write unit test: 200 OK, all three validation headers present → all three error fields non-null
- [x] 4.8 Write unit test: two concurrent calls for the same input → output paths are different
- [x] 4.9 Write unit tests for `ConversionResult` bool properties (`HasXmlErrors`, `HasPdfErrors`, `HasPdfA3Errors`)
- [x] 4.10 Set up `HttpClient` mocking (`MockHttpHandler` — custom `HttpMessageHandler` subclass, no NuGet needed)
- [ ] 4.11 Run all unit tests — confirm all pass
      *(requires .NET SDK — run `dotnet test` or use VS Test Explorer)*
- [ ] 4.12 Run manual integration smoke test against real API:
  - [ ] 4.12.1 Supply a real invoice PDF and valid ZUGFeRD XML
  - [ ] 4.12.2 Assert `OutputPath` file exists and is non-empty
  - [ ] 4.12.3 Open the output file and confirm it is a valid PDF
  - [ ] 4.12.4 Optionally run a PDF/A-3 validator against the output

---

## Phase 5 — Build & COM Registration

- [ ] 5.1 Build the solution in **Release | x86** configuration
- [ ] 5.2 Confirm output at `bin\x86\Release\ZugferdNavision.Converter.dll`
- [ ] 5.3 Generate a strong name key (`sn -k ZugferdNavision.snk`) and sign the assembly if required
- [ ] 5.4 Copy `ZugferdNavision.Converter.dll` to the Navision server (e.g. `C:\NavAddins\`)
- [ ] 5.5 Copy `System.Net.Http.dll` to the same folder if not present in the server's GAC
- [ ] 5.6 On the server, run RegAsm using the 32-bit framework path:
      `C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe /codebase C:\NavAddins\ZugferdNavision.Converter.dll`
- [ ] 5.7 Confirm RegAsm output: "Types registered successfully"
- [ ] 5.8 Confirm the assembly GUID appears under `HKEY_CLASSES_ROOT\CLSID` in the registry

---

## Phase 6 — Navision C/AL Integration

- [ ] 6.1 Declare Automation variable `Converter` of SubType `'ZugferdNavision'.ZugferdConverter`
- [ ] 6.2 Declare Automation variable `Result` of SubType `'ZugferdNavision'.ConversionResult`
- [ ] 6.3 Declare Text variables: `ApiUrl`, `PdfPath`, `XmlPath`
- [ ] 6.4 Add C/AL code to export the invoice PDF to `C:\Temp\invoice.pdf`
- [ ] 6.5 Add C/AL code to export the invoice XML to `C:\Temp\invoice.xml`
- [ ] 6.6 Add C/AL code to call `Converter.ConvertToZugferd(ApiUrl, PdfPath, XmlPath, 'BASIC', '')`
- [ ] 6.7 Add C/AL code to check `Result.HasXmlErrors` and display/log `Result.XmlValidationErrors`
- [ ] 6.8 Add C/AL code to check `Result.HasPdfErrors` and display/log `Result.PdfValidationErrors`
- [ ] 6.9 Add C/AL code to check `Result.HasPdfA3Errors` and display/log `Result.PdfA3ValidationErrors`
- [ ] 6.10 Add C/AL code to use `Result.OutputPath` (attach or send the ZUGFeRD PDF)
- [ ] 6.11 Add error handling wrapper so API failures surface as a readable Navision message
- [ ] 6.12 Add `ERASE` calls to delete temp input files after the call completes
- [ ] 6.13 Test the end-to-end flow in Navision against a real invoice
