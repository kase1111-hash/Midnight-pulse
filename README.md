# Nightflow

A procedural, endless, night-time freeway driving game focused on flow, speed, and light.

> **Tagline:** *Infinite neon freeway. One life. Flow or crash.*

**Genre:** Endless procedural night-time freeway driving (flow runner)
**Core Inspiration:** Subway Surfers × BeamNG × OutRun × high-speed night aesthetics
**Engine:** Unity DOTS 1.0+ (HDRP/Entities.Graphics)

---

## Core Pillars

| Pillar | Description |
|--------|-------------|
| **Flow Over Precision** | Smooth, forgiving controls with lane magnetism and autopilot respite |
| **Speed = Score** | Faster driving multiplies score; braking/stopping kills multiplier |
| **Visual Rhythm** | Wireframe world, dynamic night lighting, emergency strobes, tunnels/overpasses |
| **One Continuous Loop** | Crash → instant reset → autopilot → resume; no loading, no hard restarts |

---

## Gameplay

1. Drive endlessly on procedurally generated freeway
2. Steer smoothly between lanes, speed up/down, handbrake drift
3. Avoid/pass traffic, hazards, emergency vehicles
4. Small hits damage vehicle → handling degradation
5. Large hit or total failure → crash → fade → score summary → autopilot reset → resume

**Win Condition:** Highest score/distance before debilitating crash.

**Critical Rule:** No scene reloads. Ever.

---

## Controls

| Input | Function |
|-------|----------|
| Analog Steer | Smoothed, lane-based steering |
| Throttle | Speed up |
| Brake | Slow down (ends scoring) |
| Handbrake | Drift/spin (must maintain forward velocity) |

---

## Documentation

For complete technical specifications, see the [Master Spec Sheet](SPEC-SHEET.md).

The specs are organized into focused documents:

| # | Document | Description |
|---|----------|-------------|
| 01 | [Architecture](specs/01-architecture.md) | ECS entities, components, systems |
| 02 | [Vehicle Systems](specs/02-vehicle-systems.md) | Movement, lane magnetism, drift/yaw |
| 03 | [Track Generation](specs/03-track-generation.md) | Hermite splines, forks, elevation |
| 04 | [Traffic & AI](specs/04-traffic-ai.md) | Lane decisions, emergency vehicles |
| 05 | [Hazards & Damage](specs/05-hazards-damage.md) | Impulse physics, damage accumulation |
| 06 | [Camera & Rendering](specs/06-camera-rendering.md) | Visual style, lighting |
| 07 | [Audio](specs/07-audio.md) | Layers, spatial audio, reverb zones |
| 08 | [Scoring & Progression](specs/08-scoring-progression.md) | Score formula, difficulty scaling |
| 09 | [UI Systems](specs/09-ui-systems.md) | HUD, autopilot, replay |
| 10 | [Parameters](specs/10-parameters.md) | Complete tuning values |
| 11 | [Roadmap](specs/11-roadmap.md) | MVP development phases |

---

## Project Structure

```
src/
├── Components/    ECS component definitions
├── Systems/       ECS system logic (Simulation, Presentation, Audio, UI)
├── Tags/          Entity tags
├── Buffers/       Buffer element data
├── Input/         Input management & wheel support
├── Rendering/     Advanced rendering systems
├── Audio/         Audio management
├── Config/        Configuration system
├── Save/          Save system
├── UI/            UI controllers
├── Editor/        Editor tools & setup wizard
├── Materials/     Shader materials
├── Shaders/       Custom shaders
└── Archetypes/    Entity archetype definitions

specs/             Complete technical specifications
build/             Build system and scripts
```

---

## Quick Start

The project includes a one-click setup wizard. See `src/Editor/NightflowSetupWizard.cs` for automatic project configuration.

---

## License

Copyright © 2025 Kase Branham. All rights reserved.
