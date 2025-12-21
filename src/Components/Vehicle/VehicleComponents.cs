// ============================================================================
// Nightflow - Unity DOTS Components: Vehicle Control
// ============================================================================

using Unity.Entities;

namespace Nightflow.Components
{
    /// <summary>
    /// Raw player input state. Written by InputSystem, read by vehicle systems.
    /// Disabled when Autopilot.Enabled = true.
    /// </summary>
    public struct PlayerInput : IComponentData
    {
        /// <summary>Steering input [-1, 1]. Negative = left, positive = right.</summary>
        public float Steer;

        /// <summary>Throttle input [0, 1]. Controls acceleration.</summary>
        public float Throttle;

        /// <summary>Brake input [0, 1]. Slows vehicle, ends scoring when > 0.</summary>
        public float Brake;

        /// <summary>Handbrake engaged. Enables drift mechanics, reduces rear traction.</summary>
        public bool Handbrake;
    }

    /// <summary>
    /// Autopilot state. When enabled, overrides PlayerInput with AI-driven control.
    /// Activates after crash, score save, or player request.
    /// </summary>
    public struct Autopilot : IComponentData
    {
        /// <summary>Whether autopilot is currently controlling the vehicle.</summary>
        public bool Enabled;

        /// <summary>Target forward speed for autopilot (m/s).</summary>
        public float TargetSpeed;

        /// <summary>Preferred lane index. -1 = any lane.</summary>
        public int LanePreference;
    }

    /// <summary>
    /// Smoothed steering state. Prevents jerky movement.
    /// </summary>
    public struct SteeringState : IComponentData
    {
        /// <summary>Current actual steering angle (rad).</summary>
        public float CurrentAngle;

        /// <summary>Target steering angle from input (rad).</summary>
        public float TargetAngle;

        /// <summary>Smoothing factor [0, 1]. Higher = slower response.</summary>
        public float Smoothness;
    }

    /// <summary>
    /// Active lane change transition state.
    /// Uses smoothstep interpolation: λ(t) = 3t² - 2t³
    /// </summary>
    public struct LaneTransition : IComponentData
    {
        /// <summary>Whether a lane change is in progress.</summary>
        public bool Active;

        /// <summary>Entity reference to source lane.</summary>
        public Entity FromLane;

        /// <summary>Entity reference to target lane.</summary>
        public Entity ToLane;

        /// <summary>Transition progress [0, 1]. 0 = at FromLane, 1 = at ToLane.</summary>
        public float Progress;

        /// <summary>Total duration of transition (seconds). Speed-dependent.</summary>
        public float Duration;

        /// <summary>Direction of change. -1 = left, +1 = right.</summary>
        public int Direction;
    }

    /// <summary>
    /// Drift and yaw state for handbrake mechanics.
    /// </summary>
    public struct DriftState : IComponentData
    {
        /// <summary>Current yaw offset from lane direction (rad).</summary>
        public float YawOffset;

        /// <summary>Current yaw rate (rad/s).</summary>
        public float YawRate;

        /// <summary>Current slip angle (rad). β = ψ - arctan(v_l/v_f)</summary>
        public float SlipAngle;

        /// <summary>Whether currently in drift state (handbrake recently used).</summary>
        public bool IsDrifting;
    }

    /// <summary>
    /// Speed tier for scoring and visual effects.
    /// </summary>
    public struct SpeedTier : IComponentData
    {
        /// <summary>Current tier: 0 = Cruise (1x), 1 = Fast (1.5x), 2 = Boosted (2.5x)</summary>
        public int Tier;

        /// <summary>Current speed multiplier based on tier.</summary>
        public float Multiplier;
    }
}
