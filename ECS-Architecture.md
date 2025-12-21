ECS ARCHITECTURE — Nightflow
0. ECS Philosophy (Important)

Entities = IDs only

Components = pure data (no logic)

Systems = logic operating over component queries

No inheritance

Everything is runtime-swappable

This is crucial for:

Procedural generation

Autopilot takeover

Damage scaling

Visual-only wireframe world

1. ENTITY CATEGORIES
Core Entities

PlayerVehicle

TrafficVehicle

EmergencyVehicle

Hazard

TrackSegment

Lane

LightSource

CameraRig

UIOverlay

ScoreSession

2. COMPONENT DEFINITIONS (DATA ONLY)
2.1 Transform & Motion
Transform
position : vec3
rotation : quat
scale    : vec3

Velocity
forward_speed : float
lateral_speed : float
angular_speed : float

2.2 Vehicle Control Components
PlayerInput
steer      : float   // -1 to 1
throttle   : float   // 0 to 1
brake      : float   // 0 to 1
handbrake  : bool

Autopilot
enabled        : bool
target_speed   : float
lane_preference: int

SteeringState
current_angle  : float
target_angle   : float
smoothing      : float

2.3 Lane & Road Components
LaneFollower
lane_id           : EntityID
magnet_strength   : float
lateral_offset    : float

LaneSpline
control_points : vec3[]
width          : float

TrackSegment
segment_type : enum { Straight, Curve, Tunnel, Overpass, Fork }
length       : float
difficulty   : float

2.4 Damage & Collision
CollisionShape
shape_type : enum { Box, Sphere, Capsule }
size       : vec3

DamageState
front  : float
rear   : float
left   : float
right  : float
total  : float

Crashable
crash_threshold : float

2.5 Traffic & AI
TrafficAI
preferred_speed : float
aggression      : float
reaction_time   : float

EmergencyAI
siren_active    : bool
overtake_bias   : float

2.6 Hazards
Hazard
hazard_type : enum { Tire, Debris, Barrier, Construction }
severity    : float

2.7 Lighting & Visuals
LightEmitter
color      : vec3
intensity  : float
radius     : float
strobe     : bool
strobe_rate: float

WireframeRender
line_thickness : float
lod_level      : int

2.8 Camera & UI
CameraFollow
offset      : vec3
fov_base    : float
fov_speed_k : float

UIState
paused            : bool
pause_cooldown    : float
menu_visible      : bool

2.9 Scoring
ScoreSession
distance        : float
multiplier      : float
active          : bool

3. SYSTEM DEFINITIONS (LOGIC)
3.1 Input Systems
PlayerInputSystem

Reads hardware input

Writes to PlayerInput

Disabled when Autopilot.enabled = true

3.2 Vehicle Motion
VehicleMovementSystem

Reads:

PlayerInput / Autopilot

SteeringState

Velocity

Writes:

Transform

Velocity

SteeringState

Responsibilities:

Apply throttle/brake

Enforce always-forward movement

Clamp steering angle

Enforce handbrake rules

3.3 Lane Magnetism
LaneMagnetismSystem

Reads:

LaneFollower

LaneSpline

PlayerInput

Writes:

Transform.position

LaneFollower.lateral_offset

Behavior:

Pull vehicle toward lane center

Strength scaled by:

No steering input

Autopilot enabled

3.4 Autopilot Control
AutopilotSystem

Reads:

Autopilot

LaneFollower

Hazard proximity

Writes:

SteeringState

Velocity.forward_speed

3.5 Procedural Track
TrackGenerationSystem

Reads:

Player position

Difficulty curve

Writes:

TrackSegment entities

LaneSpline entities

Rules:

Spawn ahead

Despawn behind

Generate forks probabilistically

3.6 Fork Resolution
ForkDecisionSystem

Reads:

Player position

Fork TrackSegments

Writes:

Active lane assignment

Despawn unused fork

3.7 Traffic AI
TrafficAISystem

Reads:

TrafficAI

LaneFollower

Velocity

Writes:

SteeringState

Velocity.forward_speed

3.8 Emergency Vehicles
EmergencyVehicleSystem

Reads:

EmergencyAI

Player position

Writes:

Velocity

LightEmitter (strobe)

3.9 Hazard Spawning
HazardSpawnSystem

Reads:

Difficulty

TrackSegment

Writes:

Hazard entities

3.10 Collision & Damage
CollisionSystem

Reads:

CollisionShape

Transform

Writes:

DamageState

CrashEvent

DamageEvaluationSystem

Reads:

DamageState

Crashable

Writes:

CrashEvent

3.11 Crash Handling
CrashSystem

Reads:

CrashEvent

Writes:

ScoreSession.active = false

Autopilot.enabled = true

Trigger fade-out

3.12 Scoring
ScoreSystem

Reads:

Velocity

ScoreSession

Writes:

ScoreSession.distance

ScoreSession.multiplier

Rules:

No braking

Speed-based multiplier

Stops when inactive

3.13 Rendering
WireframeRenderSystem

Reads:

Transform

WireframeRender

LightingSystem

Reads:

LightEmitter

Transform

3.14 Camera & UI
CameraSystem

Reads:

CameraFollow

Player velocity

Writes:

Camera transform

FOV

UISystem

Reads:

UIState

ScoreSession

4. EVENT FLOW (CRITICAL LOOP)
VehicleMovement →
LaneMagnetism →
Collision →
Damage →
Crash →
Fade →
Autopilot →
Score Reset →
Resume


No scene reloads. Ever.

5. SYSTEM EXECUTION ORDER (FRAME)

Input

Autopilot

Steering

Lane Magnetism

Movement

Collision

Damage

Crash

Track Generation

Traffic AI

Scoring

Camera

Rendering

UI

6. SCALABILITY NOTES

BeamNG-level damage = swap DamageEvaluationSystem

Raytracing = swap LightingSystem

Multiplayer = replicate ECS state deltas

Replay system = record component diffs

7. WHY THIS WORKS FOR YOUR GAME

Autopilot is just a component toggle

Lane magnetism is non-invasive

Forks are entities, not logic branches

Wireframe rendering is decoupled

Performance scales with segment count
