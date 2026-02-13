# Nightflow Build System

Build scripts for creating Windows standalone builds and installers.

## Versioning

The game version is defined in the `VERSION` file at the project root. This single source of truth is used by:
- Build scripts (build.ps1)
- Installer (installer.iss)
- GitHub Actions CI/CD

## Prerequisites

- **Unity 2023 LTS** (or compatible version with DOTS 1.0+ and Entities package)
- **Windows 10/11 (64-bit)**
- **PowerShell 5.0+** (included with Windows 10/11)
- **Inno Setup 6** (optional, for creating installer)

### Required Unity Packages
- Entities 1.0+
- Unity.Burst
- Unity.Collections
- Unity.Mathematics
- HDRP (High Definition Render Pipeline)

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
└── Nightflow_Setup_<version>.exe
```

## Project Architecture

The build compiles a Unity DOTS project with the following structure:

| Directory | Contents |
|-----------|----------|
| `src/Components/` | 21 ECS component files (60+ types) |
| `src/Systems/` | 70+ ECS systems |
| `src/Tags/` | Entity tag components |
| `src/Buffers/` | Dynamic buffer elements |
| `src/Input/` | Input management and wheel support |
| `src/Rendering/` | Screen-space reflections and wireframe systems |
| `src/Audio/` | Audio management |
| `src/Config/` | Game configuration |

Total: **131 C# source files**

## Customizing the Installer

Edit `installer.iss` to customize. Note that custom icons and license files are not included in the repository by default — you need to create them.

### Adding a License Agreement

Create a `LICENSE.txt` in the project root, then add to `installer.iss`:

```iss
[Setup]
LicenseFile=..\LICENSE.txt
```

### Adding Custom Icon

Create an `.ico` file (e.g., at `Assets/Icons/game.ico`), then add to `installer.iss`:

```iss
[Setup]
SetupIconFile=..\Assets\Icons\game.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
```

## Troubleshooting

### Unity Not Found

Specify Unity path explicitly:

```batch
build.bat --unity "C:\Program Files\Unity\Hub\Editor\2023.2.0f1\Editor\Unity.exe"
```

### Build Fails

1. Check `Build\Windows\build.log` for errors
2. Ensure all scenes are added to Build Settings in Unity
3. Verify Entities package is properly installed
4. Confirm HDRP is configured correctly

### Unity DOTS Issues

- **Burst compilation errors:** Ensure no managed types (strings, classes) are used in `[BurstCompile]` systems. Check for `ref` vs value type mismatches.
- **Entity query failures:** Verify components are registered and that `RequireForUpdate<T>()` is called in `OnCreate` for singleton dependencies.
- **NativeContainer leaks:** Check that all `EntityCommandBuffer` and `NativeList` allocations are disposed, preferably in try-finally blocks.

### Installer Fails

1. Ensure Inno Setup 6 is installed at default path
2. Verify build completed successfully first
3. Check that all files exist in `Build\Windows\`

## CI/CD with GitHub Actions

The project includes a GitHub Actions workflow (`.github/workflows/build.yml`) that:

1. **On every push/PR**: Builds the game and uploads artifacts
2. **On version tags**: Creates installer and GitHub Release

### Setting Up GitHub Actions

Add these secrets to your repository (Settings -> Secrets -> Actions):

| Secret | Description |
|--------|-------------|
| `UNITY_LICENSE` | Unity license file content |
| `UNITY_EMAIL` | Unity account email |
| `UNITY_PASSWORD` | Unity account password |

See [GameCI documentation](https://game.ci/docs/github/activation) for Unity license activation.

### Creating a Release

1. Update version in `VERSION` file:
   ```
   2.0.0
   ```

2. Commit the change:
   ```bash
   git add VERSION
   git commit -m "Bump version to 2.0.0"
   ```

3. Create and push a version tag:
   ```bash
   git tag v2.0.0
   git push origin main v2.0.0
   ```

4. GitHub Actions will automatically:
   - Build the game
   - Create the installer
   - Publish a GitHub Release with both files

### Manual Builds

For local CI-style builds:

```powershell
# Headless build for CI
powershell -ExecutionPolicy Bypass -File build.ps1 -CleanBuild -CreateInstaller
exit $LASTEXITCODE
```
