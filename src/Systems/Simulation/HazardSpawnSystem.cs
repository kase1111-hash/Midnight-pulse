// ============================================================================
// Nightflow - Hazard Spawn System
// Execution Order: 2 (Simulation Group)
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
    /// Spawns and manages road hazards (debris, cones, barriers, crashed cars).
    /// Hazard density increases with distance/score for progressive difficulty.
    ///
    /// Hazard Classification (from spec):
    /// - Loose tire: Severity 0.2, Mass 0.1, Cosmetic
    /// - Debris: Severity 0.4, Mass 0.3, Mechanical
    /// - Cone: Severity 0.3, Mass 0.2, Cosmetic
    /// - Barrier: Severity 0.9, Mass 0.9, Lethal
    /// - Crashed car: Severity 1.0, Mass 1.0, Lethal
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrackGenerationSystem))]
    public partial struct HazardSpawnSystem : ISystem
    {
        // Spawn parameters
        private const float BaseSpawnRate = 0.015f;       // hazards per meter
        private const float DifficultyScale = 0.002f;     // rate increase per 1000m
        private const float MinSpawnDistance = 150f;      // meters ahead of player
        private const float MaxSpawnDistance = 400f;      // meters ahead
        private const float DespawnDistance = 50f;        // meters behind player
        private const float SpawnCheckInterval = 15f;     // check every 15 meters
        private const float LaneWidth = 3.6f;
        private const int NumLanes = 4;

        // Hazard type distribution (cumulative)
        private const float TireChance = 0.20f;           // 20% loose tire
        private const float DebrisChance = 0.50f;         // 30% debris (cumulative 50%)
        private const float ConeChance = 0.75f;           // 25% cones (cumulative 75%)
        private const float BarrierChance = 0.92f;        // 17% barriers (cumulative 92%)
        // Remaining 8% = crashed car

        // State tracking
        private Random _random;
        private float _furthestSpawnedZ;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _random = new Random(42069);
            _furthestSpawnedZ = 0f;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // =============================================================
            // Check Game Mode - Skip hazards in Freeflow mode
            // =============================================================
            GameMode currentMode = GameMode.Nightflow;
            foreach (var modeState in SystemAPI.Query<RefRO<GameModeState>>())
            {
                currentMode = modeState.ValueRO.CurrentMode;
                break;
            }

            // Freeflow mode = no hazards, just relaxed driving
            bool spawnHazards = currentMode != GameMode.Freeflow;

            // Get player position and distance traveled
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

            // Calculate current spawn rate based on difficulty
            float difficultyMultiplier = 1f + DifficultyScale * (distanceTraveled / 100f);
            float currentSpawnRate = BaseSpawnRate * difficultyMultiplier;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // =============================================================
            // Despawn Hazards Behind Player
            // =============================================================

            float despawnZ = playerPos.z - DespawnDistance;

            foreach (var (hazard, transform, entity) in
                SystemAPI.Query<RefRO<Hazard>, RefRO<WorldTransform>>()
                    .WithAll<HazardTag>()
                    .WithEntityAccess())
            {
                if (transform.ValueRO.Position.z < despawnZ)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            // =============================================================
            // Spawn New Hazards Ahead (skipped in Freeflow mode)
            // =============================================================

            // Initialize furthest spawned if needed
            if (_furthestSpawnedZ < playerPos.z + MinSpawnDistance)
            {
                _furthestSpawnedZ = playerPos.z + MinSpawnDistance;
            }

            // Spawn hazards up to max distance (unless in Freeflow mode)
            float targetZ = playerPos.z + MaxSpawnDistance;

            while (_furthestSpawnedZ < targetZ)
            {
                // Roll for spawn at this location (only if hazards are enabled)
                if (spawnHazards && _random.NextFloat() < currentSpawnRate * SpawnCheckInterval)
                {
                    SpawnHazard(ref ecb, _furthestSpawnedZ);
                }

                _furthestSpawnedZ += SpawnCheckInterval;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void SpawnHazard(ref EntityCommandBuffer ecb, float z)
        {
            // Select random lane
            int lane = _random.NextInt(0, NumLanes);
            float laneOffset = (lane - 1.5f) * LaneWidth;

            // Add some lateral variance within lane
            float lateralVariance = _random.NextFloat(-LaneWidth * 0.3f, LaneWidth * 0.3f);
            float x = laneOffset + lateralVariance;

            // Select hazard type
            HazardType hazardType = SelectHazardType();
            float severity = GetSeverity(hazardType);
            float massFactor = GetMassFactor(hazardType);

            // Create hazard entity
            Entity hazard = ecb.CreateEntity();

            // Add components
            ecb.AddComponent(hazard, new WorldTransform
            {
                Position = new float3(x, 0f, z),
                Rotation = quaternion.RotateY(_random.NextFloat(0f, math.PI * 2f))
            });

            ecb.AddComponent(hazard, new Hazard
            {
                Type = hazardType,
                Severity = severity,
                MassFactor = massFactor,
                Hit = false
            });

            ecb.AddComponent(hazard, new CollisionShape
            {
                ShapeType = CollisionShapeType.Box,
                Size = GetHazardSize(hazardType),
                Offset = float3.zero
            });

            // Add tag
            ecb.AddComponent<HazardTag>(hazard);
        }

        private HazardType SelectHazardType()
        {
            float roll = _random.NextFloat();

            if (roll < TireChance)
                return HazardType.LooseTire;
            else if (roll < DebrisChance)
                return HazardType.Debris;
            else if (roll < ConeChance)
                return HazardType.Cone;
            else if (roll < BarrierChance)
                return HazardType.Barrier;
            else
                return HazardType.CrashedCar;
        }

        private float GetSeverity(HazardType type)
        {
            // From spec table
            switch (type)
            {
                case HazardType.LooseTire:
                    return 0.2f;
                case HazardType.Debris:
                    return 0.4f;
                case HazardType.Cone:
                    return 0.3f;
                case HazardType.Barrier:
                    return 0.9f;
                case HazardType.CrashedCar:
                    return 1.0f;
                default:
                    return 0.3f;
            }
        }

        private float GetMassFactor(HazardType type)
        {
            // From spec table
            switch (type)
            {
                case HazardType.LooseTire:
                    return 0.1f;
                case HazardType.Debris:
                    return 0.3f;
                case HazardType.Cone:
                    return 0.2f;
                case HazardType.Barrier:
                    return 0.9f;
                case HazardType.CrashedCar:
                    return 1.0f;
                default:
                    return 0.3f;
            }
        }

        private float3 GetHazardSize(HazardType type)
        {
            // Half-extents for collision box
            switch (type)
            {
                case HazardType.LooseTire:
                    return new float3(0.3f, 0.3f, 0.3f);
                case HazardType.Debris:
                    return new float3(0.5f, 0.2f, 0.5f);
                case HazardType.Cone:
                    return new float3(0.25f, 0.4f, 0.25f);
                case HazardType.Barrier:
                    return new float3(1.5f, 0.5f, 0.3f);
                case HazardType.CrashedCar:
                    return new float3(1.0f, 0.7f, 2.2f);
                default:
                    return new float3(0.5f, 0.3f, 0.5f);
            }
        }
    }
}
