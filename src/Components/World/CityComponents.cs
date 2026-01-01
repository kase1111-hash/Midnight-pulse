// ============================================================================
// Nightflow - Unity DOTS Components: Procedural City System
// GPU-light implementation with aggressive LOD for raytracing compatibility
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    // =========================================================================
    // City Generation State
    // =========================================================================

    /// <summary>
    /// Singleton for city generation state and configuration.
    /// Manages procedural building placement and LOD budgets.
    ///
    /// GPU-Light Design:
    /// - Impostor billboards for distant buildings (LOD2+)
    /// - Simple box geometry for mid-range (LOD1)
    /// - Detail only within 100m (LOD0)
    /// - Aggressive frustum + distance culling
    /// </summary>
    public struct CityGenerationState : IComponentData
    {
        /// <summary>Current generation seed.</summary>
        public uint Seed;

        /// <summary>World Z position of generation frontier.</summary>
        public float GenerationFrontier;

        /// <summary>How far ahead to generate (meters).</summary>
        public float GenerationDistance;

        /// <summary>How far behind player before cleanup.</summary>
        public float CleanupDistance;

        /// <summary>Current number of active buildings.</summary>
        public int ActiveBuildingCount;

        /// <summary>Maximum buildings allowed (GPU budget).</summary>
        public int MaxBuildings;

        /// <summary>Current number of active impostors.</summary>
        public int ActiveImpostorCount;

        /// <summary>Maximum impostors allowed.</summary>
        public int MaxImpostors;

        /// <summary>Buildings generated this frame.</summary>
        public int GeneratedThisFrame;

        /// <summary>Max buildings to generate per frame (spread load).</summary>
        public int MaxGenerationPerFrame;

        /// <summary>City density (0-1, affects building spacing).</summary>
        public float Density;

        /// <summary>Whether generation is paused.</summary>
        public bool Paused;

        public static CityGenerationState Default => new CityGenerationState
        {
            Seed = 0,
            GenerationFrontier = 0f,
            GenerationDistance = 500f,
            CleanupDistance = 100f,
            ActiveBuildingCount = 0,
            MaxBuildings = 256,         // Low for GPU budget
            ActiveImpostorCount = 0,
            MaxImpostors = 512,         // Impostors are cheaper
            GeneratedThisFrame = 0,
            MaxGenerationPerFrame = 4,  // Spread generation across frames
            Density = 0.6f,
            Paused = false
        };
    }

    // =========================================================================
    // Building Components
    // =========================================================================

    /// <summary>
    /// Procedural building definition.
    /// Defines shape and style for generation.
    /// </summary>
    public struct BuildingDefinition : IComponentData
    {
        /// <summary>Unique building ID (for instancing).</summary>
        public int BuildingId;

        /// <summary>Building footprint width (X axis, meters).</summary>
        public float Width;

        /// <summary>Building footprint depth (Z axis, meters).</summary>
        public float Depth;

        /// <summary>Building height (meters).</summary>
        public float Height;

        /// <summary>Number of floors (visual).</summary>
        public int FloorCount;

        /// <summary>Building style/type.</summary>
        public BuildingStyle Style;

        /// <summary>Base wireframe color.</summary>
        public float3 WireframeColor;

        /// <summary>Window glow color.</summary>
        public float3 WindowColor;

        /// <summary>Window density (0-1).</summary>
        public float WindowDensity;

        /// <summary>Whether this building has rooftop details.</summary>
        public bool HasRooftopDetail;

        /// <summary>Setback per floor (for tapered buildings).</summary>
        public float FloorSetback;
    }

    /// <summary>
    /// Building visual styles.
    /// </summary>
    public enum BuildingStyle : byte
    {
        Box = 0,            // Simple rectangular box
        Tower = 1,          // Tall, narrow tower
        Stepped = 2,        // Setback/stepped profile
        LShape = 3,         // L-shaped footprint
        Cylinder = 4,       // Cylindrical tower (8-sided)
        Wedge = 5,          // Triangular/wedge shape
        Billboard = 6       // 2D impostor only (far distance)
    }

    /// <summary>
    /// Building LOD state.
    /// Manages level-of-detail transitions.
    /// </summary>
    public struct BuildingLOD : IComponentData
    {
        /// <summary>Current LOD level (0 = highest detail).</summary>
        public int CurrentLOD;

        /// <summary>Target LOD based on distance.</summary>
        public int TargetLOD;

        /// <summary>Transition progress (0-1).</summary>
        public float TransitionProgress;

        /// <summary>Distance to camera (cached).</summary>
        public float DistanceToCamera;

        /// <summary>Whether building is visible (frustum check).</summary>
        public bool IsVisible;

        /// <summary>Whether building is within render distance.</summary>
        public bool InRenderDistance;

        /// <summary>Frame when last updated.</summary>
        public int LastUpdateFrame;
    }

    /// <summary>
    /// LOD distance thresholds.
    /// GPU-light: aggressive LOD to save draw calls.
    /// </summary>
    public struct LODThresholds : IComponentData
    {
        /// <summary>Distance for LOD0 (full detail).</summary>
        public float LOD0Distance;

        /// <summary>Distance for LOD1 (reduced detail).</summary>
        public float LOD1Distance;

        /// <summary>Distance for LOD2 (minimal/impostor).</summary>
        public float LOD2Distance;

        /// <summary>Distance beyond which building is culled.</summary>
        public float CullDistance;

        /// <summary>Fade distance for smooth transitions.</summary>
        public float FadeDistance;

        public static LODThresholds Default => new LODThresholds
        {
            LOD0Distance = 50f,     // Full detail within 50m
            LOD1Distance = 150f,    // Reduced detail 50-150m
            LOD2Distance = 400f,    // Impostor 150-400m
            CullDistance = 600f,    // Cull beyond 600m
            FadeDistance = 20f      // 20m fade transition
        };
    }

    // =========================================================================
    // Impostor System (Ultra-Light Distant Buildings)
    // =========================================================================

    /// <summary>
    /// Building impostor for distant rendering.
    /// Single quad with baked silhouette - extremely GPU cheap.
    /// </summary>
    public struct BuildingImpostor : IComponentData
    {
        /// <summary>World position (base center).</summary>
        public float3 Position;

        /// <summary>Impostor width.</summary>
        public float Width;

        /// <summary>Impostor height.</summary>
        public float Height;

        /// <summary>Base silhouette color.</summary>
        public float3 SilhouetteColor;

        /// <summary>Window pattern seed.</summary>
        public uint WindowSeed;

        /// <summary>Number of lit windows (randomized).</summary>
        public int LitWindowCount;

        /// <summary>Window glow intensity.</summary>
        public float WindowGlow;

        /// <summary>Alpha for distance fade.</summary>
        public float Alpha;

        /// <summary>Whether impostor faces camera (billboard).</summary>
        public bool IsBillboard;
    }

    /// <summary>
    /// City block for organized generation.
    /// Groups buildings for efficient culling.
    /// </summary>
    public struct CityBlock : IComponentData
    {
        /// <summary>Block center position.</summary>
        public float3 Center;

        /// <summary>Block size (square, meters).</summary>
        public float Size;

        /// <summary>Block seed for deterministic generation.</summary>
        public uint Seed;

        /// <summary>Number of buildings in block.</summary>
        public int BuildingCount;

        /// <summary>Block side (left or right of road).</summary>
        public CitySide Side;

        /// <summary>Block is generated.</summary>
        public bool IsGenerated;

        /// <summary>Block is visible (coarse frustum check).</summary>
        public bool IsVisible;

        /// <summary>Distance to player (for LOD).</summary>
        public float DistanceToPlayer;
    }

    /// <summary>
    /// Which side of the road the city is on.
    /// </summary>
    public enum CitySide : byte
    {
        Left = 0,
        Right = 1,
        Both = 2
    }

    // =========================================================================
    // City Lighting
    // =========================================================================

    /// <summary>
    /// City lighting state.
    /// Manages ambient city glow and window lights.
    /// </summary>
    public struct CityLightingState : IComponentData
    {
        /// <summary>Overall city ambient glow intensity.</summary>
        public float AmbientGlow;

        /// <summary>City ambient color.</summary>
        public float3 AmbientColor;

        /// <summary>Percentage of windows lit.</summary>
        public float WindowLitPercent;

        /// <summary>Base window glow intensity.</summary>
        public float WindowGlowBase;

        /// <summary>Window flicker speed.</summary>
        public float FlickerSpeed;

        /// <summary>Flicker intensity variation.</summary>
        public float FlickerAmount;

        /// <summary>Whether city is in "night mode".</summary>
        public bool IsNightMode;

        /// <summary>Fog/haze density for distant buildings.</summary>
        public float HazeDensity;

        /// <summary>Haze color.</summary>
        public float3 HazeColor;

        public static CityLightingState Default => new CityLightingState
        {
            AmbientGlow = 0.3f,
            AmbientColor = new float3(0.1f, 0.15f, 0.3f),  // Deep blue
            WindowLitPercent = 0.4f,
            WindowGlowBase = 0.8f,
            FlickerSpeed = 0.5f,
            FlickerAmount = 0.1f,
            IsNightMode = true,
            HazeDensity = 0.002f,
            HazeColor = new float3(0.15f, 0.1f, 0.2f)  // Purple haze
        };
    }

    /// <summary>
    /// Window light state for a building.
    /// Tracks which windows are lit for consistent rendering.
    /// </summary>
    public struct WindowLightState : IComponentData
    {
        /// <summary>Seed for window pattern.</summary>
        public uint PatternSeed;

        /// <summary>Number of window columns.</summary>
        public int Columns;

        /// <summary>Number of window rows (floors).</summary>
        public int Rows;

        /// <summary>Packed lit window flags (up to 64).</summary>
        public ulong LitFlags;

        /// <summary>Current glow intensity.</summary>
        public float GlowIntensity;

        /// <summary>Flicker phase.</summary>
        public float FlickerPhase;
    }

    // =========================================================================
    // Skyline Definition
    // =========================================================================

    /// <summary>
    /// Skyline profile for city silhouette.
    /// Defines height variation along the road.
    /// </summary>
    public struct SkylineProfile : IComponentData
    {
        /// <summary>Minimum building height.</summary>
        public float MinHeight;

        /// <summary>Maximum building height.</summary>
        public float MaxHeight;

        /// <summary>Height variation frequency.</summary>
        public float HeightFrequency;

        /// <summary>Height variation amplitude.</summary>
        public float HeightAmplitude;

        /// <summary>Cluster size (buildings of similar height).</summary>
        public float ClusterSize;

        /// <summary>Downtown zone center Z.</summary>
        public float DowntownCenterZ;

        /// <summary>Downtown zone radius.</summary>
        public float DowntownRadius;

        /// <summary>Downtown height multiplier.</summary>
        public float DowntownHeightMultiplier;

        public static SkylineProfile Default => new SkylineProfile
        {
            MinHeight = 15f,
            MaxHeight = 80f,
            HeightFrequency = 0.01f,
            HeightAmplitude = 0.5f,
            ClusterSize = 50f,
            DowntownCenterZ = 1000f,
            DowntownRadius = 500f,
            DowntownHeightMultiplier = 2f
        };
    }

    // =========================================================================
    // GPU Instancing Support
    // =========================================================================

    /// <summary>
    /// Instance data for GPU instanced rendering.
    /// Minimal data per instance for efficient batching.
    /// </summary>
    public struct BuildingInstance : IComponentData
    {
        /// <summary>Instance group ID (for batching).</summary>
        public int GroupId;

        /// <summary>Transform matrix (packed).</summary>
        public float4x4 Transform;

        /// <summary>Color tint.</summary>
        public float4 Color;

        /// <summary>LOD bias (for per-instance LOD).</summary>
        public float LODBias;
    }

    /// <summary>
    /// Building mesh data indices.
    /// References shared mesh data for instancing.
    /// </summary>
    public struct BuildingMeshRef : IComponentData
    {
        /// <summary>Mesh template index.</summary>
        public int MeshTemplateIndex;

        /// <summary>LOD0 vertex count.</summary>
        public int LOD0VertexCount;

        /// <summary>LOD1 vertex count.</summary>
        public int LOD1VertexCount;

        /// <summary>LOD2 vertex count (impostor = 4).</summary>
        public int LOD2VertexCount;
    }
}

