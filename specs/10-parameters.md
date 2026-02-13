# Implementation Parameters Reference

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

Complete reference of all tuning values and defaults. Source files are noted for each section to enable cross-referencing with the implementation.

---

## Core Game Constants

> **Source:** `src/Config/GameConstants.cs`

| Parameter | Value | Purpose |
|-----------|-------|---------|
| LaneWidth | 3.6 m | Standard freeway lane width |
| DefaultNumLanes | 4 | Lanes per road |
| MinForwardSpeed | 8 m/s | Never stalls (critical rule) |
| MaxForwardSpeed | 80 m/s | Speed cap |
| MaxDamage | 100 | Crash threshold |
| SegmentLength | 200 m | Track segment length |
| SegmentsAhead | 5 | Lookahead buffer |
| SegmentsBehind | 2 | Segments kept behind before culling |

---

## Lane Magnetism

> **Source:** `src/Systems/Simulation/LaneMagnetismSystem.cs`, `src/Config/GameplayConfig.cs`

| Parameter | Value |
|-----------|-------|
| omega | 8.0 |
| v_ref | 40 m/s |
| Max lateral speed | 6 m/s |
| Edge stiffness | 20 |
| Soft zone | 85% lane width |
| Magnetism strength | 0.7 (configurable) |
| Min magnetism at high speed | 0.3 |

---

## Lane Change

> **Source:** `src/Systems/Simulation/LaneChangeSystem.cs`

| Parameter | Value |
|-----------|-------|
| Steering trigger | 0.35 |
| Abort threshold | 0.7 |
| Base duration | 0.6 s |
| Min duration | 0.45 s |
| Max duration | 1.0 s |
| Lane width | 3.6 m |
| Fork magnetism | 70% (strength of lane pull at road forks) |
| Lane change cooldown | 0.2 s |

---

## Drift / Yaw

> **Source:** `src/Systems/Simulation/VehicleMovementSystem.cs`

| Parameter | Value |
|-----------|-------|
| Min forward speed | 8 m/s |
| Steering gain (k_s) | 1.2 |
| Drift gain (k_d) | 2.5 |
| Yaw damping (c_psi) | 0.8 |
| Max yaw rate | 6 rad/s |
| Slip gain (k_slip) | 1.1 |
| Drift magnetism | 0.3 |

---

## Camera

> **Source:** `src/Systems/Presentation/CameraSystem.cs`

| Parameter | Value |
|-----------|-------|
| Base FOV | 60 degrees |
| Speed FOV Bonus | 20 degrees |
| Base Height | 3 m |
| Base Distance | 8 m |
| Speed Height Bonus | 1.5 m |
| Speed Distance Bonus | 4 m |
| Look Ahead Factor | 0.5 |
| Max Look Ahead | 15 m |
| Position Smooth Speed | 8 |
| Rotation Smooth Speed | 5 |
| FOV Smooth Speed | 3 |
| Collision Shake Intensity | 0.5 |
| Collision Shake Duration | 0.3 s |
| Speed Shake Intensity | 0.02 |

---

## Emergency Vehicles

> **Source:** `src/Systems/Simulation/EmergencyVehicleSystem.cs`

| Parameter | Value |
|-----------|-------|
| Detection distance (d_max) | 120 m |
| Detection width (w_detect) | 6-8 m |
| Avoid strength (k_a) | 0.8 |
| Warning time (t_warn) | 1.5 s |
| Player min override | 0.3 |
| Light intensity boost | x3 |
| Escalation threshold | u > 0.6 |
| Yield threshold | u > 0.7 |
| Emergency speed | 180 km/h |
| Spawn distance behind player | 200 m |

---

## Hazards / Damage

> **Source:** `src/Systems/Simulation/ImpulseSystem.cs`, `src/Systems/Simulation/DamageSystem.cs`

| Parameter | Value |
|-----------|-------|
| Impulse scale (k_i) | 1.2 |
| Damage scale (k_d) | 0.04 |
| Virtual mass (m_virtual) | 1200 |
| Crash speed (v_crash) | 25 m/s |
| Max damage (D_max) | 100 |
| Min forward speed (v_min) | 8 m/s |
| Yaw kick scale (k_y) | 0.5 |
| Collision invulnerability | 0.5 s |
| Collision speed penalty | 0.3 (30%) |

### Handling Degradation Coefficients

| Effect | Coefficient |
|--------|-------------|
| Front damage -> steering | 0.4 |
| Side damage -> magnetism | 0.5 |
| Rear damage -> slip | 0.6 |

