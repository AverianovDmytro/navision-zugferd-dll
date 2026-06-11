#Requires -Version 3.0
<#
.SYNOPSIS
    Register or unregister ZugferdNavision.dll for COM Interop.

.PARAMETER DllPath
    Path to ZugferdNavision.dll. Defaults to .\bin\Release\ZugferdNavision.dll.

.PARAMETER Platform
    Target platform: x86 (default) or x64. Selects the matching regasm.exe.

.PARAMETER Unregister
    When set, unregisters the DLL instead of registering it.

.EXAMPLE
    .\Register-ZugferdNavision.ps1
    .\Register-ZugferdNavision.ps1 -DllPath "C:\NavAddins\ZugferdNavision.dll" -Platform x64
    .\Register-ZugferdNavision.ps1 -Unregister
#>
param(
    [string]$DllPath  = ".\bin\Release\ZugferdNavision.dll",
    [ValidateSet("x86", "x64")]
    [string]$Platform = "x86",
    [switch]$Unregister
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- elevation check ---
$identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator. Right-click PowerShell and choose 'Run as administrator'."
    exit 1
}

# --- resolve DLL path ---
$DllPath = [IO.Path]::GetFullPath($DllPath)
if (-not (Test-Path $DllPath)) {
    Write-Error "DLL not found: $DllPath"
    exit 1
}

# --- select regasm.exe ---
if ($Platform -eq "x86") {
    $regasm = "$env:windir\Microsoft.NET\Framework\v4.0.30319\regasm.exe"
} else {
    $regasm = "$env:windir\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
}

if (-not (Test-Path $regasm)) {
    Write-Error "regasm.exe not found at: $regasm`nEnsure .NET Framework 4.x is installed."
    exit 1
}

# --- run regasm ---
if ($Unregister) {
    Write-Host "Unregistering $DllPath [$Platform] ..."
    & $regasm $DllPath /unregister /tlb
} else {
    Write-Host "Registering $DllPath [$Platform] ..."
    & $regasm $DllPath /codebase /tlb
}

if ($LASTEXITCODE -eq 0) {
    $action = if ($Unregister) { "unregistered" } else { "registered" }
    Write-Host "SUCCESS: ZugferdNavision.dll $action successfully." -ForegroundColor Green
} else {
    Write-Error "FAILED: regasm.exe exited with code $LASTEXITCODE."
    exit $LASTEXITCODE
}
