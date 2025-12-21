// ============================================================================
// Nightflow - Emergency Vehicle Spawn System
// Execution Order: 3 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Spawns emergency vehicles behind the player at intervals.
    /// Emergency frequency increases with distance/score for progressive tension.
    ///
    /// Emergency vehicles approach from behind, creating pressure to yield.
    /// They despawn after passing the player.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrackGenerationSystem))]
    public partial struct EmergencySpawnSystem : ISystem
    {
        // Spawn parameters
        private const float BaseSpawnInterval = 45f;      // seconds between spawns
        private const float MinSpawnInterval = 20f;       // minimum at high difficulty
        private const float DifficultyScale = 0.001f;     // interval reduction per meter
        private const float SpawnDistanceBehind = 200f;   // meters behind player
        private const float DespawnDistanceAhead = 100f;  // meters ahead after passing
        private const float LaneWidth = 3.6f;

        // Spawn limits
        private const int MaxActiveEmergencies = 2;

        // State
        private float _spawnTimer;
        private Random _random;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _spawnTimer = BaseSpawnInterval * 0.5f; // First spawn earlier
            _random = new Random(31337);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get player state
            float3 playerPos = float3.zero;
            float distanceTraveled = 0f;
            bool playerActive = false;

            foreach (var (transform, scoreSession) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<ScoreSession>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                playerPos = transform.ValueRO.Position;
                distanceTraveled = scoreSession.ValueRO.Distance;
                playerActive = scoreSession.ValueRO.Active;
                break;
            }

            if (!playerActive)
                return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // =============================================================
            // Count Active Emergencies & Despawn Passed Ones
            // =============================================================

            int activeCount = 0;
            float despawnZ = playerPos.z + DespawnDistanceAhead;

            foreach (var (emergencyAI, transform, entity) in
                SystemAPI.Query<RefRO<EmergencyAI>, RefRO<WorldTransform>>()
                    .WithAll<EmergencyVehicleTag>()
                    .WithEntityAccess())
            {
                if (transform.ValueRO.Position.z > despawnZ)
                {
                    // Emergency has passed player, despawn
                    ecb.DestroyEntity(entity);
                }
                else
                {
                    activeCount++;
                }
            }

            // =============================================================
            // Spawn Timer
            // =============================================================

            // Calculate spawn interval based on difficulty
            float intervalReduction = DifficultyScale * distanceTraveled;
            float currentInterval = math.max(MinSpawnInterval, BaseSpawnInterval - intervalReduction);

            _spawnTimer -= deltaTime;

            if (_spawnTimer <= 0 && activeCount < MaxActiveEmergencies)
            {
                SpawnEmergencyVehicle(ref ecb, playerPos);
                _spawnTimer = currentInterval;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void SpawnEmergencyVehicle(ref EntityCommandBuffer ecb, float3 playerPos)
        {
            // Spawn behind player
            float spawnZ = playerPos.z - SpawnDistanceBehind;

            // Pick a lane (prefer outside lanes for approach)
            int lane = _random.NextBool() ? 0 : 3;
            float laneOffset = (lane - 1.5f) * LaneWidth;

            Entity emergency = ecb.CreateEntity();

            // Transform
            ecb.AddComponent(emergency, new WorldTransform
            {
                Position = new float3(laneOffset, 0.5f, spawnZ),
                Rotation = quaternion.identity
            });

            // Velocity
            ecb.AddComponent(emergency, new Velocity
            {
                Forward = 45f, // Emergency speed
                Lateral = 0f,
                Angular = 0f
            });

            // Lane follower
            ecb.AddComponent(emergency, new LaneFollower
            {
                CurrentLane = lane,
                TargetLane = lane,
                MagnetStrength = 12f,
                LateralOffset = 0f,
                SplineT = 0f
            });

            // Emergency AI
            ecb.AddComponent(emergency, new EmergencyAI
            {
                SirenActive = true,
                ApproachDistance = SpawnDistanceBehind,
                OvertakeBias = 0.5f,
                TargetVehicle = Entity.Null,
                Urgency = 0f,
                PressureTime = 0f,
                AggressiveOvertake = false
            });

            // Steering state for lane changes
            ecb.AddComponent(emergency, new SteeringState
            {
                CurrentAngle = 0f,
                TargetAngle = 0f,
                Smoothness = 8f,
                ChangingLanes = false,
                LaneChangeTimer = 0f,
                LaneChangeDuration = 0.8f,
                LaneChangeDir = 0
            });

            // Light emitter for siren
            ecb.AddComponent(emergency, new LightEmitter
            {
                Color = new float3(1f, 0.2f, 0.2f), // Red
                Intensity = 1f,
                Radius = 30f,
                Strobe = true,
                StrobeRate = 4f,
                StrobePhase = 0f
            });

            // Collision shape
            ecb.AddComponent(emergency, new CollisionShape
            {
                ShapeType = CollisionShapeType.Box,
                Size = new float3(1.0f, 0.8f, 2.5f),
                Offset = float3.zero
            });

            // Tags
            ecb.AddComponent<EmergencyVehicleTag>(emergency);
        }
    }
}
