# Nightflow Asset Checklist

A comprehensive list of assets needed for the game. Check off items as they're created.

---

## Audio Assets

### Engine & Vehicle Sounds
- [ ] Engine idle loop (low rumble, loopable)
- [ ] Engine acceleration layers (pitch-modulatable, 3-5 layers by RPM)
- [ ] Engine deceleration/coast sound
- [ ] Tire rolling on asphalt (loopable, speed-modulatable)
- [ ] Tire squeal/skid (intensity layers for drift)
- [ ] Wind rush loop (intensity scales with speed²)

### Collision & Impact Sounds
- [ ] Light impact thud (small hazards: cones, debris)
- [ ] Medium impact crunch (traffic sideswipe)
- [ ] Heavy crash sound (barrier, crashed car)
- [ ] Metal scrape loop (grinding against barriers)
- [ ] Glass shatter (optional, for severe crashes)

### Emergency Vehicle Sounds
- [ ] Police siren loop (doppler-shiftable)
- [ ] Ambulance siren loop (doppler-shiftable)
- [ ] Siren distant/approaching variations

### Environment & Ambient
- [ ] Tunnel reverb impulse response
- [ ] Overpass reverb impulse response
- [ ] Open road ambience (dry, minimal reverb)
- [ ] Distant highway traffic ambience
- [ ] Construction zone noise (optional)

### Music & Score
- [ ] Main gameplay music loop (synthwave/electronic, intensity-modulatable)
- [ ] Low-intensity music layer (cruising speed)
- [ ] High-intensity music layer (boosted speed)
- [ ] Terminal sequence music (end-game credits ambience)
- [ ] Menu/pause music (optional)

### UI Sounds
- [ ] Score tick/increment sound
- [ ] Multiplier increase chime
- [ ] Multiplier lost/reset sound
- [ ] Damage warning beep
- [ ] Near-miss whoosh (risk event)
- [ ] Lane change swoosh (subtle)

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
- [ ] Spark particles (collision)
- [ ] Tire smoke particles (drift/skid)
- [ ] Speed lines effect (high velocity)
- [ ] Crash screen flash effect

---

## UI Assets

### HUD Elements
- [ ] Speedometer design (digital/analog)
- [ ] Speed value display
- [ ] Score counter display
- [ ] Multiplier indicator (1.0x, 1.5x, 2.5x)
- [ ] Damage meter/bar
- [ ] Lane position indicator (optional)

### Fonts
- [ ] Primary UI font (monospace, neon-style)
- [ ] Score/number font (bold, readable at speed)
- [ ] Terminal sequence font (typewriter/console style)

### Icons & Indicators
- [ ] Offscreen emergency vehicle indicator (arrow)
- [ ] Damage warning icon
- [ ] Autopilot active indicator
- [ ] Pause icon
- [ ] Resume icon

### Screens
- [ ] Main menu background
- [ ] Pause overlay
- [ ] Game over/crash screen
- [ ] Score summary layout
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

*Last updated: 2024-12-24*
