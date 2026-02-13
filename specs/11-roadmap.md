# Development Roadmap

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## MVP Development Order

### Phase 1: Core Movement
1. Vehicle movement + lane magnetism + basic spline freeway

### Phase 2: World Generation
2. Procedural generation (straights/curves) + traffic AI

### Phase 3: Advanced Controls
3. Lane change, handbrake drift, forward constraint

### Phase 4: Hazard Loop
4. Hazards, impulses, damage, crash loop

### Phase 5: Emergency Systems
5. Emergency vehicles + avoidance + off-screen signaling

### Phase 6: Scoring
6. Scoring + risk bonuses

### Phase 7: Presentation
7. Camera + audio layers

### Phase 8: Visual Style
8. Wireframe rendering + dynamic lighting

### Phase 9: Flow Systems
9. Autopilot + UI overlay

### Phase 10: Replay
10. Replay/ghost system

### Phase 11: Polish
11. Polish (tunnels, overpasses, forks, ghosts)

---

## Implemented Features (Post-MVP)

### Phase 2 Damage - COMPLETE
- [x] Soft-body deformation system
- [x] Component failures (suspension, steering, tires, engine, transmission)
- [x] Visual mesh deformation on impact (enhanced)
- [x] Progressive handling degradation per component
- [x] Swap `DamageEvaluationSystem` with soft-body implementation
- [x] ComponentHealth tracking system
- [x] ComponentFailureSystem with cascade failure detection
- [x] Suspension camera shake effects

### Reflections & SSR - COMPLETE
> **Note:** Implementation uses screen-space reflections (SSR) and distance-based light bounce estimation. Hardware raytracing (DXR/BVH) is not implemented and would be a future enhancement.

- [x] Distance-based headlight reflections on wet roads (SSR)
- [x] Emergency vehicle light bouncing off wet roads (distance-based estimation)
- [x] Tunnel light bounce and reflections (distance-based estimation)
- [x] ReflectionSystem with configurable SSR quality levels
- [ ] Hardware raytracing support (future enhancement, not currently planned)

### Multiplayer - IN PROGRESS (Deferred to v0.3.0)
> **Note:** These systems are architectural scaffolding. No network transport layer or backend services exist. All server I/O is marked as "external service" in code.

- [ ] Network replication via input logs + deterministic seeds (~70% — framework only, no transport layer)
- [ ] Ghost racing (async multiplayer) (~40% — spawn logic is placeholder)
- [ ] Live spectator mode with 7 camera modes (~95% — camera logic complete, untestable without multiplayer)
- [ ] Leaderboard integration with multiple categories (~30% — framework only, no backend)
- [ ] Replicate ECS state deltas across network (no transport layer exists)
- [ ] Network state synchronization with prediction (framework only, no server)

### Full City - COMPLETE
- [x] Populate distant silhouettes with actual geometry
- [x] Procedural building generation
- [x] Dynamic city lights based on time/distance
- [x] LOD system for distant geometry (256 buildings, 512 impostors)
- [x] City skyline renderer
- [x] Star field and moon rendering

### Input System - COMPLETE
- [x] Full input rebinding support
- [x] Logitech wheel SDK integration
- [x] Force feedback effects
- [x] Configurable deadzones

### Challenge System - COMPLETE (Deferred to v0.2.0)
> **Note:** Fully implemented but deferred until core single-player gameplay loop is validated through playtesting.

- [x] Daily challenge generation
- [x] Challenge-specific leaderboards
- [x] Adaptive difficulty system

---

## Architecture Highlights

The ECS architecture supports clean upgrades:

| Feature | Status | Implementation |
|---------|--------|----------------|
| BeamNG-level damage | Complete | Soft-body with spring-damper physics |
| Reflections / SSR | Partial | Screen-space reflections + distance-based light bounce estimation |
| Multiplayer | Deferred (v0.3.0) | Framework only — no transport layer or backend services |
| Full City | Complete | GPU-light procedural buildings with aggressive LOD |
| Replay system | Complete | Input log recording with deterministic playback |

---

## Codebase Statistics

| Metric | Count |
|--------|-------|
| C# Files | 131 |
| Component Files | 21 |
| Component Types | 60+ |
| Systems | 70+ |
| Tags | 20+ |
| Buffer Types | 10+ |

---

## System Organization

### Simulation Systems (18)
- Input, Autopilot, Steering, Lane Magnetism, Movement
- Collision, Impulse, Damage, Component Failure, Crash
- Track Generation, Traffic AI, Emergency Vehicles, Hazards
- Scoring, Risk Events, Replay Recording/Playback
- Adaptive Difficulty, Daily Challenge

### Presentation Systems (25)
- Camera, Wireframe Render, Procedural Mesh (Road, Vehicle, Hazard, Tunnel, Overpass, Light)
- Reflections/SSR, Headlight, Lighting, Particles (Tire Smoke, Sparks, Speed Lines)
- Crash Flash, Post-Processing, Terminal Rendering
- Ambient Cycle, City Skyline, Ghost Render, Offscreen Signal
- UI System

### Audio Systems (6)
- Engine, Collision, Siren, Ambient, Music, UI Audio

### UI Systems (5)
- Screen Flow, HUD Update, Menu Navigation, Performance Stats, Warning Indicator

### Network Systems (4) — Deferred to v0.3.0
- Network Replication, Ghost Racing, Spectator, Leaderboard (framework only)

### World Systems (3)
- City Generation, City LOD, City Lighting

### Core Systems (4)
- Game Bootstrap, World Initialization, Game State, Game Mode

---

## Future Considerations

### Performance Optimization
- Burst compilation for all hot paths
- Job system parallelization
- Memory pooling for entities

### Platform Expansion
- Console controller profiles
- Steam Deck verification
- Mobile consideration (simplified visuals)

### Content Expansion
- Additional vehicle types
- Weather effects
- Time of day variations
- Additional track environments
