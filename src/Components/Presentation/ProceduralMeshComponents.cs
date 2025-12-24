// ============================================================================
// Nightflow - Procedural Mesh Generation Components
// Runtime mesh generation for road segments, tunnels, and overpasses
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    /// <summary>
    /// Marks an entity as requiring procedural mesh generation.
    /// Attached to TrackSegments when created.
    /// </summary>
    public struct ProceduralMeshData : IComponentData
    {
        /// <summary>Whether mesh has been generated.</summary>
        public bool IsGenerated;

        /// <summary>Number of vertices in the generated mesh.</summary>
        public int VertexCount;

        /// <summary>Number of triangles (indices / 3) in the mesh.</summary>
        public int TriangleCount;

        /// <summary>Road surface width in meters (all lanes combined).</summary>
        public float RoadWidth;

        /// <summary>Number of cross-section samples along the spline.</summary>
        public int LengthSegments;

        /// <summary>Number of vertices across the road width.</summary>
        public int WidthSegments;

        /// <summary>Mesh generation LOD level (0=high, 1=medium, 2=low).</summary>
        public int LODLevel;
    }

    /// <summary>
    /// Configuration for tunnel mesh generation.
    /// </summary>
    public struct TunnelMeshConfig : IComponentData
    {
        /// <summary>Tunnel wall height from road surface.</summary>
        public float WallHeight;

        /// <summary>Tunnel ceiling height from road surface.</summary>
        public float CeilingHeight;

        /// <summary>Wall thickness for collision.</summary>
        public float WallThickness;

        /// <summary>Whether to generate interior light strip geometry.</summary>
        public bool GenerateLightStrips;

        /// <summary>Spacing between light strips (meters).</summary>
        public float LightStripSpacing;
    }

    /// <summary>
    /// Configuration for overpass mesh generation.
    /// </summary>
    public struct OverpassMeshConfig : IComponentData
    {
        /// <summary>Barrier height on elevated section.</summary>
        public float BarrierHeight;

        /// <summary>Support pillar width.</summary>
        public float PillarWidth;

        /// <summary>Spacing between support pillars (meters).</summary>
        public float PillarSpacing;

        /// <summary>Whether to generate support pillar geometry.</summary>
        public bool GeneratePillars;
    }

    /// <summary>
    /// Road marking configuration for procedural generation.
    /// </summary>
    public struct RoadMarkingConfig : IComponentData
    {
        /// <summary>Lane divider line width (meters).</summary>
        public float LineWidth;

        /// <summary>Dashed line segment length (meters).</summary>
        public float DashLength;

        /// <summary>Gap between dashed line segments (meters).</summary>
        public float DashGap;

        /// <summary>Edge line width (meters).</summary>
        public float EdgeLineWidth;

        /// <summary>Whether lane lines are dashed (vs solid).</summary>
        public bool DashedLines;
    }

    /// <summary>
    /// Barrier/guardrail mesh configuration.
    /// </summary>
    public struct BarrierMeshConfig : IComponentData
    {
        /// <summary>Barrier height (meters).</summary>
        public float Height;

        /// <summary>Barrier width/thickness (meters).</summary>
        public float Width;

        /// <summary>Spacing between barrier posts (meters).</summary>
        public float PostSpacing;

        /// <summary>Whether barriers are on left edge.</summary>
        public bool LeftBarrier;

        /// <summary>Whether barriers are on right edge.</summary>
        public bool RightBarrier;
    }

    /// <summary>
    /// Mesh bounds for culling and collision.
    /// </summary>
    public struct MeshBounds : IComponentData
    {
        /// <summary>Minimum corner of axis-aligned bounding box.</summary>
        public float3 Min;

        /// <summary>Maximum corner of axis-aligned bounding box.</summary>
        public float3 Max;

        /// <summary>Center of bounding box.</summary>
        public float3 Center;

        /// <summary>Half-extents of bounding box.</summary>
        public float3 Extents;
    }
}
