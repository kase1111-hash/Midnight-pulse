// ============================================================================
// Nightflow - Impulse Application System
// Execution Order: 6 (Simulation Group)
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
    /// Calculates and applies impulse responses from collisions.
    ///
    /// From spec:
    /// J = k_i × v_impact × (0.5 + Severity)
    /// I = J × N
    /// v_l ← v_l + I_l / m_virtual
    /// v_f ← max(v_f - |I_f| / m_virtual, v_min)
    /// Δψ̇ = k_y × I_l / (v_f + ε)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionSystem))]
    [UpdateBefore(typeof(DamageSystem))]
    public partial struct ImpulseSystem : ISystem
    {
        // Impulse parameters (from spec)
        private const float ImpulseScale = 1.2f;        // k_i
        private const float VirtualMass = 1200f;        // m_virtual
        private const float YawKickScale = 0.5f;        // k_y
        // MinForwardSpeed uses GameConstants.MinForwardSpeed

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (collision, impulse, velocity, driftState) in
                SystemAPI.Query<RefRW<CollisionEvent>, RefRW<ImpulseData>,
                               RefRW<Velocity>, RefRW<DriftState>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                if (!collision.ValueRO.Occurred)
                {
                    // Clear impulse data when no collision
                    impulse.ValueRW.Magnitude = 0f;
                    continue;
                }

                // =============================================================
                // Get Hazard Severity
                // =============================================================

                float severity = GameConstants.DefaultDamageSeverity;
                float massFactor = GameConstants.DefaultMassFactor;

                Entity hazardEntity = collision.ValueRO.OtherEntity;
                if (hazardEntity != Entity.Null && SystemAPI.HasComponent<Hazard>(hazardEntity))
                {
                    var hazard = SystemAPI.GetComponent<Hazard>(hazardEntity);
                    severity = hazard.Severity;
                    massFactor = hazard.MassFactor;
                }

                // =============================================================
                // Calculate Impulse Magnitude
                // =============================================================

                float vImpact = collision.ValueRO.ImpactSpeed;
                float3 normal = collision.ValueRO.Normal;

                // J = k_i × v_impact × (0.5 + Severity)
                float J = ImpulseScale * vImpact * (0.5f + severity);

                // Adjust by mass factor (heavier hazards = more impact)
                J *= (0.5f + massFactor * 0.5f);

                impulse.ValueRW.Magnitude = J;
                impulse.ValueRW.Direction = normal;

                // =============================================================
                // Decompose into Lane Frame
                // =============================================================

                // Approximate lane frame from current velocity direction
                float3 forward = new float3(0, 0, 1);
                float3 right = new float3(1, 0, 0);

                // Impulse vector
                float3 impulseVec = J * normal;

                // I_f = I · F (forward component)
                float If = math.dot(impulseVec, forward);

                // I_l = I · R (lateral component)
                float Il = math.dot(impulseVec, right);

                impulse.ValueRW.ForwardImpulse = If;
                impulse.ValueRW.LateralImpulse = Il;

                // =============================================================
                // Apply Velocity Response
                // =============================================================

                // Lateral response (always applies): v_l ← v_l + I_l / m_virtual
                // Creates kick sideways, lane destabilization
                velocity.ValueRW.Lateral += Il / VirtualMass;

                // Forward response (clamped): v_f ← v_f - |I_f| / m_virtual
                // But v_f ≥ v_min (forward motion preserved)
                float forwardReduction = math.abs(If) / VirtualMass;
                velocity.ValueRW.Forward -= forwardReduction;
                velocity.ValueRW.Forward = math.max(velocity.ValueRO.Forward, GameConstants.MinForwardSpeed);

                // =============================================================
                // Yaw Kick
                // =============================================================

                // Δψ̇ = k_y × I_l / (v_f + ε)
                // Applied instantly for visual drama
                float yawKick = YawKickScale * Il / (velocity.ValueRO.Forward + 0.1f);
                impulse.ValueRW.YawKick = yawKick;
                velocity.ValueRW.Angular += yawKick;

                // Apply yaw offset to drift state for visual effect
                driftState.ValueRW.YawOffset += yawKick * 0.5f;
                driftState.ValueRW.YawRate += yawKick;

                // =============================================================
                // Clear Collision Event After Processing
                // =============================================================

                // Keep collision data for DamageSystem to read
                // collision.ValueRW.Occurred = false;
            }
        }
    }
}
