Nightflow — Master Technical Design Document
Version 1.0
Date: December 20, 2025
Genre: Endless procedural night-time freeway driving (flow runner)
Core Inspiration: Subway Surfers × BeamNG × OutRun × high-speed night aesthetics
Target Platforms: PC (primary), Console (stretch)
Engine Recommendation: Unity DOTS 1.0+ (HDRP/Entities.Graphics) or Unreal Mass Entity (UE5)
1. Game Overview & Pillars
Tagline: Infinite neon freeway. One life. Flow or crash.
Core Pillars

Flow Over Precision – Smooth, forgiving controls with lane magnetism and autopilot respite
Speed = Score – Faster driving multiplies score; braking/stopping kills multiplier
Visual Rhythm – Wireframe world, dynamic night lighting, emergency strobes, tunnels/overpasses
One Continuous Loop – Crash → instant reset → autopilot → resume; no loading, no hard restarts

Win Condition: Highest score/distance before debilitating crash
2. Visual Style

Entire world rendered in wireframe (initial MVP)
Solid light volumes, bloom, and additive glows for all lights
Night-time city suggested via distant light grids and silhouettes (no full geometry)
Dynamic raytracing (headlights, emergency reflections) when available; fallback screen-space

3. Core Gameplay Loop

Player drives endlessly on procedurally generated freeway
Steer smoothly between lanes, speed up/down, handbrake drift
Avoid/pass traffic, hazards, emergency vehicles
Small hits damage vehicle → handling degradation
Large hit or total failure → crash → fade → score summary → autopilot reset → resume or save

4. Controls

Analog steer (smoothed, lane-based)
Throttle (speed up)
Brake (slow down — ends scoring)
Handbrake (drift/spin — must maintain forward velocity)

5. ECS Architecture Overview (Unity DOTS / Unreal Mass Compatible)
Primary Entity Archetypes

PlayerVehicle
TrafficVehicle
EmergencyVehicle
Hazard
TrackSegment (with DynamicBuffer<LaneSplinePoint>)
LightSource
GhostVehicle (for replays)

Key Components (Data Only)

WorldTransform, Velocity
PlayerInput, Autopilot, SteeringState
LaneFollower, LaneTransition
DamageState, Crashable
TrafficAI, EmergencyAI
LightEmitter (color, intensity, strobe)
ScoreSession, OffscreenSignal, ReplayPlayer

System Execution Order (Simulation Group)

Input / Replay Playback
Autopilot
Steering & Lane Transition
Lane Magnetism
Vehicle Movement & Drift/Yaw
Collision & Impulse
Damage Evaluation
Crash Handling
Procedural Track Generation & Fork Resolution
Traffic / Emergency AI
Hazard Spawning
Scoring
Off-Screen Signaling

Presentation: Camera → Wireframe Render → Lighting → Audio → UI
6. Vehicle Systems
6.1 Lane Magnetism (Critically Damped Spring)
textx_error = current_lateral - target_lateral
a_lat = m * (-ω² x_error - 2ω lateral_vel)
Modulated by steering input, autopilot, speed, handbrake.
6.2 Lane Change & Merge
Blended virtual spline via smoothstep on lateral offset. Same math used for player, traffic, autopilot, and geometric merges.
6.3 Drift, Yaw & Forward Constraint

Decompose velocity into lane frame (vf, vl)
Enforce vf ≥ v_min (~8 m/s)
Explicit yaw torque model (steering + drift)
Slip angle adds lateral slide during handbrake
Recovery torque on handbrake release

6.4 Hazard Impulse & Damage

Impulse J ∝ v_impact × severity
Velocity kick (lateral dominant)
Damage energy Ed ∝ v_impact² × severity → distributed by normal
Handling degradation: reduced steering gain, weaker magnetism, increased slip

Crash only on lethal hazard, total damage threshold, or spin+failure compound.
7. Procedural Freeway Generation

Piecewise Cubic Hermite splines (easy tangent control, stable curvature)
Segment length 40–120 m
Curvature constrained for high-speed safety
Lane splines = centerline + right-vector offsets
Types: Straight, Curve, Tunnel, Overpass, Fork
Elevation ramps for bridges
Deterministic seeding: segmentSeed = hash(globalSeed, segmentIndex)

Forks gradually diverge; unchosen path despawns after commit.
8. Traffic & Emergency AI

Continuous lane desirability scoring (speed, density, emergency, hazard, player, merge)
Hysteresis + lock timer prevents jitter
Emergency vehicles apply rear pressure → avoidance bias in magnetism
Traffic yields and clears wave naturally

9. Camera System

Third-person chase (low angle, slight lead look-at)
Speed-coupled FOV (55°–90°), pull-back offset, motion blur
Yaw follow + drift whip
Impact recoil + procedural shake
Damage wobble, tunnel squeeze
All motion critically damped per axis

10. Audio System

Engine pitch/load blend
Tire slip/skid layer
Wind rush (speed²)
Doppler on moving sources (exaggerated for sirens)
Environment reverb (tunnel boomy, overpass ringing, open dry)
Impact thuds, scrape, damage detune
Off-screen siren intensification

11. Off-Screen Threat Signaling

Crashed vehicles ahead: red/blue strobe leak at screen edges
Emergencies behind: red/white strobe
Screen-space flare quads positioned by projected edge direction
Intensity ∝ urgency (inverse distance) × strobe pulse
Fade as threat enters view

12. Scoring System
Base: distance × speed_tier_multiplier × (1 + riskMultiplier)

Speed tiers: Cruise (1×), Fast (1.5×), Boosted (2.5×)
Risk events spike temporary riskMultiplier (close passes, dodges, emergency clears, drift recoveries)
Decay ~0.8/s
Braking instantly halves riskMultiplier + 2s rebuild delay
Damage reduces cap and rebuild

Special one-time bonuses for perfect segments, full spins, etc.
13. Replay / Ghost System

Record: globalSeed + fixed-timestep input log
Playback: Second PlayerVehicle entity driven by log (identical sim)
Deterministic via seeded PRNG and pure math
Ghosts semi-transparent, non-colliding, optional trail
Leaderboard validation via server re-simulation

14. UI & Menu

Transparent overlay (visible during autopilot)
Minimal HUD: speed, multiplier, score, damage bars
Pause with 5-second cooldown
Score summary on crash (breakdown + save option)

15. Difficulty Progression
Natural scaling via endurance:

Higher base speed
Increased traffic/hazard density
More complex forks
Higher risk reward multipliers

No discrete levels.
16. MVP Development Roadmap

Vehicle movement + lane magnetism + basic spline freeway
Procedural generation (straights/curves) + traffic AI
Lane change, handbrake drift, forward constraint
Hazards, impulses, damage, crash loop
Emergency vehicles + avoidance + off-screen signaling
Scoring + risk bonuses
Camera + audio layers
Wireframe rendering + dynamic lighting
Autopilot + UI overlay
Replay/ghost system
Polish (tunnels, overpasses, forks, ghosts)

17. Future Scalability

Phase 2 damage: Swap to soft-body / component failure
Raytracing: Replace lighting system
Multiplayer: Replicate input logs + seeds
Full city: Populate distant silhouettes

End of Document
