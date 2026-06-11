# Improvement Plan: ZugferdNavision.Converter DLL

> Analysis based on `prompts/requirements-draft-dll.md` vs. current implementation.
> Phases are ordered by priority. Each item names the exact file and change needed.

---

## Phase 1 — Critical COM Compliance (Must-Fix)

These gaps will prevent the DLL from working in NAV 2017 at all.

### 1.1 Add `[ProgId]` to `ZugferdConverter`

**File:** `src/ZugferdNavision.Converter/ZugferdConverter.cs:10`

The class currently has `[ComVisible(true)]` and `[Guid]` but is missing the `[ProgId]` attribute. Without it, NAV's Automation picker cannot locate the type by its well-known name.

```csharp
// Add after [Guid(...)]
[ProgId("ZugferdNavision.ZugferdConverter")]
```

NAV users reference this as `ZugferdNavision.ZugferdConverter` in Automation variables — the ProgId must match exactly.

---

### 1.2 Add `[ClassInterface(ClassInterfaceType.AutoDual)]` to `ZugferdConverter`

**File:** `src/ZugferdNavision.Converter/ZugferdConverter.cs:10`

Without `AutoDual`, COM clients (NAV C/AL) cannot see the public methods via IDispatch. This is the most common reason a method "doesn't show up" in NAV Automation.

```csharp
[ClassInterface(ClassInterfaceType.AutoDual)]
public class ZugferdConverter
```

---

### 1.3 Add `[ClassInterface(ClassInterfaceType.AutoDual)]` to `ConversionResult`

**File:** `src/ZugferdNavision.Converter/ConversionResult.cs:5`

Same issue as 1.2 — without `AutoDual`, NAV cannot access `OutputPath`, `HasXmlErrors`, etc. on the returned result object.

```csharp
[ClassInterface(ClassInterfaceType.AutoDual)]
public class ConversionResult
```

---

### 1.4 Upgrade Target Framework from `net46` to `net472`

**File:** `src/ZugferdNavision.Converter/ZugferdNavision.Converter.csproj:4`

Requirements specify .NET Framework **4.6.2 minimum**. `net46` is below that threshold and may be absent on target machines. `net472` is the recommended target: it includes all 4.6.2 APIs, ships with Windows 10 RS4+, and is the last version that installs as a standalone package on Server 2012 R2.

```xml
<TargetFramework>net472</TargetFramework>
```

Also update `tests/ZugferdNavision.Tests/ZugferdNavision.Tests.csproj` to match.

---

### 1.5 Add `RegisterForComInterop` to the project file

**File:** `src/ZugferdNavision.Converter/ZugferdNavision.Converter.csproj`

Allows Visual Studio to auto-register the DLL after each Release build, avoiding the manual `regasm` step during development.

```xml
<RegisterForComInterop>true</RegisterForComInterop>
```

> Note: This only runs when building as Administrator. Production deployments still use the manual `regasm` script (Phase 4).

---

## Phase 2 — Input Validation (Medium Priority)

### 2.1 Validate `apiUrl` parameter

**File:** `src/ZugferdNavision.Converter/ZugferdConverter.cs:32`

`apiUrl` is currently passed unchecked into `HttpClient.PostAsync`. A null or empty value throws an unhelpful `UriFormatException` deep in the stack. Add a guard:

```csharp
if (string.IsNullOrWhiteSpace(apiUrl))
    throw new ArgumentNullException("apiUrl", "API URL must not be empty.");
```

---

### 2.2 Validate `profile` parameter

**File:** `src/ZugferdNavision.Converter/ZugferdConverter.cs:30`

The `profile` parameter defaults to `"BASIC"` but is never validated. An incorrect value (e.g., a typo) is silently forwarded and the API returns an opaque 400. Add a whitelist check:

```csharp
private static readonly string[] ValidProfiles =
    { "MINIMUM", "BASIC WL", "BASIC", "EN16931", "EXTENDED", "XRECHNUNG" };

// In ConvertToZugferd, after null-check on apiUrl:
if (string.IsNullOrWhiteSpace(profile))
    profile = "BASIC";
else if (Array.IndexOf(ValidProfiles, profile.ToUpperInvariant()) < 0)
    throw new ArgumentException(
        string.Format("Unknown profile '{0}'. Valid values: {1}",
            profile, string.Join(", ", ValidProfiles)), "profile");
```

---

### 2.3 Auto-create output directory if missing

**File:** `src/ZugferdNavision.Converter/ZugferdConverter.cs:62`

If `outputDirectory` is provided but does not exist, `File.WriteAllBytes` throws a `DirectoryNotFoundException`. NAV passes paths that may not pre-exist. Auto-create instead:

```csharp
string outDir = !string.IsNullOrEmpty(outputDirectory)
    ? outputDirectory
    : Path.GetTempPath();

if (!Directory.Exists(outDir))
    Directory.CreateDirectory(outDir);
```

---

## Phase 3 — Configurability & Error Handling (Medium Priority)

### 3.1 Make HTTP timeout configurable via a COM-visible property

**File:** `src/ZugferdNavision.Converter/ZugferdConverter.cs`

