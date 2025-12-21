// ============================================================================
// Nightflow - Unity DOTS Buffer Elements (IBufferElementData)
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Buffers
{
    /// <summary>
    /// Spline control point for lane geometry.
    /// Stored as dynamic buffer on Lane entities.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct LaneSplinePoint : IBufferElementData
    {
        /// <summary>World-space position of control point.</summary>
        public float3 Position;

        /// <summary>Tangent direction at this point (for Hermite interpolation).</summary>
        public float3 Tangent;

        /// <summary>Cumulative arc length from start to this point.</summary>
        public float ArcLength;
    }

    /// <summary>
    /// Cached sample point along a spline for fast lookup.
    /// Pre-computed at regular arc-length intervals.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct SplineSample : IBufferElementData
    {
        /// <summary>World-space position.</summary>
        public float3 Position;

        /// <summary>Forward direction (normalized tangent).</summary>
        public float3 Forward;

        /// <summary>Right direction.</summary>
        public float3 Right;

        /// <summary>Arc length from segment start.</summary>
        public float ArcLength;

        /// <summary>Spline parameter t [0, 1].</summary>
        public float Parameter;
    }

    /// <summary>
    /// Input recording entry for replay system.
    /// Fixed timestep input samples.
    /// </summary>
    [InternalBufferCapacity(1024)]
    public struct InputLogEntry : IBufferElementData
    {
        /// <summary>Timestamp of this input (seconds from start).</summary>
        public float Timestamp;

        /// <summary>Steering input at this time.</summary>
        public float Steer;

        /// <summary>Throttle input at this time.</summary>
        public float Throttle;

        /// <summary>Brake input at this time.</summary>
        public float Brake;

        /// <summary>Handbrake state at this time.</summary>
        public bool Handbrake;
    }

    /// <summary>
    /// Lane reference buffer for track segments.
    /// Links a TrackSegment to its child Lane entities.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct LaneReference : IBufferElementData
    {
        /// <summary>Entity reference to the lane.</summary>
        public Entity Lane;

        /// <summary>Lane index (-n to +n, 0 = center).</summary>
        public int LaneIndex;
    }

    /// <summary>
    /// Nearby vehicle reference for AI awareness.
    /// Updated each frame for traffic/emergency AI.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct NearbyVehicle : IBufferElementData
    {
        /// <summary>Entity reference to nearby vehicle.</summary>
        public Entity Vehicle;

        /// <summary>Distance to vehicle (m).</summary>
        public float Distance;

        /// <summary>Relative forward position (positive = ahead).</summary>
        public float ForwardOffset;

        /// <summary>Lane index of the vehicle.</summary>
        public int LaneIndex;

        /// <summary>Whether this is an emergency vehicle.</summary>
        public bool IsEmergency;
    }

    /// <summary>
    /// Hazard reference buffer for track segments.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct HazardReference : IBufferElementData
    {
        /// <summary>Entity reference to the hazard.</summary>
        public Entity Hazard;

        /// <summary>Distance along segment (m).</summary>
        public float SegmentDistance;

        /// <summary>Lane index containing the hazard.</summary>
        public int LaneIndex;
    }
}
