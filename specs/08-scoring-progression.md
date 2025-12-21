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

- Braking stops score accumulation
- Crashing ends score run
- Score saved on crash/save action

---

## Difficulty Progression

Natural scaling via endurance (no discrete levels):

| Factor | Progression |
|--------|-------------|
| Base Speed | Increases over time |
| Traffic Density | Increases with distance |
| Hazard Frequency | Increases with distance |
| Fork Complexity | More complex choices appear |
| Risk Reward | Higher multipliers available |

The game gets harder the longer you survive, but rewards scale accordingly.