Timeout is currently hardcoded to 60 seconds. Large PDF files can exceed this. A COM-visible property lets NAV callers adjust it without recompiling the DLL:

```csharp
[ComVisible(true)]
// ...
public class ZugferdConverter
{
    public int TimeoutSeconds { get; set; } = 60;
    // ...
    private HttpClient CreateClient(string apiKey)
    {
        // ...
        client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
    }
}
```

NAV usage: `Converter.TimeoutSeconds := 120;` before calling `ConvertToZugferd`.

---

### 3.2 Improve HTTP error exception message

**File:** `src/ZugferdNavision.Converter/ZugferdConverter.cs:55`

The current message is `API error 400: <body>`. Include the URL so operators can diagnose misconfiguration from NAV's error dialog:

```csharp
throw new Exception(
    string.Format("API error {0} calling {1}: {2}",
        (int)response.StatusCode, apiUrl, error));
```

---

### 3.3 Handle `TaskCanceledException` (timeout) explicitly

**File:** `src/ZugferdNavision.Converter/ZugferdConverter.cs` — wrap the `PostAsync` call

`HttpClient` throws `TaskCanceledException` (wrapped by `GetAwaiter().GetResult()` as `AggregateException`) on timeout. NAV will show "Aggregated exception" which is uninformative. Catch and rethrow:

```csharp
try
{
    response = client.PostAsync(apiUrl, content).GetAwaiter().GetResult();
}
catch (AggregateException ae) when (ae.InnerException is TaskCanceledException)
{
    throw new TimeoutException(
        string.Format("Request to {0} timed out after {1} seconds.",
            apiUrl, TimeoutSeconds));
}
```

---

## Phase 4 — Deployment Scripts (Medium Priority)

### 4.1 Create PowerShell registration script

**New file:** `scripts/Register-ZugferdNavision.ps1`

Automates build + COM registration in one step for deployers. Should:
- Accept `-DllPath` and `-Unregister` parameters
- Auto-detect 32-bit vs. 64-bit `regasm.exe` based on `-Platform` parameter
- Print success/failure clearly
- Check for Administrator elevation before running

```powershell
param(
    [string]$DllPath = ".\bin\Release\ZugferdNavision.Converter.dll",
    [string]$Platform = "x86",   # or "x64"
    [switch]$Unregister
)
# Elevation check, regasm path selection, registration/unregistration logic
```

---

### 4.2 Create CMD batch script as fallback

**New file:** `scripts\register.cmd`

For environments where PowerShell execution policy blocks `.ps1` files. Simpler than the PS script — just wraps the two `regasm` commands with an elevation check.

---

### 4.3 Document the output DLL name mismatch

**File:** `navision/snippet.md` or inline in the registration script

`AssemblyName` is currently `ZugferdNavision.Converter`, so the output file is `ZugferdNavision.Converter.dll` — not `ZugferdNavision.dll` as mentioned in the requirements draft. Either:

- **Option A (preferred):** Rename `AssemblyName` to `ZugferdNavision` in the `.csproj` so the output matches the spec exactly.
- **Option B:** Update all documentation to reference `ZugferdNavision.Converter.dll`.

**Recommendation:** Apply Option A. Change `<AssemblyName>ZugferdNavision.Converter</AssemblyName>` → `<AssemblyName>ZugferdNavision</AssemblyName>` in the `.csproj`. This produces `ZugferdNavision.dll`, matching the requirements document and making `regasm` commands cleaner.

---

## Phase 5 — Test Coverage Expansion (Lower Priority)

Current coverage: 9 tests across file-not-found, HTTP errors, success, concurrency, and bool properties. The following gaps remain:

### 5.1 Test: null/empty `apiUrl` throws `ArgumentNullException`

```csharp
[TestMethod]
[ExpectedException(typeof(ArgumentNullException))]
public void ConvertToZugferd_NullApiUrl_ThrowsArgumentNullException()
{
    var converter = new ZugferdConverter();
    converter.ConvertToZugferd(null, _tempPdf, _tempXml);
}
```

---

### 5.2 Test: invalid `profile` value throws `ArgumentException`

```csharp
[TestMethod]
[ExpectedException(typeof(ArgumentException))]
public void ConvertToZugferd_InvalidProfile_ThrowsArgumentException()
{
    var converter = new ZugferdConverter(new MockHttpHandler(HttpStatusCode.OK, new byte[0]));
    converter.ConvertToZugferd("http://localhost/convert", _tempPdf, _tempXml, profile: "INVALID");
}
```

---

### 5.3 Test: output file is placed in specified `outputDirectory`

```csharp
[TestMethod]
public void ConvertToZugferd_CustomOutputDirectory_FileWrittenThere()
{
    string customDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    // Directory should be auto-created (Phase 2.3)
    byte[] fakePdf = Encoding.UTF8.GetBytes("%PDF-1.4 fake");
    var handler = new MockHttpHandler(HttpStatusCode.OK, fakePdf, "application/pdf");
    var converter = new ZugferdConverter(handler);

    ConversionResult result = converter.ConvertToZugferd(
        "http://localhost/convert", _tempPdf, _tempXml, outputDirectory: customDir);

    Assert.IsTrue(result.OutputPath.StartsWith(customDir));
    Assert.IsTrue(File.Exists(result.OutputPath));

    Directory.Delete(customDir, recursive: true);
}
```

