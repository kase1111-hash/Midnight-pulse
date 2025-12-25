// ============================================================================
// Nightflow - Game Mode System
// Manages game mode selection, initialization, and mode-specific rules
// Execution Order: Early in Simulation Group (before gameplay systems)
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
    /// Manages game modes and applies mode-specific gameplay rules.
    ///
    /// Modes:
    /// - Nightflow: Standard gameplay (default)
    /// - Redline: No top speed, diminishing acceleration - push your limits
    /// - Ghost: Race against your past self using a seed
    /// - Freeflow: No hazards, relaxed driving
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(InputSystem))]
    public partial struct GameModeSystem : ISystem
    {
        private bool _initialized;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _initialized = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Initialize game mode entity if needed
            if (!_initialized)
            {
                bool hasGameMode = false;
                foreach (var _ in SystemAPI.Query<RefRO<GameModeState>>())
                {
                    hasGameMode = true;
                    break;
                }

                if (!hasGameMode)
                {
                    CreateGameModeEntity(ref state);
                }
                _initialized = true;
            }

            float deltaTime = SystemAPI.Time.DeltaTime;
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

            // Update mode-specific state
            foreach (var (modeState, redlineStats) in
                SystemAPI.Query<RefRW<GameModeState>, RefRW<RedlineStats>>())
            {
                // Update Redline stats
                if (modeState.ValueRO.CurrentMode == GameMode.Redline)
                {
                    UpdateRedlineStats(ref state, ref modeState.ValueRW, ref redlineStats.ValueRW, deltaTime, elapsedTime);
                }
            }

            // Update display state based on current mode
            foreach (var (modeState, displayState) in
                SystemAPI.Query<RefRO<GameModeState>, RefRW<ModeDisplayState>>())
            {
                UpdateDisplayState(modeState.ValueRO, ref displayState.ValueRW);
            }
        }

        private void CreateGameModeEntity(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            Entity entity = ecb.CreateEntity();

            // Default to Nightflow mode
            ecb.AddComponent(entity, new GameModeState
            {
                CurrentMode = GameMode.Nightflow,
                ModeSelectActive = false,
                SessionSeed = GenerateRandomSeed(),
                IsRecording = false,
                IsPlayingGhost = false,
                RedlineMaxSpeed = 0f,
                RedlinePersonalBest = 0f,
                RedlineExtreme = 0f
            });

            // Ghost mode config
            ecb.AddComponent(entity, new GhostModeConfig
            {
                RunSeed = 0,
                ShowGhost = true,
                GhostAlpha = 0.5f,
                GhostColor = new float4(0.3f, 0.8f, 1f, 0.5f), // Cyan ghost
                TimeOffset = 0f,
                ShowTrail = true
            });

            // Redline stats
            ecb.AddComponent(entity, new RedlineStats
            {
                PeakSpeed = 0f,
                CurrentSpeed = 0f,
                TimeTo100 = 0f,
                TimeTo150 = 0f,
                TimeTo200 = 0f,
                TotalDistance = 0f,
                Reached100 = false,
                Reached150 = false,
                Reached200 = false
            });

            // Display state
            ecb.AddComponent(entity, new ModeDisplayState
            {
                ModeName = "NIGHTFLOW",
                ModeDescription = "Standard driving experience",
                ShowSpeedHUD = false,
                ShowGhostComparison = false,
                ShowRelaxedHUD = false,
                GhostTimeDelta = 0f,
                GhostDistanceDelta = 0f
            });

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private uint GenerateRandomSeed()
        {
            // Use time-based seed generation
            return (uint)((Unity.Mathematics.Random.CreateFromIndex(12345).NextUInt() ^
                          (uint)(SystemAPI.Time.ElapsedTime * 1000)) | 1);
        }

        [BurstCompile]
        private void UpdateRedlineStats(ref SystemState state, ref GameModeState modeState,
            ref RedlineStats stats, float deltaTime, float elapsedTime)
        {
            // Get player speed
            float currentSpeed = 0f;
            foreach (var velocity in SystemAPI.Query<RefRO<Velocity>>().WithAll<PlayerVehicleTag>())
            {
                currentSpeed = velocity.ValueRO.Forward;
                break;
            }

            stats.CurrentSpeed = currentSpeed;
            stats.TotalDistance += currentSpeed * deltaTime;

            // Update peak speed
            if (currentSpeed > stats.PeakSpeed)
            {
                stats.PeakSpeed = currentSpeed;
                if (currentSpeed > modeState.RedlineMaxSpeed)
                {
                    modeState.RedlineMaxSpeed = currentSpeed;
                }
                if (currentSpeed > modeState.RedlinePersonalBest)
                {
                    modeState.RedlinePersonalBest = currentSpeed;
                }
            }

            // Track time above 100 m/s
            if (currentSpeed > 100f)
            {
                modeState.RedlineExtreme += deltaTime;
            }

            // Milestone tracking
            if (!stats.Reached100 && currentSpeed >= 100f)
            {
                stats.Reached100 = true;
                stats.TimeTo100 = elapsedTime;
            }
            if (!stats.Reached150 && currentSpeed >= 150f)
            {
                stats.Reached150 = true;
                stats.TimeTo150 = elapsedTime;
            }
            if (!stats.Reached200 && currentSpeed >= 200f)
            {
                stats.Reached200 = true;
                stats.TimeTo200 = elapsedTime;
            }
        }

        [BurstCompile]
        private void UpdateDisplayState(GameModeState modeState, ref ModeDisplayState displayState)
        {
            switch (modeState.CurrentMode)
            {
                case GameMode.Nightflow:
                    displayState.ModeName = "NIGHTFLOW";
                    displayState.ModeDescription = "Standard driving experience";
                    displayState.ShowSpeedHUD = false;
                    displayState.ShowGhostComparison = false;
                    displayState.ShowRelaxedHUD = false;
                    break;

                case GameMode.Redline:
                    displayState.ModeName = "REDLINE";
                    displayState.ModeDescription = "No limits. Push your speed.";
                    displayState.ShowSpeedHUD = true;
                    displayState.ShowGhostComparison = false;
                    displayState.ShowRelaxedHUD = false;
                    break;

                case GameMode.Ghost:
                    displayState.ModeName = "GHOST";
                    displayState.ModeDescription = "Race your past self";
                    displayState.ShowSpeedHUD = false;
                    displayState.ShowGhostComparison = true;
                    displayState.ShowRelaxedHUD = false;
                    break;

                case GameMode.Freeflow:
                    displayState.ModeName = "FREEFLOW";
                    displayState.ModeDescription = "No hazards. Just drive.";
                    displayState.ShowSpeedHUD = false;
                    displayState.ShowGhostComparison = false;
                    displayState.ShowRelaxedHUD = true;
                    break;
            }
        }

        /// <summary>
        /// Sets the current game mode. Call from UI or input handler.
        /// </summary>
        public static void SetGameMode(ref GameModeState modeState, GameMode newMode)
        {
            modeState.CurrentMode = newMode;
            modeState.ModeSelectActive = false;
        }

        /// <summary>
        /// Sets the seed for ghost mode playback.
        /// </summary>
        public static void SetGhostSeed(ref GhostModeConfig config, uint seed)
        {
            config.RunSeed = seed;
        }

        /// <summary>
        /// Resets Redline stats for a new run.
        /// </summary>
        public static void ResetRedlineStats(ref RedlineStats stats)
        {
            stats.PeakSpeed = 0f;
            stats.CurrentSpeed = 0f;
            stats.TimeTo100 = 0f;
            stats.TimeTo150 = 0f;
            stats.TimeTo200 = 0f;
            stats.TotalDistance = 0f;
            stats.Reached100 = false;
            stats.Reached150 = false;
            stats.Reached200 = false;
        }

        /// <summary>
        /// Checks if hazards should be spawned for the current mode.
        /// </summary>
        public static bool ShouldSpawnHazards(GameMode mode)
        {
            return mode != GameMode.Freeflow;
        }

        /// <summary>
        /// Gets the speed limit for the current mode.
        /// Returns float.MaxValue for Redline (no limit).
        /// </summary>
        public static float GetSpeedLimit(GameMode mode)
        {
            return mode == GameMode.Redline ? float.MaxValue : 80f; // 80 m/s default
        }

        /// <summary>
        /// Calculates acceleration modifier for Redline mode.
        /// Acceleration decreases as speed increases (asymptotic approach).
        /// Formula: modifier = 1 / (1 + speed/referenceSpeed)
        /// </summary>
        public static float GetRedlineAccelerationModifier(float currentSpeed)
        {
            const float ReferenceSpeed = 50f; // Speed at which acceleration is halved
            return 1f / (1f + currentSpeed / ReferenceSpeed);
        }
    }
}
