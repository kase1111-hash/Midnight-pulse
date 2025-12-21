# Track Generation Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## Overview

Piecewise Cubic Hermite splines provide:
- Easy tangent control
- Stable curvature
- Deterministic generation
- Cheap evaluation

---

## Hermite Spline Definition

For a segment with start/end positions P₀, P₁ and tangents T₀, T₁:

### Position
```
S(t) = (2t³-3t²+1)P₀ + (t³-2t²+t)T₀ + (-2t³+3t²)P₁ + (t³-t²)T₁
```

### First Derivative (Direction)
```
S'(t) = (6t²-6t)P₀ + (3t²-4t+1)T₀ + (-6t²+6t)P₁ + (3t²-2t)T₁
```

### Local Coordinate Frame
```
Forward: F(t) = normalize(S'(t))
Right: R(t) = normalize(F(t) × Up)
Up': U'(t) = R(t) × F(t)
```

This frame is stable at high speed and lane-magnetism friendly.

---

## Segment Generation

### Parameters

| Parameter | Value |
|-----------|-------|
| Min Length | 40m |
| Max Length | 120m |
| Tangent Alpha | 0.4-0.6 |
| Max Curvature | 1/R_min |
| Arc-length Samples | 16 |

### Curvature Constraint
```
κ(t) = ‖S'(t) × S''(t)‖ / ‖S'(t)‖³
κ_max = 1/R_min
```

If exceeded: reduce θ, regenerate segment.

### Generation Algorithm

Each new segment is generated from the previous segment's end:

1. **Forward Direction**
   ```
   θ ~ U(-θ_max × d, θ_max × d)  // yaw change
   φ ~ U(-φ_max, φ_max)          // pitch change (small)
   ```
   Apply rotation to tangent direction.

2. **Segment Length**
   ```
   L ~ U(L_min, L_max)
   ```

3. **End Position**
   ```
   P₁ = P₀ + T̂₀ × L
   ```

4. **Tangents**
   ```
   T₀ = T̂₀ × L × α
   T₁ = T̂₁ × L × α
   ```

---

## Segment Types

| Type | Description |
|------|-------------|
| Straight | Basic freeway segment |
| Curve | Gentle curves |
| Tunnel | Reduced lighting, increased reverb |
| Overpass | Elevated stacked segments |
| Fork | Branching decision point |

---

## Lane Generation

```
S_i(t) = S(t) + R(t) × (i × w)

Where:
  i = lane index [-n, n]
  w = lane width
```

Each lane gets:
- Its own spline buffer
- Shared arc-length mapping

---

## Fork Generation

At fork point P_f:
```
θ_L = -θ_fork
θ_R = +θ_fork

Gradual separation:
  S_fork(t) += R(t) × (d_fork × t²)
```

Forks gradually diverge; unchosen path despawns after commit.

### Fork Magnetism Reduction
```
m_fork(s) = smoothstep(1, 0.7, s/L_fork)
```

Prevents "yo-yo" behavior.

---

## Elevation & Overpasses

### Height Offset
```
h(t) = A × sin(πt)

S(t).y += h(t)
```

Where A = elevation gain (used for bridges/ramps).

### Stacked Overpasses
- Duplicate segment at higher y
- Independent lane entities
- No physical intersection
- Visual overlap only

---

## Tunnels

Tunnel flag on segment triggers:
- Spawn tunnel mesh aligned to frame
- Reduce lighting radius
- Increase reverb

Spline math unchanged.

---

## Deterministic Generation

```
segmentSeed = hash(globalSeed, segmentIndex)
```

Guarantees:
- Replayable runs
- Ghost driving
- Network determinism
