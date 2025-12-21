# Vehicle Systems Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## Movement Model

- Always moving forward unless braking
- Lane-based steering with analog offset
- Curvature eased with cubic interpolation

### Speed Tiers

| Tier | Multiplier | Effect |
|------|------------|--------|
| Cruise | 1.0× | Base speed, standard effects |
| Fast | 1.5× | Enhanced lighting, increased FOV |
| Boosted | 2.5× | Maximum effects, highest risk/reward |

---

## Lane Magnetism System

Critically damped spring toward lane centerline.

### Core Spring-Damper Equation
```
x_error = current_lateral - target_lateral
a_lat = m × (-ω² × x_error - 2ω × lateral_vel)
```

### Magnetism Modulation
```
m = m_input × m_auto × m_speed × m_handbrake

Where:
  m_input = 1 - |steer|              (no steering = full magnetism)
  m_auto = 1.5 if autopilot else 1.0
  m_speed = √(v/v_ref), clamped [0.75, 1.25]
  m_handbrake = 0.25 if engaged else 1.0
```

### Edge Force (Soft Constraint)
```
If |x| > w_soft (85% lane width):
  a_edge = -sign(x) × k_edge × x_edge²
```

### Default Parameters

| Parameter | Value |
|-----------|-------|
| ω (omega) | 8.0 |
| v_ref | 40 m/s |
| Max lateral speed | 6 m/s |
| Edge stiffness | 20 |
| Soft zone | 85% lane width |

---

## Lane Change & Merge System

Blended virtual spline via smoothstep on lateral offset. Same math used for player, traffic, autopilot, and geometric merges.

### Trigger Conditions
- Steering exceeds threshold: `|s| > 0.35`
- Steering direction matches lane direction
- Target lane exists and is not blocked
- Not already transitioning

### Transition Math (Smoothstep)
```
λ(t) = 3t² - 2t³

x(t) = (1-λ)·x_from + λ·x_to
```

### Speed-Aware Duration
```
T = clamp(T_base × (v/v_ref), T_min, T_max)

T_base = 0.6s
T_min = 0.45s
T_max = 1.0s
```

### Steering Attenuation During Transition
```
s_effective = s × (1 - λ)
```

### Emergency Abort
- Counter-steer > 0.7 in opposite direction triggers reversal
- Progress resets using: `Progress = Duration × (1-t)`

### Default Parameters

| Parameter | Value |
|-----------|-------|
| Steering trigger | 0.35 |
| Abort threshold | 0.7 |
| Base duration | 0.6 s |
| Lane width | 3.6 m |
| Fork magnetism | 70% |

---

## Drift, Yaw & Forward Constraint

### Forward-Velocity Constraint (Critical)
```
v_f ≥ v_min (~8 m/s)

If v_f < v_min:
  v_f ← v_min
  V ← v_f·F + v_l·R
```

This is why spins never stall the run.

### Yaw Dynamics (Explicit Torque Model)
```
ψ̈ = τ_steer + τ_drift - c_ψ·ψ̇

τ_steer = k_s × s × (v_f/v_ref)
τ_drift = k_d × sign(s) × √v_f  (if handbrake)
```

### Drift Slip Angle
```
β = ψ - arctan(v_l/v_f)

During handbrake:
  v_l ← v_l + k_slip × sin(β) × v_f × Δt
```

### Drift Recovery
```
τ_recover = -k_r × ψ  (when handbrake released)
```

### Default Parameters

| Parameter | Value |
|-----------|-------|
| Min forward speed | 8 m/s |
| Steering gain | 1.2 |
| Drift gain | 2.5 |
| Yaw damping | 0.8 |
| Max yaw rate | 6 rad/s |
| Slip gain | 1.1 |
| Drift magnetism | 0.3 |

---

## Handbrake Mechanics

- Reduces rear tire traction
- Allows drift and 180°-360° spins
- Forward velocity must be maintained
- If forward velocity drops below threshold → crash state
