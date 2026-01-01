# Camera & Rendering Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## Camera System

### Base Configuration

| Property | Specification |
|----------|---------------|
| Type | Third-person chase (low angle, slight lead look-at) |
| Base FOV | 60 degrees |
| Max FOV | 80 degrees |
| FOV Coupling | Speed-driven |

### Dynamic Behaviors

| Behavior | Description |
|----------|-------------|
| Speed FOV | FOV scales with speed (baseFOV + speedFOVBonus) |
| Pull-back | Camera distance increases at high speed |
| Height Increase | Camera height increases at high speed |
| Yaw Follow | Camera follows vehicle yaw with damping |
| Drift Whip | Extra rotational follow during drift |
| Motion Blur | Speed^2 intensity scaling |
| Look Ahead | Forward offset based on speed (max 15m) |

### Impact & Feedback

| Effect | Trigger |
|--------|---------|
| Impact Recoil | Collision impulse |
| Procedural Shake | Rough road, construction, suspension damage |
| Damage Wobble | Accumulated damage |
| Tunnel Squeeze | Entering tunnel segments |
| Speed Shake | Subtle vibration at high speed |

All motion critically damped per axis.

### Default Parameters

| Parameter | Value |
|-----------|-------|
| Base FOV | 60 degrees |
| Speed FOV Bonus | 20 degrees |
| Base Height | 3 m |
| Base Distance | 8 m |
| Speed Height Bonus | 1.5 m |
| Speed Distance Bonus | 4 m |
| Max Look Ahead | 15 m |
| Position Smooth Speed | 8 |
| Rotation Smooth Speed | 5 |
| Collision Shake Intensity | 0.5 |
| Collision Shake Duration | 0.3 s |

---

## Visual Style

- Entire world rendered in wireframe (initial MVP)
- Solid light volumes, bloom, and additive glows
- Night-time city with procedural buildings and light grids
- Dynamic raytracing when available; fallback screen-space
- Neon color palette for night aesthetics

---

## Lighting Types

| Light Type | Description |
|------------|-------------|
| Headlights | Dynamic, raytraced (if available) |
| Streetlights | Static ambient |
| City glow | Distant illumination from buildings |
| Emergency strobes | Red/blue or red/white flashing |
| Tunnel lights | Interior focused |
| Window lights | Procedural flickering in buildings |
| Neon signs | Decorative city illumination |

---

## Raytracing System

### When Available
- Dynamic headlight reflections on wet roads
- Emergency vehicle light bouncing
- Tunnel light bounce and reflections
- Real-time reflection updates

### Configuration
```
RaytracingState { Enabled: bool, QualityLevel: byte, MaxBounces: int }
RTLightSource { Color: float3, Intensity: float, Range: float, CastShadows: bool }
RTReflectionProbe { Position: float3, Range: float, UpdateFrequency: float }
```

### SSR Fallback
- Enabled automatically on non-RT hardware
- Screen-space reflections with configurable step size
- Maximum step count for performance control

```
SSRFallback { Enabled: bool, StepSize: float, MaxSteps: int }
```

---

## Procedural City System

### Building Generation
- Maximum 256 full buildings + 512 impostors
- GPU-light geometry for performance
- Aggressive LOD system

### LOD Distances
| Level | Distance | Description |
|-------|----------|-------------|
| LOD0 | 0-50m | Full detail geometry |
| LOD1 | 50-150m | Simplified geometry |
| LOD2 | 150-400m | Impostor billboards |
| Cull | 600m+ | Not rendered |

### City Lighting
- Dynamic window lights with flicker patterns
- Neon sign intensity control
- Time/distance-based illumination

### Components
```
CityGenerationState { Seed: uint, MaxBuildings: int, MaxImpostors: int }
BuildingDefinition { Position: float3, Footprint: float2, Height: float, Style: byte }
BuildingLOD { CurrentLevel: byte, DistanceToCamera: float, FadeProgress: float }
CityLightingState { WindowLightDensity: float, NeonIntensity: float }
```

---

## Post-Processing

### Effects
| Effect | Configuration |
|--------|---------------|
| Motion Blur | Intensity proportional to speed^2 |
| Bloom | Additive glow for lights |
| Chromatic Aberration | Subtle at high speed |
| Vignette | Subtle darkening at edges |

### Crash Flash
- Flash-in: 0.05s
- Hold: 0.08s
- Fade-out: 0.4s

---

## Wireframe Rendering

### Road Mesh
- Procedural generation from spline data
- Lane markers as separate geometry
- Support for tunnels and overpasses

### Vehicle Mesh
- Procedural wireframe with soft-body deformation
- Damage-responsive vertex displacement
- Component failure visual indicators

### Hazard Mesh
- Type-specific wireframe geometry
- Severity-based visual intensity

---

## Particle Systems

| System | Description |
|--------|-------------|
| Tire Smoke | Drift-triggered smoke particles |
| Sparks | Collision-triggered spark bursts |
| Speed Lines | High-speed visual streaks |

---

## Environment Rendering

### Star Field
- Procedural star placement
- Subtle twinkle effect

### Moon
- Single moon renderer
- Phase-independent (always full)

### Ground Fog
- Distance-based density
- Color matched to city glow

### City Skyline
- Distant building silhouettes
- Light grid patterns
