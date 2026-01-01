# UI & Systems Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## HUD Elements

| Element | Description |
|---------|-------------|
| Speed | Current velocity (km/h or mph) |
| Multiplier | Active score modifier with tier indicator |
| Score | Running total with smooth interpolation |
| Damage | Zone indicators (bars for front/rear/left/right) |
| Component Health | Optional detailed component status |
| Distance | Total distance traveled |
| Time | Current run time |

---

## Design Principles

- Transparent overlay (visible during autopilot)
- Minimal HUD
- No full-screen interruptions
- Information at a glance
- Score smoothing factor: 8.0
- Warning flash rate: 4 Hz

---

## Screen Flow System

### Screens
- Main Menu
- Gameplay
- Pause
- Score Summary
- Settings
- Leaderboards
- Ghost Select

### Transitions
- Smooth fade transitions
- No loading screens
- Instant state changes

---

## Pause System

- Pausing allowed
- 5-second cooldown before next pause
- Cooldown visible to player
- Menu overlay during pause

---

## Crash Flow

1. Impact detected
2. Screen shake (proportional to impact)
3. Crash flash effect:
   - Flash-in: 0.05s
   - Hold: 0.08s
   - Fade-out: 0.4s
4. Quick fade to black
5. Score summary (breakdown + save option)
6. Vehicle reset (no reload)
7. Autopilot resumes instantly

No loading screens.

---

## Autopilot System

### Activation
- After crash
- After score save
- Player chooses to disengage controls

### Behavior
- Lane-following with magnetism
- Medium speed (40 m/s target)
- Avoids hazards
- Maintains visual flow
- Menu overlay stays active

### Override
- Any player input disables autopilot
- Immediate control handoff

---

## Off-Screen Threat Signaling

### Visual Indicators

| Threat | Signal |
|--------|--------|
| Crashed vehicles ahead | Red/blue strobe leak at screen edges |
| Emergencies behind | Red/white strobe at screen edges |
| Hazards ahead | Warning indicator pulse |

### Implementation
```
Screen-space flare quads positioned by projected edge direction
Intensity proportional to urgency (inverse distance) x strobe pulse
Fade as threat enters view
```

### Light Signaling Math
```
Intensity: I = I_0 x (1 + 2u)
Strobe rate: f = f_0 + 4u
Light radius: r = r_0 x (1 + u)
```

Where `u` = urgency scalar (0-1)

---

## Replay & Ghost System

### Recording
```
Record: globalSeed + fixed-timestep input log
Input entries: Tick, Steer, Throttle, Brake, Handbrake
```

### Playback
```
Second PlayerVehicle entity driven by log (identical sim)
Deterministic via seeded PRNG and pure math
```

### Ghost Rendering
- Semi-transparent, non-colliding
- Optional trail effect
- Different color tint
- Leaderboard validation via server re-simulation

---

## Ghost Racing System

### Race Configuration
- Select difficulty: Personal Best, Easy, Medium, Hard, Expert, World Record
- Multiple ghost opponents (up to 8)
- Real-time position tracking
- Distance to nearest ghost indicator

### During Race
- Current position display
- Gap timing
- Ghost visibility toggle

---

## Spectator Mode

### Camera Modes (7 total)

| Mode | Description |
|------|-------------|
| Follow Target | Standard follow cam on target player |
| Cinematic | Smooth cinematic angles with auto-cuts |
| Overhead | Bird's eye view of track section |
| Trackside | Fixed trackside cameras |
| Free Cam | Free-flying camera with WASD controls |
| First Person | Target vehicle cockpit view |
| Chase | Chase cam behind target |

### Controls
- Next/Previous target
- Camera mode cycling
- Auto-switch toggle (follows action)
- Free cam speed adjustment

### Auto-Switch
- Configurable delay (0 = manual only)
- Follows interesting events (close calls, crashes, overtakes)

---

## Leaderboard System

### Categories

| Type | Description |
|------|-------------|
| High Score | Overall high scores |
| Best Time | Fastest runs to distance |
| Longest Run | Maximum distance achieved |
| Max Speed | Highest speed recorded |
| Total Distance | Cumulative lifetime distance |
| Weekly | Weekly challenge rankings |
| Friends | Friends-only leaderboard |

### Time Filters
- All Time
- Today
- This Week
- This Month
- This Season

### Display
- Paginated view (50 entries per page)
- Local player highlight
- Friend indicators
- Region codes

### Submission
- Automatic on crash/save
- Server validation via replay verification
- Anti-cheat via deterministic simulation

---

## Daily Challenges

### Challenge Types
- Distance target
- Score target
- Time survival
- Speed challenge
- No-damage run

### Features
- Daily seed generation
- Unique leaderboard per challenge
- Streak tracking
- Reward multipliers

---

## Performance Stats Display

### Metrics
- FPS (current and average)
- Frame time
- Entity count
- Memory usage

### Toggle
- Debug mode only
- Developer console access

---

## Warning Indicator System

### Indicators
- Damage warnings (zone-specific)
- Component failure alerts
- Emergency vehicle proximity
- Hazard ahead markers

### Presentation
- Edge-of-screen placement
- Urgency-based pulse rate
- Color-coded by type

---

## UI Systems Summary

| System | Function |
|--------|----------|
| ScreenFlowSystem | Screen transitions and state |
| HUDUpdateSystem | Score, speed, damage display |
| MenuNavigationSystem | Menu input handling |
| PerformanceStatsSystem | Debug overlay |
| WarningIndicatorSystem | Threat/damage warnings |
