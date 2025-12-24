# Nightflow Asset Checklist

A comprehensive list of assets needed for the game. Check off items as they're created.

---

## Audio Assets

**Audio System Status: IMPLEMENTED** - ECS audio systems and MonoBehaviour bridge complete. Audio clip files need to be created/sourced.

### Engine & Vehicle Sounds
- [ ] Engine idle loop (low rumble, loopable) - **SYSTEM READY** (EngineAudioSystem.cs)
- [ ] Engine acceleration layers (pitch-modulatable, 3-5 layers by RPM) - **SYSTEM READY**
- [ ] Engine deceleration/coast sound - **SYSTEM READY**
- [ ] Tire rolling on asphalt (loopable, speed-modulatable) - **SYSTEM READY**
- [ ] Tire squeal/skid (intensity layers for drift) - **SYSTEM READY**
- [ ] Wind rush loop (intensity scales with speed²) - **SYSTEM READY**

### Collision & Impact Sounds
- [ ] Light impact thud (small hazards: cones, debris) - **SYSTEM READY** (CollisionAudioSystem.cs)
- [ ] Medium impact crunch (traffic sideswipe) - **SYSTEM READY**
- [ ] Heavy crash sound (barrier, crashed car) - **SYSTEM READY**
- [ ] Metal scrape loop (grinding against barriers) - **SYSTEM READY**
- [ ] Glass shatter (optional, for severe crashes) - **SYSTEM READY**

### Emergency Vehicle Sounds
- [ ] Police siren loop (doppler-shiftable) - **SYSTEM READY** (SirenAudioSystem.cs)
- [ ] Ambulance siren loop (doppler-shiftable) - **SYSTEM READY**
- [ ] Siren distant/approaching variations - **SYSTEM READY** (Doppler effect implemented)

### Environment & Ambient
- [ ] Tunnel reverb impulse response - **SYSTEM READY** (AmbientAudioSystem.cs)
- [ ] Overpass reverb impulse response - **SYSTEM READY**
- [ ] Open road ambience (dry, minimal reverb) - **SYSTEM READY**
- [ ] Distant highway traffic ambience - **SYSTEM READY**
- [ ] Construction zone noise (optional)

### Music & Score
- [ ] Main gameplay music loop (synthwave/electronic, intensity-modulatable) - **SYSTEM READY** (MusicSystem.cs)
- [ ] Low-intensity music layer (cruising speed) - **SYSTEM READY**
- [ ] High-intensity music layer (boosted speed) - **SYSTEM READY**
- [ ] Terminal sequence music (end-game credits ambience) - **SYSTEM READY**
- [ ] Menu/pause music (optional) - **SYSTEM READY**

### UI Sounds
- [ ] Score tick/increment sound - **SYSTEM READY** (UIAudioSystem.cs)
- [ ] Multiplier increase chime - **SYSTEM READY**
- [ ] Multiplier lost/reset sound - **SYSTEM READY**
- [ ] Damage warning beep - **SYSTEM READY**
- [ ] Near-miss whoosh (risk event) - **SYSTEM READY**
- [ ] Lane change swoosh (subtle) - **SYSTEM READY**

---

## Visual Assets

### Shaders
- [x] Wireframe rendering shader (with glow/bloom support) - **IMPLEMENTED** (NeonWireframe.shader, VehicleWireframe.shader)
- [x] Road surface shader (lane lines, reflections) - **IMPLEMENTED** (RoadSurface.shader)
- [x] Neon glow shader (for lights and UI) - **IMPLEMENTED** (NeonEmitter.shader)
- [x] Motion blur post-process shader - **IMPLEMENTED** (MotionBlur.shader)
- [x] Bloom post-process shader - **IMPLEMENTED** (NeonBloom.shader)
- [x] Film grain overlay shader - **IMPLEMENTED** (FilmGrain.shader)
- [x] Skybox/horizon gradient shader - **IMPLEMENTED** (NightSkybox.shader)

