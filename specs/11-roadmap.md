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

## Post-MVP TODO

### Phase 2 Damage ✓
- [x] Soft-body deformation system
- [x] Component failures (suspension, steering, tires, engine, transmission)
- [x] Visual mesh deformation on impact (enhanced)
- [x] Progressive handling degradation per component
- [x] Swap `DamageEvaluationSystem` with soft-body implementation
- [x] ComponentHealth tracking system
- [x] ComponentFailureSystem with cascade failure detection
- [x] Suspension camera shake effects

### Raytracing ✓
- [x] Full RT for dynamic headlight reflections
- [x] Emergency vehicle light bouncing off wet roads
- [x] Tunnel light bounce and reflections
- [x] Screen-space fallback for non-RT hardware
- [x] RaytracingSystem with SSR fallback integration

### Multiplayer ✓
- [x] Network replication via input logs + deterministic seeds
- [x] Ghost racing (async multiplayer)
- [x] Live spectator mode
- [x] Leaderboard integration
- [x] Replicate ECS state deltas across network

### Full City
- [ ] Populate distant silhouettes with actual geometry
- [ ] Procedural building generation
- [ ] Dynamic city lights based on time/distance
- [ ] LOD system for distant geometry

---

## Scalability Architecture Notes

The ECS architecture supports clean upgrades:

- **BeamNG-level damage:** ✓ Swapped to soft-body implementation (spring-damper physics)
- **Raytracing:** ✓ RaytracingSystem with headlight/emergency/tunnel reflections + SSR fallback
- **Multiplayer:** ✓ Input-based replication + ghost racing + spectator mode + leaderboards
- **Replay system:** Record component diffs for perfect playback
