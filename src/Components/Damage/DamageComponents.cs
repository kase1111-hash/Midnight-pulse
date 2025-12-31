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
        CompoundFailure = 3,
        ComponentFailure = 4
    }

    // =========================================================================
    // Phase 2 Damage: Component-Level Health & Failures
    // =========================================================================

    /// <summary>
    /// Tracks health of individual vehicle components.
    /// Each component degrades independently based on damage zone and impact type.
    /// Health values are [0, 1] where 1 = perfect, 0 = failed.
    /// </summary>
    public struct ComponentHealth : IComponentData
    {
        /// <summary>Suspension health. Affects handling stability and camera shake.</summary>
        public float Suspension;

        /// <summary>Steering system health. Affects steering responsiveness and drift.</summary>
        public float Steering;

        /// <summary>Tire condition. Affects grip and lane magnetism.</summary>
        public float Tires;

        /// <summary>Engine health. Affects acceleration and max speed.</summary>
        public float Engine;

        /// <summary>Transmission health. Affects speed changes and drift recovery.</summary>
        public float Transmission;

        /// <summary>Creates a new ComponentHealth with all systems at full health.</summary>
        public static ComponentHealth FullHealth => new ComponentHealth
        {
            Suspension = 1f,
            Steering = 1f,
            Tires = 1f,
            Engine = 1f,
            Transmission = 1f
        };

        /// <summary>Returns the lowest component health (most damaged).</summary>
        public float LowestHealth => math.min(
            math.min(Suspension, Steering),
            math.min(Tires, math.min(Engine, Transmission))
        );

        /// <summary>Returns average component health.</summary>
        public float AverageHealth => (Suspension + Steering + Tires + Engine + Transmission) / 5f;
    }

    /// <summary>
    /// Flags for which components have critically failed.
    /// Once failed, a component cannot be repaired during the run.
    /// </summary>
    [System.Flags]
    public enum ComponentFailures : byte
    {
        None = 0,
        Suspension = 1 << 0,
        Steering = 1 << 1,
        Tires = 1 << 2,
        Engine = 1 << 3,
        Transmission = 1 << 4,
        All = Suspension | Steering | Tires | Engine | Transmission
    }

    /// <summary>
    /// Tracks which components have failed and failure effects.
    /// </summary>
    public struct ComponentFailureState : IComponentData
    {
        /// <summary>Bit flags of failed components.</summary>
        public ComponentFailures FailedComponents;

        /// <summary>Time since last component failure (for visual effects).</summary>
        public float TimeSinceLastFailure;

        /// <summary>Number of failed components.</summary>
        public int FailureCount => math.countbits((int)FailedComponents);

        /// <summary>Check if a specific component has failed.</summary>
        public bool HasFailed(ComponentFailures component) =>
            (FailedComponents & component) != 0;

        /// <summary>Returns true if any critical component (steering/suspension) has failed.</summary>
        public bool HasCriticalFailure =>
            HasFailed(ComponentFailures.Steering) || HasFailed(ComponentFailures.Suspension);

        /// <summary>Returns true if 3+ components have failed (cascade failure).</summary>
        public bool HasCascadeFailure => FailureCount >= 3;
    }

    /// <summary>
    /// Configuration for component damage sensitivity.
    /// Determines how zone damage translates to component damage.
    /// </summary>
    public struct ComponentDamageConfig : IComponentData
    {
        /// <summary>Health threshold below which component fails [0, 1]. Default 0.1.</summary>
        public float FailureThreshold;

        /// <summary>How much front damage affects steering. Default 0.8.</summary>
        public float FrontToSteeringRatio;

        /// <summary>How much rear damage affects transmission. Default 0.6.</summary>
        public float RearToTransmissionRatio;

        /// <summary>How much side damage affects suspension. Default 0.5.</summary>
        public float SideToSuspensionRatio;

        /// <summary>How much total damage affects engine. Default 0.3.</summary>
        public float TotalToEngineRatio;

        /// <summary>How much all impacts affect tires. Default 0.4.</summary>
        public float ImpactToTiresRatio;

        /// <summary>Default configuration values.</summary>
        public static ComponentDamageConfig Default => new ComponentDamageConfig
        {
            FailureThreshold = 0.1f,
            FrontToSteeringRatio = 0.8f,
            RearToTransmissionRatio = 0.6f,
            SideToSuspensionRatio = 0.5f,
            TotalToEngineRatio = 0.3f,
            ImpactToTiresRatio = 0.4f
        };
    }

    /// <summary>
    /// Soft-body deformation state for enhanced visual damage.
    /// Tracks deformation velocity for smooth interpolation.
    /// </summary>
    public struct SoftBodyState : IComponentData
    {
        /// <summary>Current deformation magnitude per zone [0, 1].</summary>
        public float4 CurrentDeformation; // x=front, y=rear, z=left, w=right

        /// <summary>Target deformation based on damage.</summary>
        public float4 TargetDeformation;

        /// <summary>Deformation velocity for smooth interpolation.</summary>
        public float4 DeformationVelocity;

        /// <summary>Spring constant for deformation interpolation. Default 8.</summary>
        public float SpringConstant;

        /// <summary>Damping factor for deformation. Default 0.7.</summary>
        public float Damping;
    }
}
