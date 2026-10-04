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
    ///
    /// Runs are folded into the profile by CrashSystem when a player-driven run
    /// ends (AdaptiveDifficultyLogic.CompleteRun); the rules live in
    /// AdaptiveDifficultyLogic so they can be unit tested.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ScoringSystem))]
    public partial struct AdaptiveDifficultySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var profile in SystemAPI.Query<RefRW<DifficultyProfile>>())
            {
                UpdateDifficultyProfile(ref profile.ValueRW, ref state, deltaTime);
                break;
            }

            // The profile is created by GameBootstrapSystem
        }

        private void UpdateDifficultyProfile(
            ref DifficultyProfile profile,
            ref SystemState state,
            float deltaTime)
        {
            profile.SessionPlayTime += deltaTime;

            if (profile.AdjustmentCooldown > 0)
            {
                profile.AdjustmentCooldown -= deltaTime;
            }

            // =============================================================
            // Rolling multiplier average while the player is driving a run
            // =============================================================

            foreach (var (scoreSession, autopilot) in
                SystemAPI.Query<RefRO<ScoreSession>, RefRO<Autopilot>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                // Autopilot (idle/menu) freezes the run, so it says nothing about skill
                if (scoreSession.ValueRO.Active && !autopilot.ValueRO.Enabled)
                {
                    // Score formula: tier * (1 + risk), so total multiplier must match
                    float currentMultiplier = scoreSession.ValueRO.Multiplier * (1f + scoreSession.ValueRO.RiskMultiplier);

                    if (currentMultiplier > 0)
                    {
                        profile.AverageMultiplier = math.lerp(
                            profile.AverageMultiplier,
                            currentMultiplier,
                            math.saturate(AdaptiveDifficultyLogic.AverageWeight * deltaTime));
                    }
                }
                break;
            }

            // =============================================================
            // Skill -> target -> smoothed modifier
            // =============================================================

            profile.SkillRating = AdaptiveDifficultyLogic.SkillRating(profile);
            profile.TargetDifficulty = AdaptiveDifficultyLogic.TargetDifficulty(profile);
            profile.DifficultyModifier = AdaptiveDifficultyLogic.StepModifier(
                profile.DifficultyModifier, profile.TargetDifficulty, profile.AdjustmentCooldown, deltaTime);
        }
    }
}
