# Audio Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## Core Audio Layers

| Layer | Description |
|-------|-------------|
| Engine | Pitch/load blend based on speed |
| Tires | Slip/skid layer |
| Wind | Rush intensity ∝ speed² |
| Environment | Reverb zones (tunnel boomy, overpass ringing, open dry) |

---

## Spatial Audio

| Feature | Implementation |
|---------|----------------|
| Doppler | Exaggerated for sirens and passing traffic |
| Directional | Emergency sirens positioned in 3D space |
| Distance Attenuation | Realistic falloff for all sources |

---

## Event Audio

| Event | Effect |
|-------|--------|
| Emergency sirens | Directional, doppler, off-screen intensification |
| Crashes | Impact thuds, scrape sounds |
| Damage | Engine detune, handling audio feedback |
| Construction | Zone-based ambient |
| Tunnels | Reverb increase |

---

## Environment Reverb Zones

| Zone | Reverb Character |
|------|------------------|
| Open Road | Dry, minimal reverb |
| Tunnel | Boomy, long decay |
| Overpass | Ringing, metallic |
| Construction | Industrial, cluttered |
