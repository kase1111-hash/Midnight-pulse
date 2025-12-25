// ============================================================================
// Nightflow - Unity DOTS Components: Core/Transform
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    /// <summary>
    /// World-space position and rotation for all entities.
    /// Used by: All renderable/physical entities
    /// </summary>
    public struct WorldTransform : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
    }

    /// <summary>
    /// Velocity decomposed into lane-relative components.
    /// Forward = along lane direction, Lateral = perpendicular to lane, Angular = yaw rate
    /// </summary>
    public struct Velocity : IComponentData
    {
        /// <summary>Speed along lane forward direction (m/s). Always >= v_min during gameplay.</summary>
        public float Forward;

        /// <summary>Speed perpendicular to lane (m/s). Positive = right, negative = left.</summary>
        public float Lateral;

        /// <summary>Yaw rotation rate (rad/s). Used for drift/spin mechanics.</summary>
        public float Angular;
    }

    /// <summary>
    /// Previous frame's transform for interpolation and collision detection.
    /// </summary>
    public struct PreviousTransform : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
    }
}
