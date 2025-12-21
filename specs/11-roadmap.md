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

## Future Scalability

| Feature | Upgrade Path |
|---------|--------------|
| Phase 2 Damage | Swap to soft-body / component failure |
| Raytracing | Replace lighting system with full RT |
| Multiplayer | Replicate input logs + seeds |
| Full City | Populate distant silhouettes with geometry |

---

## Scalability Architecture Notes

The ECS architecture supports clean upgrades:

- **BeamNG-level damage:** Swap `DamageEvaluationSystem` with soft-body implementation
- **Raytracing:** Swap `LightingSystem` with RT implementation
- **Multiplayer:** Replicate ECS state deltas across network
- **Replay system:** Record component diffs for perfect playback
