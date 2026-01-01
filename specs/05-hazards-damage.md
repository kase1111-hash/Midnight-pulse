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
- Component failures add depth without instant fails

---

## Hazard Classification

Each hazard has two orthogonal properties:

| Hazard | Severity | Mass | Type | Damage |
|--------|----------|------|------|--------|
| Loose tire | 0.2 | 0.1 | Cosmetic | 0.15 |
| Road debris | 0.4 | 0.3 | Mechanical | 0.10 |
| Construction cone | 0.3 | 0.2 | Cosmetic | 0.05 |
| Barrier | 0.9 | 0.9 | Lethal | 0.25 |
| Crashed car | 1.0 | 1.0 | Lethal | 0.40 |

---

## Collision Frame Setup

At collision moment:
- Vehicle velocity: V
- Hazard normal: N (unit)

---

## Impulse Calculation

### Impact Speed
```
v_impact = max(0, -V dot N)
```

Glancing contacts ignored automatically.

### Impulse Magnitude
```
J = k_i x v_impact x (0.5 + Severity)
```

Speed-weighted, not mass-simulated. Ensures:
- High speed = meaningful hit
- Small junk never stops you cold

### Impulse Decomposition
```
I = J x N
I_f = I dot F (forward)
I_l = I dot R (lateral)
```

---

## Velocity Response

### Lateral Response (Always Applies)
```
v_l <- v_l + I_l / m_virtual
```

Creates:
- Kick sideways
- Lane destabilization
- Recovery via magnetism

### Forward Response (Clamped)
```
v_f <- v_f - |I_f| / m_virtual
v_f <- max(v_f, v_min)
```

This is what keeps runs alive.

### Yaw Kick
```
delta_psi_dot = k_y x I_l / (v_f + epsilon)
```

Applied instantly for visual drama.

---

## Damage Accumulation (Phase 1)

Damage is energy-based, not collision count.

### Damage Energy
```
E_d = k_d x v_impact^2 x Severity
```

### Directional Distribution

Let contact normal project into vehicle space with weights w_front, w_rear, w_left, w_right (sum to 1):

```
Damage.Front += E_d x w_front
Damage.Left += E_d x w_left
Damage.Rear += E_d x w_rear
Damage.Right += E_d x w_right
Damage.Total += E_d
```

---

## Handling Degradation (Phase 1)

Handling penalties are continuous, not binary.

| Effect | Formula |
|--------|---------|
| Steering Response | `k_steer <- k_steer x (1 - 0.4 x D_front)` |
| Magnetism Reduction | `omega <- omega x (1 - 0.5 x D_side)` |
| Drift Stability Loss | `k_slip <- k_slip x (1 + 0.6 x D_rear)` |

The car becomes sloppier and harder to recover, but still driveable.

---

## Component Health System (Phase 2)

### Components

| Component | Initial Health | Failure Threshold |
|-----------|---------------|-------------------|
| Suspension | 1.0 | 0.1 |
| Steering | 1.0 | 0.1 |
| Tires | 1.0 | 0.1 |
| Engine | 1.0 | 0.1 |
| Transmission | 1.0 | 0.1 |

### Damage Transfer Ratios

| Damage Zone | Component | Ratio |
|-------------|-----------|-------|
| Front | Steering | 0.8 |
| Rear | Transmission | 0.6 |
| Left/Right | Suspension | 0.5 |
| Total | Engine | 0.3 |
| Any Impact | Tires | 0.4 |

### Health Degradation
```
ComponentHealth[component] -= E_d x transfer_ratio
```

### Failure Effects

| Component | Effect When Failed |
|-----------|-------------------|
| Suspension | Heavy camera shake, reduced handling stability |
| Steering | Complete loss of steering control |
| Tires | Severe magnetism reduction, grip loss |
| Engine | Limp mode (50% acceleration), max speed reduced |
| Transmission | Sluggish speed changes, drift recovery impaired |

### Cascade Failure
```
If FailureCount >= 3:
  Trigger crash (cascade failure)

If Steering OR Suspension failed:
  Trigger crash (critical component failure)
```

---

## Soft-Body Deformation

### Deformation Model
```
CurrentDeformation[zone] -> TargetDeformation[zone]

TargetDeformation = Damage[zone] x DeformationScale

Interpolation: Spring-damper system
  SpringConstant = 8.0
  Damping = 0.7
```

### Visual Application
- Mesh vertex displacement based on zone
- Progressive crumpling effect
- Maintains wireframe integrity

---

## Crash Conditions

A crash occurs ONLY if one of the following is true:

### A. Lethal Hazard + Speed
```
Severity > 0.8 AND v_impact > v_crash (25 m/s)
```

### B. Structural Damage Exceeded
```
Damage.Total > D_max (100)
```

### C. Compound Failure
```
(|psi| > psi_fail) AND (v_f ~= v_min) AND (Damage.Total > 0.6 x D_max)
```

### D. Component Cascade Failure
```
FailureCount >= 3
```

### E. Critical Component Failure
```
Steering.Failed OR Suspension.Failed
```

**Note:** Spinning alone never causes crash.

---

## Crash Reasons

| Reason | Description |
|--------|-------------|
| LethalHazard | High-speed impact with barrier/crashed car |
| TotalDamage | Accumulated damage exceeded threshold |
| CompoundFailure | Spin + low speed + high damage |
| ComponentFailure | Critical or cascade component failure |

---

## Camera & Feedback Coupling

### Camera Shake Magnitude
```
S = clamp(E_d / E_ref, 0, 1)
```

### Screen Flash Intensity
```
F = 0.6 x Severity
```

### Force Feedback (Wheel)
- Impact jolt proportional to impulse
- Direction matches contact normal

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
| Yaw kick scale (k_y) | 0.5 |
| Failure threshold | 0.1 |

### Handling Degradation Coefficients

| Effect | Coefficient |
|--------|-------------|
| Front damage -> steering | 0.4 |
| Side damage -> magnetism | 0.5 |
| Rear damage -> slip | 0.6 |

### Component Damage Ratios

| Transfer | Ratio |
|----------|-------|
| Front -> Steering | 0.8 |
| Rear -> Transmission | 0.6 |
| Side -> Suspension | 0.5 |
| Total -> Engine | 0.3 |
| Impact -> Tires | 0.4 |
