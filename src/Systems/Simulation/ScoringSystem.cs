// ============================================================================
// Nightflow - Scoring System
// Execution Order: 12 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Calculates and accumulates score based on distance, speed tier, and risk.
    /// Score = Distance × Speed_Tier × (1 + RiskMultiplier)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CrashSystem))]
    public partial struct ScoringSystem : ISystem
    {
        // Speed tier thresholds (m/s)
        private const float FastThreshold = 30f;
        private const float BoostedThreshold = 50f;

        // Speed tier multipliers
        private const float CruiseMultiplier = 1.0f;
        private const float FastMultiplier = 1.5f;
        private const float BoostedMultiplier = 2.5f;

        // Risk parameters
        private const float RiskDecay = 0.8f;           // per second
        private const float BrakePenalty = 0.5f;        // 50% reduction
        private const float BrakeCooldown = 2.0f;       // seconds

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (scoreSession, riskState, velocity, input, speedTier, summary) in
                SystemAPI.Query<RefRW<ScoreSession>, RefRW<RiskState>,
                               RefRO<Velocity>, RefRO<PlayerInput>, RefRW<SpeedTier>,
                               RefRW<ScoreSummary>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                if (!scoreSession.ValueRO.Active)
                    continue;

                // =============================================================
                // Check for Brake Penalty
                // =============================================================

                if (input.ValueRO.Brake > 0.1f)
                {
                    if (!riskState.ValueRO.BrakePenaltyActive)
                    {
                        // Apply brake penalty
                        riskState.ValueRW.Value *= BrakePenalty;
                        riskState.ValueRW.BrakePenaltyActive = true;
                        riskState.ValueRW.BrakeCooldown = BrakeCooldown;
                    }
                }

                // Update brake cooldown
                if (riskState.ValueRO.BrakePenaltyActive)
                {
                    riskState.ValueRW.BrakeCooldown -= deltaTime;
                    if (riskState.ValueRO.BrakeCooldown <= 0)
                    {
                        riskState.ValueRW.BrakePenaltyActive = false;
                    }
                }

                // =============================================================
                // Update Speed Tier
                // =============================================================

                float speed = velocity.ValueRO.Forward;

                if (speed >= BoostedThreshold)
                {
                    speedTier.ValueRW.Tier = 2;
                    speedTier.ValueRW.Multiplier = BoostedMultiplier;
                }
                else if (speed >= FastThreshold)
                {
                    speedTier.ValueRW.Tier = 1;
                    speedTier.ValueRW.Multiplier = FastMultiplier;
                }
                else
                {
                    speedTier.ValueRW.Tier = 0;
                    speedTier.ValueRW.Multiplier = CruiseMultiplier;
                }

                // =============================================================
                // Decay Risk Multiplier
                // =============================================================

                if (!riskState.ValueRO.BrakePenaltyActive)
                {
                    riskState.ValueRW.Value -= RiskDecay * deltaTime;
                    riskState.ValueRW.Value = math.max(0f, riskState.ValueRO.Value);
                }

                // Clamp to cap (reduced by damage)
                riskState.ValueRW.Value = math.min(riskState.ValueRO.Value, riskState.ValueRO.Cap);

                // =============================================================
                // Accumulate Distance and Track Stats
                // =============================================================

                float distanceThisFrame = speed * deltaTime;
                scoreSession.ValueRW.Distance += distanceThisFrame;

                // Track highest speed
                if (speed > summary.ValueRO.HighestSpeed)
                {
                    summary.ValueRW.HighestSpeed = speed;
                }

                // Track time survived
                summary.ValueRW.TimeSurvived += deltaTime;
                summary.ValueRW.TotalDistance = scoreSession.ValueRO.Distance;

                // =============================================================
                // Calculate Score
                // =============================================================

                // Score = Distance × Speed_Tier × (1 + RiskMultiplier)
                float tierMultiplier = speedTier.ValueRO.Multiplier;
                float riskMultiplier = riskState.ValueRO.Value;

                // Clamp multiplier inputs to prevent manipulation
                tierMultiplier = math.clamp(tierMultiplier, 0f, BoostedMultiplier);
                riskMultiplier = math.clamp(riskMultiplier, 0f, riskState.ValueRO.Cap);

                float totalMultiplier = tierMultiplier * (1f + riskMultiplier);

                float scoreThisFrame = distanceThisFrame * totalMultiplier;

                // Cap per-frame score to prevent exploits
                // At max speed (80 m/s), max tier (2.5×), max risk (2.0):
                // 80 * 0.016 * 2.5 * 3.0 ≈ 9.6 per frame. 50 gives generous headroom.
                const float MaxScorePerFrame = 50f;
                scoreThisFrame = math.min(scoreThisFrame, MaxScorePerFrame);

                scoreSession.ValueRW.Score += scoreThisFrame;

                // Hard cap on total score
                const float MaxTotalScore = 999_999_999f;
                scoreSession.ValueRW.Score = math.min(scoreSession.ValueRO.Score, MaxTotalScore);

                // Update multiplier display value
                scoreSession.ValueRW.Multiplier = tierMultiplier;
                scoreSession.ValueRW.RiskMultiplier = riskMultiplier;

                // Track highest multiplier
                if (totalMultiplier > scoreSession.ValueRO.HighestMultiplier)
                {
                    scoreSession.ValueRW.HighestMultiplier = totalMultiplier;
                }
            }
        }
    }
}
