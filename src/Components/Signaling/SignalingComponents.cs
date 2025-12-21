// ============================================================================
// Nightflow - Unity DOTS Components: Signaling & Presentation
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    /// <summary>
    /// Light emitter for dynamic lighting effects.
    /// </summary>
    public struct LightEmitter : IComponentData
    {
        /// <summary>Light color (RGB, 0-1 range).</summary>
        public float3 Color;

        /// <summary>Light intensity multiplier.</summary>
        public float Intensity;

        /// <summary>Light radius/range (m).</summary>
        public float Radius;

        /// <summary>Whether light strobes on/off.</summary>
        public bool Strobe;

        /// <summary>Strobe rate (Hz). Increases with urgency for emergencies.</summary>
        public float StrobeRate;

        /// <summary>Current strobe phase [0, 1].</summary>
        public float StrobePhase;
    }

    /// <summary>
    /// Off-screen threat indicator signal.
    /// </summary>
    public struct OffscreenSignal : IComponentData
    {
        /// <summary>Screen-space direction to threat (normalized).</summary>
        public float2 Direction;

        /// <summary>Urgency level [0, 1]. Affects intensity and strobe rate.</summary>
        public float Urgency;

        /// <summary>Type of threat being signaled.</summary>
        public OffscreenSignalType Type;

        /// <summary>Whether signal is currently active.</summary>
        public bool Active;
    }

    /// <summary>
    /// Types of off-screen threats.
    /// </summary>
    public enum OffscreenSignalType : byte
    {
        None = 0,
        CrashedVehicleAhead = 1,    // Red/blue strobe
        EmergencyBehind = 2          // Red/white strobe
    }

    /// <summary>
    /// Replay recording/playback state.
    /// </summary>
    public struct ReplayState : IComponentData
    {
        /// <summary>Whether currently recording.</summary>
        public bool Recording;

        /// <summary>Whether currently playing back.</summary>
        public bool Playing;

        /// <summary>Current timestamp in replay (seconds).</summary>
        public float Timestamp;

        /// <summary>Total replay duration (seconds).</summary>
        public float Duration;

        /// <summary>Global seed used for this run (for deterministic replay).</summary>
        public uint GlobalSeed;
    }

    /// <summary>
    /// Camera rig state for chase camera.
    /// </summary>
    public struct CameraState : IComponentData
    {
        /// <summary>Current camera offset from vehicle.</summary>
        public float3 Offset;

        /// <summary>Current field of view (degrees).</summary>
        public float FOV;

        /// <summary>Target FOV based on speed.</summary>
        public float TargetFOV;

        /// <summary>Current shake intensity [0, 1].</summary>
        public float ShakeIntensity;

        /// <summary>Current shake offset.</summary>
        public float3 ShakeOffset;

        /// <summary>Yaw follow offset (for drift whip).</summary>
        public float YawOffset;
    }

    /// <summary>
    /// Hazard entity definition.
    /// </summary>
    public struct Hazard : IComponentData
    {
        /// <summary>Hazard type.</summary>
        public HazardType Type;

        /// <summary>Severity [0, 1]. Affects damage and impulse.</summary>
        public float Severity;

        /// <summary>Mass factor [0, 1]. Affects impulse calculations.</summary>
        public float MassFactor;

        /// <summary>Whether this hazard has been hit.</summary>
        public bool Hit;
    }

    /// <summary>
    /// Types of road hazards.
    /// </summary>
    public enum HazardType : byte
    {
        LooseTire = 0,      // Severity 0.2, Cosmetic
        Debris = 1,          // Severity 0.4, Mechanical
        Cone = 2,            // Severity 0.3, Cosmetic
        Barrier = 3,         // Severity 0.9, Lethal
        CrashedCar = 4       // Severity 1.0, Lethal
    }

    /// <summary>
    /// Damage type classification for hazards.
    /// </summary>
    public enum DamageType : byte
    {
        Cosmetic = 0,    // Visual only, minor handling effect
        Mechanical = 1,  // Handling degradation
        Lethal = 2       // Can cause immediate crash at speed
    }
}
