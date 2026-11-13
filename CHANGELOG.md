# Changelog

All notable changes to Nightflow will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added

#### Momentum Without Terminal Velocity
- **VehicleDynamics** - One pure, unit-tested home for how the car builds speed and how it loses control (`VehicleDynamicsTests`, tuned against a Python model of the real track generator). There is no top speed in any mode: while the player drives, the run carries the car at a pace that keeps climbing (`25 + 0.25·t²/(t + 30) + 0.0012·d` m/s: ~195 km/h at 2 min, ~340 km/h at 4 min, ~505 km/h at 6 min). Throttle boosts above it (shrinking with speed, never zero), the brake trims it but never below 70% of the pace, and nothing slows the car on its own
- **Grip and loss of control** - Grip falls with speed (the car goes light: halved at 140 m/s) while corners (`v²·curvature`) and lane changes load the tyres more. Past the limit the car runs wide and the rear steps out; catch it (brake, counter-steer, stop asking) or slide past the survivable slip angle for the speed and the run ends with the new `LOST CONTROL` crash. Slow spins stay survivable; the handbrake gets riskier as the momentum builds. Tightest corners start to slide at ~85 m/s, lane changes on a straight at ~110 m/s
- **TractionState** - Tyre load vs grip written by LaneMagnetismSystem and read by movement, crash, camera, audio, post-processing and the HUD
- **Readable edge** - `TRACTION LIMIT` / `LOSING GRIP` HUD warning (with hysteresis), tyre squeal from tyre load, camera shake and chromatic fringe as usage nears 1; a slide caught without crashing pays a "slide save" risk bonus
- **Speed keeps building on screen** - FOV, camera pull-back, motion blur, chromatic aberration, speed lines, wind and engine pitch follow one curve (`VehicleDynamics.SpeedFeel`) that keeps rising instead of maxing out at 250 km/h; the engine climbs through a five-ratio gearbox plus an endless top gear; the HUD speed dots span 100-340 km/h
- **Crash slow motion** - `TimeScaleDriver` (self-instantiating) finally applies the crash flow's time-scale hint: 0.3x on impact easing back to 1x before the fade, never below 0.25x
- **Collision speed penalty** - Hits cost 30% of the impact speed (spec 10), which the pace then rebuilds
- `GameFlowLogic.TakeoverIntent`, `AutopilotLogic.EaseTargetSpeed/DetectionRange/SpeedControl`, `SplineUtilities.EvaluateSignedCurvature`, `DifficultyCurve.TrafficSpeedReference`, `GameConstants.MaxPlausibleSpeed`, `CrashReason.LostControl`, `GameState.TakeoverArmed`, `UIState.TractionUsage/TractionWarning`

#### In-Run Difficulty Curve
- **DifficultyCurve** - One smooth, unit-tested ramp per run drives traffic density and speed, hazard rate and lethality, and emergency frequency. It eases in, hits the spec's full difficulty at 10 km / 5 min, and keeps building on an asymptotic overdrive tail with no steps, kinks or plateaus. Covered by `DifficultyCurveTests`. (Its capped base cruise speed was superseded by the uncapped pace, see Momentum Without Terminal Velocity)
- **HazardPlacement** - Fairness rule for hazard lanes: within a lane change's worth of road (~90 m at top speed) two lanes always stay free and no blocked lane is cut off from a free neighbour. Covered by `HazardPlacementTests`
- **AdaptiveDifficultyLogic** - Pure cross-run skill rules (run counting, profile update, target, smoothing). Covered by `AdaptiveDifficultyLogicTests`
- `Hazard.Lane` (lane stored at spawn) and `ScoreSummary.HazardsHit`

### Fixed

