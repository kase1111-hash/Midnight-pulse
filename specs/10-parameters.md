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
| MaxForwardSpeed | 80 m/s | Reference "fast" speed (not a cap: there is no top speed) |
| MaxPlausibleSpeed | 400 m/s | Save-data sanity bound for recorded speeds |
| MaxDamage | 100 | Crash threshold |
| SegmentLength | 200 m | Track segment length |
| SegmentsAhead | 5 | Lookahead buffer |
| SegmentsBehind | 2 | Segments kept behind before culling |

---

## Momentum (no terminal velocity)

> **Source:** `src/Systems/Core/VehicleDynamics.cs` (rules), `src/Systems/Simulation/VehicleMovementSystem.cs`

`pace(t, d) = 25 + 0.25 × t² / (t + 30) + 0.0012 × d` (t = seconds driven this run, d = metres driven). There is no top speed and no drag: off the pedals the car is pulled up to the pace, throttle boosts above it, the brake trims down to the floor, and speed you gain is speed you keep.

| Parameter | Value |
|-----------|-------|
| Pace at run start | 25 m/s (90 km/h) |
| Pace climb (eased in over 30 s) | 0.25 m/s per second |
| Pace per metre driven | 0.0012 m/s |
| Pull toward the pace | 0.8/s of the gap, max 3 m/s² |
| Throttle boost | 7 / (1 + v / 25) m/s² (never zero) |
| Brake | 25 m/s² |
| Momentum floor (brake limit) | 70% of the pace (a held brake trails it as it rises) |
| Redline mode | pace climb ×1.3, throttle boost ×1.6 |
| Freeflow mode | no pace climb, no floor |
| Collision speed penalty | 30% of impact speed |

Cruising at the pace: ~195 km/h after 2 min, ~265 km/h after 3 min, ~340 km/h after 4 min, ~420 km/h after 5 min, ~505 km/h after 6 min. Full throttle reaches 360 km/h in ~40 s.

## Grip & Loss of Control

> **Source:** `src/Systems/Core/VehicleDynamics.cs`, `src/Systems/Simulation/LaneMagnetismSystem.cs`, `src/Systems/Simulation/CrashSystem.cs`

`grip(v) = 32 × tyres × suspension / (1 + (v / 140)²)` m/s², `load = v² × curvature + LaneLoad(v) × lane-spring acceleration`, `LaneLoad(v) = 0.5 × max(0, v − 20) / 40`. Usage = load / grip (smoothed at 12/s). Past usage 1 the tyre force is scaled back by the usage (the car runs wide), the alignment torque fades (`1 / (1 + 4 × excess)`) and an oversteer torque (`6 × excess × v / 40`) steps the rear out. The run ends (`LOST CONTROL`) when the slip angle passes the limit for the speed.

| Parameter | Value |
|-----------|-------|
| Grip at low speed | 32 m/s² |
| Lift speed (grip halves) | 140 m/s |
| Worn / failed tyres | 70% / 55% |
| Worn / failed suspension | 90% / 80% |
| Handbrake grip | 35% |
| Autopilot grip | 200% (attract mode never slides) |
| Traction warning | usage 0.85 (off below 0.75) |
| Survivable slip | any below 35 m/s, easing to 0.85 rad (~49°) at 75 m/s and above |

Where it bites: the generated road's tightest bends (the generator caps the curvature at R = 300 m, but only reaches it for a metre or two at each segment join; segment middles are ~R 7.5 km) start to slide at ~97 m/s and spin out without a correction at ~120 m/s; lane changes on a straight start to slide at ~108 m/s and spin out at ~140 m/s; handbrake drifts are survivable up to ~55 m/s. (A sustained R = 300 m arc, which the generator never builds, would slide at ~85 m/s.)

---

## Lane Magnetism

> **Source:** `src/Systems/Simulation/LaneMagnetismSystem.cs`, `src/Systems/Core/VehicleDynamics.cs`

Critically damped spring toward the lane target (and the lane-change blend's velocity, so lane changes don't lag), integrated in sub-steps of at most 1/120 s so it behaves the same at any frame rate. The in-lane lean and the emergency yield move the target's position only (their rate is never fed to the spring's damper).

| Parameter | Value |
|-----------|-------|
| omega | 8.0 |
| v_ref | 40 m/s |
| Max lateral speed | 6 m/s (10 m/s sliding, 15 m/s drifting) |
| Edge stiffness | 60 |
| Soft zone | 85% of half the road width |
| Analog in-lane lean | up to 0.9 m below the lane-change threshold, eased at 6/s |
| Emergency yield | moves at most 1.5 m/s, never aims within 0.5 m of the soft edge; a lane change takes it over (it fades with the blend, its rate fed to the spring) |
| Magnetism strength | 1.0 (side damage reduces it) |

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
| Fork magnetism | eases to 70% across a fork, back to 100% after it |
| Lane change cooldown | 0.2 s |
| Blocking window ahead | 12 m + closing speed × lane-change time |
| Blocking window behind | 10 m + closing speed × 1.5 s |

