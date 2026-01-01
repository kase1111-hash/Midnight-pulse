// ============================================================================
// Nightflow - Procedural City Generation System
// GPU-light building generation with aggressive LOD
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;
using Nightflow.Config;

namespace Nightflow.Systems
{
    /// <summary>
    /// Generates procedural city buildings along the highway.
    /// GPU-light design with impostor billboards for distant buildings.
    ///
    /// From spec:
    /// - Populate distant silhouettes with actual geometry
    /// - Procedural building generation
    ///
    /// GPU Budget Strategy:
    /// - Max 256 full buildings (LOD0/1)
    /// - Max 512 impostors (LOD2, single quad each)
    /// - Spread generation across frames
    /// - Aggressive frustum + distance culling
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrackGenerationSystem))]
    public partial struct CityGenerationSystem : ISystem
    {
        // Generation parameters
        private const float BlockSize = 100f;           // City block size
        private const float RoadOffset = 25f;           // Distance from road center
        private const float BuildingSpacing = 8f;       // Min spacing between buildings
        private const float MaxBuildingsPerBlock = 12f; // Buildings per block

        // Building size ranges
        private const float MinWidth = 10f;
        private const float MaxWidth = 30f;
        private const float MinDepth = 10f;
        private const float MaxDepth = 25f;
        private const float MinHeight = 15f;
        private const float MaxHeight = 120f;

        // Pseudo-random state
        private uint _randomState;

        public void OnCreate(ref SystemState state)
        {
            _randomState = 73856093;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get player position
            float playerZ = 0f;
            foreach (var transform in SystemAPI.Query<RefRO<WorldTransform>>()
                .WithAll<PlayerVehicleTag>())
            {
                playerZ = transform.ValueRO.Position.z;
                break;
            }

            // Get city generation state
            foreach (var cityState in SystemAPI.Query<RefRW<CityGenerationState>>())
            {
                if (cityState.ValueRO.Paused) continue;

                ref var city = ref cityState.ValueRW;
                city.GeneratedThisFrame = 0;

                // Sync seed
                if (city.Seed == 0)
                {
                    foreach (var netState in SystemAPI.Query<RefRO<NetworkState>>())
                    {
                        city.Seed = netState.ValueRO.SessionSeed;
                        break;
                    }
                    if (city.Seed == 0) city.Seed = 12345;
                }

                _randomState = city.Seed;

                // =============================================================
                // Generate New Blocks Ahead
                // =============================================================

                float targetFrontier = playerZ + city.GenerationDistance;

                while (city.GenerationFrontier < targetFrontier &&
                       city.GeneratedThisFrame < city.MaxGenerationPerFrame &&
                       city.ActiveBuildingCount < city.MaxBuildings)
                {
                    // Generate block on left side
                    if (city.ActiveBuildingCount < city.MaxBuildings)
                    {
                        GenerateBlockBuildings(
                            ref state,
                            ref city,
                            city.GenerationFrontier,
                            CitySide.Left
                        );
                    }

                    // Generate block on right side
                    if (city.ActiveBuildingCount < city.MaxBuildings)
                    {
                        GenerateBlockBuildings(
                            ref state,
                            ref city,
                            city.GenerationFrontier,
                            CitySide.Right
                        );
                    }

                    city.GenerationFrontier += BlockSize;
                }
            }

            // =============================================================
            // Cleanup Buildings Behind Player
            // =============================================================

            float cleanupZ = playerZ;
            foreach (var cityState in SystemAPI.Query<RefRO<CityGenerationState>>())
            {
                cleanupZ = playerZ - cityState.ValueRO.CleanupDistance;
                break;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (transform, buildingDef, entity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<BuildingDefinition>>()
                    .WithAll<BuildingTag>()
                    .WithEntityAccess())
            {
                if (transform.ValueRO.Position.z < cleanupZ)
                {
                    ecb.DestroyEntity(entity);

                    // Decrement count
                    foreach (var cityState in SystemAPI.Query<RefRW<CityGenerationState>>())
                    {
                        cityState.ValueRW.ActiveBuildingCount--;
                    }
                }
            }

            // Cleanup impostors
            foreach (var (impostor, entity) in
                SystemAPI.Query<RefRO<BuildingImpostor>>()
                    .WithAll<ImpostorTag>()
                    .WithEntityAccess())
            {
                if (impostor.ValueRO.Position.z < cleanupZ)
                {
                    ecb.DestroyEntity(entity);

                    foreach (var cityState in SystemAPI.Query<RefRW<CityGenerationState>>())
                    {
                        cityState.ValueRW.ActiveImpostorCount--;
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void GenerateBlockBuildings(
            ref SystemState state,
            ref CityGenerationState city,
            float blockZ,
            CitySide side)
        {
            // Calculate block center
            float xOffset = side == CitySide.Left ? -RoadOffset - BlockSize * 0.5f
                                                   : RoadOffset + BlockSize * 0.5f;

            float3 blockCenter = new float3(xOffset, 0f, blockZ + BlockSize * 0.5f);

            // Determine building count based on density
            int buildingCount = (int)(MaxBuildingsPerBlock * city.Density);
            buildingCount = math.max(2, buildingCount);

            // Generate skyline height for this block
            float skylineHeight = GetSkylineHeight(blockZ, city.Seed);

            for (int i = 0; i < buildingCount && city.GeneratedThisFrame < city.MaxGenerationPerFrame; i++)
            {
                // Random position within block
                _randomState = Hash(_randomState);
                float localX = ((_randomState & 0xFFFF) / 65535f - 0.5f) * (BlockSize - MaxWidth);

                _randomState = Hash(_randomState);
                float localZ = ((_randomState & 0xFFFF) / 65535f - 0.5f) * (BlockSize - MaxDepth);

                float3 position = blockCenter + new float3(localX, 0f, localZ);

                // Random building dimensions
                _randomState = Hash(_randomState);
                float width = math.lerp(MinWidth, MaxWidth, (_randomState & 0xFFFF) / 65535f);

                _randomState = Hash(_randomState);
                float depth = math.lerp(MinDepth, MaxDepth, (_randomState & 0xFFFF) / 65535f);

                _randomState = Hash(_randomState);
                float heightFactor = (_randomState & 0xFFFF) / 65535f;
                float height = math.lerp(MinHeight, MaxHeight, heightFactor * heightFactor) * skylineHeight;

                // Random style
                _randomState = Hash(_randomState);
                BuildingStyle style = (BuildingStyle)((_randomState % 5)); // Exclude Billboard

                // Random color (dark with color tint)
                _randomState = Hash(_randomState);
                float hue = (_randomState & 0xFFFF) / 65535f;
                float3 wireColor = HueToRGB(hue) * 0.3f + new float3(0.1f, 0.1f, 0.15f);

                // Window color (warm variations)
                _randomState = Hash(_randomState);
                float windowHue = 0.1f + ((_randomState & 0xFFFF) / 65535f) * 0.1f; // Yellow-orange
                float3 windowColor = HueToRGB(windowHue) * 0.8f + new float3(0.2f, 0.15f, 0.05f);

                // Create building entity
                var entity = state.EntityManager.CreateEntity();

                state.EntityManager.AddComponentData(entity, new BuildingDefinition
                {
                    BuildingId = (int)(city.Seed ^ Hash((uint)(blockZ * 1000 + i))),
                    Width = width,
                    Depth = depth,
                    Height = height,
                    FloorCount = (int)(height / 4f), // ~4m per floor
                    Style = style,
                    WireframeColor = wireColor,
                    WindowColor = windowColor,
                    WindowDensity = 0.5f + heightFactor * 0.3f,
                    HasRooftopDetail = height > 50f,
                    FloorSetback = style == BuildingStyle.Stepped ? 0.5f : 0f
                });

                state.EntityManager.AddComponentData(entity, new WorldTransform
                {
                    Position = position,
                    Rotation = quaternion.RotateY((_randomState & 0x3) * math.PI * 0.5f)
                });

                state.EntityManager.AddComponentData(entity, new BuildingLOD
                {
                    CurrentLOD = 2,     // Start at lowest detail
                    TargetLOD = 2,
                    TransitionProgress = 1f,
                    DistanceToCamera = 1000f,
                    IsVisible = false,
                    InRenderDistance = false,
                    LastUpdateFrame = 0
                });

                // Add window state
                int cols = (int)(width / 3f);
                int rows = (int)(height / 4f);
                _randomState = Hash(_randomState);

                state.EntityManager.AddComponentData(entity, new WindowLightState
                {
                    PatternSeed = _randomState,
                    Columns = math.min(cols, 8),
                    Rows = math.min(rows, 8),
                    LitFlags = GenerateWindowPattern(_randomState, 0.4f),
                    GlowIntensity = 0.8f,
                    FlickerPhase = (_randomState & 0xFFFF) / 65535f
                });

                state.EntityManager.AddComponent<BuildingTag>(entity);
                state.EntityManager.AddComponent<PendingMeshTag>(entity);

                city.ActiveBuildingCount++;
                city.GeneratedThisFrame++;
            }
        }

        private float GetSkylineHeight(float z, uint seed)
        {
            // Base noise for height variation
            float noise = math.sin(z * 0.01f + seed * 0.001f) * 0.5f + 0.5f;
            noise += math.sin(z * 0.003f + seed * 0.0007f) * 0.3f;

            // Cluster factor (groups of similar height)
            float cluster = math.sin(z * 0.002f) * 0.2f + 0.8f;

            return math.saturate(noise * cluster) * 0.5f + 0.5f;
        }

        private static ulong GenerateWindowPattern(uint seed, float litPercent)
        {
            ulong pattern = 0;
            uint s = seed;

            for (int i = 0; i < 64; i++)
            {
                s = Hash(s);
                if ((s & 0xFFFF) / 65535f < litPercent)
                {
                    pattern |= (1UL << i);
                }
            }

            return pattern;
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x85ebca6b;
            x ^= x >> 13;
            x *= 0xc2b2ae35;
            x ^= x >> 16;
            return x;
        }

        private static float3 HueToRGB(float hue)
        {
            float r = math.abs(hue * 6f - 3f) - 1f;
            float g = 2f - math.abs(hue * 6f - 2f);
            float b = 2f - math.abs(hue * 6f - 4f);
            return math.saturate(new float3(r, g, b));
        }
    }

    /// <summary>
    /// Generates building meshes from definitions.
    /// Creates simplified geometry for GPU efficiency.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CityGenerationSystem))]
    public partial struct BuildingMeshGenerationSystem : ISystem
    {
        // Mesh generation limits (GPU budget)
        private const int MaxMeshesPerFrame = 2;
        private const int MaxVerticesLOD0 = 64;     // Simple box = 24, tower = 48
        private const int MaxVerticesLOD1 = 24;     // Always box
        private const int MaxVerticesLOD2 = 4;      // Quad impostor

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int meshesGenerated = 0;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (buildingDef, transform, lod, entity) in
                SystemAPI.Query<RefRO<BuildingDefinition>, RefRO<WorldTransform>, RefRO<BuildingLOD>>()
                    .WithAll<PendingMeshTag>()
                    .WithEntityAccess())
            {
                if (meshesGenerated >= MaxMeshesPerFrame) break;

                // Only generate mesh if potentially visible
                if (!lod.ValueRO.InRenderDistance) continue;

                // Add mesh data component
                var meshData = new VehicleMeshData
                {
                    IsGenerated = true,
                    VehicleLength = buildingDef.ValueRO.Depth,
                    VehicleWidth = buildingDef.ValueRO.Width,
                    WireframeColor = new float4(buildingDef.ValueRO.WireframeColor, 1f)
                };

                ecb.AddComponent(entity, meshData);
                ecb.AddComponent(entity, new BuildingMeshRef
                {
                    MeshTemplateIndex = (int)buildingDef.ValueRO.Style,
                    LOD0VertexCount = MaxVerticesLOD0,
                    LOD1VertexCount = MaxVerticesLOD1,
                    LOD2VertexCount = MaxVerticesLOD2
                });

                // Add vertex buffer
                ecb.AddBuffer<MeshVertex>(entity);

                // Remove pending tag
                ecb.RemoveComponent<PendingMeshTag>(entity);

                meshesGenerated++;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // Generate actual vertices for buildings with buffers
            foreach (var (buildingDef, meshData, lod, entity) in
                SystemAPI.Query<RefRO<BuildingDefinition>, RefRW<VehicleMeshData>, RefRO<BuildingLOD>>()
                    .WithAll<BuildingTag>()
                    .WithEntityAccess())
            {
                if (!SystemAPI.HasBuffer<MeshVertex>(entity)) continue;

                var vertices = SystemAPI.GetBuffer<MeshVertex>(entity);
                if (vertices.Length > 0) continue; // Already generated

                // Generate box vertices based on LOD
                GenerateBuildingMesh(
                    ref vertices,
                    buildingDef.ValueRO,
                    lod.ValueRO.CurrentLOD
                );
            }
        }

        private static void GenerateBuildingMesh(
            ref DynamicBuffer<MeshVertex> vertices,
            BuildingDefinition def,
            int lod)
        {
            float w = def.Width * 0.5f;
            float d = def.Depth * 0.5f;
            float h = def.Height;

            float4 color = new float4(def.WireframeColor, 1f);
            float4 windowColor = new float4(def.WindowColor, 1f);

            if (lod >= 2)
            {
                // LOD2: Single quad (impostor)
                vertices.Add(new MeshVertex { Position = new float3(-w, 0, 0), UV = new float2(0, 0), Color = color });
                vertices.Add(new MeshVertex { Position = new float3(w, 0, 0), UV = new float2(1, 0), Color = color });
                vertices.Add(new MeshVertex { Position = new float3(w, h, 0), UV = new float2(1, 1), Color = color });
                vertices.Add(new MeshVertex { Position = new float3(-w, h, 0), UV = new float2(0, 1), Color = color });
            }
            else if (lod == 1)
            {
                // LOD1: Simple box (24 vertices)
                GenerateBox(ref vertices, w, d, h, color);
            }
            else
            {
                // LOD0: Box with window details
                GenerateBox(ref vertices, w, d, h, color);

                // Add window strip vertices (simplified)
                if (def.WindowDensity > 0.3f)
                {
                    float windowH = h * 0.7f;
                    float windowY = h * 0.15f;

                    // Front window strip
                    vertices.Add(new MeshVertex { Position = new float3(-w * 0.8f, windowY, d + 0.01f), UV = new float2(0, 0), Color = windowColor });
                    vertices.Add(new MeshVertex { Position = new float3(w * 0.8f, windowY, d + 0.01f), UV = new float2(1, 0), Color = windowColor });
                    vertices.Add(new MeshVertex { Position = new float3(w * 0.8f, windowY + windowH, d + 0.01f), UV = new float2(1, 1), Color = windowColor });
                    vertices.Add(new MeshVertex { Position = new float3(-w * 0.8f, windowY + windowH, d + 0.01f), UV = new float2(0, 1), Color = windowColor });
                }
            }
        }

        private static void GenerateBox(ref DynamicBuffer<MeshVertex> vertices, float w, float d, float h, float4 color)
        {
            // Front face
            vertices.Add(new MeshVertex { Position = new float3(-w, 0, d), Normal = new float3(0, 0, 1), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(w, 0, d), Normal = new float3(0, 0, 1), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(w, h, d), Normal = new float3(0, 0, 1), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(-w, h, d), Normal = new float3(0, 0, 1), Color = color });

            // Back face
            vertices.Add(new MeshVertex { Position = new float3(w, 0, -d), Normal = new float3(0, 0, -1), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(-w, 0, -d), Normal = new float3(0, 0, -1), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(-w, h, -d), Normal = new float3(0, 0, -1), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(w, h, -d), Normal = new float3(0, 0, -1), Color = color });

            // Left face
            vertices.Add(new MeshVertex { Position = new float3(-w, 0, -d), Normal = new float3(-1, 0, 0), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(-w, 0, d), Normal = new float3(-1, 0, 0), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(-w, h, d), Normal = new float3(-1, 0, 0), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(-w, h, -d), Normal = new float3(-1, 0, 0), Color = color });

            // Right face
            vertices.Add(new MeshVertex { Position = new float3(w, 0, d), Normal = new float3(1, 0, 0), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(w, 0, -d), Normal = new float3(1, 0, 0), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(w, h, -d), Normal = new float3(1, 0, 0), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(w, h, d), Normal = new float3(1, 0, 0), Color = color });

            // Top face
            vertices.Add(new MeshVertex { Position = new float3(-w, h, d), Normal = new float3(0, 1, 0), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(w, h, d), Normal = new float3(0, 1, 0), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(w, h, -d), Normal = new float3(0, 1, 0), Color = color });
            vertices.Add(new MeshVertex { Position = new float3(-w, h, -d), Normal = new float3(0, 1, 0), Color = color });
        }
    }

    /// <summary>
    /// Initializes city generation singletons.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct CityInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Create city generation state
            if (!SystemAPI.HasSingleton<CityGenerationState>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, CityGenerationState.Default);
                state.EntityManager.AddComponentData(entity, LODThresholds.Default);
                state.EntityManager.AddComponentData(entity, SkylineProfile.Default);
                state.EntityManager.AddComponentData(entity, CityLightingState.Default);
                state.EntityManager.SetName(entity, "CityGenerationState");
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;
        }
    }
}
