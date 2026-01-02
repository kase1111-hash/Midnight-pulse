# Nightflow - Master Technical Specification

**Version:** 0.1.0-alpha
**Date:** January 2026
**Copyright:** (c) 2025-2026 Kase Branham. All rights reserved.

---

> **Tagline:** *Infinite neon freeway. One life. Flow or crash.*

**Genre:** Endless procedural night-time freeway driving (flow runner)
**Core Inspiration:** Subway Surfers x BeamNG x OutRun x high-speed night aesthetics
**Target Platforms:** PC (primary), Console (stretch)
**Engine:** Unity DOTS 1.0+ (HDRP/Entities.Graphics)

---

## Core Design Pillars

| Pillar | Description |
|--------|-------------|
| **Flow Over Precision** | Smooth, forgiving controls with lane magnetism and autopilot respite |
| **Speed = Score** | Faster driving multiplies score; braking/stopping kills multiplier |
| **Visual Rhythm** | Wireframe world, dynamic night lighting, emergency strobes, tunnels/overpasses |
| **One Continuous Loop** | Crash -> instant reset -> autopilot -> resume; no loading, no hard restarts |

---

## Controls

| Input | Function |
|-------|----------|
| **Analog Steer** | Smoothed, lane-based steering with magnetism assist |
| **Throttle** | Speed up (accelerate) |
| **Brake** | Slow down (ends scoring) |
| **Handbrake** | Drift/spin (must maintain forward velocity >= 8 m/s) |

**Wheel Support:** Logitech SDK integration with force feedback.

---

## Specification Documents

The complete technical specification is organized into focused documents:

| # | Document | Description |
|---|----------|-------------|
| 01 | [Architecture](specs/01-architecture.md) | ECS philosophy, entity archetypes, components, system execution order |
| 02 | [Vehicle Systems](specs/02-vehicle-systems.md) | Movement, lane magnetism, lane change, drift/yaw, component health |
| 03 | [Track Generation](specs/03-track-generation.md) | Hermite splines, procedural segments, forks, tunnels, overpasses |
| 04 | [Traffic & AI](specs/04-traffic-ai.md) | Traffic AI decisions, emergency vehicles, yielding behavior |
| 05 | [Hazards & Damage](specs/05-hazards-damage.md) | Hazard types, impulse physics, component failures, crash conditions |
| 06 | [Camera & Rendering](specs/06-camera-rendering.md) | Camera dynamics, visual style, raytracing, city generation |
| 07 | [Audio](specs/07-audio.md) | Audio layers, spatial audio, event sounds, reverb zones |
| 08 | [Scoring & Progression](specs/08-scoring-progression.md) | Score formula, risk multipliers, difficulty scaling, challenges |
| 09 | [UI & Systems](specs/09-ui-systems.md) | HUD, autopilot, replay/ghost, spectator modes, leaderboards |
| 10 | [Parameters](specs/10-parameters.md) | Complete tuning values reference |
| 11 | [Roadmap](specs/11-roadmap.md) | MVP development phases, implemented features |

---

## Advanced Features

### Raytracing
- Dynamic headlight reflections on wet roads
- Emergency vehicle light bouncing
- Tunnel light bounce and reflections
- Screen-space reflections (SSR) fallback for non-RT hardware
- Integrated RaytracingSystem with quality levels and max bounces

### Multiplayer
- Input-based network replication with deterministic seeds
- Ghost racing (async multiplayer) using recorded input logs
- Live spectator mode with 7 camera modes:
  - Follow Target, Cinematic, Overhead, Trackside, Free Cam, First Person, Chase
- Leaderboard integration with multiple categories:
  - High Score, Best Time, Longest Run, Max Speed, Total Distance, Weekly, Friends
- Network state synchronization with prediction and rollback

### Procedural City
- GPU-light building generation (max 256 buildings, 512 impostors)
- Aggressive LOD system: LOD0=50m, LOD1=150m, LOD2=400m, Cull=600m
- Dynamic window lights with flicker patterns
- Impostor billboards for distant geometry (4 vertices each)
- City skyline renderer with star field and moon

### Advanced Damage
- Two-phase damage system:
  - Phase 1: Zone-based damage (front/rear/left/right)
  - Phase 2: Component health degradation