---

## Drift / Yaw

> **Source:** `src/Systems/Core/VehicleDynamics.cs`, `src/Systems/Simulation/VehicleMovementSystem.cs`

| Parameter | Value |
|-----------|-------|
| Min forward speed | 8 m/s |
| Steering gain (k_s) | 1.2 |
| Drift gain (k_d) | 2.5 |
| Recovery gain (k_r) | 2.0 |
| Alignment stiffness | 40 rad/s² per rad of slip |
| Yaw damping | 9/s at 40 m/s, × sqrt(40 / v) (min 45%): twitchier at speed |
| Handbrake alignment | 15% |
| Max yaw rate | 6 rad/s |
| Slip gain (k_slip) | 1.1 |
| Drift magnetism | 0.3 |

---

## Camera

> **Source:** `src/Systems/Presentation/CameraSystem.cs`

Speed-driven values follow `SpeedFeel(v) = 1 − exp(−max(0, v − 12) / 50)` (~0.5 at 50 m/s, ~0.8 at 90 m/s, still creeping toward 1), so FOV, pull-back, blur, speed lines and audio keep building with no top speed. The camera follows the smoothed direction of travel (not the car's nose), smooths its offset rather than its world position (no speed-proportional lag), and cuts to its chase position whenever the car is reset under the black (crash, RESTART, leaving a run for the menu). All smoothing is exponential (frame-rate independent).

| Parameter | Value |
|-----------|-------|
| Base FOV | 55 degrees |
| Max FOV (asymptote) | 95 degrees (+5 drifting) |
| Base Height | 3 m |
| Base Distance | 8 m |
| Speed Height Bonus | 1.5 m |
| Speed Distance Bonus | 4 m |
| Look Ahead Factor | 0.5 |
| Max Look Ahead | 15 m |
| Heading / Distance / Height / FOV response | 6 / 4 / 4 / 3 per s |
| Look response | 10 per s |
| Acceleration pull-back | 0.12 m per m/s² (max 1.2 m back, 0.8 m in) |
| Speed shake | up to 0.06 m, from 45 m/s |
| Traction shake | up to 0.12 m as usage passes 0.8 |
| Collision Shake Intensity | 0.5 |

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

> **Source:** `src/Systems/Core/DifficultyCurve.cs` (in-run ramp), `src/Systems/Core/AdaptiveDifficultyLogic.cs` (cross-run skill modifier), `src/Systems/Core/HazardPlacement.cs` (fairness)

Run progress = `0.5 × distance / 10 km + 0.5 × time / 5 min`. Intensity eases in from 0, reaches 1 ("full difficulty") at progress 1 while still climbing, then continues on a smooth exponential tail toward 1.5. Each value below is `start + (full − start) × intensity`: no steps, no plateau.

| Parameter | Start | Full (intensity 1) | Cap (intensity 1.5) |
|-----------|-------|--------------------|---------------------|
| Full difficulty distance / time | | 10 km / 5 minutes | |
| Traffic multiplier | 1x | 2x | 2.5x |
| Traffic speed bonus | 0 | 30 km/h | 45 km/h |
| Hazard multiplier | 1x | 2.5x | 3.25x |
| Lethal hazard share | 0.6x | 1x | 1.2x |
| Emergency multiplier | 1x (every 45 s) | 3x (15 s) | 4x (~11 s) |
Speed is not on this table: the run's pace (see Momentum above) climbs with no ceiling and is what ends runs. Traffic never spawns faster than 65% of the 80 m/s reference speed (52 m/s), so the player only pulls further ahead as the run builds; traffic and hazards spawn at least 1.6 s / 2 s of travel ahead so they come out of the fog at any speed, and emergency vehicles always drive at least 12 m/s faster than the player.

### Adaptive Difficulty (across runs)

Every player-driven run that lasts at least 3 s is folded into a skill profile when it crashes (rolling averages of score-per-meter multiplier, survival time and hazard avoidance, plus good/bad streaks). After a warm-up of 60 s and 2 runs, a modifier eases (10%/s, held 5 s after each crash) toward a target derived from skill, and multiplies traffic density, traffic speed, hazard rate and hazard lethality.

| Parameter | Value |
|-----------|-------|
| Modifier range | 0.6x – 1.5x |
| Struggling streak (3 runs: avg multiplier < 1.5x or < 30 s) | target × 0.8 |
| Dominating streak (3 runs: avg multiplier ≥ 3x and ≥ 120 s) | target × 1.2 |

### Hazard Fairness

Any stretch of road as long as a lane change at the current speed (lane-change time × speed + 10 m, ~90 m at 80 m/s, never sized below 80 m/s) keeps at least two lanes free, and every blocked lane keeps a free neighbour. Hazards that would break this are placed in another lane instead. Brute-force path search finds no unavoidable walls at any hazard rate.
