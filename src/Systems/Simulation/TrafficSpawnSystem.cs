// ============================================================================
// Nightflow - Traffic Spawn System
// Spawns and manages traffic vehicle population
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;
using Nightflow.Utilities;
using Nightflow.Config;

namespace Nightflow.Systems
{
    /// <summary>
    /// Spawns traffic vehicles ahead of the player and despawns them behind.
    /// Target density and speed follow the run's DifficultyCurve.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrackGenerationSystem))]
    public partial struct TrafficSpawnSystem : ISystem
    {
        // Spawn parameters
        private const int BaseTrafficCount = 12;          // Target vehicles at start
        private const int MaxTrafficCount = 30;           // Maximum vehicles
        private const float SpawnAheadMin = 80f;          // Min spawn distance ahead
        private const float SpawnAheadMax = 250f;         // Max spawn distance ahead
        private const float DespawnBehind = 60f;          // Distance behind to despawn
        private const float MinSpacing = 15f;             // Min distance between vehicles
        // GameConstants.LaneWidth uses GameConstants.GameConstants.LaneWidth

        // Speed parameters
        private const float BaseFlowSpeed = 22f;          // m/s base traffic speed
        private const float SpeedVariance = 0.2f;         // ±20% speed variance

        private Random _random;
        private int _spawnCounter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _random = new Random(54321);
            _spawnCounter = 0;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get player state
            float3 playerPos = float3.zero;
            float playerZ = 0f;
            float distanceTraveled = 0f;
            float timeSurvived = 0f;
            int playerLane = 1;

            foreach (var (transform, laneFollower, scoreSession, summary) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<LaneFollower>, RefRO<ScoreSession>, RefRO<ScoreSummary>>()
                    .WithAll<PlayerVehicleTag>())
            {
                playerPos = transform.ValueRO.Position;
                playerZ = playerPos.z;
                distanceTraveled = scoreSession.ValueRO.Distance;
                timeSurvived = summary.ValueRO.TimeSurvived;
                playerLane = laneFollower.ValueRO.CurrentLane;
                break;
            }

            DifficultyLevels levels = DifficultyCurve.Evaluate(distanceTraveled, timeSurvived);

            // =============================================================
            // Get Adaptive Difficulty Modifier
            // =============================================================

            float adaptiveDifficulty = 1f;
            foreach (var profile in SystemAPI.Query<RefRO<DifficultyProfile>>())
            {
                adaptiveDifficulty = profile.ValueRO.DifficultyModifier;
                break;
            }

            // Calculate target traffic count with adaptive scaling
            // The run's difficulty curve provides base progression, adaptive adjusts density
            float baseTarget = BaseTrafficCount * levels.TrafficDensityScale;

            // Apply adaptive modifier: struggling players get less traffic
            // Skilled players get more traffic
            float adaptiveTarget = baseTarget * adaptiveDifficulty;
            int targetCount = (int)math.clamp(adaptiveTarget, BaseTrafficCount * 0.5f, MaxTrafficCount);

