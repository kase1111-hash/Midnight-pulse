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
- [ ] Wireframe rendering shader (with glow/bloom support)
- [ ] Road surface shader (lane lines, reflections)
- [ ] Neon glow shader (for lights and UI)
- [ ] Motion blur post-process shader
- [ ] Bloom post-process shader
- [ ] Film grain overlay shader
- [ ] Skybox/horizon gradient shader

### Materials
- [ ] Player vehicle material (Cyan glow - #00FFFF)
- [ ] Traffic vehicle material (Magenta - #FF00FF)
- [ ] Emergency vehicle material (Red/Blue strobe)
- [ ] Road surface material (dark with lane markings)
- [ ] Barrier material (Orange - #FF8800)
- [ ] Hazard materials (cone, debris, tire)
- [ ] Streetlight material (warm sodium - #FFD080)
- [ ] Tunnel interior material
- [ ] Overpass material

### 3D Models/Meshes

#### Vehicles
- [ ] Player car mesh (low-poly/wireframe style)
- [ ] Traffic car variant 1 (sedan)
- [ ] Traffic car variant 2 (SUV)
- [ ] Traffic car variant 3 (truck/van)
- [ ] Police car mesh
- [ ] Ambulance mesh
- [ ] Crashed car mesh (damaged, stationary)

#### Road & Environment
- [x] Road segment mesh (straight, 4-lane) - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [x] Road segment mesh (curved) - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [x] Tunnel entrance/exit mesh - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [x] Tunnel interior segment mesh - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [x] Overpass structure mesh - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [x] Fork/merge segment mesh - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [ ] Streetlight post mesh
- [x] Highway barrier mesh (continuous) - **PROCEDURAL** (ProceduralRoadMeshSystem)
- [ ] Guardrail mesh

#### Hazards
- [x] Traffic cone mesh - **PROCEDURAL** (ProceduralHazardMeshSystem) - Neon Orange #FF8800
- [x] Debris pile mesh - **PROCEDURAL** (ProceduralHazardMeshSystem)
- [x] Loose tire mesh - **PROCEDURAL** (ProceduralHazardMeshSystem)
- [x] Barrier block mesh - **PROCEDURAL** (ProceduralHazardMeshSystem)
- [x] Crashed car mesh - **PROCEDURAL** (ProceduralHazardMeshSystem)

### Particles & Effects
- [ ] Headlight beam effect
- [ ] Taillight glow effect
- [ ] Emergency strobe effect (red/blue)
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
1. Player car mesh
2. Basic wireframe shader
3. ~~Road segment meshes~~ - **DONE** (Procedural generation implemented)
4. Engine sound loop
5. Primary UI font
6. Speedometer HUD
7. Score display

### Phase 2: Traffic & Environment
1. Traffic car variants (2-3)
2. Streetlight mesh
3. ~~Barrier mesh~~ - **DONE** (Procedural generation implemented)
4. Tire/wind sounds
5. Collision sounds
6. ~~Hazard meshes~~ - **DONE** (Procedural generation implemented)

### Phase 3: Polish & Atmosphere
1. Emergency vehicle assets
2. Siren sounds with doppler
3. ~~Tunnel/overpass meshes~~ - **DONE** (Procedural generation implemented)
4. Reverb impulse responses
5. Music loops
6. Particle effects

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

**Files:**
- `src/Components/Presentation/ProceduralMeshComponents.cs` - Mesh data components
- `src/Systems/Presentation/ProceduralRoadMeshSystem.cs` - Road mesh generation
- `src/Systems/Presentation/ProceduralHazardMeshSystem.cs` - Hazard mesh generation
- `src/Buffers/BufferElements.cs` - MeshVertex, MeshTriangle, SubMeshRange buffers

---

*Last updated: 2024-12-24*
