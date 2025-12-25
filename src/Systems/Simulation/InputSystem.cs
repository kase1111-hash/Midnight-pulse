// ============================================================================
// Nightflow - Input System
// Execution Order: 1 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;
using UnityEngine;

namespace Nightflow.Systems
{
    /// <summary>
    /// Reads hardware input and writes to PlayerInput component.
    /// Supports keyboard, gamepad, and steering wheel input.
    /// Disabled when Autopilot is active; player input re-enables control.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct InputSystem : ISystem
    {
        // Input configuration constants
        private const float SteerDeadzone = 0.1f;
        private const float TriggerDeadzone = 0.05f;
        private const float SteerSensitivity = 1.0f;
        private const float SteerExponent = 1.5f; // Non-linear sensitivity curve

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerVehicleTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Read raw input from hardware
            float rawSteer = Input.GetAxis("Horizontal");
            float rawThrottle = GetThrottleInput();
            float rawBrake = GetBrakeInput();
            bool handbrake = Input.GetButton("Jump") || Input.GetKey(KeyCode.Space);

            // Apply deadzone and sensitivity curves
            float processedSteer = ProcessSteerInput(rawSteer);
            float processedThrottle = ApplyDeadzone(rawThrottle, TriggerDeadzone);
            float processedBrake = ApplyDeadzone(rawBrake, TriggerDeadzone);

            // Check if player is providing meaningful input (to override autopilot)
            bool hasPlayerInput = math.abs(processedSteer) > 0.01f ||
                                  processedThrottle > 0.01f ||
                                  processedBrake > 0.01f ||
                                  handbrake;

            foreach (var (input, autopilot) in
                SystemAPI.Query<RefRW<PlayerInput>, RefRW<Autopilot>>()
                    .WithAll<PlayerVehicleTag>())
            {
                if (autopilot.ValueRO.Enabled)
                {
                    // Check if player wants to take control
                    if (hasPlayerInput)
                    {
                        // Disable autopilot and transfer control to player
                        autopilot.ValueRW.Enabled = false;

                        input.ValueRW.Steer = processedSteer;
                        input.ValueRW.Throttle = processedThrottle;
                        input.ValueRW.Brake = processedBrake;
                        input.ValueRW.Handbrake = handbrake;
                    }
                    else
                    {
                        // Clear input when autopilot is active and no player input
                        input.ValueRW.Steer = 0f;
                        input.ValueRW.Throttle = 0f;
                        input.ValueRW.Brake = 0f;
                        input.ValueRW.Handbrake = false;
                    }
                }
                else
                {
                    // Normal player control mode
                    input.ValueRW.Steer = processedSteer;
                    input.ValueRW.Throttle = processedThrottle;
                    input.ValueRW.Brake = processedBrake;
                    input.ValueRW.Handbrake = handbrake;
                }
            }
        }

        /// <summary>
        /// Gets throttle input from multiple possible sources.
        /// Supports: W key, Up arrow, RT trigger, positive vertical axis.
        /// </summary>
        private float GetThrottleInput()
        {
            float keyboardThrottle = 0f;

            // Keyboard: W or Up arrow
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                keyboardThrottle = 1f;
            }

            // Gamepad: Right trigger (mapped to "Accelerate" or positive Vertical)
            float gamepadThrottle = math.max(0f, Input.GetAxis("Vertical"));

            // Also check for dedicated trigger axis if configured
            float triggerAxis = 0f;
            try
            {
                triggerAxis = math.max(0f, Input.GetAxis("Accelerate"));
            }
            catch
            {
                // Axis not configured, ignore
            }

            return math.max(keyboardThrottle, math.max(gamepadThrottle, triggerAxis));
        }

        /// <summary>
        /// Gets brake input from multiple possible sources.
        /// Supports: S key, Down arrow, LT trigger.
        /// </summary>
        private float GetBrakeInput()
        {
            float keyboardBrake = 0f;

            // Keyboard: S or Down arrow
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                keyboardBrake = 1f;
            }

            // Gamepad: Left trigger or negative Vertical
            float gamepadBrake = math.max(0f, -Input.GetAxis("Vertical"));

            // Also check for dedicated brake axis if configured
            float triggerAxis = 0f;
            try
            {
                triggerAxis = math.max(0f, Input.GetAxis("Brake"));
            }
            catch
            {
                // Axis not configured, ignore
            }

            return math.max(keyboardBrake, math.max(gamepadBrake, triggerAxis));
        }

        /// <summary>
        /// Processes steering input with deadzone and non-linear sensitivity curve.
        /// Uses exponential curve for fine control at low deflections.
        /// </summary>
        private float ProcessSteerInput(float raw)
        {
            // Apply deadzone
            float deadzoned = ApplyDeadzone(raw, SteerDeadzone);

            // Apply non-linear sensitivity curve: sign(x) * |x|^exponent
            // This gives fine control at small deflections, aggressive at full throw
            float sign = math.sign(deadzoned);
            float magnitude = math.abs(deadzoned);
            float curved = sign * math.pow(magnitude, SteerExponent);

            // Apply sensitivity multiplier
            return math.clamp(curved * SteerSensitivity, -1f, 1f);
        }

        /// <summary>
        /// Applies deadzone to an input value, remapping the remaining range to [0, 1].
        /// </summary>
        private float ApplyDeadzone(float value, float deadzone)
        {
            float sign = math.sign(value);
            float magnitude = math.abs(value);

            if (magnitude < deadzone)
            {
                return 0f;
            }

            // Remap from [deadzone, 1] to [0, 1]
            float remapped = (magnitude - deadzone) / (1f - deadzone);
            return sign * math.clamp(remapped, 0f, 1f);
        }
    }
}