- Component failures (suspension, steering, tires, engine, transmission)
- Progressive handling degradation per component:
  - Suspension failure: Camera shake, handling instability
  - Steering failure: Complete loss of control
  - Tire failure: Reduced grip and magnetism
  - Engine failure: Limp mode (50% acceleration)
  - Transmission failure: Reduced speed changes
- Cascade failure detection (3+ components = crash)
- Soft-body deformation with spring-damper physics

### Input & Accessibility
- Full input rebinding support
- Logitech wheel SDK integration
- Force feedback effects (impact, road feel, drift)
- Configurable deadzones
- Autopilot override on any player input

---

## Quick Reference

### Core Gameplay Loop

1. Player drives endlessly on procedurally generated freeway
2. Steer smoothly between lanes, speed up/down, handbrake drift
3. Avoid/pass traffic, hazards, emergency vehicles
4. Small hits damage vehicle -> handling degradation -> component failures
5. Large hit or total failure -> crash -> fade -> score summary -> autopilot reset -> resume or save

### Win Condition

Highest score/distance before debilitating crash.

### Critical Rule

**No scene reloads. Ever.**

---

## System Execution Order (Quick View)

**Simulation (60 ticks/second):**
1. Input -> 2. Autopilot -> 3. Steering -> 4. Lane Magnetism -> 5. Movement -> 6. Collision -> 7. Impulse -> 8. Damage -> 9. Component Failure -> 10. Crash -> 11. Track Gen -> 12. Traffic AI -> 13. Emergency Vehicles -> 14. Hazards -> 15. Scoring -> 16. Risk Events -> 17. Signaling -> 18. Replay Recording

**Presentation:**
19. Camera -> 20. Wireframe Render -> 21. Procedural Mesh -> 22. Raytracing / SSR -> 23. City LOD -> 24. Lighting -> 25. Particles -> 26. Post-Processing -> 27. Audio -> 28. UI

**Network (when multiplayer active):**
- Input Capture & Replication
- State Synchronization
- Prediction & Reconciliation
- Ghost Race Playback
- Spectator Camera Control
- Leaderboard Updates

---

## Key Formulas (Quick View)

### Lane Magnetism
```
a_lat = m x (-omega^2 x x_error - 2*omega x lateral_vel)
```

### Lane Change (Smoothstep)
```
lambda(t) = 3t^2 - 2t^3
```

### Scoring
```
Score = Distance x Speed_Tier x (1 + RiskMultiplier)
```

### Forward Constraint
```
v_f >= v_min (8 m/s) - spins never stall
```

### Component Health Degradation
```
Health[component] -= damage x sensitivity_ratio
Failure when Health < 0.1
```

---

## Key Constants

| Parameter | Value | Purpose |
|-----------|-------|---------|
| `LaneWidth` | 3.6 m | Standard freeway lane |
| `MinForwardSpeed` | 8 m/s | Never stalls (critical rule) |
| `MaxForwardSpeed` | 80 m/s | Speed cap |
| `MaxDamage` | 100 | Crash threshold |
| `SegmentLength` | 200 m | Track segment length |
| `SegmentsAhead` | 5 | Lookahead buffer |
| `SegmentsBehind` | 2 | Segments kept behind |
| `DefaultNumLanes` | 4 | Lanes per road |

---

## Architecture Overview

| Metric | Value |
|--------|-------|
| C# Files | 131 |
| Component Files | 21 (60+ component types) |
| Systems | 50+ |
| Tags | 20+ |
| Buffer Types | 10+ |

### System Groups

| Group | Systems |
|-------|---------|
| Simulation | Input, Autopilot, Steering, Lane Magnetism, Movement, Collision, Impulse, Damage, Crash, Track Gen, Traffic, Hazards, Scoring, Signaling, Replay |
| Presentation | Camera, Wireframe, Procedural Mesh, Raytracing, City, Lighting, Particles, Post-Processing, UI |
| Audio | Engine, Collision, Siren, Ambient, Music, UI Audio |
| Network | Replication, Ghost Racing, Spectator, Leaderboard |
| World | City Generation, LOD, Lighting |

---

## Engine Parity

| Concept | Unity DOTS | Unreal Mass |
|---------|------------|-------------|
| Component | IComponentData | Fragment |
| System | ISystem | Processor |
| Archetype | EntityArchetype | EntityConfig |

---

*See individual spec documents for complete details.*