#### Smooth Play
- Lane changes crawled: the lateral position step was multiplied by `dt` twice (~0.03 at 60 fps), so a lane change took ~18 s and depended on the frame rate. Lateral motion now integrates properly in fixed sub-steps (identical at 30/60/144 fps) and the lane spring tracks the lane-change blend's velocity, so the car follows it without lag
- Holding a steering key never moved the car: `m_input = 1 − |steer|` switched the lane spring off at full steer. Analog steering below the lane-change threshold now leans the car inside its lane instead (smoothed, so it hands over to a lane change without a jump)
- The car stayed permanently crooked after every lane change: yaw integrated steering with nothing pulling it back. The tyres now align the nose with the direction of travel; yaw is wrapped, so a full spin settles facing forward
- The attract-mode autopilot weaved between the two centre lanes forever (it steered against the road centre, past the lane-change threshold). Lane magnetism centres it now; it only leans below the threshold and changes lanes by request
- Taking the wheel from a fast player, the autopilot stood on the brakes toward its 35 m/s cruise; it now eases down at 4 m/s² from the handed-over speed, looks hazards up 2.5 s ahead instead of a fixed 80 m, and drives with 2x grip so attract mode never slides
- A lane-change abort snapped the car back (the target jumped to the old lane); the blend now runs backwards from where the car is
- Traffic lane changes were advanced twice per frame (SteeringSystem and TrafficMovementSystem) and ran at double speed
- Overpasses launched the car skyward: OverpassSystem added the elevation offset to the car's height every frame (the road surface isn't raised there). It no longer moves the car, and the camera's overpass lift eases to a target instead of drifting up forever
- The car could drift off the road surface on pitched segments; it now settles onto the road at ride height
- Chase camera: smoothed its world position, so it fell further behind the faster the car went (16 m extra at 80 m/s) and swooped in under braking; it now smooths its offset and follows the direction of travel (no orbiting during spins), all damping is exponential (frame-rate independent), and it cuts to the chase position when the car is reset under the crash fade instead of swooping during the fade-in. CameraSyncBridge no longer double-smooths the FOV
- Lane changes were refused far too often at speed: the blocked-lane window grew to 1.5 s of closing speed (170 m at 100 m/s) for anything in the target lane; it now covers only the road closed during the change (the same window hazard fairness keeps clear)
- Traffic and hazards spawned at fixed distances and would pop in under the car's nose at speed; they now spawn at least 1.6 s / 2 s of travel ahead
- The engine never revved on throttle and the tyres never squealed: nothing wrote `EngineAudio.ThrottleInput` or `TireAudio.SlipRatio`
- The drift-recovery bonus could never fire (it read the yaw on the drift's last frame, already under the exit threshold); it now uses the drift's peak angle
- The HUD rebuilt its speed-tier dots and reformatted every label every frame; it now allocates nothing per frame (no GC hitches)

#### Flow In and Out of Gameplay
- The crash fade to black never appeared: the fade overlay starts `hidden` (`display: none`) and nothing ever showed it, so the summary popped over the live game and the vehicle reset happened in plain view. It is now shown while the crash flow runs, follows the flow's alpha exactly (its CSS transition restarted every frame and lagged ~0.5 s), sits under the summary panel instead of covering it, and never blocks clicks
- The Space/Enter that started a run (or S/W used to navigate a menu, or the key that dismissed the summary) grabbed the wheel the moment the menu closed, starting the run with a handbrake drift or a brake. Takeover now needs a fresh input once every control has been released
- Idle handoff waits 15 s (was 10 s): with the pace carrying the car and magnetism holding the lane, riding a clear straight needs no input and was mistaken for walking away
- High scores from fast runs were silently dropped: save validation rejected any max speed above 100 m/s; the bound is now a plausibility limit (400 m/s)
- The per-frame score cap (50) would eat score at uncapped speeds and low frame rates; it is now a rate (5000/s)


#### Late-Run Difficulty
- Traffic speed grew +2 m/s per km without a cap and overtook the player after ~28 km, emptying the road at the top end; it now follows the curve and is clamped below the player's top speed
- Emergency vehicles were locked to 45 m/s, so they never caught a fast player and the two that spawned sat behind forever, blocking new spawns; they now overtake at player speed + 12 m/s and despawn if they fall 400 m behind
- Track heading was an unbounded random walk (median 1.4 km lateral drift by 10 km, road turning past 90° in a third of 40 km runs); segments now steer gently back toward +Z
- Hazards were placed on world-axis lanes at y = 0 and ended up off the road as soon as it curved away; they now sit on the road spline
- Traffic spacing was checked against world-axis positions instead of the on-road spawn point
- Hazard layouts could form unavoidable walls: random lanes blocked three or four lanes within one lane change of road in 19 of 30 simulated 3 km runs at the original hazard rate, and in all of them at late-run rates; with the fairness rule, none
- Crashed cars could poke into the neighbouring lane through lateral jitter; jitter now stays inside the lane
- Adaptive difficulty never adapted: nothing called `OnRunCompleted`, so `RunsCompleted` stayed 0 and the modifier was stuck at 1.0 forever. CrashSystem now reports every player-driven run, with its score-per-meter multiplier and hazards dodged/hit (hits were never counted). Autopilot time no longer feeds the multiplier average, and the range is narrowed to 0.6–1.5x because it now stacks on the in-run curve
- Traffic AI, lane blocking and steering read hazard lanes from world x, which stopped matching once hazards were placed on the curving road; they now use the stored lane
- Autopilot judged "hazard in my lane" by world x within one lane width and dodged by the sign of x, so on curves it reacted to hazards in other lanes, missed ones in its own, and swerved the wrong way. It now reads lanes (`Hazard.Lane`, its own lane or its committed lane-change target), skips hazards already hit, and escapes to the clearer neighbouring lane (ties toward the road centre), braking instead when no neighbour is safer. Rules live in `AutopilotLogic`, covered by `AutopilotLogicTests`

#### Continuous Play & Autopilot Handoff
- **GameFlowLogic** - Pure, unit-tested rules for the continuous loop: pilot arbitration (menu → autopilot, idle → autopilot, control input → player), crash-phase timing, coasting speed, and fresh-run defaults; covered by `GameFlowLogicTests`
- **Attract mode** - The car drives itself from the first frame under the main menu; the player's first control input hands over the wheel and starts the scoring run. Releasing every control for 10 s hands the wheel back to the autopilot with the score frozen (not lost); the next input resumes it
- **In-place vehicle reset** - CrashSystem performs the post-crash reset (damage, component health, soft-body deformation, drift, collision state) without a scene reload and engages the autopilot; the same path serves Retry, Restart-from-pause and Menu-from-summary
- **RunResultSaveSystem** - Finished runs are submitted to the local leaderboard when the crash summary appears; the summary panel now shows "New High Score" and rank
- `Autopilot.Reason` / `Autopilot.HumanInputDetected`, `SteeringState.LaneChangeRequested/LaneChangeDirection` (autopilot lane changes now go through SteeringSystem's blocked-lane check), `GameState.VehicleResetPending`, `UIState.AutopilotActive` (HUD autopilot indicator is finally wired)
- `CameraState.DistanceOffset/TargetOffset/FOVOffset/YawOffset` consumed by CameraSystem (fork pull-back, overpass elevation follow, tunnel FOV squeeze, screen-space signaling)

#### Atmosphere Overhaul
- **AtmosphereController** - Runtime owner of global distance fog (dense indigo exp2 fog with slow density "breathing") and the night skybox; builds no longer depend on editor-baked lighting settings
- **GroundFog shader** (`Nightflow/GroundFog`) - Animated fbm smoke noise, per-vertex density, neon tint, blends into global fog; now used by GroundFogRenderer
- **NeonVertexGlow shader** (`Nightflow/NeonVertexGlow`) - Additive vertex-color glow with partial fog influence; used by city skyline windows, star field, and moon halo so distant city lights read as soft halos in the haze
- **Light fixture rendering** - Streetlight poles, glowing lamp heads, and tunnel lights generated by ProceduralLightMeshSystem are now actually drawn by ProceduralMeshRenderer
- **StreetlightSpawnSystem** - Spawns sodium streetlights on alternating road sides every 40m and fluorescent ceiling strips in tunnels, culled behind the player with the track; previously no LightSource entities were ever created, leaving the entire light-fixture pipeline dead
- **Always Included Shaders registration** - Setup wizard and play-mode auto-setup register all runtime-found Nightflow shaders in GraphicsSettings so player builds don't strip them

### Changed

#### No Top Speed
- `GameConstants.MaxForwardSpeed` (80 m/s) is a reference speed, not a cap; traffic is tuned against the larger of it and the run's pace
- `DifficultyCurve` no longer carries the capped base cruise speed (`BaseCruiseSpeed`, `ApplyCruiseAssist`); the uncapped pace in `VehicleDynamics` replaces it. Unused `GameModeSystem.GetSpeedLimit` / `GetRedlineAccelerationModifier` removed
- Redline: pace climbs 1.3x faster with a 1.6x throttle boost. Freeflow: no pace climb and no brake floor (still no top speed)
- Lane magnetism edge stiffness 20 → 60 so a sliding car is held on the road

#### The World Never Stops
- Menus (main, mode select, settings, credits, leaderboard, pause) are overlays: they no longer zero the time scale or flag `IsPaused`; the autopilot drives underneath and menu navigation can never grab the wheel. `IsPaused` now means exactly "the pause menu is open"
- Crash flow has a single owner (GameStateSystem); ScreenFlowSystem only mirrors flags to the UI and UISystem shows the game-over panel only once the fade to black completes. A crashed vehicle coasts along the track (never below 8 m/s) instead of freezing
- The autopilot drives through the normal input → steering → lane magnetism → movement pipeline (the separate `AutopilotActiveTag` movement branch is gone; the tag is informational). While it drives, no score or risk events accrue and it takes no structural damage, so the self-playing loop can run unattended without entering the crash flow
- Hazard and emergency spawners keep the world alive in every state instead of stopping when no run is active
- Damage-based lane-magnetism penalty now follows the spec formula (`ω × (1 − 0.5·D_side)`) instead of clamping above the default
- Ghost vehicles only spawn in Ghost mode
- AtmosphereController re-asserts the fog and skybox binding every frame and syncs the skybox horizon haze to the fog color; GroundFog, skyline, star and moon renderers re-acquire the camera when it is created after them

#### Neon Wireframe Pipeline
- ProceduralMeshRenderer now binds the custom `Nightflow/NeonWireframe` and `Nightflow/NeonEmitter` shaders (previously fell back to stock unlit shaders, losing vertex colors, glow, and wireframe edges entirely); per-surface fill alpha: near-solid road/tunnel, translucent glowing vehicle shells
- ParticleMaterialProvider now binds `Nightflow/NeonParticle`, `Nightflow/SmokeParticle`, and `Nightflow/SpeedLines` so per-particle instanced colors work
- All world shaders (wireframes, road, emitters, particles, ground fog) now sample URP distance fog; additive glows dim into the haze with distance
- Denser atmosphere defaults: global fog density 0.002 → 0.008 with indigo tint, ground fog opacity 0.7 → 0.85 with 10 layers, bloom threshold 0.8 → 0.65, bloom intensity 1.5 → 2.2, wireframe glow 2.0 → 2.5
- Night skybox horizon haze strengthened to match the global fog palette

### Fixed
- Compile errors: duplicate `CrashReason`, `CollisionEvent` (particle buffer renamed `CollisionEffectEvent`) and `LeaderboardEntry` (network record renamed `NetworkLeaderboardEntry`) types in `Nightflow.Components`; `SteeringState` lacked the lane-change request fields AutopilotSystem wrote; `SplineSample` has no `Up`; `LightEmitter.Color` assigned a `float4`; `SystemAPI.Query` calls with eight type arguments; `RefRO` of zero-sized tags in queries; `SystemAPI` used in a helper without a `SystemState`; `ref` passed from `RefRW.ValueRO`; missing `using` for ScreenFlowSystem helpers
- After the first crash the car stopped forever: `CrashedTag`/`AutopilotActiveTag` were never removed, damage/score/health were never reset, and player input froze the vehicle
- Two crash-flow state machines advanced the same timer twice with different durations; the pause overlay hijacked the main menu (`IsPaused` was set by every menu)
- ComponentFailureSystem set `IsCrashed` directly, bypassing score finalization and the crash flow; it now requests the crash and CrashSystem owns it
- `EntityArchetypes.Initialize` was never called (ghost spawn used an invalid archetype)
- Mode selection was never applied to `GameModeState`; the Settings screen always returned to the main menu even when opened from pause
- Duplicate HUD writer (HUDUpdateSystem) fought UISystem with different speed-tier thresholds and an ever-growing survival timer
- Ordering attributes that crossed the OrderFirst bucket (ignored with warnings) removed
- Second review pass, compile errors: duplicate `GetPotentialRank`, ambiguous `AudioListener` (SaveManager) and `Random` (AudioManager), non-existent `SaveManager.SaveSettings()`, `LightEmitter.Falloff`, a single-type query deconstructed into a tuple (HeadlightSystem), foreach iteration variables passed by `ref` (spark/smoke/speed-line/music systems), a `WireframeRender → Lighting → Reflection → WireframeRender` ordering cycle, and managed arrays inside Burst-compiled mesh generators (road, vehicle, hazard, overpass) and the tire-smoke wheel table
- Second review pass, runtime: cars, hazards and the bootstrap's first kilometre of road were never given mesh data or vertex buffers (invisible) — `ProceduralMeshInitSystem` now adds them; `EnvironmentState`, `OffscreenSignal`, `DifficultyProfile`, `SirenAudio`, `CollisionEffectEvent` buffers and spark/speed-line `ParticleEmitter`s were never created, leaving the fork/overpass/tunnel effects, off-screen threat signals, adaptive difficulty, sirens, impact flash, sparks and speed lines dead; the crash flash and impact flash queried `GameState`/`CollisionEffectEvent` on the wrong entity; environment systems queried `CameraState` on the player instead of the camera; `CityGenerationSystem` performed structural changes while iterating a query (now uses a command buffer); several systems queried `Unity.Transforms.LocalTransform`, which no Nightflow entity carries
- Particle materials never enabled GPU instancing, so `Graphics.DrawMeshInstanced` failed and tire smoke/sparks/speed lines did not render
- Play-mode auto-setup never enabled `renderPostProcessing` on cameras it created, silently disabling bloom and all post effects; it also never created a ParticleMaterialProvider
- `CreateEmissiveMaterial` preferred the built-in `Particles/Standard Unlit` shader, which renders broken under URP; URP-compatible shaders are now tried first
- Five shader `.meta` files had invalid GUID lengths (33-34 hex chars instead of 32)
- `NightSkybox` was missing the `RenderPipeline = UniversalPipeline` tag
- Documentation incorrectly stated HDRP; the project is URP throughout
- Light fixture housing boxes had all-zero UVs, rendering nearly invisible under the UV-radial NeonEmitter shader; box faces now span the full UV quad
- AtmosphereController no longer leaves RenderSettings.skybox pointing at a destroyed material, and only writes RenderSettings per-frame in play mode (no edit-mode dirty-scene spam)
- Light fixture mesh cache is version-checked against entity reuse and evicted when fixtures are culled (previously unbounded growth over an endless run)

---

## [0.1.0-alpha] - 2026-01-02

### Added

#### Core Systems (MVP Phases 1-11)
- **Vehicle Movement System** - Forward movement, drift, yaw dynamics with lane magnetism
- **Track Generation** - Procedural Hermite spline-based freeway generation
- **Lane Magnetism** - Smooth lane-following assist with configurable strength
- **Lane Change System** - Smoothstep-based lane transitions
- **Handbrake Drift** - Drift mechanics maintaining minimum forward velocity (8 m/s)
- **Collision System** - Zone-based collision detection
- **Impulse Physics** - Impact-based physics responses
- **Damage System** - Zone-based damage (front/rear/left/right)
- **Crash Detection** - Crash loop with instant reset and autopilot recovery
- **Traffic AI** - Lane decisions, movement, and behavior
- **Emergency Vehicles** - Ambulance/police with sirens and yielding behavior
- **Hazard Spawning** - Road debris and hazard generation
- **Off-Screen Signaling** - Warning indicators for approaching hazards
- **Scoring System** - Score calculation with speed tiers and risk multipliers
- **Camera System** - Chase camera with dynamic positioning
- **Wireframe Rendering** - Neon wireframe visual style
- **Audio Systems** - Engine, collision, siren, ambient, and music layers
- **Autopilot System** - AI driving during post-crash recovery
- **HUD Overlay** - Speed, score, damage indicators
- **Replay System** - Input log recording and deterministic playback

#### Post-MVP Features (Complete)
- **Soft-Body Deformation** - Spring-damper physics for visual mesh deformation
- **Component Failures** - Suspension, steering, tires, engine, transmission failures
- **Progressive Handling Degradation** - Per-component handling effects
- **Cascade Failure Detection** - 3+ component failures trigger crash
- **Reflections / SSR** - Distance-based headlight reflections, emergency light bounce estimation, screen-space reflections
- **Procedural City** - GPU-light buildings (256 buildings, 512 impostors)
- **City LOD System** - Aggressive LOD (LOD0=50m, LOD1=150m, LOD2=400m, Cull=600m)
- **City Skyline Renderer** - Star field and moon rendering
- **Input Rebinding** - Full input rebinding support
- **Logitech Wheel Support** - SDK integration with force feedback

#### Post-MVP Features (Framework / Deferred)
- **Ghost Racing** - Async multiplayer using recorded input logs (framework only — ghost entity spawn not implemented)
- **Live Spectator Mode** - 7 camera modes (camera logic implemented, requires multiplayer infrastructure)
- **Leaderboards** - Multiple categories (framework only — no backend integration)
- **Network Replication** - Input-based deterministic replication (framework only — no transport layer)
- **Daily Challenges** - Procedurally generated challenges (deferred to v0.2.0 — fully implemented but awaiting core loop validation)
- **Adaptive Difficulty** - Skill-based scaling (deferred to v0.2.0 — awaiting gameplay data)

#### Architecture
- 131 C# source files organized in modular ECS architecture
- 21 component files defining 60+ component types
- 75+ systems across Simulation, Presentation, Audio, UI, Network, and World groups
- 20+ entity tags and 10+ buffer types
- Configuration-driven design with JSON and C# config files
- Burst-compiled hot paths for performance
- Deterministic simulation for replay playback (ghost racing and network replication deferred)

#### Documentation
- Complete technical specification (SPEC-SHEET.md)
- 11 detailed spec documents covering all systems
- Build system documentation with CI/CD integration
- Editor setup wizard for one-click project configuration

#### Build System
- PowerShell build scripts with batch wrapper
- GitHub Actions CI/CD pipeline
- Automated installer generation with Inno Setup

### Technical Notes

This is the first public alpha release of Nightflow. Core single-player gameplay systems are implemented. Multiplayer systems (ghost racing, leaderboards, spectator, network replication) are framework scaffolding only and deferred to v0.3.0. The game follows the "no scene reloads" critical rule - crashes result in instant reset with autopilot recovery.

**Known Limitations:**
- Alpha release - expect bugs and balance issues
- Performance may vary on lower-end hardware
- Reflection system uses SSR (hardware raytracing not yet implemented)
- Multiplayer systems are framework only (no server/transport layer)
- Daily challenges and adaptive difficulty deferred until core loop validated

---

## [Unreleased]

### Planned
- Performance optimizations (additional Burst compilation)
- Console controller profiles
- Steam Deck verification
- Additional vehicle types
- Weather effects
- Time of day variations
- Additional track environments
