// ============================================================================
// Nightflow - Adaptive Difficulty Logic
// Pure, Burst-friendly cross-run skill tracking shared by
// AdaptiveDifficultySystem, CrashSystem and the EditMode tests.
//
// The in-run ramp (DifficultyCurve) makes every run build up the same way;
// this layer nudges the whole curve up or down between runs depending on how
// the player has been doing, so beginners get room to learn and strong
// players don't coast through the first minutes.
// ============================================================================

using Unity.Mathematics;
using Nightflow.Components;

namespace Nightflow.Systems
{
    public static class AdaptiveDifficultyLogic
    {
        // Difficulty range. Kept narrower than the in-run ramp on purpose: the
        // curve already climbs 1x -> 3.25x within a run, and this multiplies it
        public const float MinDifficulty = 0.6f;
        public const float MaxDifficulty = 1.5f;

        public const float DifficultyLerpRate = 0.1f;     // per second toward target
        public const float AdjustmentCooldown = 5f;       // seconds after a run ends

        // Performance thresholds
        public const float HighMultiplierThreshold = 3.0f;
        public const float LowMultiplierThreshold = 1.5f;
        public const float ShortRunThreshold = 30f;       // seconds
        public const float LongRunThreshold = 120f;       // seconds

        public const int StreakThreshold = 3;             // consecutive runs to trigger adjustment
        public const float AverageWeight = 0.3f;          // EMA weight of the newest run

        // Warm-up
        public const float WarmUpTime = 60f;              // seconds of session before adjusting
        public const int WarmUpRuns = 2;                  // runs before adjusting

        /// <summary>Runs shorter than this are treated as false starts and ignored.</summary>
        public const float MinCountedRunTime = 3f;

        /// <summary>
        /// Only real, player-driven runs feed the skill model: not crashes
        /// with no run in progress, and not instant false starts.
        /// </summary>
        public static bool ShouldCountRun(bool runWasActive, float timeSurvived)
        {
            return runWasActive && timeSurvived >= MinCountedRunTime;
        }

        /// <summary>
        /// Distance-weighted average multiplier for a run. Score accrues as
        /// distance × multiplier, so score / distance is the run's typical
        /// multiplier (a peak would reward one lucky burst).
        /// </summary>
        public static float RunAverageMultiplier(float score, float distance)
        {
            if (!(distance > 1f) || !(score > 0f))
                return 1f;

            return math.max(1f, score / distance);
        }

        /// <summary>
        /// Folds a finished run into the profile: rolling averages, streaks,
        /// run count and the post-run cooldown.
        /// </summary>
        public static void CompleteRun(
            ref DifficultyProfile profile,
            float averageMultiplier,
            float survivalTime,
            float distance,
            int hazardsDodged,
            int hazardsHit)
        {
            profile.RunsCompleted++;

            profile.AverageSurvivalTime = math.lerp(profile.AverageSurvivalTime, survivalTime, AverageWeight);

            // The in-run average multiplier is an EMA over play time; make sure
            // the run as a whole is reflected too
            profile.AverageMultiplier = math.lerp(profile.AverageMultiplier, averageMultiplier, AverageWeight);

            int totalHazards = hazardsDodged + hazardsHit;
            if (totalHazards > 0)
            {
                float runAvoidance = (float)hazardsDodged / totalHazards;
                profile.HazardAvoidanceRate = math.lerp(profile.HazardAvoidanceRate, runAvoidance, AverageWeight);
            }

            if (distance > profile.SessionBestDistance)
                profile.SessionBestDistance = distance;

            bool wasHighPerformance = averageMultiplier >= HighMultiplierThreshold &&
                                      survivalTime >= LongRunThreshold;
            bool wasStruggling = averageMultiplier < LowMultiplierThreshold ||
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

            profile.AdjustmentCooldown = AdjustmentCooldown;
        }

        /// <summary>Composite skill [0, 1] from multiplier, survival, avoidance and streaks.</summary>
        public static float SkillRating(in DifficultyProfile profile)
        {
            float multiplierSkill = math.saturate(profile.AverageMultiplier / 5f);   // 5x = max skill
            float survivalSkill = math.saturate(profile.AverageSurvivalTime / 180f); // 3 min = max skill
            float avoidanceSkill = math.saturate(profile.HazardAvoidanceRate);
            float streakBonus = profile.HighPerformanceStreak > 0
                ? math.min(profile.HighPerformanceStreak * 0.05f, 0.1f)
                : -math.min(profile.StrugglingStreak * 0.05f, 0.1f);

            return math.saturate(
                multiplierSkill * 0.4f +
                survivalSkill * 0.3f +
                avoidanceSkill * 0.3f +
                streakBonus);
        }

        /// <summary>Difficulty the modifier should drift toward.</summary>
        public static float TargetDifficulty(in DifficultyProfile profile)
        {
            if (profile.SessionPlayTime < WarmUpTime || profile.RunsCompleted < WarmUpRuns)
                return 1f;

            // Skill 0 -> 0.6, 0.5 -> ~1.05, 1 -> 1.5
            float target = math.lerp(MinDifficulty, MaxDifficulty, profile.SkillRating);

            if (profile.StrugglingStreak >= StreakThreshold)
                target *= 0.8f;
            else if (profile.HighPerformanceStreak >= StreakThreshold)
                target *= 1.2f;

            return math.clamp(target, MinDifficulty, MaxDifficulty);
        }

        /// <summary>Smoothly moves the modifier toward its target (held during cooldown).</summary>
        public static float StepModifier(float current, float target, float cooldown, float deltaTime)
        {
            if (cooldown > 0f)
                return current;

            float next = math.lerp(current, target, math.saturate(DifficultyLerpRate * deltaTime));
            return math.clamp(next, MinDifficulty, MaxDifficulty);
        }
    }
}
