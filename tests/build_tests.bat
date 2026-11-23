@echo off
setlocal
echo ===================================================================
echo   Compiling Antigravity Companion Production E2E Test Suite
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

"%CSC%" /codepage:65001 /utf8output /target:exe /platform:anycpu /optimize+ /debug:pdbonly /main:AntigravityCompanion.Tests.TestRunner /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /res:"%~dp0..\ui\index.html",AntigravityCompanion.ui.index.html /res:"%~dp0..\ui\styles.css",AntigravityCompanion.ui.styles.css /res:"%~dp0..\ui\app.js",AntigravityCompanion.ui.app.js /out:"%~dp0TestRunner.exe" "%~dp0..\AppServices.cs" "%~dp0..\CapabilityRepositoryManager.cs" "%~dp0..\LocalWebHost.cs" "%~dp0..\LocalWorkspaceManager.cs" "%~dp0..\MainForm.cs" "%~dp0..\ProtoAstCodec.cs" "%~dp0..\SessionMetadataExtractor.cs" "%~dp0..\SessionRenameEngine.cs" "%~dp0..\StreamingZipIndexReader.cs" "%~dp0..\TranscriptMarkdownExporter.cs" "%~dp0..\VisualDiffEngine.cs" "%~dp0..\WebShellLauncher.cs" "%~dp0TestRunner.cs"

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
