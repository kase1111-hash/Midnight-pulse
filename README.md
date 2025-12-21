
A procedural, endless, night-time freeway driving game focused on flow, speed, and light.

1. Core Game Pillars

These guide all design decisions:

Flow Over Precision

Smooth steering

Magnetic lanes

Forgiving controls unless impact is severe

Speed = Score

Faster driving multiplies score

Stopping or slowing removes multiplier

Visual Rhythm

City lights

Emergency flashes

Tunnels, bridges, stacked overpasses

Minimal geometry, high lighting contrast

One Continuous Loop

No hard restarts

Crashes fade → autopilot → resume or save

2. Camera System
2.1 Default Camera

Third-person chase camera

Low angle, slightly offset downward

FOV increases with speed

Subtle camera lag for motion feel

2.2 Dynamic Effects

Speed-based motion blur

Headlight bloom

Light streaks at high velocity

Camera shake:

Minor: rough road / construction

Major: impacts / debris hits

3. Player Vehicle System
3.1 Movement Model

Always moving forward unless braking

Player controls:

Steering (analog, smoothed)

Throttle (speed up)

Brake (slow down, ends scoring)

Handbrake (drift / spin)

Steering

Lane-based steering with analog offset

No sharp snaps

Curvature is eased with cubic interpolation

Speed

Speed tiers:

Cruise

Fast

Boosted

Speed affects:

Score multiplier

Lighting intensity

Traffic density

3.2 Lane Magnetism System

Each lane has a magnetic force field:

Strength increases when:

Player stops steering input

Autopilot is active

Weakens when:

Player steers aggressively

Handbrake is engaged

Purpose:

Allow “hands-off” cruising

Reduce fatigue

Preserve flow

Implementation concept:

Lanes define invisible spline rails

Vehicle lateral position interpolates toward nearest spline center

3.3 Handbrake Mechanics

Engaging handbrake:

Reduces rear tire traction

Allows drift

Allows 180°–360° spins

Rules:

Vehicle must maintain forward velocity

If forward velocity drops below threshold → crash state

Steering + handbrake controls drift angle

4. Vehicle Damage System (Scalable)
Phase 1 (Wireframe Era)

Damage zones:

Front

Rear

Left

Right

Small hazards:

Cosmetic deformation

Handling penalty

Big hazards:

Crash state

Phase 2 (BeamNG-Level Future)

Soft-body deformation

Component damage:

Suspension

Steering alignment

Tires

Damage accumulates until failure

5. Procedural Track Generation
5.1 Track Philosophy

Endless freeway network

Generated ahead of the player

Destroyed behind

5.2 Track Segments

Each segment contains:

Road mesh (wireframe)

Lane definitions

Traffic spawn zones

Lighting anchors

Hazard spawn points

Segment types:

Straight freeway

Gentle curves

Stacked overpasses

Tunnels

Forks / splits

5.3 Fork Decision System

Forks appear gradually

Only render far enough to:

Visually suggest options

Lock player decision at commit point

Once committed:

Unchosen path despawns

Design goal:

No paralysis

Always feels intentional

6. Traffic System
6.1 Regular Traffic

AI-driven, lane-following

Predictable behavior

Slight randomness in speed

6.2 Crashed Vehicles

Spawn ahead

Pre-signaled visually:

Red & blue flashing lights just off-screen

Appears gradually as player approaches

Forces lane decision

6.3 Emergency Vehicles

Ambulances / police

Spawn behind player

Overtake aggressively

Red/white (ambulance) or red/blue (police) lights

Behavior:

Audible cue (doppler siren)

Forces player to move aside or react quickly

7. Hazard System

Hazards spawn dynamically based on speed and difficulty.

Hazard Types

Loose tires

Road debris

Construction cones

Narrowed lanes

Temporary barriers

Rules:

Small hazards:

Damage vehicle

Reduce handling

Large hazards:

Immediate crash

8. Lighting & Rendering Framework
8.1 Visual Style

Entire world rendered in wireframe

Solid light volumes

No textures (initially)

8.2 Lighting Types

Headlights (dynamic, raytraced)

Streetlights

City glow

Emergency strobes

Tunnel lights

8.3 Raytracing (If Available)

Headlight reflections

Emergency light reflections

Light bounce inside tunnels

Fallback:

Screen-space lighting approximations

9. World Boundaries

Only just-off-road geometry rendered

City suggested via:

Light grids

Distant silhouettes

No full city rendering to preserve performance

10. Autopilot System
When It Activates

After crash

After score save

Player chooses to disengage controls

Behavior

Lane-following

Medium speed

Avoids hazards

Maintains visual flow

Menu overlay stays active during autopilot.

11. Scoring System
Score Accumulation

Distance driven × speed multiplier

Speed tiers define multiplier

Modifiers

Close calls

High-speed passes

Drift usage

Emergency vehicle avoidance

Scoring Rules

Braking stops score accumulation

Crashing ends score run

12. Crash & Reset Loop
Crash Detection

Severe impact

Velocity drop below threshold

Major obstruction collision

Crash Flow

Impact

Screen shake

Quick fade to black

Score summary appears

Vehicle reset

Autopilot resumes instantly

No loading screens.

13. UI / Menu System
Design

Transparent overlay

Minimal HUD

No full-screen interruptions

HUD Elements

Speed

Multiplier

Score

Damage indicators

Pause System

Pausing allowed

5-second cooldown before next pause

Cooldown visible to player

14. Audio System
Core Audio Layers

Engine sound (pitch-based on speed)

Tire noise

Wind

City ambience

Event Audio

Emergency sirens (directional)

Crashes

Construction noise

Tunnel reverb

15. Difficulty Scaling

Difficulty increases by:

Speed

Traffic density

Hazard frequency

Fork complexity

No levels — only progression by endurance.

16. Engine Architecture Recommendation
Systems Breakdown

Vehicle Controller System

Lane Magnetism System

Procedural Track Generator

Traffic AI System

Lighting & FX System

Damage System

Score System

UI Overlay System

Data-Driven Design

JSON / ScriptableObjects for:

Track segments

Hazard definitions

Traffic behaviors

Lighting presets

17. MVP Development Order

Vehicle movement + lane magnetism

Procedural freeway generation

Traffic AI

Basic scoring loop

Crash → fade → autopilot loop

Lighting + wireframe rendering

Hazards & emergency vehicles

Polish & performance
