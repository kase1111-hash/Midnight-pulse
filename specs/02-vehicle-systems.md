# Vehicle Systems Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## Movement Model

- Always moving forward; there is **no top speed** (no terminal velocity)
- Lane-based steering with analog offset
- Curvature eased with cubic interpolation

### Momentum

The run carries the car at a **pace** that keeps climbing:

```
pace(t, d) = 25 + 0.25 x t^2 / (t + 30) + 0.0012 x d     (m/s)

t = seconds driven this run, d = metres driven this run
```

- Off the pedals the car is pulled up to the pace (0.8/s of the gap, max 3 m/s^2)
- Throttle boosts above it: `7 / (1 + v/25)` m/s^2, shrinking with speed but never zero
- The brake trims speed (25 m/s^2) but never below the **momentum floor**, 70% of the pace
- Nothing slows the car on its own (no drag): speed you gain is speed you keep
- Hits cost momentum (30% of the impact speed); the pace rebuilds it

So every run keeps getting faster (~195 km/h at 2 min, ~340 km/h at 4 min, ~505 km/h at 6 min) until the driver can't hold it. Redline climbs 1.3x faster with a 1.6x throttle boost; Freeflow has no climb and no floor. Rules: `VehicleDynamics` (pure, unit-tested).

### Grip and Loss of Control

```
grip(v) = 32 x tyres x suspension / (1 + (v/140)^2)       (m/s^2)
load    = v^2 x curvature + LaneLoad(v) x a_lane
LaneLoad(v) = 0.5 x max(0, v - 20) / 40
usage   = |load| / grip      (smoothed, 12/s)
```

The car goes light as it gets faster (no downforce holds it down) while corners and lane changes ask for more. Past usage 1:
- the tyre force is scaled back by the usage, so the car runs wide of its lane
- the alignment that keeps the nose in line fades: `1 / (1 + 4 x excess)`
- the rear steps out toward the load: `6 x excess x v/40` rad/s^2

Brake, counter-steer or stop asking for more and the tyres bite again (a **slide save** pays a risk bonus). Let the slip angle pass the survivable limit (unlimited below 35 m/s, easing to 0.85 rad at 75 m/s and above) and the run ends: `LOST CONTROL`. The tightest corners (R = 300 m) start to slide at ~85 m/s; lane changes on a straight at ~110 m/s; handbrake drifts are survivable up to ~55 m/s. The HUD warns `TRACTION LIMIT` / `LOSING GRIP`, the tyres squeal, the camera shakes and the image fringes as usage nears 1.

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
m = m_auto x m_speed x m_handbrake x m_drift x m_damage

Where:
  m_auto = 1.5 if autopilot else 1.0
  m_speed = sqrt(v/v_ref), clamped [0.75, 1.25]
  m_handbrake = 0.25 if engaged else 1.0
  m_drift = 0.3 while drifting
  m_damage = 1 - 0.5 x D_side        (side damage reduces magnetism)
```

Steering does not weaken the spring (an earlier `m_input = 1 - |steer|` switched it off at full steer, so a held key never moved the car). Instead steering **leans** the car inside its lane (up to 0.9 m at the lane-change threshold, holding there beyond it when the change is blocked or at the road edge; smoothed), and the spring tracks the target's velocity as well as its position:

```
a_lat = m x (-omega^2 x (x - x_target) - 2 x omega x (v_lat - v_target))
```

Integrated in sub-steps of at most 1/120 s, so lane changes take the same time at 30, 60 or 144 fps.

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
| Max lateral speed | 6 m/s (10 sliding, 15 drifting) |
| Edge stiffness | 60 |
| Soft zone | 85% of half the road width |

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
- Progress resets using `Progress = Duration x (1-t)` and the blend runs backwards (`1 - lambda`) from where the car is (smoothstep is symmetric), so the car reverses without snapping; the lanes are not swapped, so the car's lane never flips to one it didn't reach

### Blocked Lanes
A lane change is refused while a car or hazard in the target lane is within `12 m + closing speed x lane-change time` ahead (or `10 m + closing speed x 1.5 s` behind): the road the car would close while the change is under way. It scales with speed but never refuses a change because of something far down the road.

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
psi_ddot = tau_steer + tau_drift + tau_align + tau_slide - c_psi(v) x psi_dot

tau_steer = k_s x s x (v_f/v_ref)
tau_drift = k_d x sign(s) x sqrt(v_f)              (if handbrake)
tau_align = -k_a x align x beta                     (tyres pull the nose in line with the velocity)
tau_slide = k_sl x sign(load) x excess x v_f/v_ref  (past the grip limit)
c_psi(v)  = 9 x max(0.45, sqrt(v_ref / v_f))        (twitchier at speed)
```

`beta` is the slip angle between the nose and the direction of travel. Without the alignment term the yaw used to integrate steering with nothing pulling it back, leaving the car permanently crooked after every lane change. Yaw is kept wrapped to [-pi, pi), so a full spin settles facing forward.

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
| Yaw damping | 9/s at v_ref, x sqrt(v_ref/v) (min 45%) |
| Alignment stiffness | 40 |
| Max yaw rate | 6 rad/s |
| Slip gain | 1.1 |
| Drift magnetism | 0.3 |

---

## Handbrake Mechanics

- Reduces rear tire traction (35% grip)
- Allows drift and 180-360 degree spins: survivable at low speed, but a big drift angle at speed is a `LOST CONTROL` crash, so the handbrake gets riskier as the momentum builds
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
- Lane-following with magnetism (it never steers against the road centre, so it never weaves)
- Target speed: 20-35 m/s cruise; taking over from a faster car it eases down at 4 m/s^2 instead of braking hard
- Hazard avoidance (looks max(80 m, 2.5 s) ahead; lane changes go through the same blocked-lane check as the player's)
- Traffic evasion
- Visual flow maintenance
- Perfect tyres (2x grip): attract mode never slides

### Override
- Any player input disables autopilot
- Immediate control handoff
- A key still held from a menu or the crash summary must be released first (`GameFlowLogic.TakeoverIntent`), so the Space/Enter that started a run never fires a handbrake or brake
