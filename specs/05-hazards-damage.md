# Hazards & Damage Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## Design Goals

- Small hazards = momentum + damage, not instant death
- Big hazards = crash if hit badly
- Damage degrades handling before ending run
- Speed matters more than mass
- Forward motion is preserved unless crash is explicit
- Works with wireframe MVP and future soft-body

---

## Hazard Classification

Each hazard has two orthogonal properties:

| Hazard | Severity | Mass | Type |
|--------|----------|------|------|
| Loose tire | 0.2 | 0.1 | Cosmetic |
| Road debris | 0.4 | 0.3 | Mechanical |
| Construction cone | 0.3 | 0.2 | Cosmetic |
| Barrier | 0.9 | 0.9 | Lethal |
| Crashed car | 1.0 | 1.0 | Lethal |

---

## Collision Frame Setup

At collision moment:
- Vehicle velocity: V
- Hazard normal: N (unit)

---

## Impulse Calculation

### Impact Speed
```
v_impact = max(0, -V · N)
```

Glancing contacts ignored automatically.

### Impulse Magnitude
```
J = k_i × v_impact × (0.5 + Severity)
```

Speed-weighted, not mass-simulated. Ensures:
- High speed = meaningful hit
- Small junk never stops you cold

### Impulse Decomposition
```
I = J × N
I_f = I · F (forward)
I_l = I · R (lateral)
```

---

## Velocity Response

### Lateral Response (Always Applies)
```
v_l ← v_l + I_l / m_virtual
```

Creates:
- Kick sideways
- Lane destabilization
- Recovery via magnetism

### Forward Response (Clamped)
```
v_f ← v_f - |I_f| / m_virtual
v_f ← max(v_f, v_min)
```

This is what keeps runs alive.

### Yaw Kick
```
Δψ̇ = k_y × I_l / (v_f + ε)
```

Applied instantly for visual drama.

---

## Damage Accumulation

Damage is energy-based, not collision count.

### Damage Energy
```
E_d = k_d × v_impact² × Severity
```

### Directional Distribution

Let contact normal project into vehicle space with weights w_front, w_rear, w_left, w_right (sum to 1):

```
Damage.Front += E_d × w_front
Damage.Left += E_d × w_left
Damage.Rear += E_d × w_rear
Damage.Right += E_d × w_right
Damage.Total += E_d
```

---

## Handling Degradation

Handling penalties are continuous, not binary.

| Effect | Formula |
|--------|---------|
| Steering Response | `k_steer ← k_steer × (1 - 0.4 × D_front)` |
| Magnetism Reduction | `ω ← ω × (1 - 0.5 × D_side)` |
| Drift Stability Loss | `k_slip ← k_slip × (1 + 0.6 × D_rear)` |

The car becomes sloppier and harder to recover, but still driveable.

---

## Crash Conditions

A crash occurs ONLY if one of the following is true:

### A. Lethal Hazard + Speed
```
Severity > 0.8 AND v_impact > v_crash
```

### B. Structural Damage Exceeded
```
Damage.Total > D_max
```

### C. Compound Failure
```
(|ψ| > ψ_fail) AND (v_f ≈ v_min) AND (Damage.Total > 0.6 × D_max)
```

**Note:** Spinning alone never causes crash.

---

## Camera & Feedback Coupling

### Camera Shake Magnitude
```
S = clamp(E_d / E_ref, 0, 1)
```

### Screen Flash Intensity
```
F = 0.6 × Severity
```

---

## Default Parameters

| Parameter | Value |
|-----------|-------|
| Impulse scale (k_i) | 1.2 |
| Damage scale (k_d) | 0.04 |
| Virtual mass | 1200 |
| Crash speed | 25 m/s |
| Max damage | 100 |
| Min forward speed | 8 m/s |