---

## Component Health

> **Source:** `src/Systems/Simulation/ComponentFailureSystem.cs`

| Parameter | Value |
|-----------|-------|
| Failure threshold | 0.1 |
| Front -> Steering ratio | 0.8 |
| Rear -> Transmission ratio | 0.6 |
| Side -> Suspension ratio | 0.5 |
| Total -> Engine ratio | 0.3 |
| Impact -> Tires ratio | 0.4 |
| Cascade failure threshold | 3 components |

---

## Soft-Body Deformation

> **Source:** `src/Systems/Simulation/SoftBodyDeformationSystem.cs`

| Parameter | Value |
|-----------|-------|
| Spring constant | 8.0 |
| Damping | 0.7 |

---

## Traffic AI

> **Source:** `src/Systems/Simulation/TrafficAISystem.cs`

| Weight | Value |
|--------|-------|
| Speed (w_s) | 0.35 |
| Density (w_d) | 0.25 |
| Emergency (w_e) | 0.4 |
| Hazard (w_h) | 0.3 |
| Player (w_p) | 0.15 |
| Merge (w_m) | 0.3 |

| Parameter | Value |
|-----------|-------|
| Decision threshold | 0.15 |
| Lock time | 1.2 s |
| Base traffic density | 3 vehicles/100m |
| Max traffic density | 8 vehicles/100m |
| Base traffic speed | 100 km/h |
| Speed variation | +/- 20 km/h |
| Slow vehicle chance | 15% |
| Lane change chance/sec | 5% |

---

## Scoring

> **Source:** `src/Systems/Simulation/ScoringSystem.cs`, `src/Systems/Simulation/RiskEventSystem.cs`

| Parameter | Value |
|-----------|-------|
| Cruise multiplier | 1.0x |
| Fast multiplier | 1.5x |
| Boosted multiplier | 2.5x |
| Fast threshold | 30 m/s |
| Boosted threshold | 50 m/s |
| Risk decay | 0.8/s |
| Brake penalty | 50% + 2s delay |
| Base risk cap | 2.0x |
| Min risk cap | 0.5x |
| Points per meter | 1 |
| Points per second | 10 |
| Near miss distance | 2 m |
| Near miss points | 500 |
| Perfect dodge distance | 1 m |
| Perfect dodge points | 1000 |
| Kilometer bonus | 1000 points |
| Minute bonus | 500 points |

---

## Procedural Generation

> **Source:** `src/Systems/Simulation/TrackGenerationSystem.cs`, `src/Utilities/SplineUtilities.cs`

| Parameter | Value |
|-----------|-------|
| Segment min length | 40 m |
| Segment max length | 120 m |
| Tangent alpha | 0.4-0.6 |
| Arc-length samples | 16 |

---

## Light Signaling

| Parameter | Formula |
|-----------|---------|
| Intensity | I = I_0 x (1 + 2u) |
| Strobe rate | f = f_0 + 4u |
| Light radius | r = r_0 x (1 + u) |
| Warning flash rate | 4 Hz |

---

## UI Parameters

| Parameter | Value |
|-----------|-------|
| Score display smoothing | 8.0 |
| Crash flash-in duration | 0.05 s |
| Crash flash hold duration | 0.08 s |
| Crash flash fade-out | 0.4 s |
| Pause cooldown | 5 s |

---

## City Generation

> **Source:** `src/Systems/World/CityGenerationSystem.cs`, `src/Systems/World/CityLODSystem.cs`

| Parameter | Value |
|-----------|-------|
| Max buildings | 256 |
| Max impostors | 512 |
| LOD0 distance | 50 m |
| LOD1 distance | 150 m |
| LOD2 distance | 400 m |
| Cull distance | 600 m |

---

## Network

> **Source:** `src/Systems/Network/` (framework only — deferred to v0.3.0, no transport layer or backend)

| Parameter | Value |
|-----------|-------|
| Tick rate | 60 ticks/sec |
| Max players | 8 |
| Max ghosts | 8 |
| Input buffer size | 128 |
| Leaderboard page size | 50 |

---

## Difficulty Scaling

> **Source:** `src/Systems/Simulation/AdaptiveDifficultySystem.cs`

| Parameter | Value |
|-----------|-------|
| Full difficulty distance | 10 km |
| Full difficulty time | 5 minutes |
| Max traffic multiplier | 2x |
| Max hazard multiplier | 2.5x |
| Max traffic speed bonus | 30 km/h |
| Max emergency multiplier | 3x |
