// ============================================================================
// Nightflow - Procedural Mesh Init System
// Gives every renderable entity the mesh-state component and vertex/index
// buffers the procedural mesh generators and ProceduralMeshRenderer require
// ============================================================================

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Buffers;
using Nightflow.Components;
using Nightflow.Config;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Spawners and the bootstrap create vehicles, hazards and the first track
    /// segments without mesh components; the generators (ProceduralVehicleMeshSystem,
    /// ProceduralHazardMeshSystem, ProceduralRoadMeshSystem) and the renderer only
    /// match entities that carry them. This system fills the gap once per entity
    /// so everything on the freeway actually gets a neon wireframe.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(GameBootstrapSystem))]
    public partial struct ProceduralMeshInitSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            try
            {
                // Vehicles: player, traffic, emergency (ghosts come from the archetype)
                foreach (var (_, entity) in
                    SystemAPI.Query<RefRO<WorldTransform>>()
                        .WithAny<PlayerVehicleTag, TrafficVehicleTag, EmergencyVehicleTag>()
                        .WithNone<VehicleMeshData>()
                        .WithEntityAccess())
                {
                    ecb.AddComponent(entity, new VehicleMeshData
                    {
                        IsGenerated = false,
                        BodyStyle = entity.Index % 3,          // sedan / SUV / truck variety for traffic
                        WireframeColor = new float4(1f, 1f, 1f, 1f),
                        GlowIntensity = 1f
                    });
                    AddMeshBuffers(ref ecb, entity);
                }

                // Hazards
                foreach (var (_, entity) in
                    SystemAPI.Query<RefRO<Hazard>>()
                        .WithAll<HazardTag>()
                        .WithNone<HazardMeshData>()
                        .WithEntityAccess())
                {
                    ecb.AddComponent(entity, new HazardMeshData
                    {
                        IsGenerated = false,
                        VertexCount = 0,
                        TriangleCount = 0,
                        GlowIntensity = 1f
                    });
                    AddMeshBuffers(ref ecb, entity);
                }

                // Track segments (the bootstrap's first kilometre)
                foreach (var (_, entity) in
                    SystemAPI.Query<RefRO<TrackSegment>>()
                        .WithAll<TrackSegmentTag>()
                        .WithNone<ProceduralMeshData>()
                        .WithEntityAccess())
                {
                    ecb.AddComponent(entity, new ProceduralMeshData
                    {
                        IsGenerated = false,
                        VertexCount = 0,
                        TriangleCount = 0,
                        RoadWidth = GameConstants.RoadWidth,
                        LengthSegments = 0,
                        WidthSegments = 0,
                        LODLevel = 0
                    });
                    ecb.AddComponent(entity, new MeshBounds());
                    AddMeshBuffers(ref ecb, entity);
                }

                ecb.Playback(state.EntityManager);
            }
            finally
            {
                ecb.Dispose();
            }
        }

        private static void AddMeshBuffers(ref EntityCommandBuffer ecb, Entity entity)
        {
            ecb.AddBuffer<MeshVertex>(entity);
            ecb.AddBuffer<MeshTriangle>(entity);
            ecb.AddBuffer<SubMeshRange>(entity);
        }
    }
}
