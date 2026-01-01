// ============================================================================
// Nightflow - Leaderboard System
// Score tracking, submission, and retrieval
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
    /// Manages leaderboard data fetching and display.
    ///
    /// From spec:
    /// - Leaderboard integration
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct LeaderboardSystem : ISystem
    {
        // Fetch parameters
        private const float AutoRefreshInterval = 60f;  // Refresh every 60 seconds
        private const float MinFetchInterval = 5f;      // Minimum time between fetches

        private float _timeSinceLastFetch;
        private bool _pendingFetch;

        public void OnCreate(ref SystemState state)
        {
            _timeSinceLastFetch = 0f;
            _pendingFetch = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

            _timeSinceLastFetch += deltaTime;

            foreach (var leaderboard in SystemAPI.Query<RefRW<LeaderboardState>>())
            {
                ref var lb = ref leaderboard.ValueRW;

                // Auto-refresh check
                if (lb.IsAvailable && !lb.IsFetching &&
                    _timeSinceLastFetch > AutoRefreshInterval)
                {
                    _pendingFetch = true;
                }

                // Process pending fetch
                if (_pendingFetch && !lb.IsFetching &&
                    _timeSinceLastFetch > MinFetchInterval)
                {
                    lb.IsFetching = true;
                    _pendingFetch = false;

                    // Actual fetch would be performed by external service
                    // This system just manages the state
                }

                // Update last fetch time when fetch completes
                // (would be set by external service callback)
                if (!lb.IsFetching && lb.LastFetchTime > 0f)
                {
                    _timeSinceLastFetch = elapsedTime - lb.LastFetchTime;
                }
            }
        }
    }

    /// <summary>
    /// Submits scores to the leaderboard when runs complete.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(LeaderboardSystem))]
    public partial struct LeaderboardSubmissionSystem : ISystem
    {
        // Submission parameters
        private const int MinScoreForSubmission = 100;
        private const float MinDistanceForSubmission = 100f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Check for completed runs to submit
            foreach (var (scoring, crashState, velocity, transform) in
                SystemAPI.Query<RefRO<ScoringState>, RefRO<CrashState>,
                               RefRO<Velocity>, RefRO<WorldTransform>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<GhostRaceTag>())
            {
                // Only submit when run ends (crash)
                if (!crashState.ValueRO.IsCrashed) continue;

                // Only submit once per crash
                if (crashState.ValueRO.CrashTime > 0.1f) continue;

                int score = scoring.ValueRO.TotalScore;
                float distance = transform.ValueRO.Position.z; // Simplified distance

                // Minimum requirements for submission
                if (score < MinScoreForSubmission) continue;
                if (distance < MinDistanceForSubmission) continue;

                // Get max speed from run (would be tracked separately)
                float maxSpeed = 0f;

                // Create leaderboard entry for submission
                // Actual submission handled by external service
                var entry = new LeaderboardEntry
                {
                    Rank = 0, // Server assigns rank
                    PlayerId = 0, // From network state
                    PlayerNameHash = 0, // From profile
                    Score = score,
                    BestTime = 0f, // Time-based modes
                    MaxSpeed = maxSpeed,
                    TotalDistance = distance,
                    Timestamp = 0, // Server assigns
                    RegionCode = 0 // From profile
                };

                // Queue for submission
                // External service handles actual network request
            }
        }
    }

    /// <summary>
    /// Updates local player rank tracking.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LeaderboardSubmissionSystem))]
    public partial struct LeaderboardRankTrackingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get current score
            int currentScore = 0;

            foreach (var scoring in SystemAPI.Query<RefRO<ScoringState>>()
                .WithAll<PlayerVehicleTag>()
                .WithNone<GhostRaceTag>())
            {
                currentScore = scoring.ValueRO.TotalScore;
                break;
            }

            if (currentScore == 0) return;

            foreach (var (leaderboard, entity) in
                SystemAPI.Query<RefRW<LeaderboardState>>()
                    .WithEntityAccess())
            {
                if (!SystemAPI.HasBuffer<LeaderboardEntryBuffer>(entity)) continue;

                var entries = SystemAPI.GetBuffer<LeaderboardEntryBuffer>(entity);

                // Calculate current rank based on score
                int estimatedRank = 1;

                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Score > currentScore)
                    {
                        estimatedRank++;
                    }
                }

                // Only update if score beats personal best
                if (currentScore > leaderboard.ValueRO.LocalPlayerBestScore)
                {
                    leaderboard.ValueRW.LocalPlayerRank = estimatedRank;
                }
            }
        }
    }

    /// <summary>
    /// Manages leaderboard pagination and filtering.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct LeaderboardNavigationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Navigation handled by UI input events
            // This system processes type/filter changes

            foreach (var leaderboard in SystemAPI.Query<RefRW<LeaderboardState>>())
            {
                ref var lb = ref leaderboard.ValueRW;

                // Validate page bounds
                int maxPage = (lb.TotalEntries - 1) / lb.PageSize;
                lb.CurrentPage = math.clamp(lb.CurrentPage, 0, maxPage);

                // Check if page size changed (recalculate pages)
                if (lb.PageSize <= 0)
                {
                    lb.PageSize = 10; // Default
                }
            }
        }
    }

    /// <summary>
    /// Tracks statistics for leaderboard submission.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct RunStatisticsSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Track max speed during run
            foreach (var (velocity, runStats) in
                SystemAPI.Query<RefRO<Velocity>, RefRW<RunStatistics>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                float currentSpeed = velocity.ValueRO.Forward;
                if (currentSpeed > runStats.ValueRO.MaxSpeed)
                {
                    runStats.ValueRW.MaxSpeed = currentSpeed;
                }

                // Update run time
                runStats.ValueRW.RunTime += SystemAPI.Time.DeltaTime;
            }

            // Track near misses for bonus stats
            foreach (var (transform, velocity, runStats) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>, RefRW<RunStatistics>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                float3 playerPos = transform.ValueRO.Position;
                float playerSpeed = velocity.ValueRO.Forward;

                // Count near misses with traffic
                foreach (var (trafficTransform, trafficVelocity) in
                    SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>>()
                        .WithAll<TrafficVehicleTag>())
                {
                    float dist = math.distance(playerPos, trafficTransform.ValueRO.Position);

                    // Near miss: within 3m at high speed
                    if (dist < 3f && playerSpeed > 30f)
                    {
                        runStats.ValueRW.NearMisses++;
                        break; // Only count once per frame
                    }
                }
            }
        }
    }

    /// <summary>
    /// Initializes leaderboard singleton and buffers.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct LeaderboardInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Create leaderboard state singleton
            if (!SystemAPI.HasSingleton<LeaderboardState>())
            {
                var entity = state.EntityManager.CreateEntity();

                state.EntityManager.AddComponentData(entity, new LeaderboardState
                {
                    IsAvailable = true,
                    IsFetching = false,
                    LastFetchTime = 0f,
                    CurrentType = LeaderboardType.HighScore,
                    TimeFilter = LeaderboardTimeFilter.AllTime,
                    LocalPlayerRank = 0,
                    LocalPlayerBestScore = 0,
                    TotalEntries = 0,
                    CurrentPage = 0,
                    PageSize = 10
                });

                // Add entry buffer
                state.EntityManager.AddBuffer<LeaderboardEntryBuffer>(entity);

                state.EntityManager.SetName(entity, "LeaderboardState");
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            // One-time init
            state.Enabled = false;
        }
    }
}

