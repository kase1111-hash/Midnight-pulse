// ============================================================================
// Nightflow - Damage Evaluation System
// Execution Order: 7 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Config;

namespace Nightflow.Systems
{
    /// <summary>
    /// Accumulates damage from collisions and applies handling degradation.
    ///
    /// From spec:
    /// E_d = k_d × v_impact² × Severity
    ///
    /// Directional damage distribution weighted by contact normal.
    ///
    /// Handling penalties:
    /// - Steering Response: k_steer × (1 - 0.4 × D_front)
    /// - Magnetism: ω × (1 - 0.5 × D_side)
    /// - Drift Stability: k_slip × (1 + 0.6 × D_rear)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ImpulseSystem))]
    [UpdateBefore(typeof(CrashSystem))]
    public partial struct DamageSystem : ISystem
    {
        // Damage parameters (from spec)
        private const float DamageScale = 0.04f;        // k_d
        // MaxDamage uses GameConstants.MaxDamage

        // Handling degradation coefficients
        private const float FrontSteeringPenalty = 0.4f;
        private const float SideMagnetismPenalty = 0.5f;
        private const float RearSlipPenalty = 0.6f;

        // Risk cap degradation - uses GameConstants.BaseRiskCap
        private const float RiskCapDamageMultiplier = 0.7f; // Cap reduced by 70% of damage ratio

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (damage, impulse, collision, laneFollower, riskState) in
                SystemAPI.Query<RefRW<DamageState>, RefRO<ImpulseData>,
                               RefRO<CollisionEvent>, RefRW<LaneFollower>, RefRW<RiskState>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                if (!collision.ValueRO.Occurred || impulse.ValueRO.Magnitude < 0.1f)
                    continue;

                // =============================================================
                // Get Hazard Severity from Entity
                // =============================================================

                float severity = GameConstants.DefaultDamageSeverity;

                Entity hazardEntity = collision.ValueRO.OtherEntity;
                if (hazardEntity != Entity.Null && SystemAPI.HasComponent<Hazard>(hazardEntity))
                {
                    var hazard = SystemAPI.GetComponent<Hazard>(hazardEntity);
                    severity = hazard.Severity;
                }

                // =============================================================
                // Calculate Damage Energy
                // =============================================================

                // Reverse-engineer v_impact from impulse magnitude
                // J = k_i × v_impact × (0.5 + Severity)
                // So: v_impact ≈ J / (k_i × (0.5 + Severity))
                float vImpact = collision.ValueRO.ImpactSpeed;

                // E_d = k_d × v_impact² × Severity
                float Ed = DamageScale * vImpact * vImpact * severity;

                // =============================================================
                // Distribute to Damage Zones
                // =============================================================

                float3 normal = impulse.ValueRO.Direction;

                // Calculate directional weights based on impact normal
                // Front: positive Z, Rear: negative Z, Left: negative X, Right: positive X
                float wFront = math.max(0f, -normal.z); // Hit from front means normal points back
                float wRear = math.max(0f, normal.z);   // Hit from rear means normal points forward
                float wLeft = math.max(0f, normal.x);   // Hit from left means normal points right
                float wRight = math.max(0f, -normal.x); // Hit from right means normal points left

                // Normalize weights
                float totalWeight = wFront + wRear + wLeft + wRight;
                if (totalWeight > 0.01f)
                {
                    wFront /= totalWeight;
                    wRear /= totalWeight;
                    wLeft /= totalWeight;
                    wRight /= totalWeight;
                }
                else
                {
                    // Fallback: spread evenly
                    wFront = wRear = wLeft = wRight = 0.25f;
                }

                // Apply damage to zones (normalized to [0, 1])
                float normalizedDamage = Ed / GameConstants.MaxDamage;
                damage.ValueRW.Front += normalizedDamage * wFront;
                damage.ValueRW.Rear += normalizedDamage * wRear;
                damage.ValueRW.Left += normalizedDamage * wLeft;
                damage.ValueRW.Right += normalizedDamage * wRight;
                damage.ValueRW.Total += Ed;

                // Clamp zone damage to [0, 1]
                damage.ValueRW.Front = math.saturate(damage.ValueRW.Front);
                damage.ValueRW.Rear = math.saturate(damage.ValueRW.Rear);
                damage.ValueRW.Left = math.saturate(damage.ValueRW.Left);
                damage.ValueRW.Right = math.saturate(damage.ValueRW.Right);

                // =============================================================
                // Apply Handling Degradation
                // =============================================================

                // Steering degradation from front damage
                // Applied each frame in SteeringSystem as:
                // k_steer = k_steer × (1 - 0.4 × D_front)

                // Magnetism reduction from side damage (immediate effect)
                // ω = ω × (1 - 0.5 × D_side)
                float sideDamage = (damage.ValueRO.Left + damage.ValueRO.Right) * 0.5f;
                float magnetismReduction = SideMagnetismPenalty * sideDamage * normalizedDamage;
                laneFollower.ValueRW.MagnetStrength -= magnetismReduction;
                laneFollower.ValueRW.MagnetStrength = math.max(laneFollower.ValueRO.MagnetStrength, 2f);

                // Drift stability loss from rear damage
                // Applied in VehicleMovementSystem as:
                // k_slip = k_slip × (1 + 0.6 × D_rear)

                // =============================================================
                // Reduce Risk Cap and Rebuild Rate Based on Damage
                // =============================================================

                // Risk cap reduces as damage accumulates
                float damageRatio = damage.ValueRO.Total / GameConstants.MaxDamage;
                float riskCapReduction = damageRatio * RiskCapDamageMultiplier;
                riskState.ValueRW.Cap = GameConstants.BaseRiskCap * (1f - riskCapReduction);
                riskState.ValueRW.Cap = math.max(riskState.ValueRO.Cap, GameConstants.MinRiskCap);

                // Rebuild rate also degrades with damage (faster decay when damaged)
                riskState.ValueRW.RebuildRate = 1f - (damageRatio * 0.5f);
                riskState.ValueRW.RebuildRate = math.max(riskState.ValueRO.RebuildRate, GameConstants.MinRebuildRate);

                // Clamp current risk to new cap
                if (riskState.ValueRO.Value > riskState.ValueRO.Cap)
                {
                    riskState.ValueRW.Value = riskState.ValueRO.Cap;
                }
            }
        }
    }
}
