// ============================================================================
// Nightflow - Collision & Impulse System
// Execution Order: 6 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Detects collisions and calculates impulse responses.
    /// J = k_i × v_impact × (0.5 + Severity)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VehicleMovementSystem))]
    public partial struct CollisionSystem : ISystem
    {
        // Collision parameters (from spec)
        private const float ImpulseScale = 1.2f;        // k_i
        private const float VirtualMass = 1200f;        // m_virtual
        private const float YawKickScale = 0.5f;        // k_y
        private const float MinForwardSpeed = 8f;       // v_min

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // TODO: Implement spatial query for collision detection
            // This is a simplified placeholder

            foreach (var (collision, impulse, velocity, collisionShape, transform) in
                SystemAPI.Query<RefRW<CollisionEvent>, RefRW<ImpulseData>,
                               RefRW<Velocity>, RefRO<CollisionShape>, RefRO<WorldTransform>>()
                    .WithAll<PlayerVehicleTag>())
            {
                if (!collision.ValueRO.Occurred)
                    continue;

                // =============================================================
                // Calculate Impact Speed
                // =============================================================

                float3 normal = collision.ValueRO.Normal;
                float3 velocityWorld = new float3(0, 0, velocity.ValueRO.Forward); // Simplified

                // v_impact = max(0, -V · N)
                float vImpact = math.max(0f, -math.dot(velocityWorld, normal));

                if (vImpact < 0.1f)
                {
                    // Glancing contact, ignore
                    collision.ValueRW.Occurred = false;
                    continue;
                }

                // =============================================================
                // Get Hazard Severity
                // =============================================================

                // TODO: Look up hazard component from OtherEntity
                float severity = 0.3f; // Placeholder

                // =============================================================
                // Calculate Impulse
                // =============================================================

                // J = k_i × v_impact × (0.5 + Severity)
                float J = ImpulseScale * vImpact * (0.5f + severity);

                // Impulse direction
                float3 impulseDir = J * normal;

                impulse.ValueRW.Magnitude = J;
                impulse.ValueRW.Direction = normal;

                // =============================================================
                // Decompose into Lane Frame
                // =============================================================

                // TODO: Get actual lane frame
                float3 laneForward = new float3(0, 0, 1);
                float3 laneRight = new float3(1, 0, 0);

                // I_f = I · F (forward component)
                float If = math.dot(impulseDir, laneForward);

                // I_l = I · R (lateral component)
                float Il = math.dot(impulseDir, laneRight);

                impulse.ValueRW.ForwardImpulse = If;
                impulse.ValueRW.LateralImpulse = Il;

                // =============================================================
                // Apply Velocity Response
                // =============================================================

                // Lateral response: v_l += I_l / m_virtual
                velocity.ValueRW.Lateral += Il / VirtualMass;

                // Forward response (clamped): v_f -= |I_f| / m_virtual
                velocity.ValueRW.Forward -= math.abs(If) / VirtualMass;
                velocity.ValueRW.Forward = math.max(velocity.ValueRW.Forward, MinForwardSpeed);

                // =============================================================
                // Yaw Kick
                // =============================================================

                // Δψ̇ = k_y × I_l / (v_f + ε)
                float yawKick = YawKickScale * Il / (velocity.ValueRO.Forward + 0.1f);
                impulse.ValueRW.YawKick = yawKick;
                velocity.ValueRW.Angular += yawKick;

                // Clear collision event
                collision.ValueRW.Occurred = false;
            }
        }
    }
}
