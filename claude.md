# Claude.md - Nightflow Project Guide

## Project Overview

**Nightflow** is a synthwave-inspired endless driving game built with Unity DOTS (Data-Oriented Technology Stack) 1.0+. It features procedurally generated highways with fast-paced arcade gameplay in a neon-lit night environment.

- **Philosophy:** "Flow over precision" - relaxing yet high-speed gameplay
- **Core Loop:** Infinite freeway driving, one-life scoring, instant crash reset to autopilot
- **Tagline:** *"Infinite neon freeway. One life. Flow or crash."*

## Tech Stack

| Layer | Technology |
|-------|------------|
| Engine | Unity 2023 LTS with DOTS 1.0+ (ECS) |
| Rendering | HDRP + Entities.Graphics |
| Language | C# |
| Physics | Custom impulse-based (Unity.Physics) |
| Performance | Unity.Burst for hot paths |
| Math | Unity.Mathematics |
| Build | PowerShell/Batch scripts, Inno Setup |
| CI/CD | GitHub Actions |

## Project Structure

```
src/
├── Components/          # ECS components (60+ types)
│   ├── Core/           # WorldTransform, Velocity, PreviousTransform
│   ├── Vehicle/        # PlayerInput, Autopilot, SteeringState, DriftState
│   ├── Damage/         # DamageState, ComponentHealth, ComponentFailureState
│   └── ...             # Scoring, Network, Lane, AI, World, Presentation, Audio, UI
├── Systems/            # ECS systems (70+ organized in groups)
│   ├── Simulation/     # Movement, physics, AI, collision, damage
│   ├── Presentation/   # Camera, rendering, particles, post-processing
│   ├── Audio/          # Audio playback and mixing
│   ├── UI/             # HUD, menus, performance stats
│   ├── Network/        # Multiplayer, ghost racing, leaderboards
│   └── World/          # City generation, LOD, lighting
├── Tags/               # Entity tag components (20+)
├── Buffers/            # Dynamic buffer types (10+)
├── Config/             # GameConstants, GameplayConfig, VisualConfig
├── Input/              # Input management, wheel support
├── Rendering/          # Raytracing, post-processing, city skyline
├── Audio/              # Audio manager, clip collections
├── Save/               # Save/load system
├── UI/                 # UI controllers and canvas
├── Editor/             # Setup wizard, editor tools
└── Utilities/          # SplineUtilities, logging, validation

specs/                  # Technical specification documents (11 files)
build/                  # Build scripts (build.ps1, build.bat, installer.iss)
```

## Build & Run

```bash
# Build game only
./build/build.bat

# Build game + create installer
./build/build.bat --installer

# PowerShell with options
./build/build.ps1 -CreateInstaller -CleanBuild
```

**Output:**
- Game: `Build/Windows/Nightflow.exe`
- Installer: `Installer/Nightflow_Setup_<version>.exe`

## Architecture

### ECS Pattern

- **Entities:** Pure IDs without data
- **Components:** Immutable data structures (`IComponentData`)
- **Systems:** Query components and apply logic
- **Tags:** Lightweight query filters (e.g., `PlayerVehicleTag`)

### System Execution Order (60 ticks/sec)

1. **Simulation Group:** Input → Autopilot → Steering → Lane Magnetism → Movement → Collision → Damage → Track Generation → Traffic AI → Scoring
2. **Presentation Group:** Camera → Rendering → Particles → Post-Processing → Audio → UI
3. **Network/World Groups:** As needed

### Key Patterns

- **Burst Compilation:** Hot paths marked with `[BurstCompile]`
- **Centralized Constants:** All magic numbers in `GameConstants.cs`
- **Configuration Objects:** `GameplayConfig`, `VisualConfig`, `AudioConfig`
- **Two-Phase Damage:** Zone-based damage → Component health degradation

## Code Conventions

### Naming

