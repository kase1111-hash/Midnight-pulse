// ============================================================================
// Nightflow - Procedural Road Mesh Generation System
// Generates road surface, barriers, and lane markings from spline data
// Execution Order: After TrackGenerationSystem, before WireframeRenderSystem
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;
using Nightflow.Utilities;

namespace Nightflow.Systems
{
    /// <summary>
    /// Generates procedural mesh geometry for road segments.
    /// Creates vertices, triangles, and UVs for wireframe rendering.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(WireframeRenderSystem))]
    public partial struct ProceduralRoadMeshSystem : ISystem
    {
        // Road geometry parameters
        private const float LaneWidth = 3.6f;
        private const int NumLanes = 4;
        private const float RoadWidth = LaneWidth * NumLanes;       // 14.4m total
        private const float ShoulderWidth = 1.5f;                   // Each side
        private const float TotalWidth = RoadWidth + ShoulderWidth * 2f; // 17.4m

        // Mesh resolution
        private const int LengthSegmentsHigh = 40;                  // LOD 0
        private const int LengthSegmentsMed = 20;                   // LOD 1
        private const int LengthSegmentsLow = 10;                   // LOD 2
        private const int WidthSegments = 8;                        // Cross-section verts

        // Barrier parameters
        private const float BarrierHeight = 0.8f;
        private const float BarrierWidth = 0.15f;

        // Lane marking parameters
        private const float LaneLineWidth = 0.15f;
        private const float EdgeLineWidth = 0.2f;

