// ============================================================================
// Nightflow - Unity DOTS Components: Lane & Track
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    /// <summary>
    /// Links a vehicle to its current lane and magnetism state.
    /// </summary>
    public struct LaneFollower : IComponentData
    {
        /// <summary>Reference to the Lane entity being followed.</summary>
        public Entity LaneEntity;

        /// <summary>Base magnetism strength (ω). Typically 6-12.</summary>
        public float MagnetStrength;

        /// <summary>Current lateral offset from lane center (m). Positive = right.</summary>
        public float LateralOffset;

        /// <summary>Current parameter t along the lane spline [0, 1].</summary>
        public float SplineParameter;

        /// <summary>Current lane index (for multi-lane roads).</summary>
        public int LaneIndex;
    }

    /// <summary>
    /// Defines a lane's properties. Actual spline points stored in LaneSplinePoint buffer.
    /// </summary>
    public struct LaneSpline : IComponentData
    {
        /// <summary>Lane width in meters. Default 3.6m.</summary>
        public float Width;

        /// <summary>Total arc length of this lane segment (m).</summary>
        public float ArcLength;

        /// <summary>Reference to parent TrackSegment entity.</summary>
        public Entity TrackSegment;

        /// <summary>Lane index within the track segment. 0 = center, negative = left, positive = right.</summary>
        public int LaneIndex;
    }

    /// <summary>
    /// Track segment types for procedural generation.
    /// </summary>
    public enum TrackSegmentType : byte
    {
        Straight = 0,
        Curve = 1,
        Tunnel = 2,
        Overpass = 3,
        Fork = 4
    }

    /// <summary>
    /// A procedural track segment containing multiple lanes.
    /// </summary>
    public struct TrackSegment : IComponentData
    {
        /// <summary>Segment type affects rendering and audio.</summary>
        public TrackSegmentType Type;

        /// <summary>Length of this segment (m).</summary>
        public float Length;

        /// <summary>Difficulty scalar [0, 1] for hazard/traffic spawning.</summary>
        public float Difficulty;

        /// <summary>Number of lanes in this segment.</summary>
        public int LaneCount;

        /// <summary>Segment index in the procedural sequence (for deterministic generation).</summary>
        public int SegmentIndex;

        /// <summary>Reference to next segment (null if not yet generated).</summary>
        public Entity NextSegment;

        /// <summary>Reference to previous segment (null if despawned).</summary>
        public Entity PreviousSegment;
    }

    /// <summary>
    /// Fork-specific data for branching track segments.
    /// </summary>
    public struct ForkData : IComponentData
    {
        /// <summary>Entity reference to left branch segment.</summary>
        public Entity LeftBranch;

        /// <summary>Entity reference to right branch segment.</summary>
        public Entity RightBranch;

        /// <summary>Fork angle (rad). Branches diverge by ±ForkAngle.</summary>
        public float ForkAngle;

        /// <summary>Distance along segment where fork begins (m).</summary>
        public float ForkStartDistance;

        /// <summary>Whether player has committed to a branch.</summary>
        public bool Committed;

        /// <summary>Which branch was chosen. -1 = left, 1 = right, 0 = uncommitted.</summary>
        public int ChosenBranch;
    }

    /// <summary>
    /// Hermite spline control point for a lane.
    /// </summary>
    public struct HermiteSpline : IComponentData
    {
        /// <summary>Start position P0.</summary>
        public float3 P0;

        /// <summary>End position P1.</summary>
        public float3 P1;

        /// <summary>Start tangent T0 (scaled by segment length × alpha).</summary>
        public float3 T0;

        /// <summary>End tangent T1 (scaled by segment length × alpha).</summary>
        public float3 T1;
    }

    /// <summary>
    /// Cached local coordinate frame at a point on the spline.
    /// </summary>
    public struct SplineFrame : IComponentData
    {
        /// <summary>Forward direction (normalized tangent).</summary>
        public float3 Forward;

        /// <summary>Right direction (perpendicular to forward and up).</summary>
        public float3 Right;

        /// <summary>Up direction (corrected from world up).</summary>
        public float3 Up;

        /// <summary>Position on spline.</summary>
        public float3 Position;
    }
}
