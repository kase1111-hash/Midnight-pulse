# Traffic & AI Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## Traffic AI Lane-Change Decision

Continuous lane desirability scoring — cars "slide downhill" in a desirability landscape.

### Lane Candidates

For current lane index i, candidates are:
```
L = {i-1, i, i+1}
```

Filtered by:
- Lane existence
- Merge legality
- Barrier presence

### Lane Score Function
```
S_i = w_s·S_speed + w_d·S_density + w_e·S_emergency + w_h·S_hazard + w_p·S_player + w_m·S_merge
```

All terms normalized to [0, 1].

### Score Terms

| Term | Formula | Description |
|------|---------|-------------|
| Speed Advantage | `S_speed = clamp(v_i/v_t, 0, 1)` | Prefer lanes allowing target speed |
| Density | `S_density = e^(-k_d × n_i)` | Avoid crowded lanes |
| Emergency Pressure | `S_emergency = 1 - u` | Avoid emergency vehicle lanes |
| Hazard Avoidance | `S_hazard = clamp(d_h/d_safe, 0, 1)` | Avoid hazards ahead |
| Player Proximity | `S_player = clamp(d_p/w_lane, 0.3, 1)` | Avoid crowding player |
| Merge Logic | `S_merge = l_valid × (1 - d_m/d_merge)` | Early, smooth merges |

### Decision Hysteresis
```
Change allowed only if: ΔS > θ (threshold = 0.15)
```

Prevents jitter.

### Commitment Lock
```
Lock time = 1.2s during lane change
```

Prevents mid-change dithering.

### Default Weights

| Weight | Value |
|--------|-------|
| Speed | 0.35 |
| Density | 0.25 |
| Emergency | 0.4 |
| Hazard | 0.3 |
| Player | 0.15 |
| Merge | 0.3 |
| Threshold | 0.15 |
| Lock time | 1.2 s |

---

## Emergency Vehicle System

### Detection Space (Rear Influence Cone)
```
d_f = D · F (behind if < 0)
d_l = D · R (lateral offset)

Detection if:
  d_f < 0 AND |d_l| < w_detect AND |d_f| < d_max
```

### Urgency Scalar
```
u = clamp(1 - |d_f|/d_max, 0, 1)
```

This single scalar drives:
- Light intensity
- Audio volume
- Steering pressure
- Scoring penalties

### Avoidance Offset
```
dir = -sign(d_l)
x_avoid = dir × k_a × u × w

Player override:
  m_player = clamp(1 - |s|, 0.3, 1)
  x_avoid *= m_player
```

### Escalation Logic
```
If u > 0.6 AND time > 1.5s AND no lane change:
  - Emergency initiates aggressive overtake
  - Flash rate increases
  - Audio spikes
  - Score multiplier decays: M ← M × (1 - 0.1 × u × Δt)
```

No instant fail.

### Collision Rules (Fairness)

Only crash if:
- Player is braking
- OR speed < threshold
- OR vehicle is already damaged

Otherwise:
- Heavy shake
- Forced lane displacement
- Score ends

Emergency vehicles should feel invincible, not lethal.

### Default Parameters

| Parameter | Value |
|-----------|-------|
| Detection distance | 120 m |
| Detection width | 6-8 m |
| Avoid strength | 0.8 |
| Warning time | 1.5 s |
| Player min override | 0.3 |
| Light intensity boost | ×3 |

---

## Traffic Yielding Behavior

```
If emergency urgency u > 0.7:
  v_f ← v_f × (1 - 0.3u)
  Prefer outermost lane
```

Traffic yields and clears wave naturally — emergent behavior with no extra AI logic.

---

## Emergent Behaviors

The scoring system produces these behaviors "for free":
- Natural lane waves
- Emergency clearing corridors
- Traffic bunching before hazards
- Player gets breathing room
- No AI cheating
- Fully deterministic replay
