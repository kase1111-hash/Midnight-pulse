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
PreviousTransform { Position: float3, Rotation: quaternion }
VehicleVelocity { Linear: float3, Angular: float3 }
```

### Vehicle Control
```
PlayerInput { Steer: float[-1,1], Throttle: float[0,1], Brake: float[0,1], Handbrake: bool }
Autopilot { Enabled: bool, TargetSpeed: float, LanePreference: int }
SteeringState { CurrentAngle: float, TargetAngle: float, Smoothness: float, ChangingLanes: bool, LaneChangeTimer: float, LaneChangeDuration: float, LaneChangeDir: int }
LaneTransition { Active: bool, FromLane: Entity, ToLane: Entity, Progress: float, Duration: float, Direction: int }
DriftState { YawOffset: float, YawRate: float, SlipAngle: float, IsDrifting: bool }
SpeedTier { Tier: int, Multiplier: float }
```

### Lane & Track
```
LaneFollower { LaneEntity: Entity, MagnetStrength: float, LateralOffset: float }
LaneSpline { ControlPoints: float3[], Width: float }
TrackSegment { Type: enum, Length: float, Difficulty: float }
```

### Damage & Health
```
DamageState { Front: float, Rear: float, Left: float, Right: float, Total: float }
Crashable { CrashThreshold: float, CrashSpeed: float, YawFailThreshold: float }
CrashState { IsCrashed: bool, CrashTime: float, Reason: CrashReason }
CollisionEvent { Occurred: bool, OtherEntity: Entity, ImpactSpeed: float, Normal: float3, ContactPoint: float3 }
ImpulseData { Magnitude: float, Direction: float3, ForwardImpulse: float, LateralImpulse: float, YawKick: float }
```

### Component Health (Phase 2 Damage)
```
ComponentHealth { Suspension: float, Steering: float, Tires: float, Engine: float, Transmission: float }
ComponentFailureState { FailedComponents: ComponentFailures, TimeSinceLastFailure: float }
ComponentDamageConfig { FailureThreshold: float, FrontToSteeringRatio: float, RearToTransmissionRatio: float, SideToSuspensionRatio: float, TotalToEngineRatio: float, ImpactToTiresRatio: float }
SoftBodyState { CurrentDeformation: float4, TargetDeformation: float4, DeformationVelocity: float4, SpringConstant: float, Damping: float }
```

### Scoring
```
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
NetworkState { IsConnected: bool, IsHost: bool, CurrentTick: uint, LastConfirmedTick: uint, RTT: float, SessionId: uint, SessionSeed: uint, LocalPlayerId: int, PlayerCount: int, Mode: NetworkMode }
NetworkPlayer { PlayerId: int, IsLocal: bool, IsHost: bool, LastInputTick: uint, Latency: float, ConnectionState: PlayerConnectionState }
NetworkInput { Steer: float, Throttle: float, Brake: float, Handbrake: bool, Tick: uint, SequenceNumber: uint, Acknowledged: bool }
NetworkTransform { NetworkPosition: float3, NetworkRotation: quaternion, NetworkVelocity: float3, StateTick: uint, NeedsCorrection: bool }
GhostRaceState { IsRacing: bool, GhostCount: int, CurrentPosition: int, DistanceToNearest: float, Difficulty: GhostDifficulty }
SpectatorState { IsSpectating: bool, TargetEntity: Entity, CameraMode: SpectatorCameraMode, FreeCamPosition: float3, AutoSwitchDelay: float }
LeaderboardState { IsAvailable: bool, IsFetching: bool, CurrentType: LeaderboardType, LocalPlayerRank: int, LocalPlayerBestScore: int, TotalEntries: int }
```

### City Generation
```
CityGenerationState { Seed: uint, MaxBuildings: int, MaxImpostors: int }
BuildingDefinition { Position: float3, Footprint: float2, Height: float, Style: byte }
BuildingLOD { CurrentLevel: byte, DistanceToCamera: float, FadeProgress: float }
BuildingImpostor { SourceBuilding: Entity, BillboardSize: float2 }
CityLightingState { WindowLightDensity: float, NeonIntensity: float }
```

### Reflections
```
ReflectionState { Enabled: bool, QualityLevel: byte, MaxBounces: int }
RTLightSource { Color: float3, Intensity: float, Range: float, CastShadows: bool }
RTReflectionProbe { Position: float3, Range: float, UpdateFrequency: float }
SSRFallback { Enabled: bool, StepSize: float, MaxSteps: int }
```

---

## Entity Tags

### Vehicle Tags
```
PlayerVehicleTag, TrafficVehicleTag, EmergencyVehicleTag, GhostVehicleTag
RemotePlayerTag, GhostRaceTag
```

### State Tags
```
AutopilotActiveTag, CrashedTag, LaneTransitionActiveTag, DriftingTag
```

### Track Tags
```
TrackSegmentTag, LaneTag, ForkSegmentTag, TunnelTag, OverpassTag, LaneMarkerTag
```

### Hazard Tags
```
HazardTag, LethalHazardTag
```

### System Tags
```
LightSourceTag, SirenActiveTag, DestroyTag, NeedsInitializationTag, JustSpawnedTag
SpectatorCameraTag, NetworkSyncTag
```

---

## System Execution Order

### Simulation Group (60 ticks/second)
1. Input / Replay Playback
2. Autopilot
3. Steering & Lane Transition
4. Lane Magnetism
5. Vehicle Movement & Drift/Yaw
6. Collision Detection
7. Impulse Application
8. Damage Evaluation
9. Component Failure Evaluation
10. Crash Handling
11. Procedural Track Generation & Fork Resolution
12. Traffic / Emergency AI
13. Hazard Spawning
14. Scoring & Risk Events
15. Off-Screen Signaling
16. Replay Recording
17. Adaptive Difficulty
18. Daily Challenge

### Presentation Group
19. Camera
20. Wireframe Render
21. Procedural Mesh Generation (Road, Vehicle, Hazard, Tunnel, Overpass, Light)
22. Reflections / SSR
23. City LOD & Lighting
24. Headlight System
25. Particles (Tire Smoke, Sparks, Speed Lines)
26. Crash Flash
27. Post-Processing Sync
28. Terminal Rendering
29. Ambient Cycle
30. City Skyline
31. Ghost Render
32. Offscreen Signal
33. Audio
34. UI

### Audio Group
- Engine Audio
- Collision Audio
- Siren Audio
- Ambient Audio
- Music
- UI Audio

### UI Group
- Screen Flow
- HUD Update
- Menu Navigation
- Performance Stats
- Warning Indicator

### Network Group (when multiplayer active)
- Network Replication
- Ghost Racing
- Spectator
- Leaderboard

### World Generation Group
- City Generation
- City LOD
- City Lighting

### Core/Initialization
- Game Bootstrap
- World Initialization
- Game State
- Game Mode

---

## Critical Event Flow

```
VehicleMovement -> LaneMagnetism -> Collision -> Impulse -> Damage -> ComponentFailure -> Crash -> Fade -> Autopilot -> Score Reset -> Resume
```

**Rule:** No scene reloads. Ever.

---

## Buffer Elements

```
LaneSplinePointBuffer    - Spline control points for lanes
InputLogEntry           - Recorded inputs for replay
NetworkInputEntry       - Network synchronized inputs
LeaderboardEntryBuffer  - Leaderboard rankings
GhostRunReference       - Ghost race references
ProceduralMeshBuffer    - Generated mesh vertices/indices
```

---

## Engine Parity Reference

| Concept | Unity DOTS | Unreal Mass |
|---------|------------|-------------|
| Component | IComponentData | Fragment |
| System | ISystem | Processor |
| Tag | Empty IComponentData | MassTag |
| Buffer | IBufferElementData | Shared Fragment |
| Archetype | EntityArchetype | EntityConfig |
