# Nightflow

A **synthwave driving game** featuring procedural night highway generation for an endless night drive experience. This **atmospheric driving** game combines lo-fi aesthetics with high-speed gameplay, creating a **relaxing driving game** that's as much about flow as it is about speed.

> **Tagline:** *Infinite neon freeway. One life. Flow or crash.*

**Genre:** Endless **procedural driving** / **night driving game** (flow runner)
**Core Inspiration:** Subway Surfers x BeamNG x OutRun x high-speed night aesthetics
**Engine:** Unity DOTS 1.0+ (HDRP/Entities.Graphics)
**Version:** 0.1.0-alpha (January 2026)

---

## Core Pillars

| Pillar | Description |
|--------|-------------|
| **Flow Over Precision** | Smooth, forgiving controls with lane magnetism and autopilot respite |
| **Speed = Score** | Faster driving multiplies score; braking/stopping kills multiplier |
| **Visual Rhythm** | Wireframe world, dynamic night lighting, emergency strobes, tunnels/overpasses |
| **One Continuous Loop** | Crash -> instant reset -> autopilot -> resume; no loading, no hard restarts |

---

## Gameplay

Experience the ultimate **endless highway game** - a **chill driving experience** through neon-lit procedural highways.

1. Drive endlessly on procedurally generated freeway
2. Steer smoothly between lanes, speed up/down, handbrake drift
3. Avoid/pass traffic, hazards, emergency vehicles
4. Small hits damage vehicle -> handling degradation -> component failures
5. Large hit or total failure -> crash -> fade -> score summary -> autopilot reset -> resume

Whether you're looking for a **night driving simulator** to unwind or a **vaporwave driving simulator** to test your reflexes, Nightflow delivers both.

**Win Condition:** Highest score/distance before debilitating crash.

**Critical Rule:** No scene reloads. Ever.

---

## Features

| Feature | Description |
|---------|-------------|
| **Raytracing** | Dynamic headlight reflections, emergency light bouncing, SSR fallback |
| **Multiplayer** | Ghost racing (async), live spectator mode (7 camera modes), leaderboards |
| **Procedural City** | GPU-light buildings with aggressive LOD (256 buildings, 512 impostors) |
| **Advanced Damage** | Component-level failures (suspension, steering, tires, engine, transmission) |
| **Soft-Body Deformation** | Spring-damper physics for visual mesh deformation |
| **Force Feedback** | Logitech wheel support with dynamic force feedback |
| **Daily Challenges** | Procedurally generated challenges with leaderboards |
| **Adaptive Difficulty** | Skill-based scaling for traffic, hazards, and emergency vehicles |

---

## Controls

| Input | Function |
|-------|----------|
| Analog Steer | Smoothed, lane-based steering with magnetism |
| Throttle | Speed up (accelerate) |
| Brake | Slow down (ends active scoring) |
| Handbrake | Drift/spin (maintains forward velocity >= 8 m/s) |

**Wheel Support:** Full Logitech SDK integration with force feedback effects.

---

## Documentation

For complete technical specifications, see the [Master Spec Sheet](SPEC-SHEET.md).

The specs are organized into focused documents:

| # | Document | Description |
|---|----------|-------------|
| 01 | [Architecture](specs/01-architecture.md) | ECS entities, components, systems, execution order |
| 02 | [Vehicle Systems](specs/02-vehicle-systems.md) | Movement, lane magnetism, drift/yaw, component health |
| 03 | [Track Generation](specs/03-track-generation.md) | Hermite splines, forks, tunnels, overpasses |
| 04 | [Traffic & AI](specs/04-traffic-ai.md) | Lane decisions, emergency vehicles, yielding |
| 05 | [Hazards & Damage](specs/05-hazards-damage.md) | Impulse physics, component failures, cascade damage |
| 06 | [Camera & Rendering](specs/06-camera-rendering.md) | Visual style, raytracing, city generation |
| 07 | [Audio](specs/07-audio.md) | Layers, spatial audio, reverb zones |
| 08 | [Scoring & Progression](specs/08-scoring-progression.md) | Score formula, difficulty scaling, challenges |
| 09 | [UI Systems](specs/09-ui-systems.md) | HUD, autopilot, replay, spectator modes |
| 10 | [Parameters](specs/10-parameters.md) | Complete tuning values reference |
| 11 | [Roadmap](specs/11-roadmap.md) | MVP phases, implemented features |

