// ============================================================================
// Nightflow - Save System Bridge
// ECS system that syncs with SaveManager for loading/saving game state
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Nightflow.Components;
using Nightflow.Buffers;

namespace Nightflow.Save
{
    /// <summary>
    /// ECS system that bridges between SaveManager and ECS components.
    /// Handles syncing leaderboard data and triggering saves on game events.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Nightflow.Systems.GameBootstrapSystem))]
    public partial class SaveSystemBridge : SystemBase
    {
        private bool initialized;
        private bool leaderboardSynced;

        protected override void OnCreate()
        {
            initialized = false;
            leaderboardSynced = false;
        }

        protected override void OnUpdate()
        {
            if (SaveManager.Instance == null)
                return;

            if (!initialized)
            {
                Initialize();
                initialized = true;
            }

            // Sync leaderboard on first frame after init
            if (!leaderboardSynced)
            {
                SyncLeaderboardToECS();
                leaderboardSynced = true;
            }
        }

        private void Initialize()
        {
            // Subscribe to save manager events
            SaveManager.Instance.OnNewHighScore += OnNewHighScore;
        }

        protected override void OnDestroy()
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.OnNewHighScore -= OnNewHighScore;
            }
        }

        /// <summary>
        /// Sync high scores from SaveManager to ECS LeaderboardEntry buffer.
        /// </summary>
        private void SyncLeaderboardToECS()
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return;

            // Find or create leaderboard entity
            Entity leaderboardEntity = Entity.Null;

            foreach (var (state, entity) in SystemAPI.Query<RefRO<LeaderboardState>>().WithEntityAccess())
            {
                leaderboardEntity = entity;
                break;
            }

            if (leaderboardEntity == Entity.Null)
            {
                // Create leaderboard entity if it doesn't exist
                leaderboardEntity = EntityManager.CreateEntity();
                EntityManager.AddComponent<LeaderboardState>(leaderboardEntity);
                EntityManager.AddComponent<LeaderboardTag>(leaderboardEntity);
                EntityManager.AddBuffer<LeaderboardEntry>(leaderboardEntity);
            }

            // Get the buffer
            if (!EntityManager.HasBuffer<LeaderboardEntry>(leaderboardEntity))
            {
                EntityManager.AddBuffer<LeaderboardEntry>(leaderboardEntity);
            }

            var buffer = EntityManager.GetBuffer<LeaderboardEntry>(leaderboardEntity);
            buffer.Clear();

            // Load all scores
            var allScores = saveManager.GetHighScores(null);
            int rank = 1;

            foreach (var score in allScores)
            {
                buffer.Add(new LeaderboardEntry
                {
                    Rank = rank++,
                    Initials = new FixedString32Bytes(score.Initials),
                    Score = score.Score,
                    MaxSpeed = score.MaxSpeed,
                    Distance = score.Distance / 1000f, // Convert to km
                    Mode = ConvertGameMode(score.Mode),
                    Timestamp = score.Timestamp,
                    IsCurrentPlayer = false
                });
            }

            // Update leaderboard state
            var stateRW = SystemAPI.GetComponentRW<LeaderboardState>(leaderboardEntity);
            stateRW.ValueRW.EntryCount = buffer.Length;
        }

        /// <summary>
        /// Convert save GameMode to ECS GameMode.
        /// </summary>
        private Components.GameMode ConvertGameMode(Save.GameMode mode)
        {
            return mode switch
            {
                Save.GameMode.Nightflow => Components.GameMode.Nightflow,
                Save.GameMode.Redline => Components.GameMode.Redline,
                Save.GameMode.Ghost => Components.GameMode.Ghost,
                Save.GameMode.Freeflow => Components.GameMode.Freeflow,
                _ => Components.GameMode.Nightflow
            };
        }

        /// <summary>
        /// Called when a new high score is added.
        /// </summary>
        private void OnNewHighScore(HighScoreEntry entry)
        {
            // Re-sync leaderboard
            SyncLeaderboardToECS();
        }

        /// <summary>
        /// Save current run as a high score.
        /// Call from crash/game over system.
        /// </summary>
        public static void SaveRunAsHighScore(
            string initials,
            int score,
            float maxSpeed,
            float distance,
            float timeSurvived,
            int closePasses,
            int hazardsDodged,
            Components.GameMode mode,
            uint seed)
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return;

            var entry = new HighScoreEntry
            {
                Initials = initials,
                Score = score,
                MaxSpeed = maxSpeed,
                Distance = distance,
                TimeSurvived = timeSurvived,
                ClosePasses = closePasses,
                HazardsDodged = hazardsDodged,
                Mode = ConvertToSaveMode(mode),
                Seed = seed
            };

            saveManager.AddHighScore(entry);
        }

        /// <summary>
        /// Save ghost recording from player input buffer.
        /// </summary>
        public static void SaveGhostRecording(
            EntityManager entityManager,
            Entity playerEntity,
            uint seed,
            Components.GameMode mode,
            int score,
            Vector3 startPos,
            Quaternion startRot)
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return;

            var ghost = saveManager.CreateGhostFromInputBuffer(
                playerEntity,
                seed,
                ConvertToSaveMode(mode),
                score,
                startPos,
                startRot
            );

            if (ghost != null && ghost.Inputs.Count > 0)
            {
                saveManager.SaveGhostRecording(ghost);
            }
        }

        /// <summary>
        /// Check if current score qualifies as high score.
        /// </summary>
        public static bool IsHighScore(int score, Components.GameMode mode)
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return false;

            return saveManager.IsHighScore(score, ConvertToSaveMode(mode));
        }

        /// <summary>
        /// Get potential rank for a score.
        /// </summary>
        public static int GetPotentialRank(int score, Components.GameMode mode)
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return 0;

            return saveManager.GetPotentialRank(score, ConvertToSaveMode(mode));
        }

        /// <summary>
        /// Convert ECS GameMode to save GameMode.
        /// </summary>
        private static Save.GameMode ConvertToSaveMode(Components.GameMode mode)
        {
            return mode switch
            {
                Components.GameMode.Nightflow => Save.GameMode.Nightflow,
                Components.GameMode.Redline => Save.GameMode.Redline,
                Components.GameMode.Ghost => Save.GameMode.Ghost,
                Components.GameMode.Freeflow => Save.GameMode.Freeflow,
                _ => Save.GameMode.Nightflow
            };
        }

        /// <summary>
        /// Load a ghost recording into an ECS entity.
        /// </summary>
        public static bool LoadGhostToEntity(
            EntityManager entityManager,
            Entity ghostEntity,
            string ghostId)
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return false;

            var ghost = saveManager.GetGhostRecording(ghostId);
            if (ghost == null) return false;

            // Ensure entity has InputLogEntry buffer
            if (!entityManager.HasBuffer<InputLogEntry>(ghostEntity))
            {
                entityManager.AddBuffer<InputLogEntry>(ghostEntity);
            }

            var buffer = entityManager.GetBuffer<InputLogEntry>(ghostEntity);
            buffer.Clear();

            // Copy inputs
            foreach (var input in ghost.Inputs)
            {
                buffer.Add(new InputLogEntry
                {
                    Timestamp = input.Timestamp,
                    Steer = input.Steer,
                    Throttle = input.Throttle,
                    Brake = input.Brake,
                    Handbrake = input.Handbrake
                });
            }

            // Set replay state
            if (entityManager.HasComponent<ReplayState>(ghostEntity))
            {
                var replayState = entityManager.GetComponentData<ReplayState>(ghostEntity);
                replayState.GlobalSeed = ghost.Seed;
                replayState.TotalDuration = ghost.Duration;
                replayState.StartPosition = ghost.StartPosition.ToVector3().ToFloat3();
                replayState.StartRotation = ghost.StartRotation.ToQuaternion().ToQuaternion();
                replayState.CurrentInputIndex = 0;
                replayState.PlaybackTime = 0f;
                replayState.IsPlaying = false;
                replayState.IsComplete = false;
                entityManager.SetComponentData(ghostEntity, replayState);
            }

            return true;
        }

        /// <summary>
        /// Update audio settings from saved preferences.
        /// </summary>
        public static void ApplySavedAudioSettings()
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null) return;

            // SaveManager applies settings automatically on load
        }

        /// <summary>
        /// Mark save data as dirty for auto-save.
        /// </summary>
        public static void MarkDirty()
        {
            SaveManager.Instance?.MarkDirty();
        }

        /// <summary>
        /// Force immediate save.
        /// </summary>
        public static void ForceSave()
        {
            SaveManager.Instance?.ForceSave();
        }
    }

    /// <summary>
    /// Extension methods for type conversion.
    /// </summary>
    public static class SaveTypeExtensions
    {
        public static float3 ToFloat3(this Vector3 v) => new float3(v.x, v.y, v.z);
        public static quaternion ToQuaternion(this Quaternion q) => new quaternion(q.x, q.y, q.z, q.w);
    }
}
