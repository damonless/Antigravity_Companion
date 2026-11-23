@echo off
setlocal
echo ===================================================================
echo   Compiling Antigravity Companion V0.8 (Standalone Executable)
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

echo   [3/3] Building Modern Workbench + --legacy-ui Single EXE...
"%CSC%" /target:winexe /platform:anycpu /optimize+ /debug:pdbonly /codepage:65001 /utf8output %ICON_FLAG% %MANIFEST_FLAG% /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /res:"%~dp0ui\index.html",AntigravityCompanion.ui.index.html /res:"%~dp0ui\styles.css",AntigravityCompanion.ui.styles.css /res:"%~dp0ui\app.js",AntigravityCompanion.ui.app.js /out:"%~dp0Antigravity_Companion.exe" "%~dp0AppServices.cs" "%~dp0AssemblyInfo.cs" "%~dp0CapabilityRepositoryManager.cs" "%~dp0LocalWebHost.cs" "%~dp0LocalWorkspaceManager.cs" "%~dp0MainForm.cs" "%~dp0ProtoAstCodec.cs" "%~dp0SessionMetadataExtractor.cs" "%~dp0SessionRenameEngine.cs" "%~dp0StreamingZipIndexReader.cs" "%~dp0TranscriptMarkdownExporter.cs" "%~dp0VisualDiffEngine.cs" "%~dp0WebShellLauncher.cs"

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] Build completed! Output: %~dp0Antigravity_Companion.exe
    exit /b 0
) else (
    echo [ERROR] Build failed!
    exit /b 1
)
