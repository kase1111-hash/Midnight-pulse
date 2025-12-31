# ============================================================================
# Nightflow - Windows Build Script
# Builds the Unity project and creates distributable package
# ============================================================================

param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\2022.3.0f1\Editor\Unity.exe",
    [string]$BuildTarget = "Win64",
    [string]$Configuration = "Release",
    [switch]$CleanBuild,
    [switch]$CreateInstaller
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$BuildOutput = Join-Path $ProjectRoot "Build\Windows"
$GameName = "Nightflow"

# Read version from VERSION file
$VersionFile = Join-Path $ProjectRoot "VERSION"
if (Test-Path $VersionFile) {
    $GameVersion = (Get-Content $VersionFile -First 1).Trim()
} else {
    $GameVersion = "1.0.0"
    Write-Host "WARNING: VERSION file not found, using default version $GameVersion" -ForegroundColor Yellow
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Nightflow Windows Build System" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Verify Unity installation
if (-not (Test-Path $UnityPath)) {
    # Try common Unity paths
    $commonPaths = @(
        "C:\Program Files\Unity\Hub\Editor\*\Editor\Unity.exe",
        "C:\Program Files\Unity Hub\Editor\*\Editor\Unity.exe",
        "$env:PROGRAMFILES\Unity\Hub\Editor\*\Editor\Unity.exe"
    )

    foreach ($pattern in $commonPaths) {
        $found = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) {
            $UnityPath = $found.FullName
            Write-Host "Found Unity at: $UnityPath" -ForegroundColor Green
            break
        }
    }

    if (-not (Test-Path $UnityPath)) {
        Write-Host "ERROR: Unity not found. Please specify Unity path with -UnityPath parameter." -ForegroundColor Red
        Write-Host "Example: .\build.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\2022.3.0f1\Editor\Unity.exe'"
        exit 1
    }
}

# Clean build directory if requested
if ($CleanBuild -and (Test-Path $BuildOutput)) {
    Write-Host "Cleaning previous build..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $BuildOutput
}

# Create build output directory
if (-not (Test-Path $BuildOutput)) {
    New-Item -ItemType Directory -Path $BuildOutput -Force | Out-Null
}

Write-Host "Building $GameName v$GameVersion for Windows ($Configuration)..." -ForegroundColor Green
Write-Host "Project: $ProjectRoot"
Write-Host "Output: $BuildOutput"
Write-Host ""

# Build arguments
$buildArgs = @(
    "-quit",
    "-batchmode",
    "-projectPath", "`"$ProjectRoot`"",
    "-buildTarget", "Win64",
    "-buildWindows64Player", "`"$BuildOutput\$GameName.exe`"",
    "-logFile", "`"$BuildOutput\build.log`""
)

if ($Configuration -eq "Release") {
    $buildArgs += "-releaseCodeOptimization"
}

# Execute Unity build
Write-Host "Starting Unity build process..." -ForegroundColor Yellow
$startTime = Get-Date

$process = Start-Process -FilePath $UnityPath -ArgumentList $buildArgs -Wait -PassThru -NoNewWindow

$endTime = Get-Date
$duration = $endTime - $startTime

if ($process.ExitCode -ne 0) {
    Write-Host "Build FAILED with exit code: $($process.ExitCode)" -ForegroundColor Red
    Write-Host "Check build log at: $BuildOutput\build.log"
    exit $process.ExitCode
}

Write-Host ""
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Build time: $($duration.TotalMinutes.ToString('F1')) minutes"
Write-Host "Output: $BuildOutput\$GameName.exe"

# Verify build output
if (-not (Test-Path "$BuildOutput\$GameName.exe")) {
    Write-Host "WARNING: Build executable not found. Build may have failed." -ForegroundColor Yellow
    exit 1
}

# Get build size
$buildSize = (Get-ChildItem -Path $BuildOutput -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "Build size: $($buildSize.ToString('F1')) MB"

# Create installer if requested
if ($CreateInstaller) {
    Write-Host ""
    Write-Host "Creating installer..." -ForegroundColor Yellow

    $innoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    $issPath = Join-Path $PSScriptRoot "installer.iss"

    if (-not (Test-Path $innoSetupPath)) {
        Write-Host "Inno Setup not found at: $innoSetupPath" -ForegroundColor Red
        Write-Host "Please install Inno Setup 6 or specify correct path."
        exit 1
    }

    if (-not (Test-Path $issPath)) {
        Write-Host "Installer script not found at: $issPath" -ForegroundColor Red
        exit 1
    }

    # Pass version to Inno Setup
    $installerArgs = @("/DMyAppVersion=$GameVersion", "`"$issPath`"")
    $installerProcess = Start-Process -FilePath $innoSetupPath -ArgumentList $installerArgs -Wait -PassThru -NoNewWindow

    if ($installerProcess.ExitCode -ne 0) {
        Write-Host "Installer creation FAILED" -ForegroundColor Red
        exit $installerProcess.ExitCode
    }

    $installerOutput = Join-Path $ProjectRoot "Installer\Nightflow_Setup_$GameVersion.exe"
    Write-Host "Installer created successfully!" -ForegroundColor Green
    Write-Host "Output: $installerOutput"
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Build Complete!" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
