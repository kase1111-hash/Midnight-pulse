@echo off
REM ============================================================================
REM Nightflow - Windows Build Script (Batch Wrapper)
REM ============================================================================

echo.
echo ============================================
echo   Nightflow Build System
echo ============================================
echo.

REM Check for PowerShell
where powershell >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo ERROR: PowerShell is required but not found.
    echo Please install PowerShell or run build.ps1 directly.
    pause
    exit /b 1
)

REM Parse command line arguments
set ARGS=
set CREATE_INSTALLER=

:parse_args
if "%~1"=="" goto run_build
if /i "%~1"=="--installer" set CREATE_INSTALLER=-CreateInstaller
if /i "%~1"=="-i" set CREATE_INSTALLER=-CreateInstaller
if /i "%~1"=="--clean" set ARGS=%ARGS% -CleanBuild
if /i "%~1"=="-c" set ARGS=%ARGS% -CleanBuild
if /i "%~1"=="--unity" set ARGS=%ARGS% -UnityPath "%~2" & shift
shift
goto parse_args

:run_build
echo Running PowerShell build script...
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0build.ps1" %ARGS% %CREATE_INSTALLER%

if %ERRORLEVEL% neq 0 (
    echo.
    echo Build failed with error code: %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Build completed successfully!
pause
