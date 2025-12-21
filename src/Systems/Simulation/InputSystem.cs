// ============================================================================
// Nightflow - Input System
// Execution Order: 1 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Reads hardware input and writes to PlayerInput component.
    /// Disabled when Autopilot is active.
    /// Also handles replay playback input injection.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct InputSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerVehicleTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // TODO: Implement input reading
            //
            // For each PlayerVehicle without AutopilotActiveTag:
            //   1. Read raw input from Input System
            //   2. Apply deadzone and sensitivity curves
            //   3. Write to PlayerInput component
            //
            // For GhostVehicle entities:
            //   1. Read from InputLogEntry buffer at current timestamp
            //   2. Write to PlayerInput component

            foreach (var (input, autopilot) in
                SystemAPI.Query<RefRW<PlayerInput>, RefRO<Autopilot>>()
                    .WithAll<PlayerVehicleTag>())
            {
                if (autopilot.ValueRO.Enabled)
                {
                    // Clear input when autopilot is active
                    input.ValueRW.Steer = 0f;
                    input.ValueRW.Throttle = 0f;
                    input.ValueRW.Brake = 0f;
                    input.ValueRW.Handbrake = false;
                }
                else
                {
                    // TODO: Read actual hardware input
                    // input.ValueRW.Steer = UnityEngine.Input.GetAxis("Horizontal");
                    // input.ValueRW.Throttle = UnityEngine.Input.GetAxis("Accelerate");
                    // input.ValueRW.Brake = UnityEngine.Input.GetAxis("Brake");
                    // input.ValueRW.Handbrake = UnityEngine.Input.GetButton("Handbrake");
                }
            }
        }
    }
}
