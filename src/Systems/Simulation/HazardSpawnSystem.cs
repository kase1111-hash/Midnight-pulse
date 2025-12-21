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
    /// Spawns and manages road hazards (debris, potholes, barriers).
    /// Hazard density increases with distance/score for progressive difficulty.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrackGenerationSystem))]
    public partial struct HazardSpawnSystem : ISystem
    {
        // Spawn parameters
        private const float BaseSpawnRate = 0.02f;        // hazards per meter
        private const float DifficultyScale = 0.001f;     // rate increase per 1000m
        private const float MinSpawnDistance = 100f;      // meters ahead of player
        private const float MaxSpawnDistance = 300f;      // meters ahead
        private const float DespawnDistance = 50f;        // meters behind player

        // Hazard type distribution
        private const float DebrisChance = 0.5f;          // 50% debris
        private const float PotholeChance = 0.3f;         // 30% potholes
        private const float BarrierChance = 0.2f;         // 20% barriers

        // Severity ranges by type
        private const float DebrisSeverityMin = 0.1f;
        private const float DebrisSeverityMax = 0.4f;
        private const float PotholeSeverityMin = 0.2f;
        private const float PotholeSeverityMax = 0.5f;
        private const float BarrierSeverityMin = 0.6f;
        private const float BarrierSeverityMax = 1.0f;

        private Random _random;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _random = new Random(12345);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get player position and distance traveled
            float3 playerPos = float3.zero;
            float distanceTraveled = 0f;

            foreach (var (transform, scoreSession) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<ScoreSession>>()
                    .WithAll<PlayerVehicleTag>())
            {
                playerPos = transform.ValueRO.Position;
                distanceTraveled = scoreSession.ValueRO.Distance;
                break;
            }

            // Calculate current spawn rate based on difficulty
            float currentSpawnRate = BaseSpawnRate + DifficultyScale * (distanceTraveled / 1000f);

            // =============================================================
            // Despawn Hazards Behind Player
            // =============================================================

            var ecb = new EntityCommandBuffer(Allocator.Temp);
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
            // Spawn New Hazards Ahead
            // =============================================================

            // TODO: Track furthest spawned hazard Z
            // TODO: Use spatial hashing to avoid overlapping hazards

            // Placeholder spawn logic (would be triggered by track generation)
            // float spawnZ = playerPos.z + MinSpawnDistance;
            // while (spawnZ < playerPos.z + MaxSpawnDistance)
            // {
            //     if (_random.NextFloat() < currentSpawnRate)
            //     {
            //         SpawnHazard(ref state, ref ecb, spawnZ);
            //     }
            //     spawnZ += 10f; // Check every 10 meters
            // }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private HazardType SelectHazardType(ref Random random)
        {
            float roll = random.NextFloat();

            if (roll < DebrisChance)
                return HazardType.Debris;
            else if (roll < DebrisChance + PotholeChance)
                return HazardType.Pothole;
            else
                return HazardType.Barrier;
        }

        private float GetSeverity(HazardType type, ref Random random)
        {
            switch (type)
            {
                case HazardType.Debris:
                    return random.NextFloat(DebrisSeverityMin, DebrisSeverityMax);
                case HazardType.Pothole:
                    return random.NextFloat(PotholeSeverityMin, PotholeSeverityMax);
                case HazardType.Barrier:
                    return random.NextFloat(BarrierSeverityMin, BarrierSeverityMax);
                default:
                    return 0.3f;
            }
        }
    }

    public enum HazardType
    {
        Debris,
        Pothole,
        Barrier
    }
}
