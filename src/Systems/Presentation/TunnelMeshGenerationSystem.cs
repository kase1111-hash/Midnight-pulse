// ============================================================================
// Nightflow - Tunnel Mesh Generation System
// Generates tunnel wall and ceiling geometry from TunnelGeometry entities
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
    /// Generates procedural mesh geometry for tunnel walls and ceilings.
    /// Processes entities with TunnelGeometry and TunnelMeshTag.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProceduralRoadMeshSystem))]
    public partial struct TunnelMeshGenerationSystem : ISystem
    {
        // Tunnel visual parameters
        private static readonly float4 WallColor = new float4(0.15f, 0.2f, 0.25f, 1f);
        private static readonly float4 CeilingColor = new float4(0.12f, 0.15f, 0.2f, 1f);
        private static readonly float4 LightStripColor = new float4(0.9f, 0.95f, 1f, 1f);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TunnelMeshTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (geometry, spline, entity) in
                SystemAPI.Query<RefRO<TunnelGeometry>, RefRO<HermiteSpline>>()
                    .WithAll<TunnelMeshTag>()
                    .WithNone<ProceduralMeshData>()
                    .WithEntityAccess())
            {
                // Add mesh buffers to this entity
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

            // Generate mesh for entities that have buffers but aren't generated
            foreach (var (geometry, spline, meshData, entity) in
                SystemAPI.Query<RefRO<TunnelGeometry>, RefRO<HermiteSpline>, RefRW<ProceduralMeshData>>()
                    .WithAll<TunnelMeshTag>()
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

                GenerateTunnelGeometry(
                    vertices,
                    triangles,
                    subMeshes,
                    geometry.ValueRO,
                    spline.ValueRO
                );

                meshData.ValueRW.IsGenerated = true;
                meshData.ValueRW.VertexCount = vertices.Length;
                meshData.ValueRW.TriangleCount = triangles.Length / 3;
            }
        }

        private void GenerateTunnelGeometry(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            DynamicBuffer<SubMeshRange> subMeshes,
            TunnelGeometry geometry,
            HermiteSpline spline)
        {
            int lengthSegments = geometry.LengthSegments;
            int archSegments = geometry.ArchSegments;
            float wallHeight = geometry.WallHeight;
            float wallOffset = geometry.WallOffset;

            // Generate left wall
            GenerateWall(vertices, triangles, spline, lengthSegments,
                -wallOffset, wallHeight, WallColor, true);

            // Generate right wall
            GenerateWall(vertices, triangles, spline, lengthSegments,
                wallOffset, wallHeight, WallColor, false);

            // Add wall sub-mesh
            int wallIndexCount = triangles.Length;
            subMeshes.Add(new SubMeshRange
            {
                StartIndex = 0,
                IndexCount = wallIndexCount,
                MaterialType = 3 // Tunnel wall material
            });

            // Generate ceiling arch
            int ceilingStartIndex = triangles.Length;
            GenerateCeiling(vertices, triangles, spline, lengthSegments,
                archSegments, wallOffset, wallHeight, CeilingColor);

            // Add ceiling sub-mesh
            int ceilingIndexCount = triangles.Length - ceilingStartIndex;
            if (ceilingIndexCount > 0)
            {
                subMeshes.Add(new SubMeshRange
                {
                    StartIndex = ceilingStartIndex,
                    IndexCount = ceilingIndexCount,
                    MaterialType = 3 // Same tunnel material
                });
            }
        }

        private void GenerateWall(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            HermiteSpline spline,
            int lengthSegments,
            float lateralOffset,
            float height,
            float4 color,
            bool faceInward)
        {
            int startVert = vertices.Length;

            for (int z = 0; z <= lengthSegments; z++)
            {
                float t = z / (float)lengthSegments;

                SplineUtilities.BuildFrameAtT(
                    spline.P0, spline.T0, spline.P1, spline.T1, t,
                    out float3 position, out float3 forward, out float3 right, out float3 up);

                float3 wallBase = position + right * lateralOffset;
                float3 normal = faceInward ? right : -right;

                // Bottom vertex
                vertices.Add(new MeshVertex
                {
                    Position = wallBase,
                    Normal = normal,
                    UV = new float2(0f, t),
                    Color = color
                });

                // Top vertex
                vertices.Add(new MeshVertex
                {
                    Position = wallBase + up * height,
                    Normal = normal,
                    UV = new float2(1f, t),
                    Color = color
                });
            }

            // Generate triangles
            for (int z = 0; z < lengthSegments; z++)
            {
                int baseIdx = startVert + z * 2;

                if (faceInward)
                {
                    triangles.Add(new MeshTriangle { Index = baseIdx + 0 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 2 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 3 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 0 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 3 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 1 });
                }
                else
                {
                    triangles.Add(new MeshTriangle { Index = baseIdx + 0 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 3 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 2 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 0 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 1 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 3 });
                }
            }
        }

        private void GenerateCeiling(
            DynamicBuffer<MeshVertex> vertices,
            DynamicBuffer<MeshTriangle> triangles,
            HermiteSpline spline,
            int lengthSegments,
            int archSegments,
            float wallOffset,
            float wallHeight,
            float4 color)
        {
            int startVert = vertices.Length;

            for (int z = 0; z <= lengthSegments; z++)
            {
                float t = z / (float)lengthSegments;

                SplineUtilities.BuildFrameAtT(
                    spline.P0, spline.T0, spline.P1, spline.T1, t,
                    out float3 position, out float3 forward, out float3 right, out float3 up);

                // Generate arch cross-section
                for (int a = 0; a <= archSegments; a++)
                {
                    float archT = a / (float)archSegments;
                    float angle = math.PI * archT; // 0 to PI for half-circle

                    float x = math.cos(angle) * wallOffset;
                    float y = wallHeight + math.sin(angle) * (wallOffset * 0.5f);

                    float3 archPos = position + right * x + up * y;
                    float3 archNormal = -math.normalize(new float3(math.cos(angle), math.sin(angle) * 0.5f, 0));
                    archNormal = right * archNormal.x + up * archNormal.y;

                    vertices.Add(new MeshVertex
                    {
                        Position = archPos,
                        Normal = archNormal,
                        UV = new float2(archT, t),
                        Color = color
                    });
                }
            }

            // Generate triangles
            int vertsPerSection = archSegments + 1;
            for (int z = 0; z < lengthSegments; z++)
            {
                for (int a = 0; a < archSegments; a++)
                {
                    int baseIdx = startVert + z * vertsPerSection + a;

                    triangles.Add(new MeshTriangle { Index = baseIdx });
                    triangles.Add(new MeshTriangle { Index = baseIdx + vertsPerSection });
                    triangles.Add(new MeshTriangle { Index = baseIdx + vertsPerSection + 1 });
                    triangles.Add(new MeshTriangle { Index = baseIdx });
                    triangles.Add(new MeshTriangle { Index = baseIdx + vertsPerSection + 1 });
                    triangles.Add(new MeshTriangle { Index = baseIdx + 1 });
                }
            }
        }
    }
}