### Materials
- [x] Player vehicle material (Cyan glow - #00FFFF) - **IMPLEMENTED** (PlayerVehicle.mat)
- [x] Traffic vehicle material (Magenta - #FF00FF) - **IMPLEMENTED** (TrafficVehicle.mat)
- [x] Emergency vehicle material (Red/Blue strobe) - **IMPLEMENTED** (EmergencyVehiclePolice.mat, EmergencyVehicleAmbulance.mat)
- [x] Road surface material (dark with lane markings) - **IMPLEMENTED** (RoadSurface.mat)
- [x] Barrier material (Orange - #FF8800) - **IMPLEMENTED** (Barrier.mat)
- [x] Hazard materials (cone, debris, tire) - **IMPLEMENTED** (HazardCone.mat, HazardDebris.mat, HazardTire.mat)
- [x] Streetlight material (warm sodium - #FFD080) - **IMPLEMENTED** (Streetlight.mat, StreetlightPole.mat)
- [x] Tunnel interior material - **IMPLEMENTED** (TunnelInterior.mat, TunnelLight.mat)
- [x] Overpass material - **IMPLEMENTED** (Overpass.mat)
- [x] Ghost vehicle material - **IMPLEMENTED** (GhostVehicle.mat)
- [x] Headlight/Taillight materials - **IMPLEMENTED** (Headlight.mat, Taillight.mat)

### 3D Models/Meshes

#### Vehicles
- [x] Player car mesh - **PROCEDURAL** (ProceduralVehicleMeshSystem) - Neon Cyan #00FFFF + headlights/taillights
- [x] Traffic car variant 1 (sedan) - **PROCEDURAL** (ProceduralVehicleMeshSystem) - Neon Magenta #FF00FF
- [x] Traffic car variant 2 (SUV) - **PROCEDURAL** (ProceduralVehicleMeshSystem) - Neon Magenta #FF00FF
- [x] Traffic car variant 3 (truck/van) - **PROCEDURAL** (ProceduralVehicleMeshSystem) - Neon Magenta #FF00FF
- [x] Police car mesh - **PROCEDURAL** (ProceduralVehicleMeshSystem) - Red/Blue #FF0000/#0066FF + light bar
- [x] Ambulance mesh - **PROCEDURAL** (ProceduralVehicleMeshSystem) - Red/White + light bar
- [x] Ghost vehicle mesh - **PROCEDURAL** (ProceduralVehicleMeshSystem) - Dim Cyan (50% alpha)

#### Road & Environment
- [x] Road segment mesh (straight, 4-lane) - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [x] Road segment mesh (curved) - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [x] Tunnel entrance/exit mesh - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [x] Tunnel interior segment mesh - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [x] Overpass structure mesh - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [x] Fork/merge segment mesh - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [x] Streetlight post mesh - **PROCEDURAL** (ProceduralLightMeshSystem) - Warm Sodium #FFD080
- [x] Highway barrier mesh (continuous) - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [x] Tunnel ceiling lights - **PROCEDURAL** (ProceduralLightMeshSystem) - Cool fluorescent
- [x] Overpass underside lights - **PROCEDURAL** (ProceduralLightMeshSystem)

#### Hazards
- [x] Traffic cone mesh - **PROCEDURAL** (ProceduralHazardMeshSystem) - Neon Orange #FF8800
- [x] Debris pile mesh - **PROCEDURAL** (ProceduralHazardMeshSystem)
- [x] Loose tire mesh - **PROCEDURAL** (ProceduralHazardMeshSystem)
- [x] Barrier block mesh - **PROCEDURAL** (ProceduralHazardMeshSystem)
- [x] Crashed car mesh - **PROCEDURAL** (ProceduralHazardMeshSystem)

### Particles & Effects
- [x] Headlight beam effect - **PROCEDURAL** (ProceduralVehicleMeshSystem) - Warm white glow
- [x] Taillight glow effect - **PROCEDURAL** (ProceduralVehicleMeshSystem) - Red glow
- [x] Emergency strobe effect (red/blue) - **PROCEDURAL** (ProceduralVehicleMeshSystem) - Light bar
- [x] Spark particles (collision) - **IMPLEMENTED** (SparkParticleSystem.cs + NeonParticle.shader)
- [x] Tire smoke particles (drift/skid) - **IMPLEMENTED** (TireSmokeParticleSystem.cs + SmokeParticle.shader)
- [x] Speed lines effect (high velocity) - **IMPLEMENTED** (SpeedLinesSystem.cs + SpeedLines.shader)
- [x] Crash screen flash effect - **IMPLEMENTED** (CrashFlashSystem.cs + CrashFlash.shader)

---

## UI Assets

### HUD Elements
- [x] Speedometer design (digital/analog) - **IMPLEMENTED** (NightflowHUD.uxml + UIController.cs)
- [x] Speed value display - **IMPLEMENTED** (NightflowHUD.uxml)
- [x] Score counter display - **IMPLEMENTED** (NightflowHUD.uxml)
- [x] Multiplier indicator (1.0x, 1.5x, 2.5x) - **IMPLEMENTED** (NightflowHUD.uxml)
- [x] Damage meter/bar - **IMPLEMENTED** (NightflowHUD.uxml + damage zones)
- [x] Lane position indicator (optional) - **IMPLEMENTED** (NightflowHUD.uxml)

### Fonts
- [ ] Primary UI font (monospace, neon-style)
- [ ] Score/number font (bold, readable at speed)
- [ ] Terminal sequence font (typewriter/console style)

### Icons & Indicators
- [x] Offscreen emergency vehicle indicator (arrow) - **IMPLEMENTED** (NightflowHUD.uxml + WarningIndicatorSystem.cs)
- [x] Damage warning icon - **IMPLEMENTED** (NightflowHUD.uxml warning-indicator)
- [x] Autopilot active indicator - **IMPLEMENTED** (NightflowHUD.uxml status-indicator)
- [ ] Pause icon
- [ ] Resume icon

### Screens
- [ ] Main menu background
- [x] Pause overlay - **IMPLEMENTED** (NightflowHUD.uxml pause-overlay)
- [x] Game over/crash screen - **IMPLEMENTED** (NightflowHUD.uxml gameover-overlay)
- [x] Score summary layout - **IMPLEMENTED** (NightflowHUD.uxml score-summary)
- [ ] Terminal sequence background (end credits)

---

## Configuration Files

### Gameplay Tuning (JSON/YAML)
- [ ] Vehicle physics parameters
- [ ] Lane magnetism settings
- [ ] Traffic AI weights and thresholds
- [ ] Hazard spawn rates and types
- [ ] Scoring multipliers and thresholds
- [ ] Camera behavior parameters

### Audio Configuration
- [ ] Engine pitch curves (speed to pitch mapping)
- [ ] Volume curves (speed to volume mapping)
- [ ] Doppler effect parameters
- [ ] Reverb zone presets (tunnel, overpass, open)
- [ ] Music intensity mapping

### Visual Configuration
- [ ] Color palette definitions
- [ ] Light intensity presets
- [ ] Bloom/post-process settings
- [ ] Wireframe rendering parameters

---

## Priority Order (Recommended)

### Phase 1: Core Gameplay (MVP)
1. ~~Player car mesh~~ - **DONE** (Procedural generation implemented)
2. Basic wireframe shader
3. ~~Road segment meshes~~ - **DONE** (Procedural generation implemented)
4. Engine sound loop
5. Primary UI font
6. Speedometer HUD
7. Score display

### Phase 2: Traffic & Environment
1. ~~Traffic car variants (2-3)~~ - **DONE** (Procedural generation implemented)
2. ~~Streetlight mesh~~ - **DONE** (Procedural generation implemented)
3. ~~Barrier mesh~~ - **DONE** (Procedural generation implemented)
4. Tire/wind sounds
5. Collision sounds
6. ~~Hazard meshes~~ - **DONE** (Procedural generation implemented)

### Phase 3: Polish & Atmosphere
1. ~~Emergency vehicle assets~~ - **DONE** (Procedural generation implemented)
2. Siren sounds with doppler
3. ~~Tunnel/overpass meshes~~ - **DONE** (Procedural generation implemented)
4. Reverb impulse responses
5. Music loops
6. Particle effects (mostly done - sparks/smoke remaining)

### Phase 4: Final Polish
1. Motion blur shader
2. Film grain effect
3. All UI screens
4. Terminal sequence assets
5. Configuration externalization

---

## Color Palette Reference

| Element | Color | Hex |
|---------|-------|-----|
| Player Vehicle | Cyan | `#00FFFF` |
| Traffic Vehicles | Magenta | `#FF00FF` |
| Emergency (Police) | Red/Blue | `#FF0000` / `#0000FF` |
| Hazards/Warnings | Orange | `#FF8800` |
| Lane Markings | Blue | `#4488FF` |
| Streetlights | Warm Sodium | `#FFD080` |
| Road Surface | Dark Gray | `#1A1A1A` |
| Background/Sky | Deep Black | `#0A0A0A` |

---

## Notes

- All audio should be high-quality (44.1kHz minimum, 48kHz preferred)
- Loopable sounds should have seamless loop points
- 3D models should be optimized for real-time rendering (low poly count)
- Wireframe aesthetic means many meshes can be simple geometry
- ~~Consider procedural generation for road segments to reduce asset count~~ **IMPLEMENTED!**
- UI should be readable at high speeds (large, high-contrast text)

---

## Procedural Generation Details

### Road & Environment (`ProceduralRoadMeshSystem`)

| Asset | Generation Method | Parameters |
|-------|-------------------|------------|
| Road surface | Hermite spline extrusion | 40 length segments, 8 width segments |
| Lane markings | Overlay quads with dashing | 3m dash, 6m gap |
| Barriers | Edge extrusion | 0.8m height, both sides |
| Tunnel walls | Arch profile extrusion | 6m height, 16m width |
| Tunnel ceiling | Curved arch | 8 arch segments |
| Overpass deck | Elevated road + sinusoidal profile | h(t) = 8m × sin(πt) |
| Support pillars | Box geometry | 1.5m width, 40m spacing |

### Hazards (`ProceduralHazardMeshSystem`)

| Asset | Generation Method | Color |
|-------|-------------------|-------|
| Traffic cone | Octagonal cone with stripes | Neon Orange #FF8800 + White stripes |
| Debris pile | Irregular extruded polygon | Dim orange/yellow |
| Loose tire | Torus (12 segments × 6 ring) | Dark gray with subtle glow |
| Barrier block | Box with beveled edges | Bright orange #FF6600 |
| Crashed car | Deformed box with tilt | Dimmed Magenta #FF00FF |

**Cone Details:**
- Height: 70cm, Base radius: 20cm, Top radius: 3cm
- 8 radial segments (octagonal wireframe look)
- 3 height segments with 2 white stripe bands
- Glow intensity: 1.2x (bright neon)

### Vehicles (`ProceduralVehicleMeshSystem`)

| Vehicle | Body Style | Color | Features |
|---------|------------|-------|----------|
| Player | Sedan | Neon Cyan #00FFFF | Headlights, taillights, 1.5x glow |
| Traffic (Sedan) | Sedan | Neon Magenta #FF00FF | Headlights, taillights |
| Traffic (SUV) | SUV | Neon Magenta #FF00FF | Taller profile |
| Traffic (Truck) | Truck/Van | Neon Magenta #FF00FF | Larger box shape |
| Police | Sedan | Red/Blue | Light bar with strobes |
| Ambulance | Van | Red/White | Light bar with strobes |
| Ghost | Sedan | Dim Cyan (50% alpha) | No lights, 0.6x glow |

**Vehicle Dimensions:**
- Sedan: 4.5m × 1.8m × 1.4m
- SUV: 4.8m × 2.0m × 1.8m
- Truck: 5.5m × 2.1m × 2.2m

**Lights:**
- Headlights: 12cm radius, warm white, circular
- Taillights: 20cm × 8cm, red, rectangular

### Streetlights (`ProceduralLightMeshSystem`)

| Light Type | Structure | Color |
|------------|-----------|-------|
| Streetlight | 8m pole + 2.5m arm + fixture | Warm Sodium #FFD080 |
| Tunnel light | Ceiling strip 1.2m × 0.15m | Cool fluorescent |
| Overpass light | Circular downlight 30cm | Warm Sodium |

**Streetlight Details:**
- Hexagonal pole (6 segments, slight taper)
- Curved arm with downward droop
- Rectangular fixture with glowing bottom

**Files:**
- `src/Components/Presentation/ProceduralMeshComponents.cs` - Mesh data components
- `src/Systems/Presentation/ProceduralRoadMeshSystem.cs` - Road mesh generation
- `src/Systems/Presentation/ProceduralHazardMeshSystem.cs` - Hazard mesh generation
- `src/Systems/Presentation/ProceduralVehicleMeshSystem.cs` - Vehicle mesh generation
- `src/Systems/Presentation/ProceduralLightMeshSystem.cs` - Light fixture mesh generation
- `src/Buffers/BufferElements.cs` - MeshVertex, MeshTriangle, SubMeshRange buffers

---

## Shader Details

All shaders are written for Unity's Universal Render Pipeline (URP).

### Core Shaders

| Shader | File | Purpose |
|--------|------|---------|
| Neon Wireframe | `NeonWireframe.shader` | General wireframe with glow/bloom |
| Vehicle Wireframe | `VehicleWireframe.shader` | Vehicles with damage flash, speed pulse |
| Road Surface | `RoadSurface.shader` | Dark road with glowing lane markings |
| Neon Emitter | `NeonEmitter.shader` | Lights with optional strobe effect |
| Night Skybox | `NightSkybox.shader` | Deep black sky with stars and city glow |

### Post-Process Shaders

| Shader | File | Purpose |
|--------|------|---------|
| Neon Bloom | `NeonBloom.shader` | Multi-pass bloom optimized for neon |
| Motion Blur | `MotionBlur.shader` | Speed-based radial blur |
| Film Grain | `FilmGrain.shader` | Grain, scanlines, vignette |

### Shader Properties

**NeonWireframe / VehicleWireframe:**
- Wire Thickness: 0.001 - 0.1
- Glow Intensity: 0.5 - 5.0
- Glow Falloff: 0.1 - 10.0
- Fill Alpha: 0 - 0.3

**RoadSurface:**
- Lane Line Color: Blue #4488FF
- Edge Line Color: Orange #FF8800
- Dash Length: 3m, Gap: 6m
- Reflection Strength: 0 - 1

**NeonEmitter:**
- Emission Intensity: 0.5 - 10.0
- Strobe support for emergency lights
- Pulse animation for ambient lights

**Post-Process Stack:**
1. Bloom (threshold: 0.8, intensity: 1.5)
2. Motion Blur (speed-based, radial)
3. Film Grain (intensity: 0.1, scanlines: 0.1)

**Files:**
- `src/Shaders/NeonWireframe.shader` - General wireframe
- `src/Shaders/VehicleWireframe.shader` - Vehicle-specific wireframe
- `src/Shaders/RoadSurface.shader` - Road with lane markings
- `src/Shaders/NeonEmitter.shader` - Light emitters
- `src/Shaders/NightSkybox.shader` - Night sky
- `src/Shaders/NeonBloom.shader` - Bloom post-process
- `src/Shaders/MotionBlur.shader` - Motion blur post-process
- `src/Shaders/FilmGrain.shader` - Film grain post-process

---

## UI System Details

### Layout & Styling

| File | Purpose |
|------|---------|
| `NightflowHUD.uxml` | UI Toolkit layout - HUD, overlays, menus |
| `NightflowHUD.uss` | Stylesheet with neon aesthetic |

### ECS Components (`src/Components/UI/UIComponents.cs`)

| Component | Purpose |
|-----------|---------|
| `UIState` | HUD data singleton (speed, score, damage, warnings) |
| `GameState` | Game flow state (pause, crash phase, timescale) |
| `ScoreSummaryDisplay` | End-of-run statistics |
| `HUDNotification` | Buffer for popup messages |
| `CrashFlowPhase` | Crash sequence phases enum |
| `MenuState` | Menu state enum |
| `CrashReason` | Crash cause enum |
| `HUDNotificationType` | Notification types enum |

### Systems (`src/Systems/UI/`)

| System | Purpose |
|--------|---------|
| `HUDUpdateSystem` | Updates UIState from player vehicle data |
| `ScreenFlowSystem` | Manages game state transitions, crash flow, pause |
| `WarningIndicatorSystem` | Off-screen threat indicators |

### MonoBehaviour Bridge (`src/UI/UIController.cs`)

Connects ECS UIState to UI Toolkit elements:
- Reads UIState singleton every frame
- Updates visual elements (labels, progress bars, overlays)
- Handles button callbacks (pause, restart, quit)
- Manages notification queue and animations

### HUD Layout Structure

```
hud-root
├── top-bar
│   ├── score-container (SCORE + value)
│   ├── multiplier-container (x + fill bar)
│   └── distance-container (DISTANCE + km)
├── left-panel
│   ├── damage-indicator (INTEGRITY bar + zones)
│   └── warning-indicator (! + text)
├── right-panel
│   ├── speedometer (value + KM/H + tier dots)
│   └── lane-indicator (5 dots)
├── bottom-bar
│   ├── autopilot-indicator
│   └── boost-indicator
├── notification-container
├── emergency-arrows (left, right, behind)
├── pause-overlay (menu-panel)
├── gameover-overlay (crash-title, summary, buttons)
└── fade-overlay
```

### CSS Variables

| Variable | Value | Usage |
|----------|-------|-------|
| `--color-cyan` | #00FFFF | Primary accent, player |
| `--color-magenta` | #FF00FF | Multiplier, boost |
| `--color-orange` | #FF8800 | Warnings, hazards |
| `--color-red` | #FF0000 | Critical, damage |
| `--color-yellow` | #FFFF00 | Bonus, high score |

### Crash Flow Sequence

1. **Impact** (0.2s) - Slow-mo, initial shake
2. **ScreenShake** (0.8s) - Extended shake effect
3. **FadeOut** (0.5s) - Fade to black
4. **Summary** (2.0s min) - Score breakdown display
5. **Reset** (0.3s) - Vehicle repositioning
6. **FadeIn** (0.5s) - Fade back, autopilot starts

---

## Particle System Details

### Components (`src/Components/Presentation/ParticleComponents.cs`)

| Component | Purpose |
|-----------|---------|
| `Particle` | Individual particle data (position, velocity, color, lifetime) |
| `ParticleEmitter` | Emitter configuration (rate, colors, sizes, lifetimes) |
| `ParticleType` | Enum: Spark, TireSmoke, SpeedLine, CrashFlash, Debris, Glow |
| `SparkEmitterTag` | Tag for spark emitters on vehicles |
| `TireSmokeEmitterTag` | Tag for tire smoke emitters at wheel positions |
| `SpeedLineEffect` | Screen-space velocity streak controller |
| `CrashFlashEffect` | Screen flash overlay controller |
| `CollisionEvent` | Collision data for triggering sparks |
| `DriftState` | Drift detection for tire smoke triggering |

### Systems (`src/Systems/Presentation/`)

| System | Purpose |
|--------|---------|
| `SparkParticleSystem` | Spawns/updates orange spark particles on collision |
| `TireSmokeParticleSystem` | Spawns/updates gray smoke with cyan tint during drift |
| `SpeedLinesSystem` | Spawns/updates white/cyan streaks at high speed |
| `CrashFlashSystem` | Manages screen flash phases (FlashIn → Hold → FadeOut) |
| `ParticleRenderSystem` | GPU-instanced rendering for all particle types |
| `DriftDetectionSystem` | Detects drift state from vehicle physics |
| `ImpactFlashTriggerSystem` | Triggers flash effects on collision impacts |

### Shaders (`src/Shaders/`)

| Shader | Purpose |
|--------|---------|
| `NeonParticle.shader` | Additive billboard particles with glow for sparks |
| `SmokeParticle.shader` | Alpha-blended volumetric smoke with noise animation |
| `SpeedLines.shader` | Elongated additive streaks for velocity effect |
| `CrashFlash.shader` | Post-process screen flash with chromatic aberration |

### Spark Particles

| Property | Value |
|----------|-------|
| Particles per impact | 12 (scales with impulse) |
| Speed | 5-15 m/s |
| Lifetime | 0.4s |
| Size | 0.02-0.08m |
| Gravity | 15 m/s² |
| Color | Orange #FF9900 → Red #FF3300 (fading) |
| Emission | 3-5x (bright glow) |

### Tire Smoke

| Property | Value |
|----------|-------|
| Emission rate | 30 particles/sec at full drift |
| Start size | 0.3m |
| End size | 1.5m |
| Speed | 1-3 m/s |
| Lifetime | 1.2s |
| Rise speed | 1.5 m/s |
| Color | Dark gray with subtle cyan tint |
| Drag | 2.0 |

### Speed Lines

| Property | Value |
|----------|-------|
| Speed threshold | 120 km/h (starts appearing) |
| Max effect speed | 250 km/h (full intensity) |
| Emission rate | 100 lines/sec at max |
| Line length | 2-8m (based on speed) |
| Lifetime | 0.1-0.3s |
| Spawn radius | 3-15m around player |
| Colors | White/cyan (70%), Cyan (20%), Magenta (10%) |

### Crash Flash

| Phase | Duration | Effect |
|-------|----------|--------|
| FlashIn | 0.05s | Quick ramp to white |
| Hold | 0.08s | Peak white, transition to red |
| FadeOut | 0.4s | Red/orange fade with chromatic aberration |

| Flash Type | Trigger | Intensity |
|------------|---------|-----------|
| Crash | GameState crash phase | Full effect |
| LightImpact | Impulse ≥ 10 | 30% alpha |
| MediumImpact | Impulse ≥ 30 | 60% alpha |
| Damage | Damage event | Red tint |

---

## Audio System Details

### Components (`src/Components/Audio/AudioComponents.cs`)

| Component | Purpose |
|-----------|---------|
| `EngineAudio` | Vehicle engine state (RPM, throttle, layer volumes, pitch) |
| `TireAudio` | Tire rolling and skid sounds (speed, slip ratio) |
| `WindAudio` | Wind rush audio (speed², turbulence) |
| `SirenAudio` | Emergency siren with doppler (position, velocity, phase) |
| `CollisionAudioEvent` | One-shot collision sound request |
| `ScrapeAudio` | Continuous metal scraping sound |
| `ReverbZone` | Environment reverb (tunnel, overpass, open road) |
| `AmbientAudio` | Ambient layer state (volume, fade) |
| `MusicState` | Dynamic music (intensity, layer volumes, transitions) |
| `UIAudioEvent` | UI sound request |
| `AudioConfig` | Global audio settings (volumes, doppler, distance) |
| `AudioListener` | Listener position/velocity for 3D audio |

### Systems (`src/Systems/Audio/`)

| System | Purpose |
|--------|---------|
| `EngineAudioSystem` | Updates engine audio from vehicle state |
| `CollisionAudioSystem` | Processes collision events, triggers sounds |
| `SirenAudioSystem` | Calculates doppler shift, updates siren state |
| `AmbientAudioSystem` | Manages ambient layers and reverb zones |
| `MusicSystem` | Dynamic intensity-based music crossfading |
| `UIAudioSystem` | Processes UI sound events |

### MonoBehaviour Bridge (`src/Audio/AudioManager.cs`)

Connects ECS audio to Unity AudioSources:
- Pooled one-shot sources for impacts
- Persistent looping sources for engine/ambient
- AudioMixer group routing
- Volume/pitch interpolation

### Engine Audio Layers

| Layer | RPM Range | Crossfade |
|-------|-----------|-----------|
| Idle | 600-1500 | Full at idle, fades as RPM rises |
| Low | 1000-3500 | Bell curve centered at 2250 |
| Mid | 3000-5500 | Bell curve centered at 4250 |
| High | 5000-8000 | Bell curve centered at 6500 |

**Pitch Modulation:** 0.8x - 2.0x based on normalized RPM

### Doppler Effect

```
f' = f × (c + v_listener) / (c + v_source)
pitch = 1 + (relativeVelocity / speedOfSound) × dopplerScale
```

- Speed of sound: 343 m/s (configurable)
- Doppler scale: 1.0 (configurable)
- Clamped: 0.5x - 2.0x pitch range

### Siren Patterns

| Type | Pattern | Frequency |
|------|---------|-----------|
| Police | Smooth sine wail | 1.5 Hz |
| Ambulance | Sharp yelp alternation | 4.0 Hz |
| Fire | Periodic horn blasts | 0.8 Hz |

### Dynamic Music

| Layer | Trigger | Intensity |
|-------|---------|-----------|
| Base | Always | 100% |
| Low Intensity | < 0.3 intensity | Fades from 100% → 20% |
| High Intensity | > 0.7 intensity | Fades in 0% → 100% |

**Intensity Sources:**
- Speed (80-200 km/h): 0-50%
- Multiplier (1x-4x): 0-30%
- Damage level: 0-20%
- Events: +20-40% for 2-8 seconds

### Reverb Presets

| Environment | Decay Time | Early Reflections | Late Reverb |
|-------------|------------|-------------------|-------------|
| Open Road | 0.5s | 0.1 | 0.05 |
| Tunnel | 3.5s | 0.6 | 0.7 |
| Overpass | 1.5s | 0.4 | 0.3 |
| Urban | 2.0s | 0.3 | 0.4 |

### Audio Mixer Groups

| Group | Contents |
|-------|----------|
| Master | All audio |
| Music | Dynamic music layers |
| SFX | Collisions, one-shots |
| Engine | Engine layers, tires, wind |
| Ambient | Environment, distant traffic |

---

*Last updated: 2024-12-24*
