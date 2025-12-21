# Midnight Pulse — Technical Specification Sheet

**Version:** 1.0
**Copyright:** © 2025 Kase Branham. All rights reserved.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Core Design Pillars](#2-core-design-pillars)
3. [Technical Architecture](#3-technical-architecture)
4. [Vehicle Systems](#4-vehicle-systems)
5. [Lane & Track Systems](#5-lane--track-systems)
6. [Traffic & AI Systems](#6-traffic--ai-systems)
7. [Hazard & Damage Systems](#7-hazard--damage-systems)
8. [Rendering & Visual Systems](#8-rendering--visual-systems)
9. [Audio System](#9-audio-system)
10. [Scoring System](#10-scoring-system)
11. [UI/UX System](#11-uiux-system)
12. [Implementation Parameters](#12-implementation-parameters)

---

## 1. Project Overview

**Midnight Pulse** is a procedural, endless, night-time freeway driving game focused on flow, speed, and visual rhythm. The game features a wireframe aesthetic with dynamic lighting, emphasizing continuous gameplay without hard restarts.

### Target Platforms
- Unity DOTS (Entities 1.0+)
- Unreal Engine 5 Mass Framework

### Core Experience
- Endless procedural freeway generation
- High-speed gameplay (200+ km/h viable)
- Flow-based driving with lane magnetism
- Crash → Autopilot → Resume loop (no loading screens)

---

## 2. Core Design Pillars

| Pillar | Description |
|--------|-------------|
| **Flow Over Precision** | Smooth steering, magnetic lanes, forgiving controls unless impact is severe |
| **Speed = Score** | Faster driving multiplies score; stopping/slowing removes multiplier |
| **Visual Rhythm** | City lights, emergency flashes, tunnels, bridges, stacked overpasses |
| **One Continuous Loop** | No hard restarts; crashes fade → autopilot → resume or save |

---

## 3. Technical Architecture

### 3.1 ECS Philosophy

| Concept | Description |
|---------|-------------|
| Entities | IDs only |
| Components | Pure data (no logic) |
| Systems | Logic operating over component queries |

### 3.2 Entity Categories

| Entity Type | Description |
|-------------|-------------|
| `PlayerVehicle` | Player-controlled vehicle with scoring |
| `TrafficVehicle` | AI-driven lane-following vehicles |
| `EmergencyVehicle` | Ambulance/police with siren and overtake behavior |
| `Hazard` | Road debris, barriers, construction |
| `TrackSegment` | Procedural road segment |
| `Lane` | Individual lane spline |
| `LightSource` | Dynamic/static light emitters |
| `CameraRig` | Chase camera system |
| `UIOverlay` | HUD elements |
| `ScoreSession` | Active scoring state |

### 3.3 Core Components

#### Transform & Motion
```
WorldTransform { Position: float3, Rotation: quaternion }
Velocity { Forward: float, Lateral: float, Angular: float }
```

#### Vehicle Control
```
PlayerInput { Steer: float[-1,1], Throttle: float[0,1], Brake: float[0,1], Handbrake: bool }
Autopilot { Enabled: bool, TargetSpeed: float, LanePreference: int }
SteeringState { CurrentAngle: float, TargetAngle: float, Smoothness: float }
```

#### Lane & Track
```
LaneFollower { LaneEntity: Entity, MagnetStrength: float, LateralOffset: float }
LaneSpline { ControlPoints: float3[], Width: float }
TrackSegment { Type: enum, Length: float, Difficulty: float }
```

#### Damage
```
DamageState { Front: float, Rear: float, Left: float, Right: float, Total: float }
Crashable { CrashThreshold: float }
```

### 3.4 System Execution Order (Per Frame)

1. Input
2. Autopilot
3. Steering
4. Lane Magnetism
5. Movement
6. Collision
7. Damage
8. Crash
9. Track Generation
10. Traffic AI
11. Scoring
12. Camera
13. Rendering
14. UI

### 3.5 Critical Event Flow

```
VehicleMovement → LaneMagnetism → Collision → Damage → Crash → Fade → Autopilot → Score Reset → Resume
```

**Rule:** No scene reloads. Ever.

---

## 4. Vehicle Systems

### 4.1 Movement Model

- Always moving forward unless braking
- Lane-based steering with analog offset
- Curvature eased with cubic interpolation

#### Speed Tiers
| Tier | Effect |
|------|--------|
| Cruise | Base multiplier |
| Fast | Enhanced multiplier, increased lighting |
| Boosted | Maximum multiplier, maximum effects |

### 4.2 Lane Magnetism System

#### Core Spring-Damper Equation
```
a_mag = -k·x - c·ẋ

Where:
  k = ω² (stiffness)
  c = 2ω (critical damping)
  ω ≈ 6-12 (natural frequency)
```

#### Magnetism Modulation
```
m = m_input × m_auto × m_speed × m_handbrake

Where:
  m_input = 1 - |steer|              (no steering = full magnetism)
  m_auto = 1.5 if autopilot else 1.0
  m_speed = √(v/v_ref), clamped [0.75, 1.25]
  m_handbrake = 0.25 if engaged else 1.0
```

#### Final Lateral Acceleration
```
a_lat = m × (-ω²x - 2ωẋ)
```

#### Edge Force (Soft Constraint)
```
If |x| > w_soft (85% lane width):
  a_edge = -sign(x) × k_edge × x_edge²
```

### 4.3 Lane Change System

#### Trigger Conditions
- Steering exceeds threshold: `|s| > 0.35`
- Steering direction matches lane direction
- Target lane exists and is not blocked
- Not already transitioning

#### Transition Math (Smoothstep)
```
λ(t) = 3t² - 2t³

x(t) = (1-λ)·x_from + λ·x_to
```

#### Speed-Aware Duration
```
T = clamp(T_base × (v/v_ref), T_min, T_max)

T_base = 0.6s
T_min = 0.45s
T_max = 1.0s
```

#### Steering Attenuation During Transition
```
s_effective = s × (1 - λ)
```

#### Emergency Abort
- Counter-steer > 0.7 in opposite direction triggers reversal
- Progress resets using: `Progress = Duration × (1-t)`

### 4.4 Drift & Yaw System

#### Forward-Velocity Constraint (Critical)
```
v_f ≥ v_min (6-10 m/s)

If v_f < v_min:
  v_f ← v_min
  V ← v_f·F + v_l·R
```

#### Yaw Dynamics
```
ψ̈ = τ_steer + τ_drift - c_ψ·ψ̇

τ_steer = k_s × s × (v_f/v_ref)
τ_drift = k_d × sign(s) × √v_f  (if handbrake)
```

#### Drift Slip Angle
```
β = ψ - arctan(v_l/v_f)

During handbrake:
  v_l ← v_l + k_slip × sin(β) × v_f × Δt
```

#### Drift Recovery
```
τ_recover = -k_r × ψ  (when handbrake released)
```

### 4.5 Handbrake Mechanics

- Reduces rear tire traction
- Allows drift and 180°-360° spins
- Forward velocity must be maintained
- If forward velocity drops below threshold → crash state

---

## 5. Lane & Track Systems

### 5.1 Procedural Spline Generation

#### Hermite Spline Definition
```
S(t) = (2t³-3t²+1)P₀ + (t³-2t²+t)T₀ + (-2t³+3t²)P₁ + (t³-t²)T₁
```

#### First Derivative (Direction)
```
S'(t) = (6t²-6t)P₀ + (3t²-4t+1)T₀ + (-6t²+6t)P₁ + (3t²-2t)T₁
```

#### Local Coordinate Frame
```
Forward: F(t) = normalize(S'(t))
Right: R(t) = normalize(F(t) × Up)
Up': U'(t) = R(t) × F(t)
```

### 5.2 Segment Generation Parameters

| Parameter | Value |
|-----------|-------|
| Min Length | 40m |
| Max Length | 120m |
| Tangent Alpha | 0.4-0.6 |
| Max Curvature | 1/R_min |

#### Curvature Constraint
```
κ(t) = ‖S'(t) × S''(t)‖ / ‖S'(t)‖³
κ_max = 1/R_min
```

### 5.3 Segment Types

| Type | Description |
|------|-------------|
| Straight | Basic freeway segment |
| Curve | Gentle curves |
| Tunnel | Reduced lighting, increased reverb |
| Overpass | Elevated stacked segments |
| Fork | Branching decision point |

### 5.4 Lane Generation
```
S_i(t) = S(t) + R(t) × (i × w)

Where:
  i = lane index [-n, n]
  w = lane width
```

### 5.5 Fork Generation

```
At fork point P_f:
  θ_L = -θ_fork
  θ_R = +θ_fork

Gradual separation:
  S_fork(t) += R(t) × (d_fork × t²)
```

#### Fork Magnetism Reduction
```
m_fork(s) = smoothstep(1, 0.7, s/L_fork)
```

### 5.6 Elevation & Overpasses
```
h(t) = A × sin(πt)

S(t).y += h(t)
```

### 5.7 Deterministic Generation
```
seed = hash(globalSeed, segmentIndex)
```

---

## 6. Traffic & AI Systems

### 6.1 Traffic AI Lane-Change Decision

#### Lane Score Function
```
S_i = w_s·S_speed + w_d·S_density + w_e·S_emergency + w_h·S_hazard + w_p·S_player + w_m·S_merge
```

#### Score Terms

| Term | Formula |
|------|---------|
| Speed Advantage | `S_speed = clamp(v_i/v_t, 0, 1)` |
| Density | `S_density = e^(-k_d × n_i)` |
| Emergency Pressure | `S_emergency = 1 - u` (if emergency in lane) |
| Hazard Avoidance | `S_hazard = clamp(d_h/d_safe, 0, 1)` |
| Player Proximity | `S_player = clamp(d_p/w_lane, 0.3, 1)` |
| Merge Logic | `S_merge = l_valid × (1 - d_m/d_merge)` |

#### Decision Hysteresis
```
Change allowed only if: ΔS > θ (threshold = 0.15)
```

#### Commitment Lock
```
Lock time = 1.2s during lane change
```

### 6.2 Emergency Vehicle System

#### Detection Space (Rear Influence Cone)
```
d_f = D · F (behind if < 0)
d_l = D · R (lateral offset)

Detection if:
  d_f < 0 AND |d_l| < w_detect AND |d_f| < d_max
```

#### Urgency Scalar
```
u = clamp(1 - |d_f|/d_max, 0, 1)
```

#### Avoidance Offset
```
dir = -sign(d_l)
x_avoid = dir × k_a × u × w

Player override:
  m_player = clamp(1 - |s|, 0.3, 1)
  x_avoid *= m_player
```

#### Escalation Logic
```
If u > 0.6 AND time > 1.5s AND no lane change:
  - Emergency initiates aggressive overtake
  - Flash rate increases
  - Audio spikes
  - Score multiplier decays: M ← M × (1 - 0.1 × u × Δt)
```

#### Light Signaling (Off-Screen)
```
Intensity: I = I₀(1 + 2u)
Strobe rate: f = f₀ + 4u
Light radius: r = r₀(1 + u)
```

---

## 7. Hazard & Damage Systems

### 7.1 Hazard Classification

| Hazard | Severity | Mass | Type |
|--------|----------|------|------|
| Loose tire | 0.2 | 0.1 | Cosmetic |
| Road debris | 0.4 | 0.3 | Mechanical |
| Construction cone | 0.3 | 0.2 | Cosmetic |
| Barrier | 0.9 | 0.9 | Lethal |
| Crashed car | 1.0 | 1.0 | Lethal |

### 7.2 Impulse Calculation

#### Impact Speed
```
v_impact = max(0, -V · N)
```

#### Impulse Magnitude
```
J = k_i × v_impact × (0.5 + Severity)
```

#### Impulse Decomposition
```
I = J × N
I_f = I · F (forward)
I_l = I · R (lateral)
```

### 7.3 Velocity Response

#### Lateral Response
```
v_l ← v_l + I_l / m_virtual
```

#### Forward Response (Clamped)
```
v_f ← v_f - |I_f| / m_virtual
v_f ← max(v_f, v_min)
```

#### Yaw Kick
```
Δψ̇ = k_y × I_l / (v_f + ε)
```

### 7.4 Damage Accumulation

#### Damage Energy
```
E_d = k_d × v_impact² × Severity
```

#### Directional Distribution
```
Damage.Front += E_d × w_front
Damage.Left += E_d × w_left
Damage.Rear += E_d × w_rear
Damage.Right += E_d × w_right
Damage.Total += E_d
```

### 7.5 Handling Degradation

| Effect | Formula |
|--------|---------|
| Steering Response | `k_steer ← k_steer × (1 - 0.4 × D_front)` |
| Magnetism Reduction | `ω ← ω × (1 - 0.5 × D_side)` |
| Drift Stability Loss | `k_slip ← k_slip × (1 + 0.6 × D_rear)` |

### 7.6 Crash Conditions (Explicit)

A crash occurs ONLY if one of the following is true:

| Condition | Formula |
|-----------|---------|
| Lethal Hazard + Speed | `Severity > 0.8 AND v_impact > v_crash` |
| Structural Damage | `Damage.Total > D_max` |
| Compound Failure | `|ψ| > ψ_fail AND v_f ≈ v_min AND Damage.Total > 0.6×D_max` |

**Note:** Spinning alone never causes crash.

---

## 8. Rendering & Visual Systems

### 8.1 Visual Style

- Entire world rendered in wireframe
- Solid light volumes
- No textures (initially)
- High lighting contrast

### 8.2 Camera System

| Property | Specification |
|----------|---------------|
| Type | Third-person chase camera |
| Angle | Low, slightly offset downward |
| FOV | Increases with speed |
| Lag | Subtle camera lag for motion feel |

#### Dynamic Effects
- Speed-based motion blur
- Headlight bloom
- Light streaks at high velocity
- Camera shake (minor: rough road, major: impacts)

### 8.3 Lighting Types

| Light Type | Description |
|------------|-------------|
| Headlights | Dynamic, raytraced (if available) |
| Streetlights | Static ambient |
| City glow | Distant illumination |
| Emergency strobes | Red/blue or red/white flashing |
| Tunnel lights | Interior focused |

### 8.4 Raytracing (If Available)

- Headlight reflections
- Emergency light reflections
- Light bounce inside tunnels

**Fallback:** Screen-space lighting approximations

### 8.5 World Boundaries

- Only just-off-road geometry rendered
- City suggested via light grids and distant silhouettes
- No full city rendering (performance preservation)

---

## 9. Audio System

### 9.1 Core Audio Layers

| Layer | Description |
|-------|-------------|
| Engine | Pitch-based on speed |
| Tires | Surface-dependent noise |
| Wind | Velocity-scaled ambience |
| City | Distant urban soundscape |

### 9.2 Event Audio

| Event | Effect |
|-------|--------|
| Emergency sirens | Directional, doppler |
| Crashes | Impact-scaled |
| Construction | Zone-based |
| Tunnels | Reverb increase |

---

## 10. Scoring System

### 10.1 Score Accumulation

```
Score = Distance × Speed_Multiplier
```

### 10.2 Multiplier Modifiers

| Modifier | Effect |
|----------|--------|
| Close calls | Bonus multiplier |
| High-speed passes | Bonus multiplier |
| Drift usage | Bonus multiplier |
| Emergency avoidance | Bonus multiplier |

### 10.3 Scoring Rules

- Braking stops score accumulation
- Crashing ends score run
- Score saved on crash/save action

---

## 11. UI/UX System

### 11.1 HUD Elements

| Element | Description |
|---------|-------------|
| Speed | Current velocity |
| Multiplier | Active score modifier |
| Score | Running total |
| Damage | Zone indicators |

### 11.2 Design Principles

- Transparent overlay
- Minimal HUD
- No full-screen interruptions

### 11.3 Pause System

- Pausing allowed
- 5-second cooldown before next pause
- Cooldown visible to player

### 11.4 Autopilot System

**Activation:**
- After crash
- After score save
- Player chooses to disengage controls

**Behavior:**
- Lane-following
- Medium speed
- Avoids hazards
- Maintains visual flow
- Menu overlay stays active

---

## 12. Implementation Parameters

### 12.1 Lane Magnetism Defaults

| Parameter | Value |
|-----------|-------|
| ω (omega) | 8.0 |
| v_ref | 40 m/s |
| Max lateral speed | 6 m/s |
| Edge stiffness | 20 |
| Soft zone | 85% lane width |

### 12.2 Lane Change Defaults

| Parameter | Value |
|-----------|-------|
| Steering trigger | 0.35 |
| Abort threshold | 0.7 |
| Base duration | 0.6 s |
| Lane width | 3.6 m |
| Fork magnetism | 70% |

### 12.3 Drift/Yaw Defaults

| Parameter | Value |
|-----------|-------|
| Min forward speed | 8 m/s |
| Steering gain | 1.2 |
| Drift gain | 2.5 |
| Yaw damping | 0.8 |
| Max yaw rate | 6 rad/s |
| Slip gain | 1.1 |
| Drift magnetism | 0.3 |

### 12.4 Emergency Vehicle Defaults

| Parameter | Value |
|-----------|-------|
| Detection distance | 120 m |
| Detection width | 6-8 m |
| Avoid strength | 0.8 |
| Warning time | 1.5 s |
| Player min override | 0.3 |
| Light intensity boost | ×3 |

### 12.5 Hazard/Damage Defaults

| Parameter | Value |
|-----------|-------|
| Impulse scale | 1.2 |
| Damage scale | 0.04 |
| Virtual mass | 1200 |
| Crash speed | 25 m/s |
| Max damage | 100 |
| Min forward speed | 8 m/s |

### 12.6 Traffic AI Defaults

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

### 12.7 Procedural Generation Defaults

| Parameter | Value |
|-----------|-------|
| Segment min length | 40 m |
| Segment max length | 120 m |
| Tangent alpha | 0.4-0.6 |
| Arc-length samples | 16 |

---

## Appendix A: MVP Development Order

1. Vehicle movement + lane magnetism
2. Procedural freeway generation
3. Traffic AI
4. Basic scoring loop
5. Crash → fade → autopilot loop
6. Lighting + wireframe rendering
7. Hazards & emergency vehicles
8. Polish & performance

---

## Appendix B: Scalability Notes

| Feature | Upgrade Path |
|---------|--------------|
| Damage System | Swap to BeamNG-level soft-body deformation |
| Lighting | Swap to full raytracing |
| Multiplayer | Replicate ECS state deltas |
| Replay | Record component diffs |

---

## Appendix C: Engine Parity Reference

| Concept | Unity DOTS | Unreal Mass |
|---------|------------|-------------|
| Component | IComponentData | Fragment |
| System | ISystem | Processor |
| Tag | Empty IComponentData | MassTag |
| Buffer | IBufferElementData | Shared Fragment |
| Archetype | EntityArchetype | EntityConfig |

---

*End of Specification*
