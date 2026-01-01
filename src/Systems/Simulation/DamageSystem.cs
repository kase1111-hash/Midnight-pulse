// ============================================================================
// Nightflow - Soft-Body Damage Evaluation System
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
    /// Accumulates damage from collisions using soft-body physics for realistic
    /// damage propagation and settling.
    ///
    /// Phase 2 Enhancement: Damage is applied as impulses to the SoftBodyState,
    /// which uses spring-damper physics to interpolate zone damage values.
    /// This creates realistic "settling" of damage after impacts.
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

        // Phase 2: Soft-body damage impulse parameters
        private const float ImpactVelocityScale = 0.5f;   // How much impact speed affects deformation velocity
        private const float MinImpactForImpulse = 5f;     // Minimum impact speed to add velocity impulse

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // =============================================================
            // Phase 2: Process damage with soft-body physics integration
            // =============================================================

            foreach (var (damage, impulse, collision, laneFollower, riskState, softBody) in
                SystemAPI.Query<RefRW<DamageState>, RefRO<ImpulseData>,
                               RefRO<CollisionEvent>, RefRW<LaneFollower>, RefRW<RiskState>,
                               RefRW<SoftBodyState>>()
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

                float vImpact = collision.ValueRO.ImpactSpeed;

                // E_d = k_d × v_impact² × Severity
                float Ed = DamageScale * vImpact * vImpact * severity;

                // =============================================================
                // Calculate Directional Weights
                // =============================================================

                float3 normal = impulse.ValueRO.Direction;

                // Calculate directional weights based on impact normal
                // Front: positive Z, Rear: negative Z, Left: negative X, Right: positive X
                float wFront = math.max(0f, -normal.z);
                float wRear = math.max(0f, normal.z);
                float wLeft = math.max(0f, normal.x);
                float wRight = math.max(0f, -normal.x);

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
                    wFront = wRear = wLeft = wRight = 0.25f;
                }

                // =============================================================
                // Phase 2: Apply Damage via Soft-Body Physics
                // =============================================================

                float normalizedDamage = Ed / GameConstants.MaxDamage;

                // Update target deformation (what the soft-body is settling toward)
                float4 damageImpulse = new float4(
                    normalizedDamage * wFront,
                    normalizedDamage * wRear,
                    normalizedDamage * wLeft,
                    normalizedDamage * wRight
                );

                softBody.ValueRW.TargetDeformation += damageImpulse;
                softBody.ValueRW.TargetDeformation = math.saturate(softBody.ValueRO.TargetDeformation);

                // Add velocity impulse for realistic "punch" on high-speed impacts
                if (vImpact > MinImpactForImpulse)
                {
                    float velocityImpulse = vImpact * ImpactVelocityScale;
                    softBody.ValueRW.DeformationVelocity += damageImpulse * velocityImpulse;
                }

                // Update DamageState from soft-body current deformation
                // This makes damage values "settle" physically
                damage.ValueRW.Front = softBody.ValueRO.CurrentDeformation.x;
                damage.ValueRW.Rear = softBody.ValueRO.CurrentDeformation.y;
                damage.ValueRW.Left = softBody.ValueRO.CurrentDeformation.z;
                damage.ValueRW.Right = softBody.ValueRO.CurrentDeformation.w;
                damage.ValueRW.Total += Ed;

                // =============================================================
                // Apply Handling Degradation
                // =============================================================

                // Magnetism reduction from side damage
                float sideDamage = (damage.ValueRO.Left + damage.ValueRO.Right) * 0.5f;
                float magnetismReduction = SideMagnetismPenalty * sideDamage * normalizedDamage;
                laneFollower.ValueRW.MagnetStrength -= magnetismReduction;
                laneFollower.ValueRW.MagnetStrength = math.max(laneFollower.ValueRO.MagnetStrength, 2f);

                // =============================================================
                // Reduce Risk Cap and Rebuild Rate Based on Damage
                // =============================================================

                float damageRatio = damage.ValueRO.Total / GameConstants.MaxDamage;
                float riskCapReduction = damageRatio * RiskCapDamageMultiplier;
                riskState.ValueRW.Cap = GameConstants.BaseRiskCap * (1f - riskCapReduction);
                riskState.ValueRW.Cap = math.max(riskState.ValueRO.Cap, GameConstants.MinRiskCap);

                riskState.ValueRW.RebuildRate = 1f - (damageRatio * 0.5f);
                riskState.ValueRW.RebuildRate = math.max(riskState.ValueRO.RebuildRate, GameConstants.MinRebuildRate);

                if (riskState.ValueRO.Value > riskState.ValueRO.Cap)
                {
                    riskState.ValueRW.Value = riskState.ValueRO.Cap;
                }
            }

            // =============================================================
            // Fallback: Handle vehicles without SoftBodyState
            // =============================================================

            foreach (var (damage, impulse, collision, laneFollower, riskState) in
                SystemAPI.Query<RefRW<DamageState>, RefRO<ImpulseData>,
                               RefRO<CollisionEvent>, RefRW<LaneFollower>, RefRW<RiskState>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag, SoftBodyState>())
            {
                if (!collision.ValueRO.Occurred || impulse.ValueRO.Magnitude < 0.1f)
                    continue;

                float severity = GameConstants.DefaultDamageSeverity;

                Entity hazardEntity = collision.ValueRO.OtherEntity;
                if (hazardEntity != Entity.Null && SystemAPI.HasComponent<Hazard>(hazardEntity))
                {
                    var hazard = SystemAPI.GetComponent<Hazard>(hazardEntity);
                    severity = hazard.Severity;
                }

                float vImpact = collision.ValueRO.ImpactSpeed;
                float Ed = DamageScale * vImpact * vImpact * severity;

                float3 normal = impulse.ValueRO.Direction;
                float wFront = math.max(0f, -normal.z);
                float wRear = math.max(0f, normal.z);
                float wLeft = math.max(0f, normal.x);
                float wRight = math.max(0f, -normal.x);

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
                    wFront = wRear = wLeft = wRight = 0.25f;
                }

                float normalizedDamage = Ed / GameConstants.MaxDamage;
                damage.ValueRW.Front += normalizedDamage * wFront;
                damage.ValueRW.Rear += normalizedDamage * wRear;
                damage.ValueRW.Left += normalizedDamage * wLeft;
                damage.ValueRW.Right += normalizedDamage * wRight;
                damage.ValueRW.Total += Ed;

                damage.ValueRW.Front = math.saturate(damage.ValueRW.Front);
                damage.ValueRW.Rear = math.saturate(damage.ValueRW.Rear);
                damage.ValueRW.Left = math.saturate(damage.ValueRW.Left);
                damage.ValueRW.Right = math.saturate(damage.ValueRW.Right);

                float sideDamage = (damage.ValueRO.Left + damage.ValueRO.Right) * 0.5f;
                float magnetismReduction = SideMagnetismPenalty * sideDamage * normalizedDamage;
                laneFollower.ValueRW.MagnetStrength -= magnetismReduction;
                laneFollower.ValueRW.MagnetStrength = math.max(laneFollower.ValueRO.MagnetStrength, 2f);

                float damageRatio = damage.ValueRO.Total / GameConstants.MaxDamage;
                float riskCapReduction = damageRatio * RiskCapDamageMultiplier;
                riskState.ValueRW.Cap = GameConstants.BaseRiskCap * (1f - riskCapReduction);
                riskState.ValueRW.Cap = math.max(riskState.ValueRO.Cap, GameConstants.MinRiskCap);

                riskState.ValueRW.RebuildRate = 1f - (damageRatio * 0.5f);
                riskState.ValueRW.RebuildRate = math.max(riskState.ValueRO.RebuildRate, GameConstants.MinRebuildRate);

                if (riskState.ValueRO.Value > riskState.ValueRO.Cap)
                {
                    riskState.ValueRW.Value = riskState.ValueRO.Cap;
                }
            }
        }
    }
}
