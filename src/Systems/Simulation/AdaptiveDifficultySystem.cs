// ============================================================================
// Nightflow - Adaptive Difficulty System
// Execution Order: 15 (Simulation Group, after Scoring)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages adaptive difficulty based on player performance.
    ///
    /// Key Metrics Tracked:
    /// - Average multiplier (sustained skill indicator)
    /// - Survival time per run
    /// - Crash frequency
    /// - Hazard avoidance rate
    ///
    /// Difficulty Adjustment:
    /// - Struggling players (frequent crashes, low multiplier): reduce difficulty
    /// - Skilled players (high multiplier, long survival): increase difficulty
    /// - Smooth interpolation prevents jarring changes
    /// - Cooldown prevents rapid oscillation
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ScoringSystem))]
    public partial struct AdaptiveDifficultySystem : ISystem
    {
        // Difficulty adjustment parameters
        private const float MinDifficulty = 0.5f;           // Minimum difficulty (50% of normal)
        private const float MaxDifficulty = 2.0f;           // Maximum difficulty (200% of normal)
        private const float DifficultyLerpRate = 0.1f;      // How fast difficulty adjusts per second
        private const float AdjustmentCooldown = 5f;        // Seconds between adjustments

        // Performance thresholds
        private const float HighMultiplierThreshold = 3.0f; // Considered "high performance"
        private const float LowMultiplierThreshold = 1.5f;  // Considered "struggling"
        private const float ShortRunThreshold = 30f;        // Seconds - considered early crash
        private const float LongRunThreshold = 120f;        // Seconds - considered good run

        // Streak thresholds for adjustment
        private const int StreakThreshold = 3;              // Consecutive runs to trigger adjustment

        // Rolling average weight (for exponential moving average)
        private const float AverageWeight = 0.3f;           // New data contribution

        // Warm-up period
        private const float WarmUpTime = 60f;               // Seconds before difficulty starts adjusting
        private const int WarmUpRuns = 2;                   // Minimum runs before adjusting

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // =============================================================
            // Get or Create Difficulty Profile (Singleton-like)
            // =============================================================

            bool foundProfile = false;

            foreach (var profile in SystemAPI.Query<RefRW<DifficultyProfile>>())
            {
                foundProfile = true;
                UpdateDifficultyProfile(ref profile.ValueRW, ref state, deltaTime);
                break;
            }

            // If no profile exists and player is active, we'll rely on initialization
            // The profile should be created by GameBootstrap or on first run
        }

        private void UpdateDifficultyProfile(
            ref DifficultyProfile profile,
            ref SystemState state,
            float deltaTime)
        {
            // =============================================================
            // Track Session Time
            // =============================================================

            profile.SessionPlayTime += deltaTime;

            // =============================================================
            // Update Cooldown
            // =============================================================

            if (profile.AdjustmentCooldown > 0)
            {
                profile.AdjustmentCooldown -= deltaTime;
            }

            // =============================================================
            // Gather Current Run Performance
            // =============================================================

            float currentMultiplier = 1f;
            float currentDistance = 0f;
            bool playerActive = false;
            bool playerCrashed = false;
            float survivalTime = 0f;

            // Check for active player
            foreach (var (scoreSession, velocity) in
                SystemAPI.Query<RefRO<ScoreSession>, RefRO<Velocity>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                currentMultiplier = scoreSession.ValueRO.Multiplier * scoreSession.ValueRO.RiskMultiplier;
                currentDistance = scoreSession.ValueRO.Distance;
                playerActive = scoreSession.ValueRO.Active;
                break;
            }

            // Check for crashed player (run just ended)
            foreach (var (scoreSession, scoreSummary) in
                SystemAPI.Query<RefRO<ScoreSession>, RefRO<ScoreSummary>>()
                    .WithAll<PlayerVehicleTag, CrashedTag>())
            {
                playerCrashed = true;
                survivalTime = scoreSummary.ValueRO.TimeSurvived;
                currentDistance = scoreSummary.ValueRO.TotalDistance;
                break;
            }

            // =============================================================
            // Update Rolling Averages During Active Play
            // =============================================================

            if (playerActive && currentMultiplier > 0)
            {
                // Exponential moving average for multiplier
                profile.AverageMultiplier = math.lerp(
                    profile.AverageMultiplier,
                    currentMultiplier,
                    AverageWeight * deltaTime
                );

                // Track best distance
                if (currentDistance > profile.SessionBestDistance)
                {
                    profile.SessionBestDistance = currentDistance;
                }
            }

            // =============================================================
            // Handle Run Completion (Crash Detection)
            // =============================================================

            // We detect a "new crash" by checking if player just crashed
            // This would normally be detected by a state change, but for simplicity
            // we update based on current state

            // =============================================================
            // Calculate Skill Rating
            // =============================================================

            // Skill is composite of:
            // - Average multiplier (40%)
            // - Survival time (30%)
            // - Hazard avoidance (20%)
            // - Streak bonuses (10%)

            float multiplierSkill = math.saturate(profile.AverageMultiplier / 5f); // 5x = max skill
            float survivalSkill = math.saturate(profile.AverageSurvivalTime / 180f); // 3min = max skill
            float avoidanceSkill = profile.HazardAvoidanceRate;
            float streakBonus = profile.HighPerformanceStreak > 0
                ? math.min(profile.HighPerformanceStreak * 0.05f, 0.1f)
                : -math.min(profile.StrugglingStreak * 0.05f, 0.1f);

            profile.SkillRating = math.saturate(
                multiplierSkill * 0.4f +
                survivalSkill * 0.3f +
                avoidanceSkill * 0.2f +
                0.5f + streakBonus // Base 0.5 with streak modifier
            );

            // =============================================================
            // Calculate Target Difficulty
            // =============================================================

            // Skip adjustment during warm-up period
            if (profile.SessionPlayTime < WarmUpTime || profile.RunsCompleted < WarmUpRuns)
            {
                profile.TargetDifficulty = 1.0f;
            }
            else
            {
                // Base target on skill rating
                // Skill 0.0 -> Difficulty 0.6
                // Skill 0.5 -> Difficulty 1.0
                // Skill 1.0 -> Difficulty 1.5
                float baseDifficulty = math.lerp(0.6f, 1.5f, profile.SkillRating);

                // Apply streak modifiers
                if (profile.StrugglingStreak >= StreakThreshold)
                {
                    // Player is really struggling, ease off more
                    baseDifficulty *= 0.8f;
                }
                else if (profile.HighPerformanceStreak >= StreakThreshold)
                {
                    // Player is dominating, increase challenge
                    baseDifficulty *= 1.2f;
                }

                // Clamp to valid range
                profile.TargetDifficulty = math.clamp(baseDifficulty, MinDifficulty, MaxDifficulty);
            }

            // =============================================================
            // Smooth Difficulty Transition
            // =============================================================

            if (profile.AdjustmentCooldown <= 0)
            {
                // Lerp current difficulty toward target
                float lerpAmount = DifficultyLerpRate * deltaTime;
                profile.DifficultyModifier = math.lerp(
                    profile.DifficultyModifier,
                    profile.TargetDifficulty,
                    lerpAmount
                );

                // Clamp to ensure valid range
                profile.DifficultyModifier = math.clamp(
                    profile.DifficultyModifier,
                    MinDifficulty,
                    MaxDifficulty
                );
            }
        }

        /// <summary>
        /// Called when a run ends to update performance metrics.
        /// Should be called by CrashSystem or game flow manager.
        /// </summary>
        public static void OnRunCompleted(
            ref DifficultyProfile profile,
            float finalMultiplier,
            float survivalTime,
            float distance,
            int hazardsDodged,
            int hazardsHit)
        {
            profile.RunsCompleted++;

            // Update rolling average for survival time
            profile.AverageSurvivalTime = math.lerp(
                profile.AverageSurvivalTime,
                survivalTime,
                AverageWeight
            );

            // Update hazard avoidance rate
            int totalHazards = hazardsDodged + hazardsHit;
            if (totalHazards > 0)
            {
                float runAvoidance = (float)hazardsDodged / totalHazards;
                profile.HazardAvoidanceRate = math.lerp(
                    profile.HazardAvoidanceRate,
                    runAvoidance,
                    AverageWeight
                );
            }

            // Update streaks based on performance
            bool wasHighPerformance = finalMultiplier >= HighMultiplierThreshold &&
                                      survivalTime >= LongRunThreshold;
            bool wasStruggling = finalMultiplier < LowMultiplierThreshold ||
                                 survivalTime < ShortRunThreshold;

            if (wasHighPerformance)
            {
                profile.HighPerformanceStreak++;
                profile.StrugglingStreak = 0;
            }
            else if (wasStruggling)
            {
                profile.StrugglingStreak++;
                profile.HighPerformanceStreak = 0;
            }
            else
            {
                // Average performance - decay both streaks
                profile.HighPerformanceStreak = math.max(0, profile.HighPerformanceStreak - 1);
                profile.StrugglingStreak = math.max(0, profile.StrugglingStreak - 1);
            }

            // Set cooldown after run completion
            profile.AdjustmentCooldown = AdjustmentCooldown;
        }
    }
}
