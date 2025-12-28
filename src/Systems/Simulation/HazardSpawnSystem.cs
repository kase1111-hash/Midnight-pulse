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

        // Hazard type distribution (cumulative) - base values at normal difficulty
        private const float BaseTireChance = 0.20f;       // 20% loose tire
        private const float BaseDebrisChance = 0.50f;     // 30% debris (cumulative 50%)
        private const float BaseConeChance = 0.75f;       // 25% cones (cumulative 75%)
        private const float BaseBarrierChance = 0.92f;    // 17% barriers (cumulative 92%)
        // Remaining 8% = crashed car

        // Adaptive lethal hazard scaling
        private const float MinLethalChance = 0.05f;      // Minimum lethal hazard chance (easy)
        private const float MaxLethalChance = 0.35f;      // Maximum lethal hazard chance (hard)

        // Per-frame spawn limits to prevent entity explosion on lag spikes
        private const int MaxHazardsPerFrame = 5;

        // State tracking
        private Random _random;
        private float _furthestSpawnedZ;
        private float _currentAdaptiveDifficulty;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _random = new Random(42069);
            _furthestSpawnedZ = 0f;
            _currentAdaptiveDifficulty = 1f;
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

            // =============================================================
            // Get Adaptive Difficulty Modifier
            // =============================================================

            _currentAdaptiveDifficulty = 1f;
            foreach (var profile in SystemAPI.Query<RefRO<DifficultyProfile>>())
            {
                _currentAdaptiveDifficulty = profile.ValueRO.DifficultyModifier;
                break;
            }

            // Calculate current spawn rate based on distance + adaptive difficulty
            // Distance scaling provides base progression
            // Adaptive difficulty adjusts based on player skill
            float distanceMultiplier = 1f + DifficultyScale * (distanceTraveled / 100f);
            float currentSpawnRate = BaseSpawnRate * distanceMultiplier * _currentAdaptiveDifficulty;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            try
            {
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
                // Limit spawns per frame to prevent entity explosion on lag spikes
                float targetZ = playerPos.z + MaxSpawnDistance;
                int hazardsSpawnedThisFrame = 0;

                while (_furthestSpawnedZ < targetZ && hazardsSpawnedThisFrame < MaxHazardsPerFrame)
                {
                    // Roll for spawn at this location (only if hazards are enabled)
                    if (spawnHazards && _random.NextFloat() < currentSpawnRate * SpawnCheckInterval)
                    {
                        SpawnHazard(ref ecb, _furthestSpawnedZ);
                        hazardsSpawnedThisFrame++;
                    }

                    _furthestSpawnedZ += SpawnCheckInterval;
                }

                // If we hit the spawn limit, cap furthestSpawnedZ to prevent runaway catch-up
                // This ensures we don't accumulate a huge backlog during sustained lag
                if (hazardsSpawnedThisFrame >= MaxHazardsPerFrame && _furthestSpawnedZ < targetZ)
                {
                    // Skip ahead to maintain reasonable spawn density rather than catching up
                    float skipDistance = (targetZ - _furthestSpawnedZ) * 0.5f;
                    if (skipDistance > SpawnCheckInterval * 3)
                    {
                        _furthestSpawnedZ += skipDistance;
                    }
                }

                ecb.Playback(state.EntityManager);
            }
            finally
            {
                // Ensure ECB is always disposed, even on exception
                ecb.Dispose();
            }
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

            // Adjust lethal hazard chance based on adaptive difficulty
            // At difficulty 0.5 (easy): lethal chance reduced to MinLethalChance
            // At difficulty 1.0 (normal): lethal chance at base rate (25%)
            // At difficulty 2.0 (hard): lethal chance increased to MaxLethalChance
            float difficultyFactor = math.saturate((_currentAdaptiveDifficulty - 0.5f) / 1.5f);
            float lethalChance = math.lerp(MinLethalChance, MaxLethalChance, difficultyFactor);

            // Non-lethal hazards take up the remaining probability
            float nonLethalTotal = 1f - lethalChance;

            // Scale non-lethal distributions to fill remaining space
            // Original ratios: tire 20%, debris 30%, cone 25% = 75% non-lethal
            float tireRatio = 0.20f / 0.75f;
            float debrisRatio = 0.30f / 0.75f;
            float coneRatio = 0.25f / 0.75f;

            float tireChance = nonLethalTotal * tireRatio;
            float debrisChance = tireChance + nonLethalTotal * debrisRatio;
            float coneChance = debrisChance + nonLethalTotal * coneRatio;

            // Lethal hazards: barrier vs crashed car ratio remains 17:8
            float barrierRatio = 17f / 25f;

            if (roll < tireChance)
                return HazardType.LooseTire;
            else if (roll < debrisChance)
                return HazardType.Debris;
            else if (roll < coneChance)
                return HazardType.Cone;
            else if (roll < coneChance + lethalChance * barrierRatio)
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
            // These must match the mesh dimensions in ProceduralHazardMeshSystem
            switch (type)
            {
                case HazardType.LooseTire:
                    // Mesh: Radius=0.35, Width=0.25 (tire lying flat)
                    return new float3(0.35f, 0.35f, 0.125f);
                case HazardType.Debris:
                    // Mesh: Radius=0.4, flat debris pile
                    return new float3(0.4f, 0.15f, 0.4f);
                case HazardType.Cone:
                    // Mesh: Height=0.7, BaseRadius=0.2
                    return new float3(0.2f, 0.35f, 0.2f);
                case HazardType.Barrier:
                    // Mesh: Width=0.6, Height=0.9, Length=1.5
                    // Half-extents: (Width/2, Height/2, Length/2)
                    return new float3(0.3f, 0.45f, 0.75f);
                case HazardType.CrashedCar:
                    // Mesh: Width=1.8, Height=1.4, Length=4.5
                    // Half-extents: (Width/2, Height/2, Length/2)
                    return new float3(0.9f, 0.7f, 2.25f);
                default:
                    return new float3(0.5f, 0.3f, 0.5f);
            }
        }
    }
}
