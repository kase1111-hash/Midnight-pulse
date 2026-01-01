# Vehicle Systems Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## Movement Model

- Always moving forward unless braking
- Lane-based steering with analog offset
- Curvature eased with cubic interpolation

### Speed Tiers

| Tier | Multiplier | Threshold | Effect |
|------|------------|-----------|--------|
| Cruise | 1.0x | < 30 m/s | Base speed, standard effects |
| Fast | 1.5x | 30-50 m/s | Enhanced lighting, increased FOV |
| Boosted | 2.5x | > 50 m/s | Maximum effects, highest risk/reward |

---

## Lane Magnetism System

Critically damped spring toward lane centerline.

### Core Spring-Damper Equation
```
x_error = current_lateral - target_lateral
a_lat = m x (-omega^2 x x_error - 2*omega x lateral_vel)
```

### Magnetism Modulation
```
m = m_input x m_auto x m_speed x m_handbrake x m_damage

Where:
  m_input = 1 - |steer|              (no steering = full magnetism)
  m_auto = 1.5 if autopilot else 1.0
  m_speed = sqrt(v/v_ref), clamped [0.75, 1.25]
  m_handbrake = 0.25 if engaged else 1.0
  m_damage = 1 - 0.5 x D_side        (side damage reduces magnetism)
```

### Edge Force (Soft Constraint)
```
If |x| > w_soft (85% lane width):
  a_edge = -sign(x) x k_edge x x_edge^2
```

### Default Parameters

| Parameter | Value |
|-----------|-------|
| omega | 8.0 |
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
lambda(t) = 3t^2 - 2t^3

x(t) = (1-lambda) x x_from + lambda x x_to
```

### Speed-Aware Duration
```
T = clamp(T_base x (v/v_ref), T_min, T_max)

T_base = 0.6s
T_min = 0.45s
T_max = 1.0s
```

### Steering Attenuation During Transition
```
s_effective = s x (1 - lambda)
```

### Emergency Abort
- Counter-steer > 0.7 in opposite direction triggers reversal
- Progress resets using: `Progress = Duration x (1-t)`

### Default Parameters

| Parameter | Value |
|-----------|-------|
| Steering trigger | 0.35 |
| Abort threshold | 0.7 |
| Base duration | 0.6 s |
| Min duration | 0.45 s |
| Max duration | 1.0 s |
| Lane width | 3.6 m |
| Fork magnetism | 70% |

---

## Drift, Yaw & Forward Constraint

### Forward-Velocity Constraint (Critical)
```
v_f >= v_min (~8 m/s)

If v_f < v_min:
  v_f <- v_min
  V <- v_f x F + v_l x R
```

This is why spins never stall the run.

### Yaw Dynamics (Explicit Torque Model)
```
psi_ddot = tau_steer + tau_drift - c_psi x psi_dot

tau_steer = k_s x s x (v_f/v_ref)
tau_drift = k_d x sign(s) x sqrt(v_f)  (if handbrake)
```

### Drift Slip Angle
```
beta = psi - arctan(v_l/v_f)

During handbrake:
  v_l <- v_l + k_slip x sin(beta) x v_f x dt
```

### Drift Recovery
```
tau_recover = -k_r x psi  (when handbrake released)
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
- Allows drift and 180-360 degree spins
- Forward velocity must be maintained
- If forward velocity drops below threshold -> crash state

---

## Component Health System

Vehicle components degrade independently based on damage zone and impact type.

### Components
| Component | Affects | Failure Effect |
|-----------|---------|----------------|
| Suspension | Handling stability | Camera shake, instability |
| Steering | Steering responsiveness | Complete loss of control |
| Tires | Grip and magnetism | Reduced lane holding |
| Engine | Acceleration and speed | Limp mode (50% power) |
| Transmission | Speed changes, drift recovery | Sluggish response |

### Health Degradation
```
Health[component] -= damage x sensitivity_ratio

Sensitivity Ratios:
  Front damage -> Steering: 0.8
  Rear damage -> Transmission: 0.6
  Side damage -> Suspension: 0.5
  Total damage -> Engine: 0.3
  All impacts -> Tires: 0.4
```

### Failure Threshold
```
Component fails when Health < 0.1
Cascade failure: 3+ components = crash
Critical failure: Steering OR Suspension = crash
```

### Handling Degradation (Before Failure)
```
k_steer <- k_steer x (1 - 0.4 x D_front)
omega <- omega x (1 - 0.5 x D_side)
k_slip <- k_slip x (1 + 0.6 x D_rear)
```

---

## Input System

### Supported Devices
- Keyboard/Mouse
- Gamepad (Xbox, PlayStation, generic)
- Racing Wheels (Logitech SDK)

### Force Feedback Effects
- Impact jolts
- Road surface texture
- Drift resistance
- Lane edge rumble

### Input Binding
- Full rebinding support via InputBindingManager
- Configurable deadzones
- Analog sensitivity curves

---

## Autopilot System

### Activation
- After crash recovery
- After score save
- Manual toggle

### Behavior
- Lane-following with magnetism
- Target speed: medium (40 m/s)
- Hazard avoidance
- Traffic evasion
- Visual flow maintenance

### Override
- Any player input disables autopilot
- Immediate control handoff