        // Wireframe color values (RGBA)
        private static readonly float4 RoadColor = new float4(0.1f, 0.1f, 0.15f, 1f);
        private static readonly float4 LaneLineColor = new float4(0.27f, 0.53f, 1f, 1f);    // Blue
        private static readonly float4 EdgeLineColor = new float4(1f, 0.53f, 0f, 1f);       // Orange
        private static readonly float4 BarrierColor = new float4(0.4f, 0.4f, 0.4f, 1f);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TrackSegmentTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Find segments that need mesh generation
            foreach (var (meshData, segment, spline, boundsRW, entity) in
                SystemAPI.Query<RefRW<ProceduralMeshData>, RefRO<TrackSegment>, RefRO<HermiteSpline>, RefRW<MeshBounds>>()
                    .WithAll<TrackSegmentTag>()
                    .WithEntityAccess())
            {
                if (meshData.ValueRO.IsGenerated)
                    continue;

                // Get buffers (they exist in archetype, just need to populate)
                var vertices = SystemAPI.GetBuffer<MeshVertex>(entity);
                var triangles = SystemAPI.GetBuffer<MeshTriangle>(entity);
                var subMeshes = SystemAPI.GetBuffer<SubMeshRange>(entity);
                vertices.Clear();
                triangles.Clear();
                subMeshes.Clear();

                // Get segment type for specialized mesh generation
                int segmentType = segment.ValueRO.Type;

                // Calculate LOD based on segment length and type
                int lodLevel = 0; // Default to high
                int lengthSegments = GetLengthSegmentsForLOD(lodLevel);

                // Generate the road mesh
                GenerateRoadMesh(
                    vertices,
                    triangles,
                    subMeshes,
                    spline.ValueRO,
                    segment.ValueRO,
                    lengthSegments,
                    ref meshData.ValueRW,
                    ref boundsRW.ValueRW
                );

                // Generate barriers
                GenerateBarrierMesh(
                    vertices,
                    triangles,
                    subMeshes,
                    spline.ValueRO,
                    lengthSegments
                );

                // Generate lane markings
                GenerateLaneMarkings(
                    vertices,
                    triangles,
                    subMeshes,
                    spline.ValueRO,
                    lengthSegments
                );

                // Generate special geometry based on segment type
                switch (segmentType)
                {
                    case 2: // Tunnel
                        GenerateTunnelMesh(ref ecb, entity, spline.ValueRO, segment.ValueRO, lengthSegments);
                        break;
                    case 3: // Overpass
                        GenerateOverpassMesh(ref ecb, entity, spline.ValueRO, segment.ValueRO, lengthSegments);
                        break;
                }

                // Mark as generated
                meshData.ValueRW.IsGenerated = true;
                meshData.ValueRW.LengthSegments = lengthSegments;
                meshData.ValueRW.WidthSegments = WidthSegments;
                meshData.ValueRW.RoadWidth = TotalWidth;
                meshData.ValueRW.LODLevel = lodLevel;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static int GetLengthSegmentsForLOD(int lod)
        {
            return lod switch
            {
                0 => LengthSegmentsHigh,
                1 => LengthSegmentsMed,
                _ => LengthSegmentsLow
            };
        }

        /// <summary>
        /// Generates the main road surface mesh.
        /// </summary>
        private void GenerateRoadMesh(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            HermiteSpline spline,
            TrackSegment segment,
            int lengthSegments,
            ref ProceduralMeshData meshData,
            ref MeshBounds bounds)
        {
            // Calculate vertex and index counts
            int vertsPerRow = WidthSegments + 1;
            int vertexCount = vertsPerRow * (lengthSegments + 1);
            int quadCount = WidthSegments * lengthSegments;
            int triangleCount = quadCount * 2;
            int indexCount = triangleCount * 3;

            meshData.VertexCount = vertexCount;
            meshData.TriangleCount = triangleCount;

            vertices.EnsureCapacity(vertexCount);
            triangles.EnsureCapacity(indexCount);

            // Generate vertices along the spline
            for (int z = 0; z <= lengthSegments; z++)
            {
                float t = z / (float)lengthSegments;

                // Get spline frame at this point
                SplineUtilities.BuildFrameAtT(
                    spline.P0, spline.T0, spline.P1, spline.T1, t,
                    out float3 position, out float3 forward, out float3 right, out float3 up);

                // Generate vertices across the road width
                for (int x = 0; x <= WidthSegments; x++)
                {
                    float xNorm = x / (float)WidthSegments;
                    float xOffset = (xNorm - 0.5f) * TotalWidth;

                    float3 vertPos = position + right * xOffset;

                    vertices.Add(new MeshVertex
                    {
                        Position = vertPos,
                        Normal = up,
                        UV = new float2(xNorm, t * segment.Length / 10f), // Repeat every 10m
                        Color = RoadColor
                    });
                }
            }

            // Generate triangle indices
            for (int z = 0; z < lengthSegments; z++)
            {
                for (int x = 0; x < WidthSegments; x++)
                {
                    int bottomLeft = z * vertsPerRow + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + vertsPerRow;
                    int topRight = topLeft + 1;

                    // Triangle 1 (bottom-left, top-left, top-right)
                    triangles.Add(new MeshTriangle { Index = bottomLeft });
                    triangles.Add(new MeshTriangle { Index = topLeft });
                    triangles.Add(new MeshTriangle { Index = topRight });

                    // Triangle 2 (bottom-left, top-right, bottom-right)
                    triangles.Add(new MeshTriangle { Index = bottomLeft });
                    triangles.Add(new MeshTriangle { Index = topRight });
                    triangles.Add(new MeshTriangle { Index = bottomRight });
                }
            }

            // Add road surface sub-mesh
            subMeshes.Add(new SubMeshRange
            {
                StartIndex = 0,
                IndexCount = indexCount,
                MaterialType = 0 // Road surface
            });

            // Calculate and store bounds
            bounds = CalculateBounds(spline, segment);
        }

        /// <summary>
        /// Generates barrier meshes on both sides of the road.
        /// </summary>
        private void GenerateBarrierMesh(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            HermiteSpline spline,
            int lengthSegments)
        {
            // Barrier: 4 verts per cross-section (inner-bottom, inner-top, outer-top, outer-bottom)
            int vertsPerSection = 4;
            int startVertexIndex = vertices.Length;
            int startTriangleIndex = triangles.Length;

            // Left barrier offset (outside road edge)
            float leftOffset = -TotalWidth / 2f - BarrierWidth / 2f;
            // Right barrier offset (outside road edge)
            float rightOffset = TotalWidth / 2f + BarrierWidth / 2f;

            // Generate both barriers
            float[] barrierOffsets = { leftOffset, rightOffset };

            foreach (float barrierOffset in barrierOffsets)
            {
                int barrierStartVert = vertices.Length;

                for (int z = 0; z <= lengthSegments; z++)
                {
                    float t = z / (float)lengthSegments;

                    SplineUtilities.BuildFrameAtT(
                        spline.P0, spline.T0, spline.P1, spline.T1, t,
                        out float3 position, out float3 forward, out float3 right, out float3 up);

                    float3 barrierCenter = position + right * barrierOffset;

                    // Inner bottom
                    vertices.Add(new MeshVertex
                    {
                        Position = barrierCenter - right * (BarrierWidth / 2f),
                        Normal = -right,
                        UV = new float2(0f, t),
                        Color = BarrierColor
                    });

                    // Inner top
                    vertices.Add(new MeshVertex
                    {
                        Position = barrierCenter - right * (BarrierWidth / 2f) + up * BarrierHeight,
                        Normal = -right,
                        UV = new float2(0f, t),
                        Color = BarrierColor
                    });

                    // Outer top
                    vertices.Add(new MeshVertex
                    {
                        Position = barrierCenter + right * (BarrierWidth / 2f) + up * BarrierHeight,
                        Normal = right,
                        UV = new float2(1f, t),
                        Color = BarrierColor
                    });

                    // Outer bottom
                    vertices.Add(new MeshVertex
                    {
                        Position = barrierCenter + right * (BarrierWidth / 2f),
                        Normal = right,
                        UV = new float2(1f, t),
                        Color = BarrierColor
                    });
                }

                // Generate triangles for barrier faces
                for (int z = 0; z < lengthSegments; z++)
                {
                    int baseIdx = barrierStartVert + z * vertsPerSection;

                    // Inner face (verts 0, 1)
                    triangles.Add(new MeshTriangle { Index = baseIdx + 0 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 4 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 5 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 0 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 5 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 1 });

                    // Top face (verts 1, 2)
                    triangles.Add(new MeshTriangle { Index = baseIdx + 1 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 5 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 6 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 1 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 6 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 2 });

                    // Outer face (verts 2, 3)
                    triangles.Add(new MeshTriangle { Index = baseIdx + 2 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 6 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 7 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 2 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 7 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 3 });
                }
            }

            // Add barrier sub-mesh range
            int barrierIndexCount = triangles.Length - startTriangleIndex;
            if (barrierIndexCount > 0)
            {
                subMeshes.Add(new SubMeshRange
                {
                    StartIndex = startTriangleIndex,
                    IndexCount = barrierIndexCount,
                    MaterialType = 1 // Barrier material
                });
            }
        }

        /// <summary>
        /// Generates lane marking geometry (lines between lanes).
        /// </summary>
        private void GenerateLaneMarkings(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            HermiteSpline spline,
            int lengthSegments)
        {
            int startTriangleIndex = triangles.Length;

            // Lane boundaries (interior dashed lines)
            float[] interiorOffsets = { -1.0f * LaneWidth, 0f, 1.0f * LaneWidth };
            // Edge lines (solid)
            float[] edgeOffsets = { -2.0f * LaneWidth, 2.0f * LaneWidth };

            // Dashing parameters
            const float dashLength = 3.0f;
            const float gapLength = 6.0f;
            const float lineHeight = 0.01f; // Slightly above road

            // Generate interior dashed lane lines
            foreach (float laneOffset in interiorOffsets)
            {
                float currentZ = 0f;
                bool isDash = true;

                while (currentZ < 1f)
                {
                    float segmentLength = isDash ? dashLength : gapLength;
                    float tStart = currentZ;
                    float tEnd = math.min(currentZ + segmentLength / 200f, 1f); // Assuming 200m segment

                    if (isDash)
                    {
                        GenerateLineQuad(vertices, triangles, spline, tStart, tEnd,
                            laneOffset, LaneLineWidth, lineHeight, LaneLineColor);
                    }

                    currentZ = tEnd;
                    isDash = !isDash;
                }
            }

            // Generate solid edge lines
            foreach (float edgeOffset in edgeOffsets)
            {
                GenerateLineQuad(vertices, triangles, spline, 0f, 1f,
                    edgeOffset, EdgeLineWidth, lineHeight, EdgeLineColor);
            }

            // Add lane markings sub-mesh range
            int markingsIndexCount = triangles.Length - startTriangleIndex;
            if (markingsIndexCount > 0)
            {
                subMeshes.Add(new SubMeshRange
                {
                    StartIndex = startTriangleIndex,
                    IndexCount = markingsIndexCount,
                    MaterialType = 2 // Lane marking material
                });
            }
        }

        /// <summary>
        /// Generates a single line quad along the spline.
        /// </summary>
        private void GenerateLineQuad(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            HermiteSpline spline,
            float tStart,
            float tEnd,
            float lateralOffset,
            float lineWidth,
            float height,
            float4 color)
        {
            int baseIdx = vertices.Length;

            // Start position
            SplineUtilities.BuildFrameAtT(
                spline.P0, spline.T0, spline.P1, spline.T1, tStart,
                out float3 posStart, out float3 fwdStart, out float3 rightStart, out float3 upStart);

            // End position
            SplineUtilities.BuildFrameAtT(
                spline.P0, spline.T0, spline.P1, spline.T1, tEnd,
                out float3 posEnd, out float3 fwdEnd, out float3 rightEnd, out float3 upEnd);

            float3 startCenter = posStart + rightStart * lateralOffset + upStart * height;
            float3 endCenter = posEnd + rightEnd * lateralOffset + upEnd * height;

            float halfWidth = lineWidth / 2f;

            // Start left
            vertices.Add(new MeshVertex
            {
                Position = startCenter - rightStart * halfWidth,
                Normal = upStart,
                UV = new float2(0f, 0f),
                Color = color
            });

            // Start right
            vertices.Add(new MeshVertex
            {
                Position = startCenter + rightStart * halfWidth,
                Normal = upStart,
                UV = new float2(1f, 0f),
                Color = color
            });

            // End right
            vertices.Add(new MeshVertex
            {
                Position = endCenter + rightEnd * halfWidth,
                Normal = upEnd,
                UV = new float2(1f, 1f),
                Color = color
            });

            // End left
            vertices.Add(new MeshVertex
            {
                Position = endCenter - rightEnd * halfWidth,
                Normal = upEnd,
                UV = new float2(0f, 1f),
                Color = color
            });

            // Two triangles for the quad
            triangles.Add(new MeshTriangle { Index = baseIdx + 0 });
            triangles.Add(new MeshTriangle { Index = baseIdx + 1 });
            triangles.Add(new MeshTriangle { Index = baseIdx + 2 });
            triangles.Add(new MeshTriangle { Index = baseIdx + 0 });
            triangles.Add(new MeshTriangle { Index = baseIdx + 2 });
            triangles.Add(new MeshTriangle { Index = baseIdx + 3 });
        }

        /// <summary>
        /// Generates tunnel enclosure mesh (walls and ceiling).
        /// </summary>
        private void GenerateTunnelMesh(
            ref EntityCommandBuffer ecb,
            Entity entity,
            HermiteSpline spline,
            TrackSegment segment,
            int lengthSegments)
        {
            const float tunnelHeight = 6f;
            const float tunnelWallOffset = TotalWidth / 2f + 0.5f;
            const int archSegments = 8;

            // Add tunnel mesh config
            ecb.AddComponent(entity, new TunnelMeshConfig
            {
                WallHeight = tunnelHeight,
                CeilingHeight = tunnelHeight,
                WallThickness = 0.5f,
                GenerateLightStrips = true,
                LightStripSpacing = 20f
            });

            // Note: Tunnel geometry is generated as a separate mesh entity
            // to allow for different materials (darker interior walls)
            // The TunnelMeshConfig component signals to a dedicated
            // TunnelMeshGenerationSystem to create the actual geometry

            // Create tunnel geometry entity
            Entity tunnelMeshEntity = ecb.CreateEntity();
            ecb.AddComponent(tunnelMeshEntity, new TunnelGeometry
            {
                ParentSegment = entity,
                WallHeight = tunnelHeight,
                WallOffset = tunnelWallOffset,
                ArchSegments = archSegments,
                LengthSegments = lengthSegments
            });
            ecb.AddComponent(tunnelMeshEntity, spline);
            ecb.AddComponent(tunnelMeshEntity, new TunnelMeshTag());
        }

        /// <summary>
        /// Generates overpass elevation and support structure mesh.
        /// </summary>
        private void GenerateOverpassMesh(
            ref EntityCommandBuffer ecb,
            Entity entity,
            HermiteSpline spline,
            TrackSegment segment,
            int lengthSegments)
        {
            // Overpass uses sinusoidal elevation: h(t) = A * sin(π * t)
            const float elevationAmplitude = 8f;
            const float pillarSpacing = 40f;
            const float pillarWidth = 1.5f;
            const float barrierHeight = 1.2f;

            // Add overpass mesh config
            ecb.AddComponent(entity, new OverpassMeshConfig
            {
                BarrierHeight = barrierHeight,
                PillarWidth = pillarWidth,
                PillarSpacing = pillarSpacing,
                GeneratePillars = true
            });

            // Calculate number of support pillars
            int numPillars = math.max(1, (int)(segment.Length / pillarSpacing));

            // Create pillar entities along the overpass
            for (int i = 0; i < numPillars; i++)
            {
                float t = (i + 0.5f) / numPillars;

                SplineUtilities.BuildFrameAtT(
                    spline.P0, spline.T0, spline.P1, spline.T1, t,
                    out float3 position, out float3 forward, out float3 right, out float3 up);

                // Calculate elevation at this point
                float elevation = elevationAmplitude * math.sin(math.PI * t);

                // Create pillar entity
                Entity pillarEntity = ecb.CreateEntity();
                ecb.AddComponent(pillarEntity, new PillarGeometry
                {
                    ParentSegment = entity,
                    Position = position,
                    Height = elevation,
                    Width = pillarWidth,
                    Forward = forward,
                    Right = right
                });
                ecb.AddComponent(pillarEntity, new OverpassPillarTag());
            }

            // Create overpass geometry entity for elevated barriers
            Entity overpassGeomEntity = ecb.CreateEntity();
            ecb.AddComponent(overpassGeomEntity, new OverpassGeometry
            {
                ParentSegment = entity,
                ElevationAmplitude = elevationAmplitude,
                BarrierHeight = barrierHeight,
                LengthSegments = lengthSegments
            });
            ecb.AddComponent(overpassGeomEntity, spline);
            ecb.AddComponent(overpassGeomEntity, new OverpassMeshTag());
        }

        /// <summary>
        /// Calculates axis-aligned bounding box for the segment.
        /// </summary>
        private MeshBounds CalculateBounds(HermiteSpline spline, TrackSegment segment)
        {
            // Sample spline to find extents
            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);

            const int samples = 10;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                SplineUtilities.BuildFrameAtT(
                    spline.P0, spline.T0, spline.P1, spline.T1, t,
                    out float3 pos, out float3 fwd, out float3 right, out float3 up);

                // Include road width extents
                float3 left = pos - right * (TotalWidth / 2f);
                float3 rightEdge = pos + right * (TotalWidth / 2f);

                min = math.min(min, left);
                min = math.min(min, rightEdge);
                max = math.max(max, left);
                max = math.max(max, rightEdge);
            }

