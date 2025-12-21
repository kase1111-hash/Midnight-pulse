# Implementation Parameters Reference

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

Complete reference of all tuning values and defaults.

---

## Lane Magnetism

| Parameter | Value |
|-----------|-------|
| ω (omega) | 8.0 |
| v_ref | 40 m/s |
| Max lateral speed | 6 m/s |
| Edge stiffness | 20 |
| Soft zone | 85% lane width |

---

## Lane Change

| Parameter | Value |
|-----------|-------|
| Steering trigger | 0.35 |
| Abort threshold | 0.7 |
| Base duration | 0.6 s |
| Min duration | 0.45 s |
| Max duration | 1.0 s |
| Lane width | 3.6 m |
| Fork magnetism | 70% |

---

## Drift / Yaw

| Parameter | Value |
|-----------|-------|
| Min forward speed | 8 m/s |
| Steering gain (k_s) | 1.2 |
| Drift gain (k_d) | 2.5 |
| Yaw damping (c_ψ) | 0.8 |
| Max yaw rate | 6 rad/s |
| Slip gain (k_slip) | 1.1 |
| Drift magnetism | 0.3 |

---

## Camera

| Parameter | Value |
|-----------|-------|
| Base FOV | 55° |
| Max FOV | 90° |
| Damping | Critical per axis |

---

## Emergency Vehicles

| Parameter | Value |
|-----------|-------|
| Detection distance (d_max) | 120 m |
| Detection width (w_detect) | 6-8 m |
| Avoid strength (k_a) | 0.8 |
| Warning time (t_warn) | 1.5 s |
| Player min override | 0.3 |
| Light intensity boost | ×3 |
| Escalation threshold | u > 0.6 |
| Yield threshold | u > 0.7 |

---

## Hazards / Damage

| Parameter | Value |
|-----------|-------|
| Impulse scale (k_i) | 1.2 |
| Damage scale (k_d) | 0.04 |
| Virtual mass (m_virtual) | 1200 |
| Crash speed (v_crash) | 25 m/s |
| Max damage (D_max) | 100 |
| Min forward speed (v_min) | 8 m/s |

### Handling Degradation Coefficients

| Effect | Coefficient |
|--------|-------------|
| Front damage → steering | 0.4 |
| Side damage → magnetism | 0.5 |
| Rear damage → slip | 0.6 |

---

## Traffic AI

| Weight | Value |
|--------|-------|
| Speed (w_s) | 0.35 |
| Density (w_d) | 0.25 |
| Emergency (w_e) | 0.4 |
| Hazard (w_h) | 0.3 |
| Player (w_p) | 0.15 |
| Merge (w_m) | 0.3 |

| Parameter | Value |
|-----------|-------|
| Decision threshold (θ) | 0.15 |
| Lock time | 1.2 s |

---

## Scoring

| Parameter | Value |
|-----------|-------|
| Cruise multiplier | 1.0× |
| Fast multiplier | 1.5× |
| Boosted multiplier | 2.5× |
| Risk decay | 0.8/s |
| Brake penalty | 50% + 2s delay |

---

## Procedural Generation

| Parameter | Value |
|-----------|-------|
| Segment min length | 40 m |
| Segment max length | 120 m |
| Tangent alpha (α) | 0.4-0.6 |
| Arc-length samples | 16 |

---

## Light Signaling

| Parameter | Formula |
|-----------|---------|
| Intensity | I = I₀ × (1 + 2u) |
| Strobe rate | f = f₀ + 4u |
| Light radius | r = r₀ × (1 + u) |
