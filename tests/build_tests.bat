@echo off
setlocal
echo ===================================================================
echo   Compiling Antigravity Companion V1.0 Production E2E Test Suite
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

rem Includes Stage 4 services (ScheduledSnapshotService.cs, TrayHost.cs) and all 14 modular UI embedded resources
"%CSC%" /codepage:65001 /utf8output /target:exe /platform:anycpu /optimize+ /debug:pdbonly /main:AntigravityCompanion.Tests.TestRunner /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /res:"%~dp0..\ui\index.html",AntigravityCompanion.ui.index.html /res:"%~dp0..\ui\styles.css",AntigravityCompanion.ui.styles.css /res:"%~dp0..\ui\app.js",AntigravityCompanion.ui.app.js /res:"%~dp0..\ui\js\api.js",AntigravityCompanion.ui.js.api.js /res:"%~dp0..\ui\js\state.js",AntigravityCompanion.ui.js.state.js /res:"%~dp0..\ui\js\components.js",AntigravityCompanion.ui.js.components.js /res:"%~dp0..\ui\js\views\overview.js",AntigravityCompanion.ui.js.views.overview.js /res:"%~dp0..\ui\js\views\cockpit.js",AntigravityCompanion.ui.js.views.cockpit.js /res:"%~dp0..\ui\js\views\sessions.js",AntigravityCompanion.ui.js.views.sessions.js /res:"%~dp0..\ui\js\views\rename.js",AntigravityCompanion.ui.js.views.rename.js /res:"%~dp0..\ui\js\views\storage.js",AntigravityCompanion.ui.js.views.storage.js /res:"%~dp0..\ui\js\views\vault.js",AntigravityCompanion.ui.js.views.vault.js /res:"%~dp0..\ui\js\views\backup.js",AntigravityCompanion.ui.js.views.backup.js /res:"%~dp0..\ui\js\views\settings.js",AntigravityCompanion.ui.js.views.settings.js /out:"%~dp0TestRunner.exe" "%~dp0..\src\Program.cs" "%~dp0..\src\Core\*.cs" "%~dp0..\src\Storage\*.cs" "%~dp0..\src\Services\*.cs" "%~dp0..\src\Host\*.cs" "%~dp0..\AssemblyInfo.cs" "%~dp0TestRunner.cs"

if %ERRORLEVEL% neq 0 goto :build_fail

echo [SUCCESS] Build completed! Running production tests...
echo.
"%~dp0TestRunner.exe"
set "TEST_EXIT=%ERRORLEVEL%"
del /Q "%~dp0TestRunner.exe" >nul 2>&1
del /Q "%~dp0TestRunner.pdb" >nul 2>&1
exit /b %TEST_EXIT%

:build_fail
del /Q "%~dp0TestRunner.exe" >nul 2>&1
del /Q "%~dp0TestRunner.pdb" >nul 2>&1
echo [ERROR] Test build failed!
exit /b 1
