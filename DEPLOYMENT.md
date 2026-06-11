# Deployment Guide — ZugferdNavision.dll

## 1. Build

**With MSBuild (classic .NET):**
```cmd
msbuild src\ZugferdNavision.Converter\ZugferdNavision.Converter.csproj ^
    /p:Configuration=Release /p:Platform=x86
```

**With the .NET SDK:**
```cmd
dotnet build src\ZugferdNavision.Converter\ZugferdNavision.Converter.csproj ^
    -c Release -p:Platform=x86
```

Output: `src\ZugferdNavision.Converter\bin\Release\ZugferdNavision.dll`

---

## 2. Register for COM Interop

Must be run **as Administrator**.

**PowerShell (recommended):**
```powershell
.\scripts\Register-ZugferdNavision.ps1 -DllPath "C:\NavAddins\ZugferdNavision.dll"
```

**CMD batch fallback** (when PS execution policy blocks `.ps1`):
```cmd
scripts\register.cmd
```

**Manual (32-bit NAV, typical):**
```cmd
C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe ^
    "C:\NavAddins\ZugferdNavision.dll" /codebase /tlb
```

**Manual (64-bit NAV):**
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm.exe ^
    "C:\NavAddins\ZugferdNavision.dll" /codebase /tlb
```

---

## 3. Verify registration

```cmd
reg query "HKEY_CLASSES_ROOT\ZugferdNavision.ZugferdConverter"
```

Expected output contains `InprocServer32` pointing to the DLL path.

---

## 4. Unregister

**PowerShell:**
```powershell
.\scripts\Register-ZugferdNavision.ps1 -Unregister
```

**Manual:**
```cmd
C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe ^
    "C:\NavAddins\ZugferdNavision.dll" /unregister
```

---

## 5. Bitness requirements and common errors

| Symptom | Cause | Fix |
|---------|-------|-----|
| Type not visible in NAV Automation picker | DLL not registered, or wrong ProgId | Run `regasm /codebase /tlb` as Admin; check `HKCR\ZugferdNavision.ZugferdConverter` |
| "Class not registered" at runtime | `regasm` not run as Administrator | Re-run as Administrator |
| "Cannot create ActiveX component" | DLL bitness ≠ NAV bitness | NAV 2017 is 32-bit; use `Framework` (not `Framework64`) regasm |
| Method not visible in NAV C/AL | Missing `[ClassInterface(AutoDual)]` | Rebuild DLL with attribute and re-register |
| "File not found" / missing DLL | `/codebase` flag not used | Always pass `/codebase` so regasm embeds the full path |
| Timeout calling the API | 60-second default too short | Set `Converter.TimeoutSeconds := 120;` before calling `ConvertToZugferd` |
