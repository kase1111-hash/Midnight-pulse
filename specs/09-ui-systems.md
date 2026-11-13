# UI & Systems Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## HUD Elements

| Element | Description |
|---------|-------------|
| Speed | Current velocity (km/h or mph); speed dots light at 100/160/220/280/340 km/h as the momentum builds |
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
- The simulation never stops: while the pause menu (or any other menu) is open the autopilot drives; the first control input after resuming hands the wheel back
- A paused run keeps its speed: the autopilot holds the run's speed underneath the menu instead of easing down (up to ~306 km/h, scaled down on worn tyres so that even a hazard-dodging lane change in the tightest bend stays below the tyre squeal; a run paused faster is brought down to that at 8 m/s²), so resuming picks the momentum up where it was left
- Pause cannot catch a slide: asked for while the car is past its grip limit or well off line, the pause is held (the HUD shows `PAUSE QUEUED`; pressing again keeps it held) and opens once the car is back in hand; a crash drops it. A press in the half second after a held pause opens is ignored, so the second press of an impatient double-tap never resumes it straight away
- The pause menu offers RESUME, RESTART, SETTINGS, MAIN MENU and QUIT. MAIN MENU abandons the run: the car is repaired under a quick fade, and the first input after Start begins a fresh run

---

## Crash Flow

1. Impact detected (or the car slides past the survivable slip angle: `LOST CONTROL`)
2. Slow motion: the world drops to 0.3x for the impact (0.3 s of game time) and eases back to 1x during the screen shake (`TimeScaleDriver` applies `GameState.TimeScale`; never below 0.25x, never zero); the chase camera pulls out and rises to watch the wreck
3. Crash flash effect:
   - Flash-in: 0.05s
   - Hold: 0.08s
   - Fade-out: 0.4s
4. Quick fade to black (the fade covers the HUD and menus but sits under the summary panel)
5. Score summary (breakdown + save option), shown on black; a driving control still held from the crash must be released before it can dismiss the summary
6. Vehicle reset (no reload); the camera cuts to its chase position under the black so the fade-in reveals a settled view
7. Autopilot resumes instantly, cruising at recovery speed

No loading screens.

### Taking the wheel
- The first control input while the autopilot drives (no menu open) starts the run
- A key still held when a menu closes or the summary is dismissed (Space/Enter to start, S/W to navigate) must be released first, so starting a run never fires a handbrake or brake the player didn't mean
- The run's pace starts at 90 km/h, just above the autopilot's 72 km/h recovery cruise, and momentum pulls the car up to it at no more than 3 m/s², so the takeover itself never jolts the car

---

## Autopilot System

### Activation
- At boot (attract mode under the main menu)
- While any menu is open
- After crash (following the in-place vehicle reset)
- After 15 s without any control input (run suspended, score frozen)

### Behavior
- Lane-following with magnetism
- Cruises at 20-35 m/s in attract mode; holds a suspended run's own speed (up to 85 m/s, times the square root of the car's grip share) under the pause menu or after an idle handoff
- Avoids hazards: changes lanes for a serious threat anywhere in range, and for anything at all (loose tyres and cones too) it would reach within 1.6 s, toward any neighbour with under half the threat; a lane the steering system finds blocked (traffic alongside) is asked for again every frame, trying the other side in turn, while it leans toward the gap; at most one change every 1.6 s; slows for hazards close ahead
- Calm lane changes: as quick as the player's well below its hold speed (attract mode), the full second near it, so even a worn car's dodges stay below the tyre squeal
- Maintains visual flow
- Menu overlay stays active

### Override
- Any player input disables autopilot (ignored while a menu is open)
- Immediate control handoff
- If no run is active (boot, post-crash) the first input starts a fresh scoring run; if a run was suspended by idling it resumes with its score intact
- The autopilot never scores, never takes structural damage or component wear and never crashes, so the self-playing loop runs unattended and a paused or idle run is never handed back damaged

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
- Traction: `TRACTION LIMIT` from tyre usage 0.85, `LOSING GRIP` past 1 (clears below 0.75); with tyre squeal, camera shake and a chromatic fringe, so the loss of control the momentum builds toward is readable before it happens

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
