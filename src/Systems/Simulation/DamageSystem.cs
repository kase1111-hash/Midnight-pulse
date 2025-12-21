// ============================================================================
// Nightflow - Damage Evaluation System
// Execution Order: 7 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Accumulates damage from collisions and applies handling degradation.
    /// E_d = k_d × v_impact² × Severity
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionSystem))]
    public partial struct DamageSystem : ISystem
    {
        // Damage parameters (from spec)
        private const float DamageScale = 0.04f;        // k_d

        // Handling degradation coefficients
        private const float FrontSteeringPenalty = 0.4f;
        private const float SideMagnetismPenalty = 0.5f;
        private const float RearSlipPenalty = 0.6f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (damage, impulse, collision, laneFollower) in
                SystemAPI.Query<RefRW<DamageState>, RefRO<ImpulseData>,
                               RefRO<CollisionEvent>, RefRW<LaneFollower>>()
                    .WithAll<PlayerVehicleTag>())
            {
                if (impulse.ValueRO.Magnitude < 0.1f)
                    continue;

                // =============================================================
                // Calculate Damage Energy
                // =============================================================

                // TODO: Get severity from hazard component
                float severity = 0.3f; // Placeholder
                float vImpact = impulse.ValueRO.Magnitude / 1.2f; // Reverse impulse scale

                // E_d = k_d × v_impact² × Severity
                float Ed = DamageScale * vImpact * vImpact * severity;

                // =============================================================
                // Distribute to Damage Zones
                // =============================================================

                float3 normal = impulse.ValueRO.Direction;

                // Calculate directional weights based on impact normal
                // Front: positive Z, Rear: negative Z, Left: negative X, Right: positive X
                float wFront = math.max(0f, normal.z);
                float wRear = math.max(0f, -normal.z);
                float wLeft = math.max(0f, -normal.x);
                float wRight = math.max(0f, normal.x);

                // Normalize weights
                float totalWeight = wFront + wRear + wLeft + wRight;
                if (totalWeight > 0.01f)
                {
                    wFront /= totalWeight;
                    wRear /= totalWeight;
                    wLeft /= totalWeight;
                    wRight /= totalWeight;
                }

                // Apply damage
                damage.ValueRW.Front += Ed * wFront;
                damage.ValueRW.Rear += Ed * wRear;
                damage.ValueRW.Left += Ed * wLeft;
                damage.ValueRW.Right += Ed * wRight;
                damage.ValueRW.Total += Ed;

                // Clamp to [0, 1] for zone damage (normalized)
                damage.ValueRW.Front = math.saturate(damage.ValueRW.Front);
                damage.ValueRW.Rear = math.saturate(damage.ValueRW.Rear);
                damage.ValueRW.Left = math.saturate(damage.ValueRW.Left);
                damage.ValueRW.Right = math.saturate(damage.ValueRW.Right);

                // =============================================================
                // Apply Handling Degradation
                // =============================================================

                // Steering degradation from front damage
                // k_steer = k_steer × (1 - 0.4 × D_front)
                // (Applied in SteeringSystem)

                // Magnetism reduction from side damage
                // ω = ω × (1 - 0.5 × D_side)
                float sideDamage = (damage.ValueRO.Left + damage.ValueRO.Right) * 0.5f;
                float magnetismMultiplier = 1f - SideMagnetismPenalty * sideDamage;
                laneFollower.ValueRW.MagnetStrength *= magnetismMultiplier;

                // Drift stability loss from rear damage
                // k_slip = k_slip × (1 + 0.6 × D_rear)
                // (Applied in VehicleMovementSystem)
            }
        }
    }
}
