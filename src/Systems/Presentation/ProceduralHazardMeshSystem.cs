// ============================================================================
// Nightflow - Procedural Hazard Mesh Generation System
// Generates traffic cones, debris, barriers, and other hazard geometry
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Component for hazard mesh generation state.
    /// </summary>
    public struct HazardMeshData : IComponentData
    {
        /// <summary>Whether mesh has been generated.</summary>
        public bool IsGenerated;

        /// <summary>Number of vertices in the mesh.</summary>
        public int VertexCount;

        /// <summary>Number of triangles in the mesh.</summary>
        public int TriangleCount;

        /// <summary>Base glow intensity for wireframe rendering.</summary>
        public float GlowIntensity;
    }

    /// <summary>
    /// Generates procedural mesh geometry for road hazards.
    /// Traffic cones, debris piles, loose tires, barriers, crashed cars.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProceduralRoadMeshSystem))]
    public partial struct ProceduralHazardMeshSystem : ISystem
    {
        // =====================================================================
        // Neon Color Palette
        // =====================================================================

        // Traffic cone: Neon Orange
        private static readonly float4 ConeColor = new float4(1f, 0.533f, 0f, 1f);           // #FF8800
        private static readonly float4 ConeStripeColor = new float4(1f, 1f, 1f, 0.9f);       // White stripes

        // Debris: Dim orange/yellow
        private static readonly float4 DebrisColor = new float4(0.8f, 0.6f, 0.2f, 1f);

        // Loose tire: Dark gray with subtle glow
        private static readonly float4 TireColor = new float4(0.3f, 0.3f, 0.35f, 1f);

        // Barrier: Bright orange warning
        private static readonly float4 BarrierColor = new float4(1f, 0.4f, 0f, 1f);          // #FF6600

        // Crashed car: Magenta (same as traffic)
        private static readonly float4 CrashedCarColor = new float4(1f, 0f, 1f, 0.7f);       // #FF00FF dimmed

        // =====================================================================
        // Cone Geometry Parameters
        // =====================================================================

        private const float ConeHeight = 0.7f;              // 70cm tall
        private const float ConeBaseRadius = 0.2f;          // 20cm base radius
        private const float ConeTopRadius = 0.03f;          // 3cm top (slightly rounded)
        private const int ConeRadialSegments = 8;           // Octagonal for wireframe look
        private const int ConeHeightSegments = 3;           // Segments for stripe bands

        // Stripe positions (as fraction of height)
        private const float StripeStart1 = 0.2f;
        private const float StripeEnd1 = 0.4f;
        private const float StripeStart2 = 0.6f;
        private const float StripeEnd2 = 0.8f;

        // =====================================================================
        // Other Hazard Parameters
        // =====================================================================

        private const float TireRadius = 0.35f;             // Outer radius
        private const float TireWidth = 0.25f;              // Tire width
        private const float TireInnerRadius = 0.15f;        // Inner hole
        private const int TireSegments = 12;

        private const float BarrierHeight = 0.9f;
        private const float BarrierWidth = 0.6f;
        private const float BarrierLength = 1.5f;

        private const float DebrisRadius = 0.4f;
        private const int DebrisPoints = 6;                 // Irregular polygon

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HazardTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (hazard, meshData, transform, verticesRO, trianglesRO, subMeshesRO, entity) in
                SystemAPI.Query<RefRO<Hazard>, RefRW<HazardMeshData>, RefRO<WorldTransform>,
                                DynamicBuffer<MeshVertex>, DynamicBuffer<MeshTriangle>, DynamicBuffer<SubMeshRange>>()
                    .WithAll<HazardTag>()
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

                // Generate mesh based on hazard type
                switch (hazard.ValueRO.Type)
                {
                    case HazardType.Cone:
                        GenerateConeMesh(vertices, triangles, subMeshes, ref meshData.ValueRW);
                        break;
                    case HazardType.LooseTire:
                        GenerateTireMesh(vertices, triangles, subMeshes, ref meshData.ValueRW);
                        break;
                    case HazardType.Debris:
                        GenerateDebrisMesh(vertices, triangles, subMeshes, ref meshData.ValueRW);
                        break;
                    case HazardType.Barrier:
                        GenerateBarrierMesh(vertices, triangles, subMeshes, ref meshData.ValueRW);
                        break;
                    case HazardType.CrashedCar:
                        GenerateCrashedCarMesh(vertices, triangles, subMeshes, ref meshData.ValueRW);
                        break;
                }

                meshData.ValueRW.IsGenerated = true;
            }
        }

        /// <summary>
        /// Generates a neon orange wireframe traffic cone with white stripes.
        /// </summary>
        private void GenerateConeMesh(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            ref HazardMeshData meshData)
        {
            // Cone vertices: base ring + height segment rings + apex
            // Each ring has ConeRadialSegments vertices
            int ringsCount = ConeHeightSegments + 2; // base + middle rings + top
            int vertexCount = ConeRadialSegments * ringsCount + 1; // +1 for apex
            int triangleCount = ConeRadialSegments * 2 * (ringsCount - 1) + ConeRadialSegments; // sides + top cap
            int indexCount = triangleCount * 3;

            meshData.VertexCount = vertexCount;
            meshData.TriangleCount = triangleCount;
            meshData.GlowIntensity = 1.2f; // Bright neon glow

            vertices.EnsureCapacity(vertexCount);
            triangles.EnsureCapacity(indexCount);

            // Generate rings from bottom to top
            for (int ring = 0; ring < ringsCount; ring++)
            {
                float t = ring / (float)(ringsCount - 1); // 0 at base, 1 at top
                float y = t * ConeHeight;
                float radius = math.lerp(ConeBaseRadius, ConeTopRadius, t);

                // Determine if this height is in a stripe band
                bool inStripe = (t >= StripeStart1 && t <= StripeEnd1) ||
                               (t >= StripeStart2 && t <= StripeEnd2);
                float4 ringColor = inStripe ? ConeStripeColor : ConeColor;

                // Generate vertices around the ring
                for (int i = 0; i < ConeRadialSegments; i++)
                {
                    float angle = (i / (float)ConeRadialSegments) * math.PI * 2f;
                    float x = math.cos(angle) * radius;
                    float z = math.sin(angle) * radius;

                    // Calculate normal (pointing outward and slightly up)
                    float3 normal = math.normalize(new float3(
                        math.cos(angle),
                        (ConeBaseRadius - ConeTopRadius) / ConeHeight,
                        math.sin(angle)
                    ));

                    vertices.Add(new MeshVertex
                    {
                        Position = new float3(x, y, z),
                        Normal = normal,
                        UV = new float2(i / (float)ConeRadialSegments, t),
                        Color = ringColor
                    });
                }
            }

            // Add apex vertex
            vertices.Add(new MeshVertex
            {
                Position = new float3(0, ConeHeight, 0),
                Normal = new float3(0, 1, 0),
                UV = new float2(0.5f, 1f),
                Color = ConeColor
            });

            // Generate side triangles connecting rings
            for (int ring = 0; ring < ringsCount - 1; ring++)
            {
                int ringStart = ring * ConeRadialSegments;
                int nextRingStart = (ring + 1) * ConeRadialSegments;

                for (int i = 0; i < ConeRadialSegments; i++)
                {
                    int nextI = (i + 1) % ConeRadialSegments;

                    int bl = ringStart + i;
                    int br = ringStart + nextI;
                    int tl = nextRingStart + i;
                    int tr = nextRingStart + nextI;

                    // Triangle 1
                    triangles.Add(new MeshTriangle { Index = bl });
                    triangles.Add(new MeshTriangle { Index = tl });
                    triangles.Add(new MeshTriangle { Index = tr });

                    // Triangle 2
                    triangles.Add(new MeshTriangle { Index = bl });
                    triangles.Add(new MeshTriangle { Index = tr });
                    triangles.Add(new MeshTriangle { Index = br });
                }
            }

            // Generate base cap (bottom ring to center)
            int baseCenterIndex = vertexCount; // We'd need to add a center vertex
            // For wireframe, we can skip the base cap or add it

            // Add sub-mesh range
            subMeshes.Add(new SubMeshRange
            {
                StartIndex = 0,
                IndexCount = indexCount,
                MaterialType = 5 // Hazard material
            });
        }

        /// <summary>
        /// Generates a loose tire mesh (torus shape).
        /// </summary>
        private void GenerateTireMesh(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            ref HazardMeshData meshData)
        {
            // Simplified tire: just the outer ring for wireframe
            int segments = TireSegments;
            int ringSegments = 6; // Cross-section segments
            int vertexCount = segments * ringSegments;
            int triangleCount = segments * ringSegments * 2;

            meshData.VertexCount = vertexCount;
            meshData.TriangleCount = triangleCount;
            meshData.GlowIntensity = 0.6f;

            float tubeRadius = (TireRadius - TireInnerRadius) * 0.5f;
            float centerRadius = TireInnerRadius + tubeRadius;

            // Generate torus vertices
            for (int i = 0; i < segments; i++)
            {
                float u = (i / (float)segments) * math.PI * 2f;
                float3 ringCenter = new float3(math.cos(u) * centerRadius, tubeRadius, math.sin(u) * centerRadius);

                for (int j = 0; j < ringSegments; j++)
                {
                    float v = (j / (float)ringSegments) * math.PI * 2f;

                    float3 offset = new float3(
                        math.cos(u) * math.cos(v) * tubeRadius,
                        math.sin(v) * tubeRadius,
                        math.sin(u) * math.cos(v) * tubeRadius
                    );

                    float3 pos = ringCenter + offset;
                    float3 normal = math.normalize(offset);

                    vertices.Add(new MeshVertex
                    {
                        Position = pos,
                        Normal = normal,
                        UV = new float2(i / (float)segments, j / (float)ringSegments),
                        Color = TireColor
                    });
                }
            }

            // Generate triangles
            for (int i = 0; i < segments; i++)
            {
                int nextI = (i + 1) % segments;
                for (int j = 0; j < ringSegments; j++)
                {
                    int nextJ = (j + 1) % ringSegments;

                    int v0 = i * ringSegments + j;
                    int v1 = i * ringSegments + nextJ;
                    int v2 = nextI * ringSegments + j;
                    int v3 = nextI * ringSegments + nextJ;

                    triangles.Add(new MeshTriangle { Index = v0 });
                    triangles.Add(new MeshTriangle { Index = v2 });
                    triangles.Add(new MeshTriangle { Index = v3 });

                    triangles.Add(new MeshTriangle { Index = v0 });
                    triangles.Add(new MeshTriangle { Index = v3 });
                    triangles.Add(new MeshTriangle { Index = v1 });
                }
            }

            subMeshes.Add(new SubMeshRange
            {
                StartIndex = 0,
                IndexCount = triangleCount * 3,
                MaterialType = 5
            });
        }

        /// <summary>
        /// Generates an irregular debris pile mesh.
        /// </summary>
        private void GenerateDebrisMesh(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            ref HazardMeshData meshData)
        {
            // Simple irregular polygon extruded slightly
            int vertexCount = DebrisPoints * 2; // Top and bottom rings
            int triangleCount = DebrisPoints * 4; // Sides + top + bottom

            meshData.VertexCount = vertexCount;
            meshData.TriangleCount = triangleCount;
            meshData.GlowIntensity = 0.8f;

            // Generate irregular shape using hash-based offsets
            uint seed = 42u;
            float height = 0.25f;

            // Bottom ring
            for (int i = 0; i < DebrisPoints; i++)
            {
                float angle = (i / (float)DebrisPoints) * math.PI * 2f;
                uint hash = (seed * 31u + (uint)i) * 0x85ebca6b;
                float radiusVar = 0.7f + (hash % 1000) / 1000f * 0.6f;
                float radius = DebrisRadius * radiusVar;

                vertices.Add(new MeshVertex
                {
                    Position = new float3(math.cos(angle) * radius, 0, math.sin(angle) * radius),
                    Normal = new float3(0, -1, 0),
                    UV = new float2(i / (float)DebrisPoints, 0),
                    Color = DebrisColor
                });
            }

            // Top ring (with height variation)
            for (int i = 0; i < DebrisPoints; i++)
            {
                float angle = (i / (float)DebrisPoints) * math.PI * 2f;
                uint hash = (seed * 31u + (uint)i) * 0x85ebca6b;
                float radiusVar = 0.6f + (hash % 1000) / 1000f * 0.5f;
                float heightVar = 0.8f + ((hash >> 10) % 1000) / 1000f * 0.4f;
                float radius = DebrisRadius * radiusVar * 0.8f;

                vertices.Add(new MeshVertex
                {
                    Position = new float3(math.cos(angle) * radius, height * heightVar, math.sin(angle) * radius),
                    Normal = new float3(0, 1, 0),
                    UV = new float2(i / (float)DebrisPoints, 1),
                    Color = DebrisColor
                });
            }

            // Side triangles
            for (int i = 0; i < DebrisPoints; i++)
            {
                int nextI = (i + 1) % DebrisPoints;
                int bl = i;
                int br = nextI;
                int tl = i + DebrisPoints;
                int tr = nextI + DebrisPoints;

                triangles.Add(new MeshTriangle { Index = bl });
                triangles.Add(new MeshTriangle { Index = tl });
                triangles.Add(new MeshTriangle { Index = tr });

                triangles.Add(new MeshTriangle { Index = bl });
                triangles.Add(new MeshTriangle { Index = tr });
                triangles.Add(new MeshTriangle { Index = br });
            }

            subMeshes.Add(new SubMeshRange
            {
                StartIndex = 0,
                IndexCount = DebrisPoints * 6,
                MaterialType = 5
            });
        }

        /// <summary>
        /// Generates a barrier block mesh.
        /// </summary>
        private void GenerateBarrierMesh(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            ref HazardMeshData meshData)
        {
            // Simple box with beveled edges for wireframe
            int vertexCount = 24; // 4 vertices per face × 6 faces
            int triangleCount = 12; // 2 triangles per face × 6 faces

            meshData.VertexCount = vertexCount;
            meshData.TriangleCount = triangleCount;
            meshData.GlowIntensity = 1.0f;

            float hw = BarrierWidth * 0.5f;
            float hh = BarrierHeight * 0.5f;
            float hl = BarrierLength * 0.5f;

            // Generate box vertices (8 corners)
            float3[] corners = new float3[8]
            {
                new float3(-hw, 0, -hl),           // 0: front-left-bottom
                new float3(hw, 0, -hl),            // 1: front-right-bottom
                new float3(hw, 0, hl),             // 2: back-right-bottom
                new float3(-hw, 0, hl),            // 3: back-left-bottom
                new float3(-hw, BarrierHeight, -hl), // 4: front-left-top
                new float3(hw, BarrierHeight, -hl),  // 5: front-right-top
                new float3(hw, BarrierHeight, hl),   // 6: back-right-top
                new float3(-hw, BarrierHeight, hl),  // 7: back-left-top
            };

            // Add diagonal warning stripes via color variation
            // Front face
            AddQuad(vertices, triangles, corners[0], corners[1], corners[5], corners[4],
                   new float3(0, 0, -1), BarrierColor);
            // Back face
            AddQuad(vertices, triangles, corners[2], corners[3], corners[7], corners[6],
                   new float3(0, 0, 1), BarrierColor);
            // Left face
            AddQuad(vertices, triangles, corners[3], corners[0], corners[4], corners[7],
                   new float3(-1, 0, 0), BarrierColor);
            // Right face
            AddQuad(vertices, triangles, corners[1], corners[2], corners[6], corners[5],
                   new float3(1, 0, 0), BarrierColor);
            // Top face
            AddQuad(vertices, triangles, corners[4], corners[5], corners[6], corners[7],
                   new float3(0, 1, 0), BarrierColor);
            // Bottom face
            AddQuad(vertices, triangles, corners[3], corners[2], corners[1], corners[0],
                   new float3(0, -1, 0), BarrierColor);

            subMeshes.Add(new SubMeshRange
            {
                StartIndex = 0,
                IndexCount = 36,
                MaterialType = 5
            });
        }

        /// <summary>
        /// Generates a crashed car mesh (simplified boxy vehicle shape).
        /// </summary>
        private void GenerateCrashedCarMesh(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            ref HazardMeshData meshData)
        {
            // Simplified crashed vehicle: tilted box with deformation
            // Similar to barrier but larger and with damage appearance

            float carLength = 4.5f;
            float carWidth = 1.8f;
            float carHeight = 1.4f;

            int vertexCount = 24;
            int triangleCount = 12;

            meshData.VertexCount = vertexCount;
            meshData.TriangleCount = triangleCount;
            meshData.GlowIntensity = 0.7f; // Dimmer glow for crashed vehicle

            float hw = carWidth * 0.5f;
            float hh = carHeight;
            float hl = carLength * 0.5f;

            // Add slight tilt for crashed appearance
            float tiltAngle = 0.15f; // radians
            float3 tiltOffset = new float3(0.1f, 0, 0);

            float3[] corners = new float3[8]
            {
                new float3(-hw, 0, -hl) + tiltOffset,
                new float3(hw, 0, -hl),
                new float3(hw, 0, hl),
                new float3(-hw, 0, hl) + tiltOffset,
                new float3(-hw + 0.1f, hh * 0.9f, -hl + 0.2f),
                new float3(hw - 0.05f, hh, -hl),
                new float3(hw, hh * 0.95f, hl - 0.1f),
                new float3(-hw + 0.15f, hh * 0.85f, hl),
            };

            // Generate faces with crashed car color
            AddQuad(vertices, triangles, corners[0], corners[1], corners[5], corners[4],
                   new float3(0, 0, -1), CrashedCarColor);
            AddQuad(vertices, triangles, corners[2], corners[3], corners[7], corners[6],
                   new float3(0, 0, 1), CrashedCarColor);
            AddQuad(vertices, triangles, corners[3], corners[0], corners[4], corners[7],
                   new float3(-1, 0, 0), CrashedCarColor);
            AddQuad(vertices, triangles, corners[1], corners[2], corners[6], corners[5],
                   new float3(1, 0, 0), CrashedCarColor);
            AddQuad(vertices, triangles, corners[4], corners[5], corners[6], corners[7],
                   new float3(0, 1, 0), CrashedCarColor);
            AddQuad(vertices, triangles, corners[3], corners[2], corners[1], corners[0],
                   new float3(0, -1, 0), CrashedCarColor);

            subMeshes.Add(new SubMeshRange
            {
                StartIndex = 0,
                IndexCount = 36,
                MaterialType = 5
            });
        }

        /// <summary>
        /// Helper to add a quad (two triangles) to the mesh buffers.
        /// </summary>
        private static void AddQuad(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float3 v0, float3 v1, float3 v2, float3 v3,
            float3 normal, float4 color)
        {
            int baseIndex = vertices.Length;

            vertices.Add(new MeshVertex { Position = v0, Normal = normal, UV = new float2(0, 0), Color = color });
            vertices.Add(new MeshVertex { Position = v1, Normal = normal, UV = new float2(1, 0), Color = color });
            vertices.Add(new MeshVertex { Position = v2, Normal = normal, UV = new float2(1, 1), Color = color });
            vertices.Add(new MeshVertex { Position = v3, Normal = normal, UV = new float2(0, 1), Color = color });

            // Triangle 1
            triangles.Add(new MeshTriangle { Index = baseIndex });
            triangles.Add(new MeshTriangle { Index = baseIndex + 1 });
            triangles.Add(new MeshTriangle { Index = baseIndex + 2 });

            // Triangle 2
            triangles.Add(new MeshTriangle { Index = baseIndex });
            triangles.Add(new MeshTriangle { Index = baseIndex + 2 });
            triangles.Add(new MeshTriangle { Index = baseIndex + 3 });
        }
    }
}
