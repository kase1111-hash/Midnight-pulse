// ============================================================================
// Nightflow - Overpass Mesh Generation System
// Generates overpass pillar and elevated barrier geometry
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
    /// Generates procedural mesh geometry for overpass pillars.
    /// Processes entities with PillarGeometry and OverpassPillarTag.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProceduralRoadMeshSystem))]
    public partial struct OverpassPillarMeshSystem : ISystem
    {
        private static readonly float4 PillarColor = new float4(0.2f, 0.22f, 0.28f, 1f);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OverpassPillarTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Add buffers to pillar entities
            foreach (var (geometry, entity) in
                SystemAPI.Query<RefRO<PillarGeometry>>()
                    .WithAll<OverpassPillarTag>()
                    .WithNone<ProceduralMeshData>()
                    .WithEntityAccess())
            {
                ecb.AddBuffer<MeshVertex>(entity);
                ecb.AddBuffer<MeshTriangle>(entity);
                ecb.AddBuffer<SubMeshRange>(entity);
                ecb.AddComponent(entity, new ProceduralMeshData
                {
                    IsGenerated = false,
                    LODLevel = 0
                });
                ecb.AddComponent(entity, new MeshBounds());
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Generate pillar meshes
            foreach (var (geometry, meshData, entity) in
                SystemAPI.Query<RefRO<PillarGeometry>, RefRW<ProceduralMeshData>>()
                    .WithAll<OverpassPillarTag>()
                    .WithEntityAccess())
            {
                if (meshData.ValueRO.IsGenerated)
                    continue;

                var vertices = SystemAPI.GetBuffer<MeshVertex>(entity);
                var triangles = SystemAPI.GetBuffer<MeshTriangle>(entity);
                var subMeshes = SystemAPI.GetBuffer<SubMeshRange>(entity);

                vertices.Clear();
                triangles.Clear();
                subMeshes.Clear();

                GeneratePillarMesh(vertices, triangles, subMeshes, geometry.ValueRO);

                meshData.ValueRW.IsGenerated = true;
                meshData.ValueRW.VertexCount = vertices.Length;
                meshData.ValueRW.TriangleCount = triangles.Length / 3;
            }
        }

        private void GeneratePillarMesh(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            PillarGeometry geometry)
        {
            float3 position = geometry.Position;
            float height = geometry.Height;
            float width = geometry.Width;
            float3 forward = geometry.Forward;
            float3 right = geometry.Right;
            float3 up = new float3(0, 1, 0);

            float halfWidth = width / 2f;

            // Box pillar: 8 vertices, 12 triangles (6 faces)
            // Bottom vertices
            vertices.Add(new MeshVertex
            {
                Position = position + forward * halfWidth + right * halfWidth,
                Normal = math.normalize(forward + right),
                UV = new float2(0, 0),
                Color = PillarColor
            });
            vertices.Add(new MeshVertex
            {
                Position = position + forward * halfWidth - right * halfWidth,
                Normal = math.normalize(forward - right),
                UV = new float2(1, 0),
                Color = PillarColor
            });
            vertices.Add(new MeshVertex
            {
                Position = position - forward * halfWidth - right * halfWidth,
                Normal = math.normalize(-forward - right),
                UV = new float2(1, 0),
                Color = PillarColor
            });
            vertices.Add(new MeshVertex
            {
                Position = position - forward * halfWidth + right * halfWidth,
                Normal = math.normalize(-forward + right),
                UV = new float2(0, 0),
                Color = PillarColor
            });

            // Top vertices
            float3 topOffset = up * height;
            vertices.Add(new MeshVertex
            {
                Position = position + forward * halfWidth + right * halfWidth + topOffset,
                Normal = math.normalize(forward + right),
                UV = new float2(0, 1),
                Color = PillarColor
            });
            vertices.Add(new MeshVertex
            {
                Position = position + forward * halfWidth - right * halfWidth + topOffset,
                Normal = math.normalize(forward - right),
                UV = new float2(1, 1),
                Color = PillarColor
            });
            vertices.Add(new MeshVertex
            {
                Position = position - forward * halfWidth - right * halfWidth + topOffset,
                Normal = math.normalize(-forward - right),
                UV = new float2(1, 1),
                Color = PillarColor
            });
            vertices.Add(new MeshVertex
            {
                Position = position - forward * halfWidth + right * halfWidth + topOffset,
                Normal = math.normalize(-forward + right),
                UV = new float2(0, 1),
                Color = PillarColor
            });

            // Front face (0, 1, 5, 4)
            triangles.Add(new MeshTriangle { Index = 0 });
            triangles.Add(new MeshTriangle { Index = 1 });
            triangles.Add(new MeshTriangle { Index = 5 });
            triangles.Add(new MeshTriangle { Index = 0 });
            triangles.Add(new MeshTriangle { Index = 5 });
            triangles.Add(new MeshTriangle { Index = 4 });

            // Right face (1, 2, 6, 5)
            triangles.Add(new MeshTriangle { Index = 1 });
            triangles.Add(new MeshTriangle { Index = 2 });
            triangles.Add(new MeshTriangle { Index = 6 });
            triangles.Add(new MeshTriangle { Index = 1 });
            triangles.Add(new MeshTriangle { Index = 6 });
            triangles.Add(new MeshTriangle { Index = 5 });

            // Back face (2, 3, 7, 6)
            triangles.Add(new MeshTriangle { Index = 2 });
            triangles.Add(new MeshTriangle { Index = 3 });
            triangles.Add(new MeshTriangle { Index = 7 });
            triangles.Add(new MeshTriangle { Index = 2 });
            triangles.Add(new MeshTriangle { Index = 7 });
            triangles.Add(new MeshTriangle { Index = 6 });

            // Left face (3, 0, 4, 7)
            triangles.Add(new MeshTriangle { Index = 3 });
            triangles.Add(new MeshTriangle { Index = 0 });
            triangles.Add(new MeshTriangle { Index = 4 });
            triangles.Add(new MeshTriangle { Index = 3 });
            triangles.Add(new MeshTriangle { Index = 4 });
            triangles.Add(new MeshTriangle { Index = 7 });

            // Add sub-mesh
            subMeshes.Add(new SubMeshRange
            {
                StartIndex = 0,
                IndexCount = triangles.Length,
                MaterialType = 4 // Overpass/pillar material
            });
        }
    }

    /// <summary>
    /// Generates elevated barrier geometry for overpass sections.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(OverpassPillarMeshSystem))]
    public partial struct OverpassBarrierMeshSystem : ISystem
    {
        private static readonly float4 BarrierColor = new float4(0.25f, 0.27f, 0.32f, 1f);
        private const float BarrierWidth = 0.2f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OverpassMeshTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (geometry, spline, entity) in
                SystemAPI.Query<RefRO<OverpassGeometry>, RefRO<HermiteSpline>>()
                    .WithAll<OverpassMeshTag>()
                    .WithNone<ProceduralMeshData>()
                    .WithEntityAccess())
            {
                ecb.AddBuffer<MeshVertex>(entity);
                ecb.AddBuffer<MeshTriangle>(entity);
                ecb.AddBuffer<SubMeshRange>(entity);
                ecb.AddComponent(entity, new ProceduralMeshData
                {
                    IsGenerated = false,
                    LODLevel = 0
                });
                ecb.AddComponent(entity, new MeshBounds());
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            foreach (var (geometry, spline, meshData, entity) in
                SystemAPI.Query<RefRO<OverpassGeometry>, RefRO<HermiteSpline>, RefRW<ProceduralMeshData>>()
                    .WithAll<OverpassMeshTag>()
                    .WithEntityAccess())
            {
                if (meshData.ValueRO.IsGenerated)
                    continue;

                var vertices = SystemAPI.GetBuffer<MeshVertex>(entity);
                var triangles = SystemAPI.GetBuffer<MeshTriangle>(entity);
                var subMeshes = SystemAPI.GetBuffer<SubMeshRange>(entity);

                vertices.Clear();
                triangles.Clear();
                subMeshes.Clear();

                GenerateElevatedBarriers(vertices, triangles, subMeshes, geometry.ValueRO, spline.ValueRO);

                meshData.ValueRW.IsGenerated = true;
                meshData.ValueRW.VertexCount = vertices.Length;
                meshData.ValueRW.TriangleCount = triangles.Length / 3;
            }
        }

        private void GenerateElevatedBarriers(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            OverpassGeometry geometry,
            HermiteSpline spline)
        {
            int lengthSegments = geometry.LengthSegments;
            float amplitude = geometry.ElevationAmplitude;
            float barrierHeight = geometry.BarrierHeight;

            const float roadHalfWidth = 8.7f; // Approximate road half-width

            // Generate elevated barriers on both sides
            float[] offsets = { -roadHalfWidth, roadHalfWidth };

            foreach (float offset in offsets)
            {
                int startVert = vertices.Length;

                for (int z = 0; z <= lengthSegments; z++)
                {
                    float t = z / (float)lengthSegments;
                    float elevation = amplitude * math.sin(math.PI * t);

                    SplineUtilities.BuildFrameAtT(
                        spline.P0, spline.T0, spline.P1, spline.T1, t,
                        out float3 position, out float3 forward, out float3 right, out float3 up);

                    float3 basePos = position + right * offset + up * elevation;

                    // Bottom
                    vertices.Add(new MeshVertex
                    {
                        Position = basePos,
                        Normal = offset > 0 ? right : -right,
                        UV = new float2(0, t),
                        Color = BarrierColor
                    });

                    // Top
                    vertices.Add(new MeshVertex
                    {
                        Position = basePos + up * barrierHeight,
                        Normal = offset > 0 ? right : -right,
                        UV = new float2(1, t),
                        Color = BarrierColor
                    });
                }

                // Generate triangles
                for (int z = 0; z < lengthSegments; z++)
                {
                    int baseIdx = startVert + z * 2;

                    triangles.Add(new MeshTriangle { Index = baseIdx + 0 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 2 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 3 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 0 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 3 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 1 });
                }
            }

            subMeshes.Add(new SubMeshRange
            {
                StartIndex = 0,
                IndexCount = triangles.Length,
                MaterialType = 4 // Overpass material
            });
        }
    }
}
