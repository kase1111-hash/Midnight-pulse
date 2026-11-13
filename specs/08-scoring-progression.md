# Scoring & Progression Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## Scoring System

### Base Formula
```
Score = Distance × Speed_Tier_Multiplier × (1 + RiskMultiplier)
```

### Speed Tier Multipliers

| Tier | Multiplier |
|------|------------|
| Cruise | 1.0× |
| Fast | 1.5× |
| Boosted | 2.5× |

---

## Risk Events

Risk events spike temporary `riskMultiplier`:

| Event | Effect |
|-------|--------|
| Close passes | Spike risk multiplier |
| Hazard dodges | Spike risk multiplier |
| Emergency clears | Spike risk multiplier |
| Drift recoveries | Spike risk multiplier |
| Slide saves (tyres past the limit for 0.25 s+, caught without crashing) | Spike risk multiplier, more the further past the limit |
| Perfect segments | One-time bonus |
| Full spins | One-time bonus |

---

## Risk Multiplier Dynamics

```
Decay: ~0.8/s
Braking: Instantly halves riskMultiplier + 2s rebuild delay
Damage: Reduces cap and rebuild rate
```

### Default Parameters

| Parameter | Value |
|-----------|-------|
| Risk decay | 0.8/s |
| Brake penalty | 50% + 2s delay |

---

## Scoring Rules

- Score accrues per metre, so it grows with the momentum; the anti-exploit cap is a rate (5000/s), not a per-frame amount, so no speed or frame rate loses score
- Braking stops score accumulation
- Crashing ends score run
- Score saved on crash/save action

---

## Difficulty Progression

Natural scaling via endurance (no discrete levels):

| Factor | Progression |
|--------|-------------|
| Base Speed | Increases over time with no ceiling (the run's pace, see 02-vehicle-systems) |
| Traffic Density | Increases with distance |
| Hazard Frequency | Increases with distance |
| Fork Complexity | More complex choices appear |
| Risk Reward | Higher multipliers available |

The game gets harder the longer you survive, but rewards scale accordingly. There is no top speed: the pace keeps climbing (~195 km/h at 2 minutes, ~340 km/h at 4) until the momentum outruns the tyres and the driver loses control; that, not a difficulty cap, is how runs end.
The ramp is one continuous curve (see `DifficultyCurve` and the Difficulty Scaling table in 10-parameters): a gentle first minute, full difficulty after ~3 minutes flat out (~5 minutes cruising), then a slow overdrive that keeps tightening for another 10+ minutes.
