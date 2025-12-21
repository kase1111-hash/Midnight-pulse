// ============================================================================
// Nightflow - Unity DOTS Components: Damage & Collision
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    /// <summary>
    /// Accumulated damage state per vehicle zone.
    /// Affects handling when damage increases.
    /// </summary>
    public struct DamageState : IComponentData
    {
        /// <summary>Front damage [0, 1]. Reduces steering response.</summary>
        public float Front;

        /// <summary>Rear damage [0, 1]. Increases slip/instability.</summary>
        public float Rear;

        /// <summary>Left side damage [0, 1]. Reduces magnetism.</summary>
        public float Left;

        /// <summary>Right side damage [0, 1]. Reduces magnetism.</summary>
        public float Right;

        /// <summary>Total accumulated damage. Crash when > D_max (100).</summary>
        public float Total;
    }

    /// <summary>
    /// Marks an entity as capable of crashing.
    /// </summary>
    public struct Crashable : IComponentData
    {
        /// <summary>Damage threshold for crash. Default 100.</summary>
        public float CrashThreshold;

        /// <summary>Speed threshold for lethal hazard crash (m/s). Default 25.</summary>
        public float CrashSpeed;

        /// <summary>Yaw threshold for compound failure (rad). Default ~2.5 rad.</summary>
        public float YawFailThreshold;
    }

    /// <summary>
    /// Collision shape for physics queries.
    /// </summary>
    public enum CollisionShapeType : byte
    {
        Box = 0,
        Sphere = 1,
        Capsule = 2
    }

    /// <summary>
    /// Defines collision bounds for an entity.
    /// </summary>
    public struct CollisionShape : IComponentData
    {
        /// <summary>Shape type for collision detection.</summary>
        public CollisionShapeType ShapeType;

        /// <summary>Size/extents. For Box: half-extents. For Sphere: x=radius. For Capsule: x=radius, y=height.</summary>
        public float3 Size;

        /// <summary>Local offset from entity position.</summary>
        public float3 Offset;
    }

    /// <summary>
    /// Result of a collision event for processing by damage system.
    /// </summary>
    public struct CollisionEvent : IComponentData
    {
        /// <summary>Whether a collision occurred this frame.</summary>
        public bool Occurred;

        /// <summary>Entity that was hit.</summary>
        public Entity OtherEntity;

        /// <summary>Impact velocity magnitude (m/s).</summary>
        public float ImpactSpeed;

        /// <summary>Collision normal (pointing away from other entity).</summary>
        public float3 Normal;

        /// <summary>Contact point in world space.</summary>
        public float3 ContactPoint;
    }

    /// <summary>
    /// Pending impulse to be applied to velocity.
    /// </summary>
    public struct ImpulseData : IComponentData
    {
        /// <summary>Impulse magnitude (J = k_i × v_impact × (0.5 + Severity)).</summary>
        public float Magnitude;

        /// <summary>Impulse direction (world space).</summary>
        public float3 Direction;

        /// <summary>Forward component of impulse (lane-relative).</summary>
        public float ForwardImpulse;

        /// <summary>Lateral component of impulse (lane-relative).</summary>
        public float LateralImpulse;

        /// <summary>Yaw kick to apply (rad/s).</summary>
        public float YawKick;
    }

    /// <summary>
    /// Crash state for handling crash → autopilot flow.
    /// </summary>
    public struct CrashState : IComponentData
    {
        /// <summary>Whether entity is currently crashed.</summary>
        public bool IsCrashed;

        /// <summary>Time since crash began (for fade timing).</summary>
        public float CrashTime;

        /// <summary>Crash reason for score summary.</summary>
        public CrashReason Reason;
    }

    /// <summary>
    /// Reason for crash (for score summary display).
    /// </summary>
    public enum CrashReason : byte
    {
        None = 0,
        LethalHazard = 1,
        TotalDamage = 2,
        CompoundFailure = 3
    }
}
