// ============================================================================
// Nightflow - Ghost Racing System
// Async multiplayer ghost racing with downloaded runs
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;
using Nightflow.Config;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages async ghost racing mode.
    /// Downloads and spawns ghost vehicles from recorded runs.
    ///
    /// From spec:
    /// - Ghost racing (async multiplayer)
    /// - Record: globalSeed + fixed-timestep input log
    /// - Second PlayerVehicle entity driven by log (identical sim)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ReplayPlaybackSystem))]
    public partial struct GhostRacingSystem : ISystem
    {
        // Ghost racing parameters
        private const int MaxGhosts = 4;
        private const float GhostSpawnOffset = 2f;       // Lateral offset between ghosts
        private const float PositionUpdateInterval = 0.1f;
        private const float NearbyThreshold = 50f;       // Distance to consider "nearby"

        private float _positionUpdateTimer;

        public void OnCreate(ref SystemState state)
        {
            _positionUpdateTimer = 0f;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get ghost race state
            bool isRacing = false;
            int ghostCount = 0;

            foreach (var raceState in SystemAPI.Query<RefRW<GhostRaceState>>())
            {
                isRacing = raceState.ValueRO.IsRacing;
                ghostCount = raceState.ValueRO.GhostCount;

                if (isRacing)
                {
                    raceState.ValueRW.CurrentTime += deltaTime;
                }
                break;
            }

            if (!isRacing) return;

            _positionUpdateTimer += deltaTime;

            // =============================================================
            // Update Race Positions
            // =============================================================

            if (_positionUpdateTimer >= PositionUpdateInterval)
            {
                _positionUpdateTimer = 0f;

                // Get player position and distance
                float playerZ = 0f;
                float playerDistance = 0f;

                foreach (var (transform, velocity) in
                    SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>>()
                        .WithAll<PlayerVehicleTag>()
                        .WithNone<GhostRaceTag>())
                {
                    playerZ = transform.ValueRO.Position.z;
                    playerDistance = playerZ; // Simplified - actual distance along track
                    break;
                }

                // Calculate positions relative to ghosts
                int position = 1; // Start at first place
                float nearestDist = float.MaxValue;

                foreach (var (transform, replayState, ghostRender) in
                    SystemAPI.Query<RefRO<WorldTransform>, RefRO<ReplayState>, RefRW<GhostRenderState>>()
                        .WithAll<GhostRaceTag>())
                {
                    float ghostZ = transform.ValueRO.Position.z;
                    float ghostDistance = ghostZ;

                    // If ghost is ahead, we're behind
                    if (ghostDistance > playerDistance)
                    {
                        position++;
                    }

                    // Track nearest ghost
                    float dist = math.abs(ghostDistance - playerDistance);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                    }

                    // Adjust ghost visibility based on distance
                    if (dist < NearbyThreshold)
                    {
                        // Fully visible when close
                        ghostRender.ValueRW.Alpha = ghostRender.ValueRO.BaseAlpha;
                    }
                    else
                    {
                        // Fade out distant ghosts
                        float fadeT = math.saturate((dist - NearbyThreshold) / 100f);
                        ghostRender.ValueRW.Alpha = math.lerp(
                            ghostRender.ValueRO.BaseAlpha,
                            0.2f,
                            fadeT
                        );
                    }
                }

                // Update race state
                foreach (var raceState in SystemAPI.Query<RefRW<GhostRaceState>>())
                {
                    raceState.ValueRW.CurrentPosition = position;
                    raceState.ValueRW.DistanceToNearest =
                        playerDistance > 0 ? nearestDist : -nearestDist;
                }
            }

            // =============================================================
            // Check for Race Completion
            // =============================================================

            foreach (var (replayState, ghostData) in
                SystemAPI.Query<RefRO<ReplayState>, RefRO<GhostRunData>>()
                    .WithAll<GhostRaceTag>())
            {
                if (replayState.ValueRO.IsComplete)
                {
                    // Ghost finished - record their time for comparison
                    foreach (var raceState in SystemAPI.Query<RefRW<GhostRaceState>>())
                    {
                        // Time already stored in GhostRunData.TotalTime
                    }
                }
            }
        }
    }

    /// <summary>
    /// Spawns ghost vehicles for ghost racing mode.
    /// Creates ghosts from downloaded run data.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GhostRaceSpawnSystem : ISystem
    {
        // Ghost visual parameters
        private static readonly float3[] GhostColors = new float3[]
        {
            new float3(1f, 0.3f, 0.3f),   // Red
            new float3(0.3f, 1f, 0.3f),   // Green
            new float3(1f, 1f, 0.3f),     // Yellow
            new float3(1f, 0.3f, 1f),     // Magenta
        };

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Check for ghost spawn requests
            bool shouldSpawn = false;

            foreach (var raceState in SystemAPI.Query<RefRO<GhostRaceState>>())
            {
                if (raceState.ValueRO.IsRacing && raceState.ValueRO.GhostCount == 0)
                {
                    // New race started, spawn ghosts
                    shouldSpawn = true;
                }
                break;
            }

            if (!shouldSpawn) return;

            // Get ghost run references
            Entity raceEntity = Entity.Null;
            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<GhostRaceState>>()
                    .WithEntityAccess())
            {
                raceEntity = entity;
                break;
            }

            if (raceEntity == Entity.Null) return;
            if (!SystemAPI.HasBuffer<GhostRunReference>(raceEntity)) return;

            var runRefs = SystemAPI.GetBuffer<GhostRunReference>(raceEntity);
            if (runRefs.Length == 0) return;

            // Get player start position
            float3 playerStart = float3.zero;
            quaternion playerRot = quaternion.identity;

            foreach (var transform in SystemAPI.Query<RefRO<WorldTransform>>()
                .WithAll<PlayerVehicleTag>()
                .WithNone<GhostRaceTag>())
            {
                playerStart = transform.ValueRO.Position;
                playerRot = transform.ValueRO.Rotation;
                break;
            }

            // Spawn ghosts (implementation would create entities with ghost components)
            // This integrates with existing GhostSpawnSystem from ReplayPlaybackSystem
            int spawnedCount = 0;
            for (int i = 0; i < runRefs.Length && spawnedCount < GhostColors.Length; i++)
            {
                var runRef = runRefs[i];

                // Calculate spawn offset (stagger ghosts laterally)
                float lateralOffset = (i - (runRefs.Length - 1) * 0.5f) * 2f;
                float3 spawnPos = playerStart + new float3(lateralOffset, 0f, -2f);

                // Ghost entity would be created here with:
                // - GhostRaceTag
                // - ReplayState (from run data)
                // - GhostRenderState (with color from GhostColors[i])
                // - GhostRunData (metadata)
                // - InputLogEntry buffer (loaded from run)
                // - Vehicle components (same as player)

                spawnedCount++;
            }

            // Update ghost count
            foreach (var raceState in SystemAPI.Query<RefRW<GhostRaceState>>())
            {
                raceState.ValueRW.GhostCount = spawnedCount;
            }
        }
    }

    /// <summary>
    /// Updates ghost run references for ghost racing mode.
    /// Handles ghost run selection based on difficulty.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(GhostRaceSpawnSystem))]
    public partial struct GhostRunSelectionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get current race state
            GhostDifficulty difficulty = GhostDifficulty.PersonalBest;
            uint trackId = 0;

            foreach (var raceState in SystemAPI.Query<RefRO<GhostRaceState>>())
            {
                difficulty = raceState.ValueRO.Difficulty;
                trackId = raceState.ValueRO.TrackId;
                break;
            }

            // Ghost selection would query downloaded runs based on:
            // - difficulty level (percentile times)
            // - track ID match
            // - valid seed match for determinism
            //
            // Selected runs are added to GhostRunReference buffer
            // Actual download/storage handled by external service
        }
    }

    /// <summary>
    /// Uploads completed runs for ghost racing.
    /// Prepares run data for leaderboard submission.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct GhostRunUploadSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Check for completed runs to upload
            foreach (var (replaySystemState, entity) in
                SystemAPI.Query<RefRO<ReplaySystemState>>()
                    .WithEntityAccess())
            {
                if (!replaySystemState.ValueRO.IsRecording) continue;

                // Check if player crashed or completed run
                bool runEnded = false;

                foreach (var crashState in SystemAPI.Query<RefRO<CrashState>>()
                    .WithAll<PlayerVehicleTag>())
                {
                    runEnded = crashState.ValueRO.IsCrashed;
                    break;
                }

                if (!runEnded) continue;

                // Prepare run data for upload
                if (!SystemAPI.HasBuffer<InputLogEntry>(entity)) continue;

                var inputLog = SystemAPI.GetBuffer<InputLogEntry>(entity);
                if (inputLog.Length < 60) continue; // At least 1 second of data

                // Calculate run statistics
                float totalTime = 0f;
                float maxSpeed = 0f;
                int finalScore = 0;

                foreach (var (velocity, scoring) in
                    SystemAPI.Query<RefRO<Velocity>, RefRO<ScoringState>>()
                        .WithAll<PlayerVehicleTag>())
                {
                    maxSpeed = math.max(maxSpeed, velocity.ValueRO.Forward);
                    finalScore = scoring.ValueRO.TotalScore;
                    break;
                }

                if (inputLog.Length > 0)
                {
                    totalTime = inputLog[inputLog.Length - 1].Timestamp;
                }

                // GhostRunData would be created and queued for upload
                // Actual network upload handled by external service
            }
        }
    }

    /// <summary>
    /// Initializes ghost race singleton on world creation.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct GhostRaceInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Create ghost race state singleton
            if (!SystemAPI.HasSingleton<GhostRaceState>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new GhostRaceState
                {
                    IsRacing = false,
                    GhostCount = 0,
                    CurrentPosition = 1,
                    DistanceToNearest = 0f,
                    PersonalBest = 0f,
                    CurrentTime = 0f,
                    TrackId = 0,
                    Difficulty = GhostDifficulty.PersonalBest
                });

                // Add ghost run reference buffer
                state.EntityManager.AddBuffer<GhostRunReference>(entity);

                state.EntityManager.SetName(entity, "GhostRaceState");
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            // One-time init
            state.Enabled = false;
        }
    }
}
