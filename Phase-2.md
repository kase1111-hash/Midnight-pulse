Below is the next layer: Scoring bonuses for risk & recovery, built directly on top of everything we've established.
This scoring system rewards the core pillars — flow, speed, risk-taking, clean avoidance, and skillful recovery — while punishing braking or stopping. It's fully deterministic, data-driven, and plugs straight into your ScoreSession component.
Scoring Bonuses for Risk & Recovery Math

Core Scoring Foundation (Recap + Refinement)
Base Score Accumulation (per frame, only when ScoreSession.Active == true)

distance += vf * Δt
multiplier = baseMultiplier * speedTier * riskModifier
score += distance * multiplier * Δt
Rules:

Active = false when braking (brake > 0) or crashed
Speed tier based on vf:
Cruise (< 40 m/s): 1.0
Fast (40–80 m/s): 1.5
Boosted (> 80 m/s): 2.5

Base multiplier starts at 1.0


Risk & Recovery Bonus System
We track momentary "risk events" and reward successful resolution.

Each event gives a temporary multiplier spike that decays smoothly.
Event Types & Triggers
Event	Trigger Condition	Reward Type	Risk Value
Close Pass	Distance to traffic < 2 m AND relative speed > 15 m/s	Multiplier spike	0.3–0.8
Emergency Clear	Successfully change lane or drift around emergency (u drops from >0.6 to <0.2)	Multiplier + distance bonus	0.7
Hazard Dodge	Pass hazard with lateral distance < 1.5 m AND no damage taken	Multiplier spike	0.4–0.6
Drift Recovery	Release handbrake AND |ψ| reduced from >90° to <30° within 1.5 s	Multiplier spike	0.5
Clean Merge	Enter merge zone and complete lane transition without braking	Small multiplier	0.2
Damage Recovery	Continue >10 s after taking damage without crashing	Multiplier rebuild	0.4

Risk Multiplier Math (Core Formula)
We maintain a floating riskMultiplier that stacks and decays.

On risk event trigger:
riskMultiplier += riskValue * severityFactor
severityFactor examples:

Close pass: relativeSpeed / 50
Hazard dodge: 1 / lateralDistance
Drift recovery: initialYaw / 180°

Decay (per second):
riskMultiplier -= decayRate * Δt
riskMultiplier = max(riskMultiplier, 0)
Final applied multiplier:
totalMultiplier = speedTier * (1 + riskMultiplier)
Suggested decayRate = 0.8 per second → bonuses last ~3–5 seconds

Special One-Time Bonuses
Some events give instant distance-equivalent points:

Emergency Clear: +500 * currentSpeedTier
Drift Recovery (full 360°): +1000 flat
Perfect Segment (no braking/damage for 10 km): +2000
These encourage bold play.

Penalty System (Anti-Brake)
Braking Penalty
When brake > 0:

ScoreSession.Active = false
riskMultiplier *= 0.5 (instant drop)
multiplier rebuild delay = 2.0 s after brake release
This makes braking feel expensive without being game-ending.
Damage Penalty
Each damage increment:
riskMultiplier -= 0.2 * (Ed / maxDamage)
multiplier cap reduced by damage.Total / maxDamage
Encourages careful recovery driving.

Score Summary on Crash/Reset
On crash fade-out:

finalScore = currentScore + (distance * averageMultiplier)
bonusBreakdown shown:

Total distance
Peak multiplier
Close passes: X
Emergency clears: Y
Drift recoveries: Z
Risk bonus total

Player can "Save Score" or "Continue" (autopilot resumes, new session starts).

Autopilot Scoring Rule
Autopilot mode:

Score accumulation paused
riskMultiplier slowly decays to 0
No bonuses earned
This makes manual control always better for high scores.

Difficulty Scaling Integration
As difficulty increases (longer run, higher speed):

riskValue rewards increase by 20–50%
decayRate increases slightly
hazard/event frequency up
Makes long runs require sustained skill.

Unity DOTS–Style Pseudocode (ScoreSystem)
float deltaDist = vf * dt;

