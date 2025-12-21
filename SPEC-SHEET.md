# Nightflow — Master Technical Specification

**Version:** 1.2
**Date:** December 2025
**Copyright:** © 2025 Kase Branham. All rights reserved.

---

> **Tagline:** *Infinite neon freeway. One life. Flow or crash.*

**Genre:** Endless procedural night-time freeway driving (flow runner)
**Core Inspiration:** Subway Surfers × BeamNG × OutRun × high-speed night aesthetics
**Target Platforms:** PC (primary), Console (stretch)
**Engine:** Unity DOTS 1.0+ (HDRP/Entities.Graphics) or Unreal Mass Entity (UE5)

---

## Core Design Pillars

| Pillar | Description |
|--------|-------------|
| **Flow Over Precision** | Smooth, forgiving controls with lane magnetism and autopilot respite |
| **Speed = Score** | Faster driving multiplies score; braking/stopping kills multiplier |
| **Visual Rhythm** | Wireframe world, dynamic night lighting, emergency strobes, tunnels/overpasses |
| **One Continuous Loop** | Crash → instant reset → autopilot → resume; no loading, no hard restarts |

---

## Controls

| Input | Function |
|-------|----------|
| **Analog Steer** | Smoothed, lane-based steering |
| **Throttle** | Speed up |
| **Brake** | Slow down (ends scoring) |
| **Handbrake** | Drift/spin (must maintain forward velocity) |

---

## Specification Documents

The complete technical specification is organized into focused documents:

| # | Document | Description |
|---|----------|-------------|
| 01 | [Architecture](specs/01-architecture.md) | ECS philosophy, entity archetypes, components, system execution order |
| 02 | [Vehicle Systems](specs/02-vehicle-systems.md) | Movement, lane magnetism, lane change, drift/yaw, handbrake |
| 03 | [Track Generation](specs/03-track-generation.md) | Hermite splines, procedural segments, forks, elevation |
| 04 | [Traffic & AI](specs/04-traffic-ai.md) | Traffic AI decisions, emergency vehicles, yielding behavior |
| 05 | [Hazards & Damage](specs/05-hazards-damage.md) | Hazard types, impulse physics, damage accumulation, crash conditions |
| 06 | [Camera & Rendering](specs/06-camera-rendering.md) | Camera dynamics, visual style, lighting, raytracing |
| 07 | [Audio](specs/07-audio.md) | Audio layers, spatial audio, event sounds, reverb zones |
| 08 | [Scoring & Progression](specs/08-scoring-progression.md) | Score formula, risk multipliers, difficulty scaling |
| 09 | [UI & Systems](specs/09-ui-systems.md) | HUD, autopilot, replay/ghost, off-screen signaling |
| 10 | [Parameters](specs/10-parameters.md) | Complete tuning values reference |
| 11 | [Roadmap](specs/11-roadmap.md) | MVP development phases, future scalability |

---

## Quick Reference

### Core Gameplay Loop

1. Player drives endlessly on procedurally generated freeway
2. Steer smoothly between lanes, speed up/down, handbrake drift
3. Avoid/pass traffic, hazards, emergency vehicles
4. Small hits damage vehicle → handling degradation
5. Large hit or total failure → crash → fade → score summary → autopilot reset → resume or save

### Win Condition

Highest score/distance before debilitating crash.

### Critical Rule

**No scene reloads. Ever.**

---

## System Execution Order (Quick View)

**Simulation:**
1. Input → 2. Autopilot → 3. Steering → 4. Lane Magnetism → 5. Movement → 6. Collision → 7. Damage → 8. Crash → 9. Track Gen → 10. Traffic AI → 11. Hazards → 12. Scoring → 13. Signaling

**Presentation:**
14. Camera → 15. Render → 16. Lighting → 17. Audio → 18. UI

---

## Key Formulas (Quick View)

### Lane Magnetism
```
a_lat = m × (-ω² × x_error - 2ω × lateral_vel)
```

### Lane Change (Smoothstep)
```
λ(t) = 3t² - 2t³
```

### Scoring
```
Score = Distance × Speed_Tier × (1 + RiskMultiplier)
```

### Forward Constraint
```
v_f ≥ v_min (8 m/s) — spins never stall
```

---

## Engine Parity

| Concept | Unity DOTS | Unreal Mass |
|---------|------------|-------------|
| Component | IComponentData | Fragment |
| System | ISystem | Processor |
| Archetype | EntityArchetype | EntityConfig |

---

*See individual spec documents for complete details.*
