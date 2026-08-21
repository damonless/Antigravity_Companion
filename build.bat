@echo off
echo ===================================================================
echo   Compiling Antigravity Companion (Standalone Executable)
echo ===================================================================

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

set ICON_FLAG=
if exist "%~dp0app.ico" (
    set ICON_FLAG=/win32icon:"%~dp0app.ico"
)

%CSC% /target:winexe /platform:anycpu /optimize+ %ICON_FLAG% /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:"%~dp0Antigravity_Companion.exe" "%~dp0MainForm.cs"

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] Build completed! Output: %~dp0Antigravity_Companion.exe
    if exist "%~dp0..\Antigravity_Companion.exe" (
        copy /Y "%~dp0Antigravity_Companion.exe" "%~dp0..\Antigravity_Companion.exe" >nul
    )
) else (
    echo [ERROR] Build failed!
)