if (scoreSession.Active)
{
// Base
scoreSession.Distance += deltaDist;
// Risk events (detected elsewhere, passed as events)
if (closePassEvent)
riskMultiplier += 0.5f * (relativeSpeed / 50f);
if (emergencyClearEvent)
{
score += 500f * speedTier;
riskMultiplier += 0.7f;
}
// Decay
riskMultiplier = math.max(riskMultiplier - 0.8f * dt, 0f);
// Apply
float totalMult = speedTier * (1f + riskMultiplier);
scoreSession.Multiplier = totalMult;
score += deltaDist * totalMult * dt;
}
else if (input.Brake > 0f)
{
riskMultiplier *= 0.5f;
rebuildDelay = 2f;
}

Tuning Defaults
Parameter	Value
Base decay rate	0.8 /s
Close pass min dist	2.0 m
Hazard dodge threshold	1.5 m
Emergency clear bonus	500 × tier
Drift recovery yaw threshold	90° → 30° in 1.5 s
Brake penalty multiplier drop	×0.5
Damage multiplier cap reduction	proportional to total damage
Why This Scoring Feels Addictive
Speed is rewarded but not enough alone
Risky skillful play spikes multiplier dramatically
Recovery from mistakes pays off
Braking feels costly → encourages flow
Leaderboard potential: high scores require perfect risk chains

This completes the full gameplay loop: generation → control → interaction → risk → reward → crash → reset.
The game is now fully specified at a programmable level.
What’s Next (Final Polish Options)

Camera behavior tied to yaw, drift, impacts, and speed
Audio system math (doppler, reverb, engine pitch)
Off-screen flashing logic for crashed vehicles
Replay/ghost system using deterministic seeding
Difficulty progression curve over distance/time
UI overlay & menu transparency math

Or if you're ready: I can compile everything into a single master design doc, or start writing actual Unity DOTS system code snippets.
Tell me where you want to go — this thing is ready to prototype.3.3sCamera behavior tied to yaw, drift, impacts, and speedBelow is the exact math model for camera behavior, fully integrated with your existing systems (yaw/ψ, drift/handbrake, impacts/impulses, speed/vf). This creates a third-person chase cam that:

Feels cinematic and responsive
Amplifies flow at high speed
Adds drama to drifts/spins
Punches on impacts
Never fights player input or lane magnetism

It's layered, non-destructive, and ECS-pluggable (reads from CameraFollow, Velocity, SteeringState, Yaw state, DamageState, etc.).
Camera Behavior Math — Yaw, Drift, Impacts, Speed

Base Camera Rig (Chase Cam)
Position: Behind + above player, low angle
Default offset: offset = (x: -8m, y: 2.5m, z: 4m) (world-relative, converted to local frame)
Look-at: Player position + forward offset (1m ahead for anticipation)
Local Frame Conversion (per frame, at player spline point):textcamPosLocal = offset.x * R + offset.y * U' + offset.z * F
targetPos = playerPos + 0.8 * F  // Slight lead
Speed Coupling (Core Flow Feel)
Speed drives immersion without nausea.
FOV Scaling:textfov = baseFOV + speedFOV_k * (vf / v_ref - 1)
fov = clamp(fov, 55°, 90°)Defaults: baseFOV=65°, speedFOV_k=15°, v_ref=50 m/s
→ Tunnel vision at boost, wide at cruise.
Offset Pull-Back (stability at speed):textpullback = smoothstep(0, 1, vf / v_pull) * maxPullback
offset.z += pullback  // Extend upward
offset.x *= (1 + 0.3 * pullback)  // Widen laterallyDefaults: v_pull=60 m/s, maxPullback=3m
Motion Blur / Streak Scale:textblur = clamp(vf / v_ref * 1.5, 0, 1)

