@echo off
setlocal
echo ===================================================================
echo   Compiling Antigravity Companion V1.0 (Standalone Executable)
echo ===================================================================

set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    set "CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
if not exist "%CSC%" (
    for /f "delims=" %%I in ('where csc.exe 2^>nul') do (
        set "CSC=%%I"
        goto :csc_found
    )
)
:csc_found
if not exist "%CSC%" (
    echo [ERROR] Could not locate .NET Framework csc.exe compiler.
    exit /b 1
)

set "ICON_FLAG="
if exist "%~dp0app.ico" (
    set "ICON_FLAG=/win32icon:"%~dp0app.ico""
)

set "MANIFEST_FLAG="
if exist "%~dp0app.manifest" (
    set "MANIFEST_FLAG=/win32manifest:"%~dp0app.manifest""
)

echo   [3/3] Building Antigravity Companion V1.0 Single EXE...
rem Includes Stage 4 services (ScheduledSnapshotService.cs, TrayHost.cs) and all 12 modular UI embedded resources
"%CSC%" /target:winexe /platform:anycpu /optimize+ /debug:pdbonly /codepage:65001 /utf8output %ICON_FLAG% %MANIFEST_FLAG% /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /res:"%~dp0ui\index.html",AntigravityCompanion.ui.index.html /res:"%~dp0ui\styles.css",AntigravityCompanion.ui.styles.css /res:"%~dp0ui\app.js",AntigravityCompanion.ui.app.js /res:"%~dp0ui\js\api.js",AntigravityCompanion.ui.js.api.js /res:"%~dp0ui\js\state.js",AntigravityCompanion.ui.js.state.js /res:"%~dp0ui\js\components.js",AntigravityCompanion.ui.js.components.js /res:"%~dp0ui\js\views\overview.js",AntigravityCompanion.ui.js.views.overview.js /res:"%~dp0ui\js\views\cockpit.js",AntigravityCompanion.ui.js.views.cockpit.js /res:"%~dp0ui\js\views\sessions.js",AntigravityCompanion.ui.js.views.sessions.js /res:"%~dp0ui\js\views\storage.js",AntigravityCompanion.ui.js.views.storage.js /res:"%~dp0ui\js\views\vault.js",AntigravityCompanion.ui.js.views.vault.js /res:"%~dp0ui\js\views\backup.js",AntigravityCompanion.ui.js.views.backup.js /out:"%~dp0Antigravity_Companion.exe" "%~dp0src\Program.cs" "%~dp0src\Core\*.cs" "%~dp0src\Storage\*.cs" "%~dp0src\Services\*.cs" "%~dp0src\Host\*.cs" "%~dp0AssemblyInfo.cs"

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] Build completed! Output: %~dp0Antigravity_Companion.exe
    exit /b 0
) else (
    echo [ERROR] Build failed!
    exit /b 1
)
