// ============================================================================
// Nightflow - Force Feedback Components
// ECS components for triggering force feedback effects from systems
// ============================================================================

using Unity.Entities;

namespace Nightflow.Components
{
    /// <summary>
    /// Force feedback event singleton - written by ECS systems, read by ForceFeedbackController.
    /// Allows Burst-compiled systems to trigger force feedback effects.
    /// </summary>
    public struct ForceFeedbackEvent : IComponentData
    {
        /// <summary>Type of force feedback effect to trigger.</summary>
        public ForceFeedbackEventType EventType;

        /// <summary>Intensity of the effect (0-100).</summary>
        public int Intensity;

        /// <summary>Direction for directional effects (-100 to 100, negative=left).</summary>
        public int Direction;

        /// <summary>Event has been triggered this frame.</summary>
        public bool Triggered;

        /// <summary>Reset event after processing.</summary>
        public void Clear()
        {
            EventType = ForceFeedbackEventType.None;
            Triggered = false;
        }
    }

    /// <summary>
    /// Types of force feedback events that can be triggered.
    /// </summary>
    public enum ForceFeedbackEventType : byte
    {
        /// <summary>No event.</summary>
        None = 0,

        /// <summary>Collision impact.</summary>
        Collision = 1,

        /// <summary>Side collision (swipe).</summary>
        SideCollision = 2,

        /// <summary>Frontal collision.</summary>
        FrontalCollision = 3,

        /// <summary>Crash - total loss of control.</summary>
        Crash = 4,

        /// <summary>Near miss with hazard.</summary>
        NearMiss = 5,

        /// <summary>Entered drift state.</summary>
        DriftStart = 6,

        /// <summary>Exited drift state.</summary>
        DriftEnd = 7,

        /// <summary>Entered tunnel.</summary>
        TunnelEnter = 8,

        /// <summary>Exited tunnel.</summary>
        TunnelExit = 9,

        /// <summary>Speed boost activated.</summary>
        Boost = 10,

        /// <summary>Damage taken.</summary>
        DamageTaken = 11
    }
}
