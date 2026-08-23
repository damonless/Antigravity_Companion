@echo off
echo ===================================================================
echo   Compiling Antigravity Companion E2E Test Runner
echo ===================================================================

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

%CSC% /target:exe /platform:anycpu /optimize+ /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:"%~dp0TestRunner.exe" "%~dp0TestRunner.cs" "%~dp0ContractReferenceEngine.cs" "%~dp0MockSandboxGenerator.cs" "%~dp0Tier1_FeatureCoverageTests.cs" "%~dp0Tier2_BoundaryCornerTests.cs" "%~dp0Tier3_PairwiseInteractionTests.cs" "%~dp0Tier4_RealWorldScenarioTests.cs"

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] Build completed! Output: %~dp0TestRunner.exe
    echo.
    echo Running tests...
    "%~dp0TestRunner.exe" %*
) else (
    echo [ERROR] Test build failed!
    exit /b 1
)
