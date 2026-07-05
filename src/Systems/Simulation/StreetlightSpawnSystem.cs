// ============================================================================
// Nightflow - Streetlight Spawn System
// Spawns light fixture entities along freshly generated track segments:
// sodium streetlights on open road, fluorescent strips in tunnels.
// Culls fixtures behind the player alongside segment culling.
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;
using Nightflow.Utilities;
using Nightflow.Config;

namespace Nightflow.Systems
{
    /// <summary>
    /// Marker added to track segments once their light fixtures exist.
    /// </summary>
    public struct SegmentLightsSpawned : IComponentData { }

    /// <summary>
    /// Creates LightSource entities (streetlights, tunnel lights) for each
    /// new track segment. ProceduralLightMeshSystem generates their meshes
    /// and ProceduralMeshRenderer draws them - together they provide the
    /// "driving past city lights" pools of glow along the freeway.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrackGenerationSystem))]
    public partial struct StreetlightSpawnSystem : ISystem
    {
        // Streetlight placement
        private const float StreetlightSpacing = 40f;      // matches LightingConfig default
        private const float RoadsideOffset = 1.2f;         // beyond road edge (m)
        private const float PoleHeight = 8f;
        private const float ArmLength = 2.5f;

        // Tunnel light placement
        private const float TunnelLightSpacing = 20f;
        private const float TunnelCeilingHeight = 6f;      // matches TrackGenerationSystem.TunnelHeight
        private const float TunnelLightDrop = 0.3f;        // below ceiling

        // Emission
        private static readonly float3 SodiumColor = new float3(1f, 0.82f, 0.5f);
        private static readonly float3 FluorescentColor = new float3(0.9f, 0.95f, 1f);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TrackSegmentTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Player position drives culling (same rule as track segments)
            float playerZ = 0f;
            foreach (var transform in SystemAPI.Query<RefRO<WorldTransform>>().WithAll<PlayerVehicleTag>())
            {
                playerZ = transform.ValueRO.Position.z;
                break;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            try
            {
                // =========================================================
                // Spawn fixtures for segments that don't have them yet
                // =========================================================

                foreach (var (segment, spline, entity) in
                    SystemAPI.Query<RefRO<TrackSegment>, RefRO<HermiteSpline>>()
                        .WithAll<TrackSegmentTag>()
                        .WithNone<SegmentLightsSpawned>()
                        .WithEntityAccess())
                {
                    bool isTunnel = SystemAPI.HasComponent<TunnelData>(entity);

                    if (isTunnel)
                    {
                        SpawnTunnelLights(ref ecb, spline.ValueRO, segment.ValueRO.Length);
                    }
                    else
                    {
                        SpawnStreetlights(ref ecb, spline.ValueRO, segment.ValueRO);
                    }

                    ecb.AddComponent<SegmentLightsSpawned>(entity);
                }

                // =========================================================
                // Cull fixtures behind the player
                // =========================================================

                float cullZ = playerZ - GameConstants.SegmentsBehind * GameConstants.SegmentLength;

                foreach (var (transform, entity) in
                    SystemAPI.Query<RefRO<WorldTransform>>()
                        .WithAll<LightSourceTag>()
                        .WithEntityAccess())
                {
                    if (transform.ValueRO.Position.z < cullZ)
                    {
                        ecb.DestroyEntity(entity);
                    }
                }

                ecb.Playback(state.EntityManager);
            }
            finally
            {
                ecb.Dispose();
            }
        }

        /// <summary>
        /// Sodium streetlights on alternating road sides, arms toward the road.
        /// </summary>
        private void SpawnStreetlights(ref EntityCommandBuffer ecb, in HermiteSpline spline, in TrackSegment segment)
        {
            float length = math.max(segment.Length, 1f);
            int lightCount = (int)(length / StreetlightSpacing);
            float sideOffset = GameConstants.RoadWidth * 0.5f + RoadsideOffset;

            // Parity of the global light index keeps sides alternating
            // seamlessly across segment boundaries
            int firstGlobalIndex = (int)(spline.P0.z / StreetlightSpacing);

            for (int i = 0; i < lightCount; i++)
            {
                float t = (i + 0.5f) / lightCount;

                SplineUtilities.BuildFrameAtT(
                    spline.P0, spline.T0, spline.P1, spline.T1, t,
                    out float3 position, out float3 forward, out float3 right, out float3 up);

                bool leftSide = ((firstGlobalIndex + i) & 1) == 0;
                float side = leftSide ? -1f : 1f;
                float3 basePos = position + right * (side * sideOffset);

                // Streetlight mesh extends its arm along local +X; aim +X at
                // the road: left poles face forward, right poles face back
                quaternion rotation = leftSide
                    ? quaternion.LookRotationSafe(forward, up)
                    : quaternion.LookRotationSafe(-forward, up);

                Entity fixture = CreateFixture(ref ecb, basePos, rotation,
                    fixtureType: 0, SodiumColor, intensity: 2f, radius: 25f);

                // Enables distance-LOD intensity in WireframeRenderSystem
                ecb.AddComponent(fixture, new Streetlight
                {
                    Color = SodiumColor,
                    Intensity = 2f,
                    Height = PoleHeight,
                    Radius = 25f,
                    Side = leftSide ? -1 : 1
                });
            }
        }

        /// <summary>
        /// Fluorescent strips along the tunnel ceiling.
        /// </summary>
        private void SpawnTunnelLights(ref EntityCommandBuffer ecb, in HermiteSpline spline, float segmentLength)
        {
            float length = math.max(segmentLength, 1f);
            int lightCount = (int)(length / TunnelLightSpacing);

            for (int i = 0; i < lightCount; i++)
            {
                float t = (i + 0.5f) / lightCount;

                SplineUtilities.BuildFrameAtT(
                    spline.P0, spline.T0, spline.P1, spline.T1, t,
                    out float3 position, out float3 forward, out float3 right, out float3 up);

                float3 lightPos = position + up * (TunnelCeilingHeight - TunnelLightDrop);
                quaternion rotation = quaternion.LookRotationSafe(forward, up);

                CreateFixture(ref ecb, lightPos, rotation,
                    fixtureType: 1, FluorescentColor, intensity: 1.5f, radius: 15f);
            }
        }

        private Entity CreateFixture(ref EntityCommandBuffer ecb, float3 position, quaternion rotation,
            int fixtureType, float3 color, float intensity, float radius)
        {
            Entity fixture = ecb.CreateEntity();

            ecb.AddComponent<LightSourceTag>(fixture);
            ecb.AddComponent(fixture, new WorldTransform
            {
                Position = position,
                Rotation = rotation
            });
            ecb.AddComponent(fixture, new LightEmitter
            {
                Color = color,
                Intensity = intensity,
                Radius = radius,
                Strobe = false,
                StrobeRate = 0f,
                StrobePhase = 0f
            });
            ecb.AddComponent(fixture, new LightFixtureMeshData
            {
                IsGenerated = false,
                FixtureType = fixtureType,
                PoleHeight = PoleHeight,
                ArmLength = ArmLength
            });
            ecb.AddBuffer<MeshVertex>(fixture);
            ecb.AddBuffer<MeshTriangle>(fixture);
            ecb.AddBuffer<SubMeshRange>(fixture);

            return fixture;
        }
    }
}
