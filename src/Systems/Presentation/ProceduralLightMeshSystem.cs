// ============================================================================
// Nightflow - Procedural Light Mesh Generation System
// Generates streetlights, tunnel lights, and other environmental lighting
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
    /// Component for light fixture mesh generation state.
    /// </summary>
    public struct LightFixtureMeshData : IComponentData
    {
        /// <summary>Whether mesh has been generated.</summary>
        public bool IsGenerated;

        /// <summary>Light fixture type: 0=streetlight, 1=tunnel, 2=overpass, 3=billboard.</summary>
        public int FixtureType;

        /// <summary>Pole height (for streetlights).</summary>
        public float PoleHeight;

        /// <summary>Arm length (for streetlights).</summary>
        public float ArmLength;
    }

    /// <summary>
    /// Generates procedural mesh geometry for light fixtures.
    /// Streetlights, tunnel lights, and other environmental lighting elements.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProceduralVehicleMeshSystem))]
    public partial struct ProceduralLightMeshSystem : ISystem
    {
        // =====================================================================
        // Light Colors
        // =====================================================================

        // Streetlight: Warm sodium vapor
        private static readonly float4 SodiumColor = new float4(1f, 0.82f, 0.5f, 1f);         // #FFD080

        // Tunnel light: Cool fluorescent
        private static readonly float4 FluorescentColor = new float4(0.9f, 0.95f, 1f, 1f);

        // Pole/structure: Dark metallic
        private static readonly float4 PoleColor = new float4(0.25f, 0.25f, 0.3f, 1f);

        // =====================================================================
        // Streetlight Dimensions
        // =====================================================================

        private const float DefaultPoleHeight = 8f;
        private const float PoleRadius = 0.08f;
        private const float ArmLength = 2.5f;
        private const float ArmRadius = 0.05f;
        private const float FixtureLength = 0.6f;
        private const float FixtureWidth = 0.3f;
        private const float FixtureHeight = 0.15f;

        // =====================================================================
        // Tunnel Light Dimensions
        // =====================================================================

        private const float TunnelLightWidth = 1.2f;
        private const float TunnelLightHeight = 0.1f;
        private const float TunnelLightDepth = 0.15f;

        private const int PoleSegments = 6; // Hexagonal pole

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LightSourceTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (meshData, lightEmitter, entity) in
                SystemAPI.Query<RefRW<LightFixtureMeshData>, RefRO<LightEmitter>>()
                    .WithAll<LightSourceTag>()
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

                switch (meshData.ValueRO.FixtureType)
                {
                    case 0: // Streetlight
                        GenerateStreetlightMesh(vertices, triangles, subMeshes,
                                               meshData.ValueRO.PoleHeight,
                                               meshData.ValueRO.ArmLength, lightEmitter.ValueRO.Color);
                        break;
                    case 1: // Tunnel light
                        GenerateTunnelLightMesh(vertices, triangles, subMeshes, lightEmitter.ValueRO.Color);
                        break;
                    case 2: // Overpass light
                        GenerateOverpassLightMesh(vertices, triangles, subMeshes, lightEmitter.ValueRO.Color);
                        break;
                }

                meshData.ValueRW.IsGenerated = true;
            }
        }

        /// <summary>
        /// Generates a streetlight with pole, arm, and light fixture.
        /// </summary>
        private void GenerateStreetlightMesh(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            float poleHeight,
            float armLength,
            float3 lightColor)
        {
            if (poleHeight <= 0) poleHeight = DefaultPoleHeight;
            if (armLength <= 0) armLength = ArmLength;

            int poleStartIndex = 0;

            // Generate vertical pole
            GeneratePole(vertices, triangles, float3.zero, poleHeight, PoleRadius, PoleColor);

            // Generate horizontal arm at top
            float3 armStart = new float3(0, poleHeight - 0.1f, 0);
            GenerateArm(vertices, triangles, armStart, armLength, ArmRadius, PoleColor);

            int poleEndIndex = triangles.Length;

            // Add structure sub-mesh
            subMeshes.Add(new SubMeshRange
            {
                StartIndex = poleStartIndex,
                IndexCount = poleEndIndex,
                MaterialType = 9 // Light structure
            });

            int fixtureStartIndex = triangles.Length;

            // Generate light fixture at end of arm
            float3 fixturePos = new float3(armLength, poleHeight - 0.2f, 0);
            float4 glowColor = new float4(lightColor, 1f) * 1.5f;
            GenerateLightFixture(vertices, triangles, fixturePos, glowColor);

            int fixtureEndIndex = triangles.Length;

            // Add fixture sub-mesh
            subMeshes.Add(new SubMeshRange
            {
                StartIndex = fixtureStartIndex,
                IndexCount = fixtureEndIndex - fixtureStartIndex,
                MaterialType = 10 // Light emitter
            });
        }

        /// <summary>
        /// Generates a tunnel ceiling light strip.
        /// </summary>
        private void GenerateTunnelLightMesh(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            float3 lightColor)
        {
            float4 glowColor = new float4(lightColor, 1f);
            if (math.length(lightColor) < 0.1f)
                glowColor = FluorescentColor;

            glowColor *= 1.5f; // Extra bright

            // Simple rectangular light strip
            GenerateBox(vertices, triangles,
                float3.zero,
                new float3(TunnelLightWidth * 0.5f, TunnelLightHeight * 0.5f, TunnelLightDepth * 0.5f),
                glowColor);

            subMeshes.Add(new SubMeshRange
            {
                StartIndex = 0,
                IndexCount = triangles.Length,
                MaterialType = 10
            });
        }

        /// <summary>
        /// Generates an overpass underside light.
        /// </summary>
        private void GenerateOverpassLightMesh(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            float3 lightColor)
        {
            float4 glowColor = new float4(lightColor, 1f);
            if (math.length(lightColor) < 0.1f)
                glowColor = SodiumColor;

            glowColor *= 1.3f;

            // Circular downlight
            GenerateCircularLight(vertices, triangles,
                float3.zero, 0.3f, glowColor, new float3(0, -1, 0));

            subMeshes.Add(new SubMeshRange
            {
                StartIndex = 0,
                IndexCount = triangles.Length,
                MaterialType = 10
            });
        }

        /// <summary>
        /// Generates a hexagonal pole.
        /// </summary>
        private static void GeneratePole(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float3 basePos,
            float height,
            float radius,
            float4 color)
        {
            int baseIndex = vertices.Length;
            int segments = PoleSegments;

            // Bottom ring
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * math.PI * 2f;
                float3 offset = new float3(math.cos(angle) * radius, 0, math.sin(angle) * radius);
                float3 normal = math.normalize(new float3(math.cos(angle), 0, math.sin(angle)));

                vertices.Add(new MeshVertex
                {
                    Position = basePos + offset,
                    Normal = normal,
                    UV = new float2(i / (float)segments, 0),
                    Color = color
                });
            }

            // Top ring (slightly tapered)
            float topRadius = radius * 0.85f;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * math.PI * 2f;
                float3 offset = new float3(math.cos(angle) * topRadius, height, math.sin(angle) * topRadius);
                float3 normal = math.normalize(new float3(math.cos(angle), 0, math.sin(angle)));

                vertices.Add(new MeshVertex
                {
                    Position = basePos + offset,
                    Normal = normal,
                    UV = new float2(i / (float)segments, 1),
                    Color = color
                });
            }

            // Side triangles
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int bl = baseIndex + i;
                int br = baseIndex + next;
                int tl = baseIndex + segments + i;
                int tr = baseIndex + segments + next;

                triangles.Add(new MeshTriangle { Index = bl });
                triangles.Add(new MeshTriangle { Index = tl });
                triangles.Add(new MeshTriangle { Index = tr });

                triangles.Add(new MeshTriangle { Index = bl });
                triangles.Add(new MeshTriangle { Index = tr });
                triangles.Add(new MeshTriangle { Index = br });
            }
        }

        /// <summary>
        /// Generates a horizontal arm extending from the pole.
        /// </summary>
        private static void GenerateArm(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float3 startPos,
            float length,
            float radius,
            float4 color)
        {
            int baseIndex = vertices.Length;
            int segments = 4; // Square cross-section for arm

            // Curved arm path (slight droop)
            int armSegments = 4;
            for (int s = 0; s <= armSegments; s++)
            {
                float t = s / (float)armSegments;
                float x = t * length;
                float y = -t * t * 0.3f; // Slight downward curve
                float3 center = startPos + new float3(x, y, 0);

                float r = radius * (1f - t * 0.2f); // Slight taper

                // 4 vertices per cross-section
                vertices.Add(new MeshVertex { Position = center + new float3(0, r, 0), Normal = new float3(0, 1, 0), UV = new float2(t, 0), Color = color });
                vertices.Add(new MeshVertex { Position = center + new float3(0, -r, 0), Normal = new float3(0, -1, 0), UV = new float2(t, 0.25f), Color = color });
                vertices.Add(new MeshVertex { Position = center + new float3(0, 0, r), Normal = new float3(0, 0, 1), UV = new float2(t, 0.5f), Color = color });
                vertices.Add(new MeshVertex { Position = center + new float3(0, 0, -r), Normal = new float3(0, 0, -1), UV = new float2(t, 0.75f), Color = color });
            }

            // Connect segments
            for (int s = 0; s < armSegments; s++)
            {
                int sectionStart = baseIndex + s * segments;
                int nextSectionStart = baseIndex + (s + 1) * segments;

                for (int v = 0; v < segments; v++)
                {
                    int nextV = (v + 1) % segments;

                    triangles.Add(new MeshTriangle { Index = sectionStart + v });
                    triangles.Add(new MeshTriangle { Index = nextSectionStart + v });
                    triangles.Add(new MeshTriangle { Index = nextSectionStart + nextV });

                    triangles.Add(new MeshTriangle { Index = sectionStart + v });
                    triangles.Add(new MeshTriangle { Index = nextSectionStart + nextV });
                    triangles.Add(new MeshTriangle { Index = sectionStart + nextV });
                }
            }
        }

        /// <summary>
        /// Generates the light fixture housing and emitter.
        /// </summary>
        private static void GenerateLightFixture(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float3 position,
            float4 glowColor)
        {
            // Fixture housing (dark box)
            GenerateBox(vertices, triangles,
                position + new float3(0, FixtureHeight * 0.5f, 0),
                new float3(FixtureLength * 0.5f, FixtureHeight * 0.5f, FixtureWidth * 0.5f),
                PoleColor);

            // Light emitter surface (bottom of fixture, glowing)
            int baseIndex = vertices.Length;
            float3 lightCenter = position;
            float hw = FixtureLength * 0.45f;
            float hd = FixtureWidth * 0.45f;

            // Bright glowing bottom surface
            float4 brightGlow = glowColor * 2f;
            brightGlow.w = 1f;

            vertices.Add(new MeshVertex { Position = lightCenter + new float3(-hw, 0, -hd), Normal = new float3(0, -1, 0), UV = new float2(0, 0), Color = brightGlow });
            vertices.Add(new MeshVertex { Position = lightCenter + new float3(hw, 0, -hd), Normal = new float3(0, -1, 0), UV = new float2(1, 0), Color = brightGlow });
            vertices.Add(new MeshVertex { Position = lightCenter + new float3(hw, 0, hd), Normal = new float3(0, -1, 0), UV = new float2(1, 1), Color = brightGlow });
            vertices.Add(new MeshVertex { Position = lightCenter + new float3(-hw, 0, hd), Normal = new float3(0, -1, 0), UV = new float2(0, 1), Color = brightGlow });

            triangles.Add(new MeshTriangle { Index = baseIndex });
            triangles.Add(new MeshTriangle { Index = baseIndex + 2 });
            triangles.Add(new MeshTriangle { Index = baseIndex + 1 });

            triangles.Add(new MeshTriangle { Index = baseIndex });
            triangles.Add(new MeshTriangle { Index = baseIndex + 3 });
            triangles.Add(new MeshTriangle { Index = baseIndex + 2 });
        }

        /// <summary>
        /// Helper: Generate a circular light.
        /// </summary>
        private static void GenerateCircularLight(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float3 center, float radius, float4 color, float3 normal)
        {
            int segments = 8;
            int centerIndex = vertices.Length;

            vertices.Add(new MeshVertex
            {
                Position = center,
                Normal = normal,
                UV = new float2(0.5f, 0.5f),
                Color = color * 2f
            });

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * math.PI * 2f;
                float3 offset;

                if (math.abs(normal.y) > 0.9f)
                    offset = new float3(math.cos(angle) * radius, 0, math.sin(angle) * radius);
                else
                    offset = new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);

                vertices.Add(new MeshVertex
                {
                    Position = center + offset,
                    Normal = normal,
                    UV = new float2(0.5f + math.cos(angle) * 0.5f, 0.5f + math.sin(angle) * 0.5f),
                    Color = color
                });
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles.Add(new MeshTriangle { Index = centerIndex });
                triangles.Add(new MeshTriangle { Index = centerIndex + 1 + i });
                triangles.Add(new MeshTriangle { Index = centerIndex + 1 + next });
            }
        }

        /// <summary>
        /// Helper: Generate a box.
        /// </summary>
        private static void GenerateBox(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            float3 center, float3 halfExtents, float4 color)
        {
            int baseIndex = vertices.Length;

            float3[] corners = new float3[8];
            corners[0] = center + new float3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
            corners[1] = center + new float3(halfExtents.x, -halfExtents.y, -halfExtents.z);
            corners[2] = center + new float3(halfExtents.x, -halfExtents.y, halfExtents.z);
            corners[3] = center + new float3(-halfExtents.x, -halfExtents.y, halfExtents.z);
            corners[4] = center + new float3(-halfExtents.x, halfExtents.y, -halfExtents.z);
            corners[5] = center + new float3(halfExtents.x, halfExtents.y, -halfExtents.z);
            corners[6] = center + new float3(halfExtents.x, halfExtents.y, halfExtents.z);
            corners[7] = center + new float3(-halfExtents.x, halfExtents.y, halfExtents.z);

            for (int i = 0; i < 8; i++)
            {
                vertices.Add(new MeshVertex
                {
                    Position = corners[i],
                    Normal = math.normalize(corners[i] - center),
                    UV = new float2(0, 0),
                    Color = color
                });
            }

            int[] indices = {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                2, 3, 7, 2, 7, 6,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5
            };

            for (int i = 0; i < indices.Length; i++)
            {
                triangles.Add(new MeshTriangle { Index = baseIndex + indices[i] });
            }
        }
    }
}