namespace Nightflow.Components
{
    /// <summary>
    /// Run statistics for leaderboard submission.
    /// Tracks performance metrics during a run.
    /// </summary>
    public struct RunStatistics : IComponentData
    {
        /// <summary>Maximum speed achieved (m/s).</summary>
        public float MaxSpeed;

        /// <summary>Total run time (seconds).</summary>
        public float RunTime;

        /// <summary>Total distance traveled (meters).</summary>
        public float TotalDistance;

        /// <summary>Number of near misses.</summary>
        public int NearMisses;

        /// <summary>Number of drifts performed.</summary>
        public int DriftCount;

        /// <summary>Total drift time (seconds).</summary>
        public float TotalDriftTime;

        /// <summary>Longest drift duration (seconds).</summary>
        public float LongestDrift;

        /// <summary>Average speed during run (m/s).</summary>
        public float AverageSpeed;

        /// <summary>Number of lane changes.</summary>
        public int LaneChanges;

        /// <summary>Closest call distance (meters).</summary>
        public float ClosestCall;

        public static RunStatistics Default => new RunStatistics
        {
            MaxSpeed = 0f,
            RunTime = 0f,
            TotalDistance = 0f,
            NearMisses = 0,
            DriftCount = 0,
            TotalDriftTime = 0f,
            LongestDrift = 0f,
            AverageSpeed = 0f,
            LaneChanges = 0,
            ClosestCall = float.MaxValue
        };
    }
}
