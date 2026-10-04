# Changelog

All notable changes to Nightflow will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added

#### In-Run Difficulty Curve
- **DifficultyCurve** - One smooth, unit-tested ramp per run drives traffic density and speed, hazard rate and lethality, emergency frequency, and a rising base cruise speed (spec 08 "Base Speed increases over time"). It eases in, hits the spec's full difficulty at 10 km / 5 min, and keeps building on an asymptotic overdrive tail with no steps, kinks or plateaus. Covered by `DifficultyCurveTests`
- **Cruise assist** - Off the pedals, the car eases up to the run's base cruise speed (90 → 180 → 225 km/h) in Nightflow mode; throttle and brake still override
- **HazardPlacement** - Fairness rule for hazard lanes: within a lane change's worth of road (~90 m at top speed) two lanes always stay free and no blocked lane is cut off from a free neighbour. Covered by `HazardPlacementTests`
- **AdaptiveDifficultyLogic** - Pure cross-run skill rules (run counting, profile update, target, smoothing). Covered by `AdaptiveDifficultyLogicTests`
- `Hazard.Lane` (lane stored at spawn) and `ScoreSummary.HazardsHit`

### Fixed

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
