# Architecture Specification

> Part of the [Nightflow Technical Specification](../SPEC-SHEET.md)

---

## ECS Philosophy

| Concept | Description |
|---------|-------------|
| Entities | IDs only |
| Components | Pure data (no logic) |
| Systems | Logic operating over component queries |

---

## Entity Archetypes

| Entity Type | Description |
|-------------|-------------|
| `PlayerVehicle` | Player-controlled vehicle with scoring |
| `TrafficVehicle` | AI-driven lane-following vehicles |
| `EmergencyVehicle` | Ambulance/police with siren and overtake behavior |
| `Hazard` | Road debris, barriers, construction |
| `TrackSegment` | Procedural road segment (with DynamicBuffer<LaneSplinePoint>) |
| `Lane` | Individual lane spline |
| `LightSource` | Dynamic/static light emitters |
| `CameraRig` | Chase camera system |
| `UIOverlay` | HUD elements |
| `ScoreSession` | Active scoring state |
| `GhostVehicle` | For replays |
| `NetworkPlayer` | Remote player in multiplayer |
| `SpectatorCamera` | Spectator mode camera rig |
| `LeaderboardState` | Score tracking singleton |
| `CityBuilding` | Procedural building entity |
| `BuildingImpostor` | LOD billboard for distant buildings |

---

## Core Components

### Transform & Motion
```
WorldTransform { Position: float3, Rotation: quaternion }
Velocity { Forward: float, Lateral: float, Angular: float }
```

### Vehicle Control
```
PlayerInput { Steer: float[-1,1], Throttle: float[0,1], Brake: float[0,1], Handbrake: bool }
Autopilot { Enabled: bool, TargetSpeed: float, LanePreference: int }
SteeringState { CurrentAngle: float, TargetAngle: float, Smoothness: float }
LaneTransition { Active: bool, FromLane: Entity, ToLane: Entity, Progress: float }
```

### Lane & Track
```
LaneFollower { LaneEntity: Entity, MagnetStrength: float, LateralOffset: float }
LaneSpline { ControlPoints: float3[], Width: float }
TrackSegment { Type: enum, Length: float, Difficulty: float }
```

### Damage & Scoring
```
DamageState { Front: float, Rear: float, Left: float, Right: float, Total: float }
Crashable { CrashThreshold: float }
ScoreSession { Distance: float, Multiplier: float, RiskMultiplier: float, Active: bool }
```

### Signaling
```
LightEmitter { Color: float3, Intensity: float, Strobe: bool, StrobeRate: float }
OffscreenSignal { Direction: float2, Urgency: float, Type: enum }
ReplayPlayer { InputLog: buffer, Timestamp: float }
```

### Network & Multiplayer
```
NetworkState { Connected: bool, IsHost: bool, PlayerId: uint, Latency: float }
NetworkPlayer { PlayerId: uint, InputSequence: uint, LastAckSequence: uint }
NetworkInput { Steer: float, Throttle: float, Brake: float, Handbrake: bool, Sequence: uint }
GhostRaceState { Active: bool, GhostPlayerId: uint, Progress: float }
SpectatorState { TargetEntity: Entity, CameraMode: enum, AutoSwitch: bool }
LeaderboardState { LocalPlayerRank: int, LocalPlayerBestScore: int, TotalEntries: int }
```

### City Generation
```
CityGenerationState { Seed: uint, MaxBuildings: int, MaxImpostors: int }
BuildingDefinition { Position: float3, Footprint: float2, Height: float, Style: byte }
BuildingLOD { CurrentLevel: byte, DistanceToCamera: float, FadeProgress: float }
BuildingImpostor { SourceBuilding: Entity, BillboardSize: float2 }
CityLightingState { WindowLightDensity: float, NeonIntensity: float }
```

### Raytracing
```
RaytracingState { Enabled: bool, QualityLevel: byte, MaxBounces: int }
RTLightSource { Color: float3, Intensity: float, Range: float, CastShadows: bool }
RTReflectionProbe { Position: float3, Range: float, UpdateFrequency: float }
SSRFallback { Enabled: bool, StepSize: float, MaxSteps: int }
```

### Advanced Damage
```
SoftBodyState { Stiffness: float, Damping: float, DeformationScale: float }
DeformationNode { LocalPosition: float3, Displacement: float3, Velocity: float3 }
ComponentHealth { Suspension: float, Steering: float, Tires: float, Engine: float }
ComponentFailure { Component: enum, FailureTime: float, Severity: float }
```

---

## System Execution Order

### Simulation Group
1. Input / Replay Playback
2. Autopilot
3. Steering & Lane Transition
4. Lane Magnetism
5. Vehicle Movement & Drift/Yaw
6. Collision & Impulse
7. Damage Evaluation
8. Crash Handling
9. Procedural Track Generation & Fork Resolution
10. Traffic / Emergency AI
11. Hazard Spawning
12. Scoring
13. Off-Screen Signaling

### Presentation Group
14. Camera
15. Wireframe Render
16. Raytracing / SSR Fallback
17. City LOD & Lighting
18. Audio
19. UI

### Network Group (when multiplayer active)
- Input Capture & Replication
- State Synchronization
- Prediction & Reconciliation
- Ghost Race Playback
- Spectator Camera Control
- Leaderboard Updates

### World Generation Group
- Track Generation
- City Generation
- Building LOD Management
- Impostor Billboard Updates
- City Lighting (window lights, neon)

---

## Critical Event Flow

```
VehicleMovement → LaneMagnetism → Collision → Damage → Crash → Fade → Autopilot → Score Reset → Resume
```

**Rule:** No scene reloads. Ever.

---

## Engine Parity Reference

| Concept | Unity DOTS | Unreal Mass |
|---------|------------|-------------|
| Component | IComponentData | Fragment |
| System | ISystem | Processor |
| Tag | Empty IComponentData | MassTag |
| Buffer | IBufferElementData | Shared Fragment |
| Archetype | EntityArchetype | EntityConfig |
