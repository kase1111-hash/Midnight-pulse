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

            foreach (var (scoreSession, riskState, velocity, input, speedTier) in
                SystemAPI.Query<RefRW<ScoreSession>, RefRW<RiskState>,
                               RefRO<Velocity>, RefRO<PlayerInput>, RefRW<SpeedTier>>()
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
                // Accumulate Distance
                // =============================================================

                float distanceThisFrame = speed * deltaTime;
                scoreSession.ValueRW.Distance += distanceThisFrame;

                // =============================================================
                // Calculate Score
                // =============================================================

                // Score = Distance × Speed_Tier × (1 + RiskMultiplier)
                float tierMultiplier = speedTier.ValueRO.Multiplier;
                float riskMultiplier = riskState.ValueRO.Value;

                float scoreThisFrame = distanceThisFrame * tierMultiplier * (1f + riskMultiplier);
                scoreSession.ValueRW.Score += scoreThisFrame;

                // Update multiplier display value
                scoreSession.ValueRW.Multiplier = tierMultiplier;
                scoreSession.ValueRW.RiskMultiplier = riskMultiplier;

                // Track highest multiplier
                float totalMultiplier = tierMultiplier * (1f + riskMultiplier);
                if (totalMultiplier > scoreSession.ValueRO.HighestMultiplier)
                {
                    scoreSession.ValueRW.HighestMultiplier = totalMultiplier;
                }
            }
        }
    }
}