---

## Project Structure

```
src/
├── Components/         ECS component definitions
│   ├── Core/           Transform, velocity components
│   ├── Vehicle/        Player input, autopilot, steering, drift state
│   ├── Damage/         Zone damage, component health, soft-body
│   ├── Scoring/        Score session, risk multiplier
│   ├── Network/        Multiplayer, ghost racing, spectator, leaderboards
│   ├── Lane/           Lane following, splines
│   ├── AI/             Traffic AI components
│   ├── World/          City building components
│   ├── Presentation/   Rendering, particles, raytracing
│   ├── Audio/          Audio state components
│   ├── Signaling/      Off-screen warning signals
│   ├── UI/             UI state components
│   ├── Input/          Force feedback components
│   ├── Replay/         Recording/playback components
│   ├── Challenge/      Daily challenge components
│   └── Ambient/        Environment ambient components
├── Systems/            ECS system logic
│   ├── Simulation/     Movement, collision, damage, scoring
│   ├── Presentation/   Camera, rendering, particles, mesh generation
│   ├── Audio/          Engine, collision, siren, ambient, music
│   ├── UI/             HUD, screen flow, warnings, performance stats
│   ├── Network/        Ghost racing, spectator, leaderboards
│   ├── World/          City generation, LOD, lighting
│   ├── Core/           Game state, mode, world initialization
│   └── Initialization/ Bootstrap system
├── Tags/               Entity tag components
├── Buffers/            Dynamic buffer element data
├── Archetypes/         Entity archetype definitions
├── Input/              Input management, wheel support, force feedback
├── Rendering/          Raytracing, wireframe, post-processing, skyline
├── Audio/              Audio manager, clip collections
├── Config/             Game constants, gameplay config, visual/audio config
├── Save/               Save/load system
├── UI/                 UI controllers
├── Editor/             Setup wizard, editor tools
├── Materials/          Shader materials
├── Shaders/            Custom shaders
└── Utilities/          Spline math, helpers

specs/                  Technical specification documents
build/                  Build system and scripts
```

---

## Key Constants

| Parameter | Value | Purpose |
|-----------|-------|---------|
| `LaneWidth` | 3.6 m | Standard freeway lane width |
| `MinForwardSpeed` | 8 m/s | Minimum speed (spins never stall) |
| `MaxForwardSpeed` | 80 m/s | Speed cap |
| `MaxDamage` | 100 | Crash threshold |
| `SegmentLength` | 200 m | Track segment length |
| `SegmentsAhead` | 5 | Lookahead buffer |
| `SegmentsBehind` | 2 | Segments kept behind before culling |

---

## Quick Start

The project includes a one-click setup wizard. See `src/Editor/NightflowSetupWizard.cs` for automatic project configuration.

### Build Commands

```batch
# Build game only
build.bat

# Build game + create installer
build.bat --installer
```

See [Build System](build/README.md) for complete build documentation.

---

## Architecture Highlights

- **131 C# Files** across a modular ECS architecture
- **50+ Systems** organized by function (Simulation, Presentation, Audio, UI, Network, World)
- **21 Component Files** defining 60+ component types
- **Burst Compiled** hot paths for performance
- **Deterministic Simulation** for ghost racing and network replication
- **No Havok Dependency** - custom impulse-based physics

---

## Related Repositories

Part of the **Game Development** collection. Check out these connected projects:

| Repository | Description |
|------------|-------------|
| [Shredsquatch](https://github.com/kase1111-hash/Shredsquatch) | 3D first-person snowboarding infinite runner - a SkiFree spiritual successor |
| [Long-Home](https://github.com/kase1111-hash/Long-Home) | Atmospheric indie narrative game built with Godot |

---

## License

Copyright (c) 2025-2026 Kase Branham. All rights reserved.