            // Add vertical padding for barriers/tunnels
            min.y -= 1f;
            max.y += 10f; // Account for tunnel height

            float3 center = (min + max) * 0.5f;
            float3 extents = (max - min) * 0.5f;

            return new MeshBounds
            {
                Min = min,
                Max = max,
                Center = center,
                Extents = extents
            };
        }
    }

    /// <summary>
    /// Generates detailed road marking geometry with dashing.
    /// Runs after initial mesh generation for additional detail.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProceduralRoadMeshSystem))]
    public partial struct RoadMarkingSystem : ISystem
    {
        // Marking parameters
        private const float DashLength = 3f;
        private const float DashGap = 6f;
        private const float LaneLineWidth = 0.15f;
        private const float EdgeLineWidth = 0.2f;

        // Colors
        private static readonly float4 WhiteLine = new float4(1f, 1f, 1f, 1f);
        private static readonly float4 YellowLine = new float4(1f, 0.9f, 0.2f, 1f);
        private static readonly float4 BlueLane = new float4(0.27f, 0.53f, 1f, 0.8f);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TrackSegmentTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Road markings are generated as additional vertex data
            // overlaid on top of the existing road mesh buffers

            foreach (var (meshData, segment, spline, vertices, triangles, subMeshes, entity) in
                SystemAPI.Query<RefRW<ProceduralMeshData>, RefRO<TrackSegment>, RefRO<HermiteSpline>,
                    DynamicBuffer<MeshVertex>, DynamicBuffer<MeshTriangle>, DynamicBuffer<SubMeshRange>>()
                    .WithAll<TrackSegmentTag>()
                    .WithEntityAccess())
            {
                if (!meshData.ValueRO.IsGenerated)
                    continue;

                // Skip if markings already generated for this segment
                if (meshData.ValueRO.MarkingsGenerated)
                    continue;

                // Generate lane line vertices
                // These overlay on top of the road surface
                GenerateDashedLines(vertices, triangles, subMeshes, spline.ValueRO, segment.ValueRO);

                // Mark as generated
                meshData.ValueRW.MarkingsGenerated = true;
            }
        }

        private void GenerateDashedLines(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            HermiteSpline spline,
            TrackSegment segment)
        {
            int startTriangleIndex = triangles.Length;

            // Lane boundaries for 4 lanes (interior dividers at lane edges):
            // Lane centers are at: -1.5, -0.5, +0.5, +1.5 lane widths
            // So boundaries between lanes are at: -1.0, 0, +1.0 lane widths
            const float laneWidth = 3.6f;
            float[] interiorOffsets = { -1.0f * laneWidth, 0f, 1.0f * laneWidth };
            float[] edgeOffsets = { -2.0f * laneWidth, 2.0f * laneWidth };

            float totalLength = segment.Length;
            const float lineHeight = 0.02f; // Slightly above road surface

            // Generate interior dashed lane lines (white)
            foreach (float laneOffset in interiorOffsets)
            {
                float currentPos = 0f;
                bool isDash = true;

                while (currentPos < totalLength)
                {
                    float segmentLen = isDash ? DashLength : DashGap;
                    float nextPos = math.min(currentPos + segmentLen, totalLength);

                    if (isDash)
                    {
                        // Convert world positions to t parameter (0-1)
                        float tStart = currentPos / totalLength;
                        float tEnd = nextPos / totalLength;

                        GenerateLineQuad(vertices, triangles, spline, tStart, tEnd,
                            laneOffset, LaneLineWidth, lineHeight, WhiteLine);
                    }

                    currentPos = nextPos;
                    isDash = !isDash;
                }
            }

            // Generate solid edge lines (yellow)
            foreach (float edgeOffset in edgeOffsets)
            {
                GenerateLineQuad(vertices, triangles, spline, 0f, 1f,
                    edgeOffset, EdgeLineWidth, lineHeight, YellowLine);
            }

            // Add sub-mesh range for the markings
            int markingsIndexCount = triangles.Length - startTriangleIndex;
            if (markingsIndexCount > 0)
            {
                subMeshes.Add(new SubMeshRange
                {
                    StartIndex = startTriangleIndex,
                    IndexCount = markingsIndexCount,
                    MaterialType = 2 // Lane marking material
                });
            }
        }

        private void GenerateLineQuad(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            HermiteSpline spline,
            float tStart,
            float tEnd,
            float lateralOffset,
            float lineWidth,
            float height,
            float4 color)
        {
            int baseIdx = vertices.Length;

            // Start position
            SplineUtilities.BuildFrameAtT(
                spline.P0, spline.T0, spline.P1, spline.T1, tStart,
                out float3 posStart, out float3 fwdStart, out float3 rightStart, out float3 upStart);

            // End position
            SplineUtilities.BuildFrameAtT(
                spline.P0, spline.T0, spline.P1, spline.T1, tEnd,
                out float3 posEnd, out float3 fwdEnd, out float3 rightEnd, out float3 upEnd);

            float3 startCenter = posStart + rightStart * lateralOffset + upStart * height;
            float3 endCenter = posEnd + rightEnd * lateralOffset + upEnd * height;

            float halfWidth = lineWidth / 2f;

            // Start left
            vertices.Add(new MeshVertex
            {
                Position = startCenter - rightStart * halfWidth,
                Normal = upStart,
                UV = new float2(0f, 0f),
                Color = color
            });

            // Start right
            vertices.Add(new MeshVertex
            {
                Position = startCenter + rightStart * halfWidth,
                Normal = upStart,
                UV = new float2(1f, 0f),
                Color = color
            });

            // End right
            vertices.Add(new MeshVertex
            {
                Position = endCenter + rightEnd * halfWidth,
                Normal = upEnd,
                UV = new float2(1f, 1f),
                Color = color
            });

            // End left
            vertices.Add(new MeshVertex
            {
                Position = endCenter - rightEnd * halfWidth,
                Normal = upEnd,
                UV = new float2(0f, 1f),
                Color = color
            });

            // Two triangles for the quad
            triangles.Add(new MeshTriangle { Index = baseIdx + 0 });
            triangles.Add(new MeshTriangle { Index = baseIdx + 1 });
            triangles.Add(new MeshTriangle { Index = baseIdx + 2 });
            triangles.Add(new MeshTriangle { Index = baseIdx + 0 });
            triangles.Add(new MeshTriangle { Index = baseIdx + 2 });
            triangles.Add(new MeshTriangle { Index = baseIdx + 3 });
        }
    }
}
