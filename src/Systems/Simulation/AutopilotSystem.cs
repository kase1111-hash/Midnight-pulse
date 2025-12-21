// ============================================================================
// Nightflow - Autopilot System
// Execution Order: 2 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// AI-driven vehicle control when autopilot is enabled.
    /// Activates after crash, score save, or player request.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InputSystem))]
    public partial struct AutopilotSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerVehicleTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (autopilot, input, laneFollower, velocity, steeringState) in
                SystemAPI.Query<RefRO<Autopilot>, RefRW<PlayerInput>, RefRO<LaneFollower>,
                               RefRO<Velocity>, RefRW<SteeringState>>()
                    .WithAll<PlayerVehicleTag>())
            {
                if (!autopilot.ValueRO.Enabled)
                    continue;

                // TODO: Implement autopilot logic
                //
                // 1. Maintain target speed
                float currentSpeed = velocity.ValueRO.Forward;
                float targetSpeed = autopilot.ValueRO.TargetSpeed;

                if (currentSpeed < targetSpeed)
                {
                    input.ValueRW.Throttle = math.saturate((targetSpeed - currentSpeed) / 10f);
                    input.ValueRW.Brake = 0f;
                }
                else
                {
                    input.ValueRW.Throttle = 0f;
                    input.ValueRW.Brake = math.saturate((currentSpeed - targetSpeed) / 20f);
                }

                // 2. Lane centering - steer toward lane center
                float lateralOffset = laneFollower.ValueRO.LateralOffset;
                float steerToCenter = -lateralOffset * 0.5f; // Simple proportional control
                input.ValueRW.Steer = math.clamp(steerToCenter, -1f, 1f);

                // 3. No handbrake during autopilot
                input.ValueRW.Handbrake = false;

                // TODO: Hazard avoidance
                // TODO: Lane change decisions based on LanePreference
                // TODO: Emergency vehicle response
            }
        }
    }
}
