# Nightflow Build System

Build scripts for creating Windows standalone builds and installers.

## Prerequisites

- **Unity 2022.3 LTS** (or compatible version with Entities package)
- **Windows 10/11 (64-bit)**
- **PowerShell 5.0+** (included with Windows 10/11)
- **Inno Setup 6** (optional, for creating installer)

### Installing Inno Setup

Download and install from: https://jrsoftware.org/isdl.php

## Quick Start

### Build Game Only

```batch
build.bat
```

### Build Game + Create Installer

```batch
build.bat --installer
```

Or using PowerShell directly:

```powershell
.\build.ps1 -CreateInstaller
```

## Build Options

### Using build.bat

| Option | Short | Description |
|--------|-------|-------------|
| `--installer` | `-i` | Create installer after build |
| `--clean` | `-c` | Clean previous build before building |
| `--unity <path>` | | Specify Unity editor path |

### Using build.ps1 (PowerShell)

```powershell
.\build.ps1 [-UnityPath <path>] [-BuildTarget <target>] [-Configuration <config>] [-CleanBuild] [-CreateInstaller]
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-UnityPath` | Auto-detect | Path to Unity.exe |
| `-BuildTarget` | Win64 | Build target platform |
| `-Configuration` | Release | Build configuration |
| `-CleanBuild` | false | Clean previous build |
| `-CreateInstaller` | false | Create installer after build |

## Output

### Build Output

```
Build/
└── Windows/
    ├── Nightflow.exe        # Main executable
    ├── Nightflow_Data/      # Game data
    ├── *.dll                # Runtime libraries
    └── build.log            # Build log
```

### Installer Output

```
Installer/
└── Nightflow_Setup_1.0.0.exe
```

## Customizing the Installer

Edit `installer.iss` to customize:

- **Version**: Update `MyAppVersion` define
- **Publisher**: Update `MyAppPublisher` define
- **Icons**: Add custom installer images
- **License**: Add `LicenseFile` directive

### Adding a License Agreement

```iss
[Setup]
LicenseFile=..\LICENSE.txt
```

### Adding Custom Icon

```iss
[Setup]
SetupIconFile=..\Assets\Icons\game.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
```

## Troubleshooting

### Unity Not Found

Specify Unity path explicitly:

```batch
build.bat --unity "C:\Program Files\Unity\Hub\Editor\2022.3.0f1\Editor\Unity.exe"
```

### Build Fails

1. Check `Build\Windows\build.log` for errors
2. Ensure all scenes are added to Build Settings in Unity
3. Verify Entities package is properly installed

### Installer Fails

1. Ensure Inno Setup 6 is installed at default path
2. Verify build completed successfully first
3. Check that all files exist in `Build\Windows\`

## CI/CD Integration

For automated builds, use:

```powershell
# Headless build for CI
powershell -ExecutionPolicy Bypass -File build.ps1 -CleanBuild -CreateInstaller
exit $LASTEXITCODE
```

## Version Updates

When releasing a new version:

1. Update version in `installer.iss`:
   ```iss
   #define MyAppVersion "1.1.0"
   ```

2. Update version in Unity Player Settings

3. Run full build with installer:
   ```batch
   build.bat --clean --installer
   ```
