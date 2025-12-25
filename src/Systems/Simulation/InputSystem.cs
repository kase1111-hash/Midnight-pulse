// ============================================================================
// Nightflow - Input System
// Execution Order: 1 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Input;
using Nightflow.Save;
using UnityEngine;

namespace Nightflow.Systems
{
    /// <summary>
    /// Reads hardware input and writes to PlayerInput component.
    /// Uses InputBindingManager for rebindable controls.
    /// Disabled when Autopilot is active; player input re-enables control.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct InputSystem : ISystem
    {
        // Input configuration - loaded from settings
        private float steerDeadzone;
        private float triggerDeadzone;
        private float steerSensitivity;
        private float steerExponent;
        private bool invertSteering;
        private bool settingsLoaded;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerVehicleTag>();
            steerDeadzone = 0.1f;
            triggerDeadzone = 0.05f;
            steerSensitivity = 1.0f;
            steerExponent = 1.5f;
            invertSteering = false;
            settingsLoaded = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Load settings from SaveManager if not yet loaded
            if (!settingsLoaded && SaveManager.Instance != null)
            {
                LoadSettings();
                settingsLoaded = true;
            }

            // Skip if InputBindingManager is rebinding
            var bindingManager = InputBindingManager.Instance;
            if (bindingManager != null && bindingManager.IsRebinding)
            {
                return;
            }

            // Read input from binding manager or fallback to legacy
            float rawSteer = GetSteerInput(bindingManager);
            float rawThrottle = GetThrottleInput(bindingManager);
            float rawBrake = GetBrakeInput(bindingManager);
            bool handbrake = GetHandbrakeInput(bindingManager);

            // Apply deadzone and sensitivity curves
            float processedSteer = ProcessSteerInput(rawSteer);
            float processedThrottle = ApplyDeadzone(rawThrottle, triggerDeadzone);
            float processedBrake = ApplyDeadzone(rawBrake, triggerDeadzone);

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

        private void LoadSettings()
        {
            var settings = SaveManager.Instance.GetSettings();
            if (settings?.Controls != null)
            {
                steerDeadzone = settings.Controls.SteeringDeadzone;
                steerSensitivity = settings.Controls.SteeringSensitivity;
                invertSteering = settings.Controls.InvertSteering;
            }
        }

        /// <summary>
        /// Gets steering input using InputBindingManager or legacy fallback.
        /// </summary>
        private float GetSteerInput(InputBindingManager bindingManager)
        {
            if (bindingManager != null)
            {
                return bindingManager.GetSteerAxis();
            }

            // Legacy fallback
            return UnityEngine.Input.GetAxis("Horizontal");
        }

        /// <summary>
        /// Gets throttle input using InputBindingManager or legacy fallback.
        /// Supports wheel pedals when in wheel mode.
        /// </summary>
        private float GetThrottleInput(InputBindingManager bindingManager)
        {
            if (bindingManager != null)
            {
                // Use wheel-aware throttle method
                return bindingManager.GetWheelThrottle();
            }

            // Legacy fallback
            float keyboardThrottle = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow))
            {
                keyboardThrottle = 1f;
            }

            float gamepadThrottle = math.max(0f, UnityEngine.Input.GetAxis("Vertical"));
            return math.max(keyboardThrottle, gamepadThrottle);
        }

        /// <summary>
        /// Gets brake input using InputBindingManager or legacy fallback.
        /// Supports wheel pedals when in wheel mode.
        /// </summary>
        private float GetBrakeInput(InputBindingManager bindingManager)
        {
            if (bindingManager != null)
            {
                // Use wheel-aware brake method
                return bindingManager.GetWheelBrake();
            }

            // Legacy fallback
            float keyboardBrake = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow))
            {
                keyboardBrake = 1f;
            }

            float gamepadBrake = math.max(0f, -UnityEngine.Input.GetAxis("Vertical"));
            return math.max(keyboardBrake, gamepadBrake);
        }

        /// <summary>
        /// Gets handbrake input using InputBindingManager or legacy fallback.
        /// </summary>
        private bool GetHandbrakeInput(InputBindingManager bindingManager)
        {
            if (bindingManager != null)
            {
                return bindingManager.IsActionPressed(InputAction.Handbrake);
            }

            // Legacy fallback
            return UnityEngine.Input.GetButton("Jump") || UnityEngine.Input.GetKey(KeyCode.Space);
        }

        /// <summary>
        /// Processes steering input with deadzone and non-linear sensitivity curve.
        /// Uses exponential curve for fine control at low deflections.
        /// </summary>
        private float ProcessSteerInput(float raw)
        {
            // Apply inversion if enabled
            if (invertSteering)
            {
                raw = -raw;
            }

            // Apply deadzone
            float deadzoned = ApplyDeadzone(raw, steerDeadzone);

            // Apply non-linear sensitivity curve: sign(x) * |x|^exponent
            // This gives fine control at small deflections, aggressive at full throw
            float sign = math.sign(deadzoned);
            float magnitude = math.abs(deadzoned);
            float curved = sign * math.pow(magnitude, steerExponent);

            // Apply sensitivity multiplier
            return math.clamp(curved * steerSensitivity, -1f, 1f);
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