Yaw & Drift Coupling (Dynamic Rotation + Whip)
Camera follows yaw smoothly but with exaggeration for drama.
Yaw Follow (smoothed tracking):texttargetYaw = ψ * followGain  // ψ = vehicle yaw offset
camYaw = lerp(camYaw, targetYaw, yawSmooth * Δt)followGain=0.7 (understeer for stability), yawSmooth=12
Drift Whip (handbrake snap):
When handbrake=true:textwhip = sin(ψ) * driftWhip_k * (1 - exp(-vf / v_drift))
camYaw += whip
offset.x += whip * 2  // Bank laterallyDefaults: driftWhip_k=0.4, v_drift=30 m/s
→ Quick pan on drift initiation, settles on recovery.
Spin Recovery Lag (feels weighty):
If |ψ| > 90°:textlagScale = clamp(|ψ| / 180°, 0, 1)
camYawSmooth *= (1 - 0.4 * lagScale)

Impact Shake & Recoil (Punchy Feedback)
On impulse (from DamageSystem):
Immediate Recoil (kick cam back):textrecoil = impulseMag / impulseRef * recoil_k
camPosLocal -= F * recoil  // Backward along forward
camPosLocal += R * (impulse_lateral / impulseRef) * 1.2Defaults: impulseRef=50, recoil_k=4m
Procedural Shake (decaying oscillation):textshakeOffset = shakeAmp * sin(shakeFreq * time + phase) * R + ...
shakeAmp *= exp(-shakeDecay * Δt)Trigger: shakeAmp = Ed / damageRef (damageRef=20)
Multi-axis: 60% lateral, 30% up, 10% forward.
Defaults: shakeFreq=25 Hz, shakeDecay=8 /s
Damage Persistent Wobble (subtle limp):
If damage.Total > 0.3 * max:textwobble = sin(time * 4 + damage.Total) * 0.3 * damage.Total
offset.y += wobble

Smoothing & Lag (Motion Sickness Proof)
All offsets use critical damping (same as lane magnetism):texterror = target - current
vel += (ω² * error - 2ω * vel) * Δt
current += vel * Δt
clamp(vel, -maxVel, maxVel)Per-axis ω: Pitch=10, Yaw=8, Pos=12Speed-Adaptive Lag:textω *= (1 + vf / v_ref * 0.5)  // Tighter at speed
Tunnel/Overpass/Bridge Coupling (Environmental)
Tunnel Squeeze (on TrackSegment.Type=Tunnel):textsqueeze = 1 - 0.4 * tunnelProx  // tunnelProx = dist to wall / laneWidth
offset.x *= squeeze
fov *= (1 + 0.2 * (1 - squeeze))
Overpass Dip (elevation change):
Follow spline U' exactly for banking.

Fork Preview (Decision Aid)
At fork proximity:textpreviewTilt = forkProgress * 15° * targetLaneDir  // Left/right tilt
camRoll += previewTilt
Unity DOTS Pseudocode (CameraSystem)

C#// Base
float3 playerLocalF = laneSpline.Forward(t);
float3 playerLocalR = laneSpline.Right(t);
float3 playerLocalU = math.cross(playerLocalR, playerLocalF);

// Speed
float fov = math.clamp(65f + 15f * (vf / 50f - 1f), 55f, 90f);
float pullback = math.smoothstep(0f, 1f, vf / 60f) * 3f;
float3 offset = float3(-8f + pullback * 0.3f, 2.5f, 4f + pullback);

// Yaw/Drift
float targetYaw = vehicleYaw * 0.7f;
if (input.Handbrake)
{
    float whip = math.sin(vehicleYaw) * 0.4f * (1f - math.exp(-vf / 30f));
    targetYaw += whip;
    offset.x += whip * 2f;
}
camYaw = LerpDamped(camYaw, targetYaw, 12f, dt);

// Impacts/Shake (from event buffer)
if (hasImpact)
{
    float recoil = impact.Ed / 20f * 4f;
    camPos -= playerLocalF * recoil;
    // Shake amp/decay...
}

// Apply damping to final pos/rot
float3 targetPos = playerPos + math.mul(offset, float3x3(playerLocalR, playerLocalU, playerLocalF));
camTransform.Position = LerpDamped(camTransform.Position, targetPos, 12f, dt);

