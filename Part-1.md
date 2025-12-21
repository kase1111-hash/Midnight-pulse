PART I — UNITY DOTS (ENTITIES 1.0+)
1. Unity DOTS Design Assumptions

IComponentData for all hot data

IBufferElementData for splines / control points

ISystem + SystemAPI.Query

Physics via Unity Physics or custom raycasts

Rendering via Entities Graphics (wireframe shader)

2. Core Component Layout (Unity)
2.1 Transform & Motion
public struct WorldTransform : IComponentData
{
    public float3 Position;
    public quaternion Rotation;
}

public struct Velocity : IComponentData
{
    public float Forward;
    public float Lateral;
    public float Angular;
}

2.2 Player & Vehicle Control
public struct PlayerInput : IComponentData
{
    public float Steer;
    public float Throttle;
    public float Brake;
    public bool Handbrake;
}

public struct Autopilot : IComponentData
{
    public bool Enabled;
    public float TargetSpeed;
    public int LanePreference;
}

public struct SteeringState : IComponentData
{
    public float CurrentAngle;
    public float TargetAngle;
    public float Smoothness;
}

2.3 Lane & Track Data
public struct LaneFollower : IComponentData
{
    public Entity LaneEntity;
    public float MagnetStrength;
    public float LateralOffset;
}

public struct TrackSegment : IComponentData
{
    public float Length;
    public float Difficulty;
    public TrackSegmentType Type;
}

public enum TrackSegmentType : byte
{
    Straight,
    Curve,
    Tunnel,
    Overpass,
    Fork
}

Lane Spline Buffer
public struct LaneSplinePoint : IBufferElementData
{
    public float3 Position;
}

2.4 Damage & Collision
public struct DamageState : IComponentData
{
    public float Front;
    public float Rear;
    public float Left;
    public float Right;
    public float Total;
}

public struct Crashable : IComponentData
{
    public float CrashThreshold;
}

public struct CollisionBounds : IComponentData
{
    public float3 Size;
}

2.5 Traffic & Emergency AI
public struct TrafficAI : IComponentData
{
    public float PreferredSpeed;
    public float Aggression;
    public float ReactionTime;
}

public struct EmergencyAI : IComponentData
{
    public bool SirenActive;
    public float OvertakeBias;
}

2.6 Hazards
public struct Hazard : IComponentData
{
    public HazardType Type;
    public float Severity;
}

public enum HazardType : byte
{
    Tire,
    Debris,
    Barrier,
    Construction
}

2.7 Lighting & Rendering
public struct LightEmitter : IComponentData
{
    public float3 Color;
    public float Intensity;
    public float Radius;
    public bool Strobe;
    public float StrobeRate;
}

public struct WireframeRender : IComponentData
{
    public float LineThickness;
    public byte LOD;
}

2.8 Camera & UI
public struct CameraFollow : IComponentData
{
    public float3 Offset;
    public float BaseFOV;
    public float SpeedFOVScale;
}

public struct UIState : IComponentData
{
    public bool Paused;
    public float PauseCooldown;
    public bool MenuVisible;
}

2.9 Scoring
public struct ScoreSession : IComponentData
{
    public float Distance;
    public float Multiplier;
    public bool Active;
}

3. Unity DOTS Archetypes
Player Vehicle Archetype
WorldTransform
Velocity
PlayerInput
SteeringState
LaneFollower
Autopilot
DamageState
Crashable
CollisionBounds
ScoreSession
WireframeRender

Traffic Vehicle Archetype
WorldTransform
Velocity
SteeringState
LaneFollower
TrafficAI
CollisionBounds
WireframeRender

Emergency Vehicle Archetype
WorldTransform
Velocity
SteeringState
LaneFollower
EmergencyAI
LightEmitter
WireframeRender

4. Unity System Groups
SimulationSystemGroup
 ├── InputSystem
 ├── AutopilotSystem
 ├── SteeringSystem
 ├── LaneMagnetismSystem
 ├── VehicleMovementSystem
 ├── CollisionSystem
 ├── DamageSystem
 ├── CrashSystem
 ├── TrackGenerationSystem
 ├── TrafficAISystem
 ├── EmergencyVehicleSystem
 ├── ScoreSystem

PresentationSystemGroup
 ├── CameraSystem
 ├── WireframeRenderSystem
 ├── LightingSystem
 ├── UISystem

PART II — UNREAL MASS (UE5)
1. Unreal Mass Design Assumptions

Fragments = components

Processors = systems

Tags for fast filtering

