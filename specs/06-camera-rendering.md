# Camera & Rendering Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## Camera System

### Base Configuration

| Property | Specification |
|----------|---------------|
| Type | Third-person chase (low angle, slight lead look-at) |
| Base FOV | 55° |
| Max FOV | 90° |
| FOV Coupling | Speed-driven |

### Dynamic Behaviors

| Behavior | Description |
|----------|-------------|
| Speed FOV | FOV scales from 55° to 90° with speed |
| Pull-back | Camera offset increases at high speed |
| Yaw Follow | Camera follows vehicle yaw with damping |
| Drift Whip | Extra rotational follow during drift |
| Motion Blur | Speed² intensity scaling |

### Impact & Feedback

| Effect | Trigger |
|--------|---------|
| Impact Recoil | Collision impulse |
| Procedural Shake | Rough road, construction |
| Damage Wobble | Accumulated damage |
| Tunnel Squeeze | Entering tunnel segments |

All motion critically damped per axis.

### Default Parameters

| Parameter | Value |
|-----------|-------|
| Base FOV | 55° |
| Max FOV | 90° |
| Damping | Critical per axis |

---

## Visual Style

- Entire world rendered in wireframe (initial MVP)
- Solid light volumes, bloom, and additive glows
- Night-time city suggested via distant light grids and silhouettes (no full geometry)
- Dynamic raytracing when available; fallback screen-space

---

## Lighting Types

| Light Type | Description |
|------------|-------------|
| Headlights | Dynamic, raytraced (if available) |
| Streetlights | Static ambient |
| City glow | Distant illumination |
| Emergency strobes | Red/blue or red/white flashing |
| Tunnel lights | Interior focused |

---

## Raytracing (If Available)

- Headlight reflections
- Emergency light reflections
- Light bounce inside tunnels

**Fallback:** Screen-space lighting approximations

---

## World Boundaries

- Only just-off-road geometry rendered
- City suggested via light grids and distant silhouettes
- No full city rendering (performance preservation)
