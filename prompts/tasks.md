# Task List: ZugferdNavision.Converter DLL Enhancements

> Derived from `prompts/plan.md`. Items are ordered by phase (priority).
> Mark each item `[x]` when complete.

---

## Phase 1 — Critical COM Compliance (Must-Fix)

1. [x] **1.1** Add `[ProgId("ZugferdNavision.ZugferdConverter")]` attribute to `ZugferdConverter` class
   - File: `src/ZugferdNavision.Converter/ZugferdConverter.cs` (after the `[Guid(...)]` line)

2. [x] **1.2** Add `[ClassInterface(ClassInterfaceType.AutoDual)]` attribute to `ZugferdConverter` class
   - File: `src/ZugferdNavision.Converter/ZugferdConverter.cs`

3. [x] **1.3** Add `[ClassInterface(ClassInterfaceType.AutoDual)]` attribute to `ConversionResult` class
   - File: `src/ZugferdNavision.Converter/ConversionResult.cs`

4. [x] **1.4** Upgrade `TargetFramework` from `net46` to `net472` in the main project
   - File: `src/ZugferdNavision.Converter/ZugferdNavision.Converter.csproj`

5. [x] **1.4b** Upgrade `TargetFramework` from `net46` to `net472` in the test project
   - File: `tests/ZugferdNavision.Tests/ZugferdNavision.Tests.csproj`

6. [x] **1.5** Add `<RegisterForComInterop>true</RegisterForComInterop>` to the main project file
   - File: `src/ZugferdNavision.Converter/ZugferdNavision.Converter.csproj`

---

## Phase 2 — Input Validation (Medium Priority)

7. [x] **2.1** Add guard for null/empty `apiUrl` parameter — throw `ArgumentNullException` with a clear message
   - File: `src/ZugferdNavision.Converter/ZugferdConverter.cs` (top of `ConvertToZugferd`)

8. [x] **2.2** Add whitelist validation for the `profile` parameter; default to `"BASIC"` when blank, throw `ArgumentException` for unknown values
   - File: `src/ZugferdNavision.Converter/ZugferdConverter.cs`
   - Valid values: `MINIMUM`, `BASIC WL`, `BASIC`, `EN16931`, `EXTENDED`, `XRECHNUNG`

9. [x] **2.3** Auto-create `outputDirectory` if the path does not yet exist (use `Directory.CreateDirectory`)
   - File: `src/ZugferdNavision.Converter/ZugferdConverter.cs` (before `File.WriteAllBytes`)

---

## Phase 3 — Configurability & Error Handling (Medium Priority)

10. [x] **3.1** Add COM-visible `TimeoutSeconds` property (default `60`) to `ZugferdConverter` and use it when constructing `HttpClient`
    - File: `src/ZugferdNavision.Converter/ZugferdConverter.cs`

11. [x] **3.2** Improve HTTP error exception message to include the target URL
    - File: `src/ZugferdNavision.Converter/ZugferdConverter.cs` (current `throw new Exception(...)` line)
    - New format: `"API error {statusCode} calling {url}: {body}"`

12. [x] **3.3** Wrap `PostAsync(...).GetAwaiter().GetResult()` in a try/catch for `AggregateException` where inner is `TaskCanceledException`; rethrow as `TimeoutException` with a human-readable message
    - File: `src/ZugferdNavision.Converter/ZugferdConverter.cs`

---

## Phase 4 — Deployment Scripts (Medium Priority)

13. [x] **4.1** Create PowerShell registration script `scripts/Register-ZugferdNavision.ps1`
    - Parameters: `-DllPath`, `-Platform` (`x86`/`x64`), `-Unregister`
    - Must check for Administrator elevation before running
    - Auto-select correct 32-bit or 64-bit `regasm.exe` path based on `-Platform`
    - Print clear success/failure output

14. [x] **4.2** Create CMD batch fallback script `scripts\register.cmd`
    - Covers environments where PowerShell execution policy blocks `.ps1` files
    - Wraps `regasm /codebase /tlb` with an elevation check

15. [x] **4.3** Rename `<AssemblyName>` in the project file from `ZugferdNavision.Converter` to `ZugferdNavision` so the output file is `ZugferdNavision.dll`
    - File: `src/ZugferdNavision.Converter/ZugferdNavision.Converter.csproj`

---

## Phase 5 — Test Coverage Expansion (Lower Priority)

16. [x] **5.1** Add test: `ConvertToZugferd` with `null` `apiUrl` throws `ArgumentNullException`
    - File: `tests/ZugferdNavision.Tests/ZugferdConverterTests.cs`

17. [x] **5.2** Add test: `ConvertToZugferd` with an invalid `profile` value throws `ArgumentException`
    - File: `tests/ZugferdNavision.Tests/ZugferdConverterTests.cs`

18. [x] **5.3** Add test: output file is written to a custom `outputDirectory` (directory auto-created)
    - File: `tests/ZugferdNavision.Tests/ZugferdConverterTests.cs`
    - Clean up the temp directory after the test

19. [x] **5.4** Add `InspectableHttpHandler` helper class and test that `X-Api-Key` header is sent when `apiKey` is provided
    - File: `tests/ZugferdNavision.Tests/ZugferdConverterTests.cs`

20. [x] **5.5** Add test: default value of `TimeoutSeconds` property is `60`
    - File: `tests/ZugferdNavision.Tests/ZugferdConverterTests.cs`

---

## Phase 6 — Documentation Updates (Lower Priority)

21. [x] **6.1** Update `navision/snippet.md`
    - Add `TimeoutSeconds` usage example (`Converter.TimeoutSeconds := 120;`)
    - Add TryFunction wrapper with error message extraction
    - Update DLL filename references to `ZugferdNavision.dll` (after Phase 4.3 rename)

22. [x] **6.2** Create `DEPLOYMENT.md` in the project root covering:
    1. Build command (`msbuild` or `dotnet build`)
    2. Registration command (`regasm /codebase /tlb`)
    3. How to verify registration (`reg query HKCR\ZugferdNavision.ZugferdConverter`)
    4. How to unregister (`regasm /unregister`)
    5. Bitness requirements and common error table
