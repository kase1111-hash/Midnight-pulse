# Nightflow — Master Technical Specification

**Version:** 1.1
**Date:** December 2025
**Copyright:** © 2025 Kase Branham. All rights reserved.

---

> **Tagline:** *Infinite neon freeway. One life. Flow or crash.*

**Genre:** Endless procedural night-time freeway driving (flow runner)
**Core Inspiration:** Subway Surfers × BeamNG × OutRun × high-speed night aesthetics
**Target Platforms:** PC (primary), Console (stretch)
**Engine:** Unity DOTS 1.0+ (HDRP/Entities.Graphics) or Unreal Mass Entity (UE5)

---

## Table of Contents

1. [Game Overview & Pillars](#1-game-overview--pillars)
2. [Controls](#2-controls)
3. [Technical Architecture](#3-technical-architecture)
4. [Vehicle Systems](#4-vehicle-systems)
5. [Lane & Track Systems](#5-lane--track-systems)
6. [Traffic & AI Systems](#6-traffic--ai-systems)
7. [Hazard & Damage Systems](#7-hazard--damage-systems)
8. [Camera System](#8-camera-system)
9. [Rendering & Visual Systems](#9-rendering--visual-systems)
10. [Audio System](#10-audio-system)
11. [Off-Screen Threat Signaling](#11-off-screen-threat-signaling)
12. [Scoring System](#12-scoring-system)
13. [Replay & Ghost System](#13-replay--ghost-system)
14. [UI/UX System](#14-uiux-system)
15. [Difficulty Progression](#15-difficulty-progression)
16. [Implementation Parameters](#16-implementation-parameters)
17. [MVP Development Roadmap](#17-mvp-development-roadmap)

---

## 1. Game Overview & Pillars

### Core Gameplay Loop

1. Player drives endlessly on procedurally generated freeway
2. Steer smoothly between lanes, speed up/down, handbrake drift
3. Avoid/pass traffic, hazards, emergency vehicles
4. Small hits damage vehicle → handling degradation
5. Large hit or total failure → crash → fade → score summary → autopilot reset → resume or save

### Win Condition
Highest score/distance before debilitating crash.

### Core Design Pillars

| Pillar | Description |
|--------|-------------|
| **Flow Over Precision** | Smooth, forgiving controls with lane magnetism and autopilot respite |
| **Speed = Score** | Faster driving multiplies score; braking/stopping kills multiplier |
| **Visual Rhythm** | Wireframe world, dynamic night lighting, emergency strobes, tunnels/overpasses |
| **One Continuous Loop** | Crash → instant reset → autopilot → resume; no loading, no hard restarts |

---

## 2. Controls

| Input | Function |
|-------|----------|
| **Analog Steer** | Smoothed, lane-based steering |
| **Throttle** | Speed up |
| **Brake** | Slow down (ends scoring) |
| **Handbrake** | Drift/spin (must maintain forward velocity) |

---

## 3. Technical Architecture

### 3.1 ECS Philosophy

| Concept | Description |
|---------|-------------|
| Entities | IDs only |
| Components | Pure data (no logic) |
| Systems | Logic operating over component queries |

### 3.2 Entity Archetypes

| Entity Type | Description |
|-------------|-------------|
| `PlayerVehicle` | Player-controlled vehicle with scoring |
| `TrafficVehicle` | AI-driven lane-following vehicles |
| `EmergencyVehicle` | Ambulance/police with siren and overtake behavior |
| `Hazard` | Road debris, barriers, construction |
| `TrackSegment` | Procedural road segment (with DynamicBuffer<LaneSplinePoint>) |
| `Lane` | Individual lane spline |
| `LightSource` | Dynamic/static light emitters |
| `CameraRig` | Chase camera system |
| `UIOverlay` | HUD elements |
| `ScoreSession` | Active scoring state |
| `GhostVehicle` | For replays |

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
LaneTransition { Active: bool, FromLane: Entity, ToLane: Entity, Progress: float }
```

#### Lane & Track
```
LaneFollower { LaneEntity: Entity, MagnetStrength: float, LateralOffset: float }
LaneSpline { ControlPoints: float3[], Width: float }
TrackSegment { Type: enum, Length: float, Difficulty: float }
```

#### Damage & Scoring
```
DamageState { Front: float, Rear: float, Left: float, Right: float, Total: float }
Crashable { CrashThreshold: float }
ScoreSession { Distance: float, Multiplier: float, RiskMultiplier: float, Active: bool }
```

#### Signaling
```
LightEmitter { Color: float3, Intensity: float, Strobe: bool, StrobeRate: float }
OffscreenSignal { Direction: float2, Urgency: float, Type: enum }
ReplayPlayer { InputLog: buffer, Timestamp: float }
```

### 3.4 System Execution Order

**Simulation Group:**
1. Input / Replay Playback
2. Autopilot
3. Steering & Lane Transition
4. Lane Magnetism
5. Vehicle Movement & Drift/Yaw
6. Collision & Impulse
7. Damage Evaluation
8. Crash Handling
9. Procedural Track Generation & Fork Resolution
10. Traffic / Emergency AI
11. Hazard Spawning
12. Scoring
13. Off-Screen Signaling

**Presentation Group:**
14. Camera
15. Wireframe Render
16. Lighting
17. Audio
18. UI

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

| Tier | Multiplier | Effect |
|------|------------|--------|
| Cruise | 1.0× | Base speed, standard effects |
| Fast | 1.5× | Enhanced lighting, increased FOV |
| Boosted | 2.5× | Maximum effects, highest risk/reward |

### 4.2 Lane Magnetism System (Critically Damped Spring)

#### Core Spring-Damper Equation
```
x_error = current_lateral - target_lateral
a_lat = m × (-ω² × x_error - 2ω × lateral_vel)
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

#### Edge Force (Soft Constraint)
```
If |x| > w_soft (85% lane width):
  a_edge = -sign(x) × k_edge × x_edge²
```

### 4.3 Lane Change & Merge System

Blended virtual spline via smoothstep on lateral offset. Same math used for player, traffic, autopilot, and geometric merges.

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

### 4.4 Drift, Yaw & Forward Constraint

#### Forward-Velocity Constraint (Critical)
```
v_f ≥ v_min (~8 m/s)

If v_f < v_min:
  v_f ← v_min
  V ← v_f·F + v_l·R
```

#### Yaw Dynamics (Explicit Torque Model)
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

Piecewise Cubic Hermite splines (easy tangent control, stable curvature, deterministic, cheap evaluation).

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
| Arc-length Samples | 16 |

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

Forks gradually diverge; unchosen path despawns after commit.

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
segmentSeed = hash(globalSeed, segmentIndex)
```

Guarantees: Replayable runs, ghost driving, network determinism.

---

## 6. Traffic & AI Systems

### 6.1 Traffic AI Lane-Change Decision

Continuous lane desirability scoring — cars "slide downhill" in a desirability landscape.

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

### 6.3 Traffic Yielding Behavior
```
If emergency urgency u > 0.7:
  v_f ← v_f × (1 - 0.3u)
  Prefer outermost lane
```

Traffic yields and clears wave naturally — emergent behavior with no extra AI logic.

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

## 8. Camera System

### 8.1 Base Configuration

| Property | Specification |
|----------|---------------|
| Type | Third-person chase (low angle, slight lead look-at) |
| Base FOV | 55° |
| Max FOV | 90° |
| FOV Coupling | Speed-driven |

### 8.2 Dynamic Behaviors

| Behavior | Description |
|----------|-------------|
| Speed FOV | FOV scales from 55° to 90° with speed |
| Pull-back | Camera offset increases at high speed |
| Yaw Follow | Camera follows vehicle yaw with damping |
| Drift Whip | Extra rotational follow during drift |
| Motion Blur | Speed² intensity scaling |

### 8.3 Impact & Feedback

| Effect | Trigger |
|--------|---------|
| Impact Recoil | Collision impulse |
| Procedural Shake | Rough road, construction |
| Damage Wobble | Accumulated damage |
| Tunnel Squeeze | Entering tunnel segments |

All motion critically damped per axis.

---

## 9. Rendering & Visual Systems

### 9.1 Visual Style

- Entire world rendered in wireframe (initial MVP)
- Solid light volumes, bloom, and additive glows
- Night-time city suggested via distant light grids and silhouettes (no full geometry)
- Dynamic raytracing when available; fallback screen-space

### 9.2 Lighting Types

| Light Type | Description |
|------------|-------------|
| Headlights | Dynamic, raytraced (if available) |
| Streetlights | Static ambient |
| City glow | Distant illumination |
| Emergency strobes | Red/blue or red/white flashing |
| Tunnel lights | Interior focused |

### 9.3 Raytracing (If Available)

- Headlight reflections
- Emergency light reflections
- Light bounce inside tunnels

**Fallback:** Screen-space lighting approximations

### 9.4 World Boundaries

- Only just-off-road geometry rendered
- City suggested via light grids and distant silhouettes
- No full city rendering (performance preservation)

---

## 10. Audio System

### 10.1 Core Audio Layers

| Layer | Description |
|-------|-------------|
| Engine | Pitch/load blend based on speed |
| Tires | Slip/skid layer |
| Wind | Rush intensity ∝ speed² |
| Environment | Reverb zones (tunnel boomy, overpass ringing, open dry) |

### 10.2 Spatial Audio

| Feature | Implementation |
|---------|----------------|
| Doppler | Exaggerated for sirens and passing traffic |
| Directional | Emergency sirens positioned in 3D space |
| Distance Attenuation | Realistic falloff for all sources |

### 10.3 Event Audio

| Event | Effect |
|-------|--------|
| Emergency sirens | Directional, doppler, off-screen intensification |
| Crashes | Impact thuds, scrape sounds |
| Damage | Engine detune, handling audio feedback |
| Construction | Zone-based ambient |
| Tunnels | Reverb increase |

---

## 11. Off-Screen Threat Signaling

### 11.1 Visual Indicators

| Threat | Signal |
|--------|--------|
| Crashed vehicles ahead | Red/blue strobe leak at screen edges |
| Emergencies behind | Red/white strobe at screen edges |

### 11.2 Implementation

```
Screen-space flare quads positioned by projected edge direction
Intensity ∝ urgency (inverse distance) × strobe pulse
Fade as threat enters view
```

### 11.3 Light Signaling Math
```
Intensity: I = I₀ × (1 + 2u)
Strobe rate: f = f₀ + 4u
Light radius: r = r₀ × (1 + u)
```

Where `u` = urgency scalar (0-1)

---

## 12. Scoring System

### 12.1 Base Formula

```
Score = Distance × Speed_Tier_Multiplier × (1 + RiskMultiplier)
```

### 12.2 Speed Tier Multipliers

| Tier | Multiplier |
|------|------------|
| Cruise | 1.0× |
| Fast | 1.5× |
| Boosted | 2.5× |

### 12.3 Risk Events

Risk events spike temporary `riskMultiplier`:

| Event | Effect |
|-------|--------|
| Close passes | Spike risk multiplier |
| Hazard dodges | Spike risk multiplier |
| Emergency clears | Spike risk multiplier |
| Drift recoveries | Spike risk multiplier |
| Perfect segments | One-time bonus |
| Full spins | One-time bonus |

### 12.4 Risk Multiplier Dynamics

```
Decay: ~0.8/s
Braking: Instantly halves riskMultiplier + 2s rebuild delay
Damage: Reduces cap and rebuild rate
```

### 12.5 Scoring Rules

- Braking stops score accumulation
- Crashing ends score run
- Score saved on crash/save action

---

## 13. Replay & Ghost System

### 13.1 Recording

```
Record: globalSeed + fixed-timestep input log
```

### 13.2 Playback

```
Second PlayerVehicle entity driven by log (identical sim)
Deterministic via seeded PRNG and pure math
```

### 13.3 Ghost Rendering

- Semi-transparent, non-colliding
- Optional trail effect
- Leaderboard validation via server re-simulation

---

## 14. UI/UX System

### 14.1 HUD Elements

| Element | Description |
|---------|-------------|
| Speed | Current velocity |
| Multiplier | Active score modifier |
| Score | Running total |
| Damage | Zone indicators (bars) |

### 14.2 Design Principles

- Transparent overlay (visible during autopilot)
- Minimal HUD
- No full-screen interruptions

### 14.3 Pause System

- Pausing allowed
- 5-second cooldown before next pause
- Cooldown visible to player

### 14.4 Crash Flow

1. Impact
2. Screen shake
3. Quick fade to black
4. Score summary (breakdown + save option)
5. Vehicle reset
6. Autopilot resumes instantly

### 14.5 Autopilot System

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

## 15. Difficulty Progression

Natural scaling via endurance (no discrete levels):

| Factor | Progression |
|--------|-------------|
| Base Speed | Increases over time |
| Traffic Density | Increases with distance |
| Hazard Frequency | Increases with distance |
| Fork Complexity | More complex choices appear |
| Risk Reward | Higher multipliers available |

---

## 16. Implementation Parameters

### 16.1 Lane Magnetism Defaults

| Parameter | Value |
|-----------|-------|
| ω (omega) | 8.0 |
| v_ref | 40 m/s |
| Max lateral speed | 6 m/s |
| Edge stiffness | 20 |
| Soft zone | 85% lane width |

### 16.2 Lane Change Defaults

| Parameter | Value |
|-----------|-------|
| Steering trigger | 0.35 |
| Abort threshold | 0.7 |
| Base duration | 0.6 s |
| Lane width | 3.6 m |
| Fork magnetism | 70% |

### 16.3 Drift/Yaw Defaults

| Parameter | Value |
|-----------|-------|
| Min forward speed | 8 m/s |
| Steering gain | 1.2 |
| Drift gain | 2.5 |
| Yaw damping | 0.8 |
| Max yaw rate | 6 rad/s |
| Slip gain | 1.1 |
| Drift magnetism | 0.3 |

### 16.4 Camera Defaults

| Parameter | Value |
|-----------|-------|
| Base FOV | 55° |
| Max FOV | 90° |
| Damping | Critical per axis |

### 16.5 Emergency Vehicle Defaults

| Parameter | Value |
|-----------|-------|
| Detection distance | 120 m |
| Detection width | 6-8 m |
| Avoid strength | 0.8 |
| Warning time | 1.5 s |
| Player min override | 0.3 |
| Light intensity boost | ×3 |

### 16.6 Hazard/Damage Defaults

| Parameter | Value |
|-----------|-------|
| Impulse scale | 1.2 |
| Damage scale | 0.04 |
| Virtual mass | 1200 |
| Crash speed | 25 m/s |
| Max damage | 100 |
| Min forward speed | 8 m/s |

### 16.7 Traffic AI Defaults

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

### 16.8 Scoring Defaults

| Parameter | Value |
|-----------|-------|
| Risk decay | 0.8/s |
| Brake penalty | 50% + 2s delay |

### 16.9 Procedural Generation Defaults

| Parameter | Value |
|-----------|-------|
| Segment min length | 40 m |
| Segment max length | 120 m |
| Tangent alpha | 0.4-0.6 |
| Arc-length samples | 16 |

---

## 17. MVP Development Roadmap

1. Vehicle movement + lane magnetism + basic spline freeway
2. Procedural generation (straights/curves) + traffic AI
3. Lane change, handbrake drift, forward constraint
4. Hazards, impulses, damage, crash loop
5. Emergency vehicles + avoidance + off-screen signaling
6. Scoring + risk bonuses
7. Camera + audio layers
8. Wireframe rendering + dynamic lighting
9. Autopilot + UI overlay
10. Replay/ghost system
11. Polish (tunnels, overpasses, forks, ghosts)

---

## Appendix A: Future Scalability

| Feature | Upgrade Path |
|---------|--------------|
| Phase 2 Damage | Swap to soft-body / component failure |
| Raytracing | Replace lighting system with full RT |
| Multiplayer | Replicate input logs + seeds |
| Full City | Populate distant silhouettes with geometry |

---

## Appendix B: Engine Parity Reference

| Concept | Unity DOTS | Unreal Mass |
|---------|------------|-------------|
| Component | IComponentData | Fragment |
| System | ISystem | Processor |
| Tag | Empty IComponentData | MassTag |
| Buffer | IBufferElementData | Shared Fragment |
| Archetype | EntityArchetype | EntityConfig |

---

*End of Specification*