---

### 5.4 Test: `X-Api-Key` header is sent when `apiKey` is provided

Requires a `MockHttpHandler` variant that captures the request. Add an `InspectableHttpHandler` to the test file:

```csharp
internal class InspectableHttpHandler : HttpMessageHandler
{
    public HttpRequestMessage LastRequest { get; private set; }
    // ... returns 200 OK with empty PDF body
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        // return OK response
    }
}

[TestMethod]
public void ConvertToZugferd_WithApiKey_SendsApiKeyHeader()
{
    var handler = new InspectableHttpHandler();
    var converter = new ZugferdConverter(handler);
    converter.ConvertToZugferd("http://localhost/convert", _tempPdf, _tempXml, apiKey: "my-secret");
    Assert.IsTrue(handler.LastRequest.Headers.Contains("X-Api-Key"));
    Assert.AreEqual("my-secret", handler.LastRequest.Headers.GetValues("X-Api-Key").First());
}
```

---

### 5.5 Test: `TimeoutSeconds` property affects behavior

**File:** `tests/ZugferdNavision.Tests/ZugferdConverterTests.cs`

Verifies the new `TimeoutSeconds` property (Phase 3.1) is respected at construction time:

```csharp
[TestMethod]
public void ZugferdConverter_DefaultTimeoutSeconds_Is60()
{
    var converter = new ZugferdConverter();
    Assert.AreEqual(60, converter.TimeoutSeconds);
}
```

---

## Phase 6 — Documentation Updates (Lower Priority)

### 6.1 Update `navision/snippet.md`

- Add `TimeoutSeconds` usage example (from Phase 3.1)
- Add TryFunction wrapper with proper error message extraction
- Clarify the correct DLL filename after Phase 4.3 rename

### 6.2 Add a `DEPLOYMENT.md` in the project root

Brief guide covering:
1. Build command (`msbuild` or `dotnet build`)
2. Registration command (`regasm /codebase /tlb`)
3. How to verify registration (`reg query HKCR\ZugferdNavision.ZugferdConverter`)
4. How to unregister (`regasm /unregister`)
5. Bitness requirements and common errors

---

## Summary Table

| # | Item | Priority | File(s) | Effort |
|---|------|----------|---------|--------|
| 1.1 | Add `[ProgId]` to `ZugferdConverter` | **Critical** | `ZugferdConverter.cs` | 1 line |
| 1.2 | Add `[ClassInterface(AutoDual)]` to `ZugferdConverter` | **Critical** | `ZugferdConverter.cs` | 1 line |
| 1.3 | Add `[ClassInterface(AutoDual)]` to `ConversionResult` | **Critical** | `ConversionResult.cs` | 1 line |
| 1.4 | Upgrade `TargetFramework` to `net472` | **Critical** | both `.csproj` files | 1 line each |
| 1.5 | Add `RegisterForComInterop` | High | `ZugferdNavision.Converter.csproj` | 1 line |
| 2.1 | Validate `apiUrl` | Medium | `ZugferdConverter.cs` | 3 lines |
| 2.2 | Validate `profile` whitelist | Medium | `ZugferdConverter.cs` | 8 lines |
| 2.3 | Auto-create output directory | Medium | `ZugferdConverter.cs` | 2 lines |
| 3.1 | Configurable `TimeoutSeconds` property | Medium | `ZugferdConverter.cs` | 3 lines |
| 3.2 | Improve HTTP error message | Medium | `ZugferdConverter.cs` | 2 lines |
| 3.3 | Handle timeout `AggregateException` | Medium | `ZugferdConverter.cs` | 6 lines |
| 4.1 | PowerShell registration script | Medium | `scripts/Register-ZugferdNavision.ps1` | new file |
| 4.2 | CMD batch script | Low | `scripts/register.cmd` | new file |
| 4.3 | Rename `AssemblyName` to `ZugferdNavision` | Medium | `.csproj` | 1 line |
| 5.1 | Test: null apiUrl | Low | `ZugferdConverterTests.cs` | 8 lines |
| 5.2 | Test: invalid profile | Low | `ZugferdConverterTests.cs` | 8 lines |
| 5.3 | Test: custom outputDirectory | Low | `ZugferdConverterTests.cs` | 15 lines |
| 5.4 | Test: apiKey header sent | Low | `ZugferdConverterTests.cs` | 15 lines + helper |
| 5.5 | Test: default TimeoutSeconds | Low | `ZugferdConverterTests.cs` | 6 lines |
| 6.1 | Update `navision/snippet.md` | Low | `navision/snippet.md` | prose |
| 6.2 | Add `DEPLOYMENT.md` | Low | `DEPLOYMENT.md` | new file |

**Start with Phase 1** — items 1.1–1.3 are the only changes required for the DLL to appear and function correctly in NAV 2017. All other phases improve robustness, maintainability, and developer experience.