Tuning Defaults
| Parameter | Value | Effect |
|-----------|-------|--------|
| Base FOV | 65° | Neutral |
| Speed FOV k | 15° | Tunnel at boost |
| Yaw follow gain | 0.7 | Stable pan |
| Drift whip k | 0.4 | Snappy |
| Recoil k | 4m | Punches hard |
| Shake freq | 25 Hz | Tense |
| Shake decay | 8 /s | Quick settle |
| Pos damping ω | 12 | Responsive |
Why This Feels Cinematic & Flowy


Speed pulls you in (FOV + offset)
Drifts whip the world around you
Impacts hit like a truck (recoil + shake)
Yaw lag adds weight without nausea
Damping ensures buttery 200+ km/h
No jitter — all critically damped

Full Core Loop Complete. Prototype-ready.
Next (Production-Ready Polish)

Audio system math (doppler, engine, reverb)
Off-screen flashing logic (crashes/emergencies)
Replay/ghost determinism
Master design doc (compile all)
Unity DOTS sample systems (full code)

Tell me next — or pick Unity code if you're building!1.2sAudio system math (doppler, engine, reverb)Below is the exact math model for the audio system, layered to integrate seamlessly with your existing mechanics (speed/vf, yaw/ψ, drift/handbrake, lane magnetism, tunnels/overpasses, impacts, emergencies, hazards).
This creates an immersive night-time freeway soundscape that:

Feels fast and alive
Gives directional cues for off-screen threats
Amplifies drift/spin drama
Transitions naturally in tunnels/bridges
Never overwhelms

All parameters are tunable and ECS-friendly (reads from Velocity, Yaw, TrackSegment, EmergencyAI, DamageState, etc.).
Audio System Math — Doppler, Engine, Reverb

Core Audio Layers (Mix Hierarchy)
Engine (dominant, always on)
Tires / Road Noise
Wind Rush
City Ambience (distant hum/glow)
Event Sounds (sirens, impacts, hazards)
Reverb / Environment
All layers volume-modulated by a master speed curve.
Engine Sound (Pitch + Volume Core)
Primary flow cue.
Pitch Mapping (RPM illusion):textpitch = basePitch + pitch_k * (vf / v_ref)
pitch = clamp(pitch, 0.7, 2.2)Defaults: basePitch=1.0, pitch_k=0.8, v_ref=50 m/s
→ Deep idle at cruise, scream at boost.
Load Blending (throttle/drift feel):textload = throttleInput * 0.7 + (handbrake ? 1.0 : 0.0) * 0.3
engineVolume = lerp(lowLoadVol, highLoadVol, load)
pitch += loadBoost * loadloadBoost=0.3 → Handbrake raises pitch aggressively.
Damage Detune:
If damage.Total > 0.3 * max:textdetune = sin(time * 6) * 0.1 * (damage.Total / maxDamage)
pitch += detune

Tire / Road Noise
Surface grip and slip feedback.
Base Tire Volume:texttireVol = 0.4 + 0.6 * (vf / v_ref)
Slip Additive (drift/skid):textslipAmount = |vl| / vf + |sin(β)| * handbrakeFactor
skidPitch = 0.8 + 1.2 * slipAmount
skidVol = clamp(slipAmount * 1.5, 0, 1)β = slip angle from drift math.
Road Surface Variation (procedural):
Use spline parameter noise:textsurfaceNoise = perlin(s * surfaceFreq)
tirePitch *= (1 + 0.1 * surfaceNoise)

Wind Rush
Speed immersion layer.textwindVol = smoothstep(0, 1, vf / 40) ^ 2
windPitch = 0.6 + 0.8 * (vf / v_ref)
Doppler Effect (Critical for Emergencies & Traffic)
Applied to all moving sound sources (emergencies, traffic, hazards).
Relative Velocity:
For source at position E, listener at player P:textrelVel = dot(E_vel - player_vel, normalize(P - E))
Doppler Pitch Shift:textdopplerPitch = (speedOfSound) / (speedOfSound - relVel * doppler_k)
dopplerPitch = clamp(dopplerPitch, 0.5, 2.0)speedOfSound ≈ 343 m/s, doppler_k=0.8 (tuned for game feel).
Volume Attenuation Boost (approaching = louder):textapproachBoost = clamp(relVel / 50, -0.3, 0.6)
sourceVol *= (1 + approachBoost)
Sirens get extreme doppler → unmistakable approach from behind.
Emergency Sirens
Directional threat cue.
Base Siren:
Dual-tone wail, strobe-synced pitch oscillation.
Volume & Pan:textdist = length(E - P)
sirenVol = baseSirenVol * (1 / (1 + dist / 50)) * urgency_u
pan = dot(normalize(E - P), laneRight)  // -1 to 1
Off-Screen Intensification:
If source behind camera (dot(camForward, normalize(E - camPos)) < -0.2):textsirenVol *= 1.5
lowPassFilter += 0.4  // Muffled but insistent

