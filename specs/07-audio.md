# Audio Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## Overview

Audio in Nightflow is managed by 6 dedicated ECS systems plus a centralized `AudioSystem` that calculates parameters for the MonoBehaviour audio bridge. All audio parameters are driven by gameplay state (speed, damage, environment) to create a responsive soundscape.

### Audio Systems

| System | File | Purpose |
|--------|------|---------|
| EngineAudioSystem | `src/Systems/Audio/EngineAudioSystem.cs` | RPM-based layered engine sounds |
| CollisionAudioSystem | `src/Systems/Audio/CollisionAudioSystem.cs` | Impact and scrape sounds |
| SirenAudioSystem | `src/Systems/Audio/SirenAudioSystem.cs` | Emergency vehicle sirens with doppler |
| AmbientAudioSystem | `src/Systems/Audio/AmbientAudioSystem.cs` | Environment sounds and reverb zones |
| MusicSystem | `src/Systems/Audio/MusicSystem.cs` | Dynamic intensity-based music layers |
| UIAudioSystem | `src/Systems/Audio/UIAudioSystem.cs` | Menu and HUD sound effects |

---

## Core Audio Layers

| Layer | Description | Speed Coupling |
|-------|-------------|----------------|
| Engine | Pitch/load blend based on RPM | RPM derived from forward speed |
| Tires | Slip/skid layer during drift | Active during handbrake/drift |
| Wind | Rush intensity proportional to speed squared | `Volume = speed² × 0.0002` |
| Environment | Reverb zones (tunnel, overpass, open road) | Zone-based transitions |

---

## Engine Audio

The engine audio system uses 4 crossfading layers based on RPM:

| Layer | RPM Range | Description |
|-------|-----------|-------------|
| Idle | 600–1,500 | Low rumble at minimum speed |
| Low | 1,000–3,500 | Cruising tone |
| Mid | 3,000–5,500 | Mid-range acceleration |
| High | 5,000–8,000 | High-speed whine |

### Parameters

| Parameter | Value | Source |
|-----------|-------|--------|
| Min Pitch | 0.8 | `EngineAudioSystem.cs` |
| Max Pitch | 2.0 | `EngineAudioSystem.cs` |
| RPM Smooth Speed | 5.0 | `EngineAudioSystem.cs` |
| Throttle Volume Boost | 0.3 | `AudioSystem.cs` |
| Damage Detune Max | 0.15 | `AudioSystem.cs` |

Engine audio detuning is applied progressively as the engine component degrades, providing audio feedback for damage.

---

## Wind Audio

Wind rush scales quadratically with speed:

```
Volume = min(speed² × WindVolumeScale, WindMaxVolume)
Pitch  = WindPitchBase + speed × WindPitchScale
```

| Parameter | Value |
|-----------|-------|
| Volume Scale | 0.0002 |
| Pitch Base | 0.6 |
| Pitch Scale | 0.01 |
| Max Volume | 0.8 |

---

## Collision Audio

| Parameter | Value |
|-----------|-------|
| Impact Volume Scale | 0.5 |
| Min Impact Volume | 0.1 |
| Max Impact Volume | 1.0 |

Impact volume scales with collision impulse magnitude. Scrape sounds are triggered for glancing collisions.

---

## Siren Audio

Emergency vehicle sirens use doppler effect with pattern-specific frequencies:

### Siren Patterns

| Vehicle Type | Waveform | Frequency |
|-------------|----------|-----------|
| Police | Slow wail | 1.5 Hz |
| Ambulance | Fast yelp | 4.0 Hz |
| Fire | Air horn blasts | 0.8 Hz |

### Pitch Ranges

| Type | Low | High |
|------|-----|------|
| Police | 0.8 | 1.2 |
| Ambulance | 0.9 | 1.4 |

### Distance Attenuation

| Parameter | Value |
|-----------|-------|
| Min Audible Distance | 5 m |
| Max Audible Distance | 300 m |
| Fade Distance | 50 m |
| Max Siren Volume | 1.0 |
| Volume Fade Speed | 2.0 |

Doppler pitch shift is exaggerated for gameplay effect, calculated from relative velocity between siren source and listener.

---

## Spatial Audio

| Feature | Implementation |
|---------|----------------|
| Doppler | Exaggerated for sirens and passing traffic |
| Directional | Emergency sirens positioned in 3D space |
| Distance Attenuation | Realistic falloff for all sources |
| Listener Position | Tracks player vehicle camera position |

---

## Dynamic Music System

Music responds to gameplay intensity through layered crossfading at 120 BPM (configurable) with measure-aligned transitions.

### Music Layers

| Layer | Intensity Threshold | Description |
|-------|---------------------|-------------|
| Base | Always active | Foundational synthwave beat |
| Low Intensity | < 0.3 | Ambient pads and subtle melodies (cruising) |
| High Intensity | > 0.7 | Driving synths and arpeggios (boosted speed) |
| Stingers | Event-triggered | One-shot accents for near misses, crashes |

### Intensity Drivers

Music intensity is derived from:
- Player speed (primary driver)
- Damage level (increases tension)
- Score multiplier (rewards aggressive play with more intense music)

### Parameters

| Parameter | Value |
|-----------|-------|
| Default BPM | 120 |
| Beats Per Measure | 4 |
| Layer Fade Speed | 1.5 |
| Intensity Smooth Speed | 2.0 |
| Intensity Decay Rate | 0.1/s |
| Low Intensity Threshold | 0.3 |
| High Intensity Threshold | 0.7 |

---

## Event Audio

| Event | Effect |
|-------|--------|
| Emergency sirens | Directional, doppler, off-screen intensification |
| Crashes | Impact thuds, scrape sounds |
| Damage | Engine detune, handling audio feedback |
| Construction | Zone-based ambient |
| Tunnels | Reverb increase |
| Near miss | Stinger accent |
| Component failure | Warning tone |

---

## Environment Reverb Zones

Reverb zones transition smoothly as the player moves between environments. The `AmbientAudioSystem` calculates zone blends based on player position relative to zone boundaries.

| Zone | Reverb Character | Ambient Volume |
|------|------------------|----------------|
| Open Road | Dry, minimal reverb | 0.3 |
| Tunnel | Boomy, long decay | 0.4 (tunnel drone) |
| Overpass | Ringing, metallic | — |
| Construction | Industrial, cluttered | — |

### Transition Parameters

| Parameter | Value |
|-----------|-------|
| Ambient Fade Speed | 1.5 |
| Reverb Blend Speed | 2.0 |
| Distant Traffic Volume | 0.2 |

---

## Audio Architecture

Audio parameter calculation runs in Burst-compiled ECS systems. Actual audio playback is handled by a MonoBehaviour bridge (`AudioManager`) that reads ECS component data and applies it to Unity AudioSources.

```
ECS Systems (Burst) → AudioState components → AudioManager (MonoBehaviour) → Unity AudioSources
```

This separation allows audio logic to run on the job system while playback uses Unity's managed audio API.