MassEntitySubsystem

Rendering via Actor proxies or custom renderer

2. Mass Fragment Layout
2.1 Transform & Motion
struct FWorldTransformFragment : public FMassFragment
{
    FVector Position;
    FQuat Rotation;
};

struct FVelocityFragment : public FMassFragment
{
    float Forward;
    float Lateral;
    float Angular;
};

2.2 Player & Vehicle Control
struct FPlayerInputFragment : public FMassFragment
{
    float Steer;
    float Throttle;
    float Brake;
    bool bHandbrake;
};

struct FAutopilotFragment : public FMassFragment
{
    bool bEnabled;
    float TargetSpeed;
    int32 LanePreference;
};

struct FSteeringStateFragment : public FMassFragment
{
    float CurrentAngle;
    float TargetAngle;
    float Smoothness;
};

2.3 Lane & Track
struct FLaneFollowerFragment : public FMassFragment
{
    FMassEntityHandle LaneEntity;
    float MagnetStrength;
    float LateralOffset;
};

struct FTrackSegmentFragment : public FMassFragment
{
    float Length;
    float Difficulty;
    uint8 Type;
};

2.4 Damage & Collision
struct FDamageStateFragment : public FMassFragment
{
    float Front;
    float Rear;
    float Left;
    float Right;
    float Total;
};

struct FCrashableFragment : public FMassFragment
{
    float CrashThreshold;
};

2.5 AI
struct FTrafficAIFragment : public FMassFragment
{
    float PreferredSpeed;
    float Aggression;
    float ReactionTime;
};

struct FEmergencyAIFragment : public FMassFragment
{
    bool bSirenActive;
    float OvertakeBias;
};

2.6 Hazards
struct FHazardFragment : public FMassFragment
{
    uint8 Type;
    float Severity;
};

2.7 Lighting & Visuals
struct FLightEmitterFragment : public FMassFragment
{
    FVector Color;
    float Intensity;
    float Radius;
    bool bStrobe;
    float StrobeRate;
};

struct FWireframeRenderFragment : public FMassFragment
{
    float LineThickness;
    uint8 LOD;
};

2.8 Camera & UI
struct FCameraFollowFragment : public FMassFragment
{
    FVector Offset;
    float BaseFOV;
    float SpeedFOVScale;
};

struct FUIStateFragment : public FMassFragment
{
    bool bPaused;
    float PauseCooldown;
    bool bMenuVisible;
};

2.9 Scoring
struct FScoreSessionFragment : public FMassFragment
{
    float Distance;
    float Multiplier;
    bool bActive;
};

3. Mass Tags (Important for Performance)
struct FPlayerVehicleTag : public FMassTag {};
struct FTrafficVehicleTag : public FMassTag {};
struct FEmergencyVehicleTag : public FMassTag {};
struct FAutopilotTag : public FMassTag {};
struct FCrashTag : public FMassTag {};

4. Mass Processors (Systems)
Processor	Runs In
PlayerInputProcessor	PrePhysics
AutopilotProcessor	PrePhysics
SteeringProcessor	PrePhysics
LaneMagnetismProcessor	PrePhysics
VehicleMovementProcessor	Physics
CollisionProcessor	Physics
DamageProcessor	PostPhysics
CrashProcessor	PostPhysics
TrackGenerationProcessor	PostPhysics
TrafficAIProcessor	PrePhysics
EmergencyVehicleProcessor	PrePhysics
ScoreProcessor	PostPhysics
CameraProcessor	PostPhysics
UIProcessor	PostPhysics
5. Entity Configurations (Mass)
Player Vehicle Config
Fragments:
- Transform
- Velocity
- PlayerInput
- SteeringState
- LaneFollower
- Autopilot
- DamageState
- Crashable
- ScoreSession

Tags:
- PlayerVehicle

Traffic Vehicle Config
Fragments:
- Transform
- Velocity
- SteeringState
- LaneFollower
- TrafficAI

Tags:
- TrafficVehicle

6. Key Architectural Parity (Unity ↔ Unreal)
Concept	Unity DOTS	Unreal Mass
Component	IComponentData	Fragment
System	ISystem	Processor
Tag	IComponentData (empty)	MassTag
Buffer	IBufferElementData	Shared Fragment
Archetype	EntityArchetype	EntityConfig
7. Why This Layout Is Correct For Your Game

Autopilot = data flip

Lane magnetism isolated

Procedural track fully data-driven

Wireframe & lighting decoupled

BeamNG damage can replace fragments

Replay/multiplayer-ready
