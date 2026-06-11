@echo off
:: register.cmd — Register or unregister ZugferdNavision.dll for COM Interop.
::
:: Usage:
::   register.cmd                    Register using x86 regasm (default)
::   register.cmd x64                Register using x64 regasm
::   register.cmd x86 unregister     Unregister using x86 regasm
::   register.cmd x64 unregister     Unregister using x64 regasm
::
:: Must be run as Administrator.

setlocal

:: --- elevation check ---
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: This script must be run as Administrator.
    echo Right-click Command Prompt and choose "Run as administrator".
    exit /b 1
)

:: --- resolve DLL path (default: sibling bin\Release folder) ---
set "DLL_PATH=%~dp0..\bin\Release\ZugferdNavision.dll"
if not exist "%DLL_PATH%" (
    echo ERROR: DLL not found: %DLL_PATH%
    exit /b 1
)

:: --- select regasm.exe based on first argument ---
set "PLATFORM=%~1"
if /i "%PLATFORM%"=="x64" (
    set "REGASM=%windir%\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
) else (
    set "REGASM=%windir%\Microsoft.NET\Framework\v4.0.30319\regasm.exe"
)

if not exist "%REGASM%" (
    echo ERROR: regasm.exe not found: %REGASM%
    echo Ensure .NET Framework 4.x is installed.
    exit /b 1
)

:: --- register or unregister ---
set "ACTION=%~2"
if /i "%ACTION%"=="unregister" (
    echo Unregistering %DLL_PATH% ...
    "%REGASM%" "%DLL_PATH%" /unregister /tlb
) else (
    echo Registering %DLL_PATH% ...
    "%REGASM%" "%DLL_PATH%" /codebase /tlb
)

if %ERRORLEVEL% EQU 0 (
    echo SUCCESS: ZugferdNavision.dll operation completed.
) else (
    echo FAILED: regasm.exe exited with code %ERRORLEVEL%.
    exit /b %ERRORLEVEL%
)

endlocal
