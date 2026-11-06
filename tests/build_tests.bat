@echo off
echo ===================================================================
echo   Compiling Antigravity Companion Production E2E Test Suite
echo ===================================================================

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

%CSC% /target:exe /platform:anycpu /optimize+ /main:AntigravityCompanion.Tests.TestRunner /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:"%~dp0TestRunner.exe" "%~dp0..\*.cs" "%~dp0TestRunner.cs"

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] Build completed! Running production tests...
    echo.
    "%~dp0TestRunner.exe"
    set TEST_EXIT=%ERRORLEVEL%
    del /Q "%~dp0TestRunner.exe" >nul 2>&1
    exit /b %TEST_EXIT%
) else (
    echo [ERROR] Test build failed!
    exit /b 1
)