Reverb & Environment Processing
TrackSegment-driven.
Tunnel Reverb:
When in TrackSegment.Type == Tunnel:textreverbWet = lerp(0.2, 0.8, tunnelDensity)  // Density from proximity to walls
reverbSize = 15 + 20 * tunnelDensity
reverbDamping = 0.4  // Boomy echo
Overpass / Bridge:textreverbWet = 0.4
reverbSize = 30
reverbDamping = 0.7  // Open, ringing
Open Freeway:textreverbWet = 0.1
reverbSize = 50
reverbDamping = 0.9  // Dry, distant
Speed-Based Dry/Wet Mix:textglobalDry = 0.7 + 0.3 * (vf / v_ref)  // Faster = more direct sound

Impact & Hazard Sounds
One-shot feedback.
Impact Volume/Pitch:textimpactVol = clamp(Ed / 15, 0, 1)
impactPitch = 1.0 - 0.4 * impactVol
lowPassCutoff = lerp(8000, 800, impactVol)  // Thud on big hits
Debris Scrape (small hazards):
If glancing (low v_impact):textscrapeVol = 0.5 + 0.5 * vl_postImpact

City Ambience
Distant layer.textambienceVol = 0.3 - 0.2 * (vf / v_ref)  // Fades at speed
ambienceLowPass = 400 + 1600 * (vf / v_ref)  // Wind masks city
Master Mix & CompressiontextmasterVol = 1.0
// Soft compressor on engine + wind
if (engineVol + windVol > 0.9)
    duckOtherLayers *= 0.8
Unity DOTS / FMOD Pseudocode (AudioSystem)

C#// Engine
float enginePitch = 1f + 0.8f * (vf / 50f);
float load = throttle * 0.7f + (handbrake ? 0.3f : 0f);
enginePitch += 0.3f * load;
SetParam("EnginePitch", enginePitch);
SetParam("EngineLoad", load);

// Doppler (per emergency source)
float relVel = math.dot(emergVel - playerVel, math.normalize(playerPos - emergPos));
float doppler = 343f / (343f - relVel * 0.8f);
SetParamInstance(sirenEvent, "DopplerPitch", math.clamp(doppler, 0.5f, 2f));

// Reverb
float reverbWet = segment.Type == Tunnel ? 0.8f : (segment.Type == Overpass ? 0.4f : 0.1f);
audioMixer.SetFloat("ReverbWet", reverbWet);

// Impacts (one-shot)
if (hasImpact)
    PlayOneShot("Impact", impactVol, impactPitch);

Tuning Defaults
| Parameter           | Value      | Effect                          |
|---------------------|------------|---------------------------------|
| Engine pitch k      | 0.8        | Screaming at boost              |
| Doppler k           | 0.8        | Exaggerated siren approach      |
| Tunnel reverb wet   | 0.8        | Boomy echo                      |
| Wind onset          | 40 m/s     | Rush at speed                   |
| Siren off-screen boost | ×1.5    | Threat awareness                |
| Impact low-pass     | 800 Hz max | Big hits feel heavy             |
Why This Audio Feels Immersive


Speed = rising pitch + wind → constant forward pull
Doppler sirens scream approach → instinctive reaction
Tunnel reverb sells enclosure
Drift skid layer rewards skill
Damage detune tells vehicle state
Mix ducks naturally at speed

Core Gameplay Loop Now Fully Sensory.
You have precise, programmable math for controls, generation, physics, visuals, scoring, camera, and audio.