```csharp
// Systems end with "System"
public partial struct VehicleMovementSystem : ISystem

// Components are nouns
public struct PlayerInput : IComponentData
public struct DriftState : IComponentData

// Tags end with "Tag"
public struct PlayerVehicleTag : IComponentData

// Constants: ALL_CAPS_SNAKE_CASE
public const float LANE_WIDTH = 3.6f;
```

### Namespaces

```csharp
namespace Nightflow.Components
namespace Nightflow.Systems
namespace Nightflow.Config
```

### System Template

```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(InputSystem))]
public partial struct MySystem : ISystem
{
    public void OnCreate(ref SystemState state) { }
    public void OnUpdate(ref SystemState state) { }
    public void OnDestroy(ref SystemState state) { }
}
```

### Documentation

- XML comments on all public types and methods
- 80-char separator lines for section headers in comments
- Heavy inline documentation

## Key Files

**Architecture:**
- `src/Components/Core/TransformComponents.cs` - Core transform data
- `src/Components/Vehicle/VehicleComponents.cs` - Vehicle state
- `src/Tags/EntityTags.cs` - Entity categorization
- `src/Config/GameConstants.cs` - Central constants reference

**Gameplay:**
- `src/Systems/Simulation/InputSystem.cs` - Input handling
- `src/Systems/Simulation/VehicleMovementSystem.cs` - Physics & drift
- `src/Systems/Simulation/SteeringSystem.cs` - Lane following
- `src/Systems/Simulation/TrackGenerationSystem.cs` - Procedural generation

**Rendering:**
- `src/Systems/Presentation/WireframeRenderSystem.cs` - Visual style
- `src/Systems/Presentation/RaytracingSystem.cs` - Dynamic reflections
- `src/Systems/Presentation/CameraSystem.cs` - Chase camera

**Configuration:**
- `src/Config/GameplayConfig.cs` - Tunable gameplay parameters
- `src/Input/InputBindingManager.cs` - Input rebinding

## Documentation

- `README.md` - Project overview and quick start
- `SPEC-SHEET.md` - Master specification index
- `CHANGELOG.md` - Version history
- `specs/` folder - 11 detailed technical specifications:
  - `01-architecture.md` - ECS design
  - `02-vehicle-systems.md` - Movement, lane magnetism, drift
  - `03-track-generation.md` - Procedural generation with Hermite splines
  - `04-traffic-ai.md` - Lane decisions, emergency vehicles
  - `05-hazards-damage.md` - Damage types, impulse physics
  - `06-camera-rendering.md` - Visual style, raytracing
  - `07-audio.md` - Audio layers, spatial audio
  - `08-scoring-progression.md` - Score formula, difficulty scaling
  - `09-ui-systems.md` - HUD, autopilot, replay modes
  - `10-parameters.md` - Complete tuning values
  - `11-roadmap.md` - MVP phases and roadmap

## Common Tasks

### Adding a New Component

1. Create in `src/Components/{Domain}/` with appropriate naming
2. Use `IComponentData` interface
3. Add XML documentation
4. Reference constants from `GameConstants.cs`

### Adding a New System

1. Create in `src/Systems/{Group}/`
2. Implement `ISystem` interface
3. Add `[UpdateInGroup]` and ordering attributes
4. Mark with `[BurstCompile]` for hot paths
5. Use `SystemAPI.Query<>` for entity iteration

### Modifying Game Constants

Edit `src/Config/GameConstants.cs` - never use magic numbers directly in systems.

### Adding a New Tag

Create in `src/Tags/EntityTags.cs` as empty struct implementing `IComponentData`.

## Critical Rules

1. **No Magic Numbers:** All constants go in `GameConstants.cs`
2. **Min Forward Speed:** Vehicle must never stall (8 m/s minimum) - spins can't stop the car
3. **No Scene Reloads:** Continuous gameplay loop, reset via entity state only
4. **ECB Disposal:** Always dispose `EntityCommandBuffer` after playback
5. **Burst Safety:** No managed types in Burst-compiled code

## Version

Current: 0.1.0-alpha (January 2026)
