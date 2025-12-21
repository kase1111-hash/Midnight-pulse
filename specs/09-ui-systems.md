# UI & Systems Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## HUD Elements

| Element | Description |
|---------|-------------|
| Speed | Current velocity |
| Multiplier | Active score modifier |
| Score | Running total |
| Damage | Zone indicators (bars) |

---

## Design Principles

- Transparent overlay (visible during autopilot)
- Minimal HUD
- No full-screen interruptions

---

## Pause System

- Pausing allowed
- 5-second cooldown before next pause
- Cooldown visible to player

---

## Crash Flow

1. Impact
2. Screen shake
3. Quick fade to black
4. Score summary (breakdown + save option)
5. Vehicle reset
6. Autopilot resumes instantly

No loading screens.

---

## Autopilot System

### Activation
- After crash
- After score save
- Player chooses to disengage controls

### Behavior
- Lane-following
- Medium speed
- Avoids hazards
- Maintains visual flow
- Menu overlay stays active

---

## Off-Screen Threat Signaling

### Visual Indicators

| Threat | Signal |
|--------|--------|
| Crashed vehicles ahead | Red/blue strobe leak at screen edges |
| Emergencies behind | Red/white strobe at screen edges |

### Implementation
```
Screen-space flare quads positioned by projected edge direction
Intensity ∝ urgency (inverse distance) × strobe pulse
Fade as threat enters view
```

### Light Signaling Math
```
Intensity: I = I₀ × (1 + 2u)
Strobe rate: f = f₀ + 4u
Light radius: r = r₀ × (1 + u)
```

Where `u` = urgency scalar (0-1)

---

## Replay & Ghost System

### Recording
```
Record: globalSeed + fixed-timestep input log
```

### Playback
```
Second PlayerVehicle entity driven by log (identical sim)
Deterministic via seeded PRNG and pure math
```

### Ghost Rendering
- Semi-transparent, non-colliding
- Optional trail effect
- Leaderboard validation via server re-simulation