            // Count current traffic
            int currentCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<TrafficAI>>().WithAll<TrafficVehicleTag>())
            {
                currentCount++;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var trafficPositions = new NativeList<float3>(Allocator.Temp);

            try
            {
                // =============================================================
                // Despawn Traffic Behind Player
                // =============================================================

                float despawnZ = playerZ - DespawnBehind;

                foreach (var (transform, entity) in
                    SystemAPI.Query<RefRO<WorldTransform>>()
                        .WithAll<TrafficVehicleTag>()
                        .WithEntityAccess())
                {
                    if (transform.ValueRO.Position.z < despawnZ)
                    {
                        ecb.DestroyEntity(entity);
                        currentCount--;
                    }
                }

                // =============================================================
                // Spawn New Traffic Ahead
                // =============================================================

                // Collect existing traffic positions for spacing check
                foreach (var transform in SystemAPI.Query<RefRO<WorldTransform>>().WithAll<TrafficVehicleTag>())
                {
                    trafficPositions.Add(transform.ValueRO.Position);
                }

                int spawnAttempts = 0;
                int maxAttempts = 5;

                while (currentCount < targetCount && spawnAttempts < maxAttempts)
                {
                    spawnAttempts++;

                    // Random spawn position
                    float spawnZ = playerZ + _random.NextFloat(SpawnAheadMin, SpawnAheadMax);
                    int spawnLane = _random.NextInt(0, 4); // Lanes 0-3

                    // Don't spawn in player's lane too close
                    if (spawnLane == playerLane && spawnZ - playerZ < SpawnAheadMin * 1.5f)
                        continue;

                    // Find track segment for proper positioning
                    float3 candidatePos = float3.zero;
                    bool foundSegment = false;

                    foreach (var (segment, segSpline) in
                        SystemAPI.Query<RefRO<TrackSegment>, RefRO<HermiteSpline>>()
                            .WithAll<TrackSegmentTag>())
                    {
                        if (spawnZ >= segment.ValueRO.StartZ && spawnZ <= segment.ValueRO.EndZ)
                        {
                            HermiteSpline spline = segSpline.ValueRO;
                            float t = (spawnZ - segment.ValueRO.StartZ) /
                                      (segment.ValueRO.EndZ - segment.ValueRO.StartZ);

                            SplineUtilities.BuildFrameAtT(spline.P0, spline.T0, spline.P1, spline.T1, t,
                                out float3 splinePos, out float3 forward, out float3 right, out float3 up);

                            candidatePos = splinePos + right * ((spawnLane - 1.5f) * GameConstants.LaneWidth) + up * 0.5f;
                            foundSegment = true;
                            break;
                        }
                    }

                    if (!foundSegment)
                        continue;

                    // Check spacing against the real on-road position (the road
                    // curves away from x = 0, so world-axis lane offsets are wrong)
                    bool tooClose = false;

                    for (int i = 0; i < trafficPositions.Length; i++)
                    {
                        if (math.distance(candidatePos, trafficPositions[i]) < MinSpacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (tooClose)
                        continue;

                    // Spawn traffic vehicle with adaptive difficulty
                    SpawnTrafficVehicle(ref ecb, candidatePos, spawnLane, levels.TrafficSpeedBonus, adaptiveDifficulty);
                    trafficPositions.Add(candidatePos);
                    currentCount++;
                }

                ecb.Playback(state.EntityManager);
            }
            finally
            {
                // Ensure native collections are always disposed, even on exception
                trafficPositions.Dispose();
                ecb.Dispose();
            }
        }

        private void SpawnTrafficVehicle(ref EntityCommandBuffer ecb, float3 position, int lane, float speedBonus, float adaptiveDifficulty)
        {
            Entity entity = ecb.CreateEntity();
            _spawnCounter++;

            // Determine speed with variance
            float speedVariation = _random.NextFloat(-SpeedVariance, SpeedVariance);
            float targetSpeed = BaseFlowSpeed * (1f + speedVariation);

            // Faster traffic as the run builds (bounded, see DifficultyCurve)
            targetSpeed += speedBonus;

            // Adaptive difficulty affects traffic aggressiveness
            // Higher difficulty = faster traffic, more challenging
            // Lower difficulty = slower traffic, more predictable
            float speedModifier = math.lerp(0.85f, 1.15f, (adaptiveDifficulty - 0.5f) / 1.5f);
            targetSpeed *= speedModifier;

            // Never let traffic outrun the player: an empty road isn't a hard road
            targetSpeed = DifficultyCurve.ClampTrafficSpeed(targetSpeed, GameConstants.MaxForwardSpeed);

            // Transform
            ecb.AddComponent(entity, new WorldTransform
            {
                Position = position,
                Rotation = quaternion.identity
            });

            ecb.AddComponent(entity, new Velocity
            {
                Forward = targetSpeed,
                Lateral = 0f,
                Angular = 0f
            });

            ecb.AddComponent(entity, new PreviousTransform
            {
                Position = position,
                Rotation = quaternion.identity
            });

            // AI components
            ecb.AddComponent(entity, new TrafficAI
            {
                TargetSpeed = targetSpeed,
                DecisionTimer = _random.NextFloat(0f, 0.5f), // Stagger decisions
                LaneChangeTimer = 0f,
                LaneChangeLock = false,
                PreferredLane = lane
            });

            ecb.AddComponent(entity, new LaneScoreCache());

            // Lane following
            ecb.AddComponent(entity, new LaneFollower
            {
                CurrentLane = lane,
                TargetLane = lane,
                LateralOffset = 0f,
                MagnetStrength = 1f,
                SplineParameter = 0f
            });

            // Steering
            ecb.AddComponent(entity, new SteeringState
            {
                CurrentAngle = 0f,
                TargetAngle = 0f,
                Smoothness = 6f,
                ChangingLanes = false
            });

            // Collision shape
            ecb.AddComponent(entity, new CollisionShape
            {
                Size = new float3(0.9f, 0.6f, 2f)
            });

            // Light emitter for rendering
            ecb.AddComponent(entity, new LightEmitter
            {
                Color = new float3(1f, 0.3f, 0.8f), // Magenta for traffic
                Intensity = 0.7f
            });

            // Tags
            ecb.AddComponent<TrafficVehicleTag>(entity);
        }
    }
}