namespace Nightflow.Buffers
{
    using Unity.Mathematics;

    /// <summary>
    /// Building position buffer for a city block.
    /// Stores building placements within the block.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct BlockBuildingPosition : IBufferElementData
    {
        /// <summary>Local offset from block center.</summary>
        public float2 LocalOffset;

        /// <summary>Building rotation (Y axis).</summary>
        public float Rotation;

        /// <summary>Building template index.</summary>
        public int TemplateIndex;

        /// <summary>Height override (0 = use template).</summary>
        public float HeightOverride;
    }

    /// <summary>
    /// Simple vertex for impostor geometry.
    /// Minimal data for GPU-light rendering.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ImpostorVertex : IBufferElementData
    {
        /// <summary>Position relative to impostor center.</summary>
        public float3 Position;

        /// <summary>UV for window pattern.</summary>
        public float2 UV;

        /// <summary>Vertex color (silhouette + glow).</summary>
        public float4 Color;
    }
}

namespace Nightflow.Tags
{
    using Unity.Entities;

    /// <summary>Tag for procedural building entities.</summary>
    public struct BuildingTag : IComponentData { }

    /// <summary>Tag for impostor entities (distant billboards).</summary>
    public struct ImpostorTag : IComponentData { }

    /// <summary>Tag for city block entities.</summary>
    public struct CityBlockTag : IComponentData { }

    /// <summary>Tag for buildings pending mesh generation.</summary>
    public struct PendingMeshTag : IComponentData { }
}
