// ============================================================================
// Nightflow - City Skyline System
// Generates and renders distant city backdrop with animated window lights
// Execution Order: Early in Presentation Group (before other rendering)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Generates and animates a procedural city skyline that wraps around the horizon.
    /// Creates silhouetted skyscrapers with lit windows that turn on/off dynamically.
    ///
    /// Visual Design:
    /// - Buildings are dark silhouettes against the night sky
    /// - Windows are mostly warm yellow, with some white, orange, and cyan accents
    /// - Windows randomly toggle on/off to simulate life in the city
    /// - Skyline follows the player, always visible on the horizon
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial struct CitySkylineSystem : ISystem
    {
        // Skyline generation parameters
        private const float DefaultSkylineDistance = 800f;      // Distance to skyline (meters)
        private const float DefaultMinHeight = 40f;             // Shortest building (meters)
        private const float DefaultMaxHeight = 280f;            // Tallest skyscraper (meters)
        private const int DefaultBuildingCount = 120;           // Buildings around 360 degrees

        // Window parameters
        private const float DefaultWindowLitRatio = 0.65f;      // 65% of windows lit
        private const float DefaultYellowRatio = 0.75f;         // 75% of lit windows are yellow
        private const float DefaultToggleInterval = 8f;         // Average seconds between toggles
        private const float DefaultToggleVariance = 4f;         // +/- variance

        // Building generation parameters
        private const int MinWindowColumns = 4;
        private const int MaxWindowColumns = 12;
        private const int MinWindowRows = 8;
        private const int MaxWindowRows = 40;
        private const float MinBuildingWidth = 0.015f;          // Radians
        private const float MaxBuildingWidth = 0.045f;          // Radians

        private bool _initialized;
        private Random _random;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _initialized = false;
            _random = Random.CreateFromIndex(12345);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

            // Check if skyline entity exists
            bool hasSkyline = false;
            Entity skylineEntity = Entity.Null;

            foreach (var (skylineState, entity) in
                SystemAPI.Query<RefRW<CitySkylineState>>()
                    .WithAll<CitySkylineTag>()
                    .WithEntityAccess())
            {
                hasSkyline = true;
                skylineEntity = entity;

                // Update animation time
                skylineState.ValueRW.AnimationTime += deltaTime;

                // Check if regeneration needed
                if (skylineState.ValueRO.NeedsRegeneration)
                {
                    RegenerateSkyline(ref state, entity, ref skylineState.ValueRW);
                }

                // Animate windows
                AnimateWindows(ref state, entity, skylineState.ValueRO, elapsedTime);
            }

            // Create skyline entity if it doesn't exist
            if (!hasSkyline && !_initialized)
            {
                CreateSkylineEntity(ref state);
                _initialized = true;
            }
        }

        [BurstCompile]
        private void CreateSkylineEntity(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            Entity entity = ecb.CreateEntity();

            // Add tag
            ecb.AddComponent<CitySkylineTag>(entity);

            // Add state
            ecb.AddComponent(entity, new CitySkylineState
            {
                SkylineDistance = DefaultSkylineDistance,
                MinBuildingHeight = DefaultMinHeight,
                MaxBuildingHeight = DefaultMaxHeight,
                BuildingCount = DefaultBuildingCount,
                AnimationTime = 0f,
                RandomSeed = 54321,
                NeedsRegeneration = true
            });

            // Add config with colors
            ecb.AddComponent(entity, new SkylineConfig
            {
                WindowLitRatio = DefaultWindowLitRatio,
                YellowWindowRatio = DefaultYellowRatio,
                WindowToggleInterval = DefaultToggleInterval,
                ToggleIntervalVariance = DefaultToggleVariance,
                // Dark blue-black silhouette
                SilhouetteColor = new float4(0.02f, 0.02f, 0.05f, 1f),
                // Warm yellow (primary - 75%)
                WindowColorYellow = new float4(1.0f, 0.85f, 0.4f, 1f),
                // Cool white (10%)
                WindowColorWhite = new float4(0.95f, 0.95f, 1.0f, 1f),
                // Warm orange (10%)
                WindowColorOrange = new float4(1.0f, 0.6f, 0.2f, 1f),
                // Cyan accent (5%)
                WindowColorCyan = new float4(0.4f, 0.9f, 1.0f, 1f)
            });

            // Add buffers for buildings and window states
            ecb.AddBuffer<SkylineBuilding>(entity);
            ecb.AddBuffer<WindowStateBlock>(entity);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private void RegenerateSkyline(ref SystemState state, Entity entity, ref CitySkylineState skylineState)
        {
            var buildings = SystemAPI.GetBuffer<SkylineBuilding>(entity);
            var windowBlocks = SystemAPI.GetBuffer<WindowStateBlock>(entity);

            buildings.Clear();
            windowBlocks.Clear();

            // Initialize random with seed
            var random = Random.CreateFromIndex(skylineState.RandomSeed);

            int buildingCount = skylineState.BuildingCount;
            float angleStep = math.PI * 2f / buildingCount;
            float currentAngle = 0f;

            // Generate buildings around the horizon
            for (int i = 0; i < buildingCount; i++)
            {
                // Randomize building properties
                float widthAngle = random.NextFloat(MinBuildingWidth, MaxBuildingWidth);

                // Height distribution: more medium buildings, fewer very tall ones
                float heightRandom = random.NextFloat();
                float height;
                if (heightRandom < 0.5f)
                {
                    // 50% chance: short to medium (0.2 - 0.5)
                    height = 0.2f + heightRandom * 0.6f;
                }
                else if (heightRandom < 0.85f)
                {
                    // 35% chance: medium to tall (0.5 - 0.8)
                    height = 0.5f + (heightRandom - 0.5f) * 0.86f;
                }
                else
                {
                    // 15% chance: very tall skyscrapers (0.8 - 1.0)
                    height = 0.8f + (heightRandom - 0.85f) * 1.33f;
                }

                // Calculate window grid based on building size
                int windowCols = (int)math.lerp(MinWindowColumns, MaxWindowColumns, widthAngle / MaxBuildingWidth);
                int windowRows = (int)math.lerp(MinWindowRows, MaxWindowRows, height);

                // Style variant affects window pattern
                int style = random.NextInt(0, 4);

                buildings.Add(new SkylineBuilding
                {
                    Angle = currentAngle,
                    WidthAngle = widthAngle,
                    Height = height,
                    WindowColumns = windowCols,
                    WindowRows = windowRows,
                    WindowSeed = random.NextUInt(),
                    StyleVariant = style
                });

                // Generate window state blocks for this building
                GenerateWindowBlocks(ref windowBlocks, i, windowCols, windowRows, random.NextUInt());

                // Move to next building position with small gap
                currentAngle += widthAngle + random.NextFloat(0.005f, 0.015f);

                // Wrap around if needed (with overlap for seamless look)
                if (currentAngle > math.PI * 2f && i < buildingCount - 1)
                {
                    currentAngle -= math.PI * 2f;
                }
            }

            skylineState.NeedsRegeneration = false;
        }

        [BurstCompile]
        private void GenerateWindowBlocks(ref DynamicBuffer<WindowStateBlock> windowBlocks,
            int buildingIndex, int windowCols, int windowRows, uint baseSeed)
        {
            int totalWindows = windowCols * windowRows;
            int blocksNeeded = (totalWindows + 31) / 32; // 32 windows per block

            var random = Random.CreateFromIndex(baseSeed);

            for (int blockIdx = 0; blockIdx < blocksNeeded; blockIdx++)
            {
                // Generate initial lit/unlit state
                uint packedState = 0;
                uint packedColors = 0;

                for (int bit = 0; bit < 32; bit++)
                {
                    int windowIdx = blockIdx * 32 + bit;
                    if (windowIdx >= totalWindows) break;

                    // Determine if window is lit (65% chance)
                    bool isLit = random.NextFloat() < DefaultWindowLitRatio;
                    if (isLit)
                    {
                        packedState |= (1u << bit);
                    }

                    // Determine window color (2 bits per window, but we can only fit 16 in uint)
                    // 0 = yellow (75%), 1 = white (10%), 2 = orange (10%), 3 = cyan (5%)
                    if (bit < 16)
                    {
                        float colorRoll = random.NextFloat();
                        uint colorBits;
                        if (colorRoll < 0.75f)
                            colorBits = 0; // Yellow
                        else if (colorRoll < 0.85f)
                            colorBits = 1; // White
                        else if (colorRoll < 0.95f)
                            colorBits = 2; // Orange
                        else
                            colorBits = 3; // Cyan

                        packedColors |= (colorBits << (bit * 2));
                    }
                }

                // Random time until first toggle
                float nextChange = random.NextFloat(DefaultToggleInterval - DefaultToggleVariance,
                    DefaultToggleInterval + DefaultToggleVariance);

                windowBlocks.Add(new WindowStateBlock
                {
                    BuildingIndex = buildingIndex,
                    BlockIndex = blockIdx,
                    PackedState = packedState,
                    PackedColors = packedColors,
                    NextChangeTime = nextChange
                });
            }
        }

        [BurstCompile]
        private void AnimateWindows(ref SystemState state, Entity entity,
            CitySkylineState skylineState, float elapsedTime)
        {
            var windowBlocks = SystemAPI.GetBuffer<WindowStateBlock>(entity);

            for (int i = 0; i < windowBlocks.Length; i++)
            {
                var block = windowBlocks[i];

                // Check if it's time to toggle some windows
                if (skylineState.AnimationTime >= block.NextChangeTime)
                {
                    // Use building seed + time for deterministic randomness
                    uint seed = (uint)(block.BuildingIndex * 1000 + block.BlockIndex * 100 + (int)(elapsedTime * 10));
                    var random = Random.CreateFromIndex(seed);

                    // Toggle 1-3 random windows in this block
                    int toggleCount = random.NextInt(1, 4);
                    uint newState = block.PackedState;

                    for (int t = 0; t < toggleCount; t++)
                    {
                        int bitToToggle = random.NextInt(0, 32);
                        newState ^= (1u << bitToToggle);
                    }

                    // Schedule next change
                    float nextChange = skylineState.AnimationTime +
                        random.NextFloat(DefaultToggleInterval - DefaultToggleVariance,
                            DefaultToggleInterval + DefaultToggleVariance);

                    block.PackedState = newState;
                    block.NextChangeTime = nextChange;
                    windowBlocks[i] = block;
                }
            }
        }

        /// <summary>
        /// Gets the world position for a point on the skyline.
        /// Used by rendering systems to position skyline geometry.
        /// </summary>
        public static float3 GetSkylinePosition(float3 playerPosition, float angle, float distance, float height)
        {
            float x = playerPosition.x + math.cos(angle) * distance;
            float z = playerPosition.z + math.sin(angle) * distance;
            return new float3(x, height, z);
        }

        /// <summary>
        /// Gets the window color based on packed color bits.
        /// </summary>
        public static float4 GetWindowColor(SkylineConfig config, uint packedColors, int windowIndex)
        {
            // Extract 2-bit color index (wraps for indices >= 16)
            int bitIndex = (windowIndex % 16) * 2;
            uint colorIndex = (packedColors >> bitIndex) & 0x3;

            return colorIndex switch
            {
                0 => config.WindowColorYellow,
                1 => config.WindowColorWhite,
                2 => config.WindowColorOrange,
                3 => config.WindowColorCyan,
                _ => config.WindowColorYellow
            };
        }

        /// <summary>
        /// Checks if a specific window is lit.
        /// </summary>
        public static bool IsWindowLit(uint packedState, int windowIndexInBlock)
        {
            return (packedState & (1u << windowIndexInBlock)) != 0;
        }
    }

    /// <summary>
    /// Renders the city skyline as procedural geometry.
    /// Creates quad strips for building silhouettes and point lights for windows.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CitySkylineSystem))]
    public partial struct CitySkylineRenderSystem : ISystem
    {
        // Rendering parameters
        private const float WindowSize = 2.5f;          // Visual size of each window
        private const float WindowGlow = 1.2f;          // Glow multiplier for lit windows
        private const float HorizonFadeStart = 0.1f;    // Start fading at bottom 10%
        private const float HorizonFadeEnd = 0.0f;      // Fully faded at ground level

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CitySkylineTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get player position for skyline centering
            float3 playerPos = float3.zero;
            foreach (var transform in SystemAPI.Query<RefRO<WorldTransform>>().WithAll<PlayerVehicleTag>())
            {
                playerPos = transform.ValueRO.Position;
                break;
            }

            // Get skyline data
            foreach (var (skylineState, config, entity) in
                SystemAPI.Query<RefRO<CitySkylineState>, RefRO<SkylineConfig>>()
                    .WithAll<CitySkylineTag>()
                    .WithEntityAccess())
            {
                var buildings = SystemAPI.GetBuffer<SkylineBuilding>(entity);
                var windowBlocks = SystemAPI.GetBuffer<WindowStateBlock>(entity);

                // Calculate render data for each building
                // (Actual GPU rendering would happen here through Unity's rendering pipeline)
                // This system prepares the data; a companion MonoBehaviour handles actual rendering

                RenderSkylineData(
                    playerPos,
                    skylineState.ValueRO,
                    config.ValueRO,
                    buildings,
                    windowBlocks
                );
            }
        }

        [BurstCompile]
        private void RenderSkylineData(
            float3 playerPos,
            CitySkylineState state,
            SkylineConfig config,
            DynamicBuffer<SkylineBuilding> buildings,
            DynamicBuffer<WindowStateBlock> windowBlocks)
        {
            float distance = state.SkylineDistance;
            float minHeight = state.MinBuildingHeight;
            float maxHeight = state.MaxBuildingHeight;

            // For each building, calculate its screen-space representation
            // This data would typically be passed to a GPU shader for rendering

            int windowBlockIdx = 0;

            for (int i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];

                // Calculate building world bounds
                float buildingHeight = minHeight + building.Height * (maxHeight - minHeight);
                float leftAngle = building.Angle;
                float rightAngle = building.Angle + building.WidthAngle;

                // Building corners in world space (follows player)
                float3 bottomLeft = CitySkylineSystem.GetSkylinePosition(playerPos, leftAngle, distance, 0);
                float3 bottomRight = CitySkylineSystem.GetSkylinePosition(playerPos, rightAngle, distance, 0);
                float3 topLeft = CitySkylineSystem.GetSkylinePosition(playerPos, leftAngle, distance, buildingHeight);
                float3 topRight = CitySkylineSystem.GetSkylinePosition(playerPos, rightAngle, distance, buildingHeight);

                // Window grid calculations
                int windowCols = building.WindowColumns;
                int windowRows = building.WindowRows;
                int totalWindows = windowCols * windowRows;
                int blocksForBuilding = (totalWindows + 31) / 32;

                // Process windows for this building
                for (int blockOffset = 0; blockOffset < blocksForBuilding && windowBlockIdx < windowBlocks.Length; blockOffset++)
                {
                    var block = windowBlocks[windowBlockIdx];
                    if (block.BuildingIndex != i)
                    {
                        // Block belongs to different building, skip
                        continue;
                    }

                    uint litMask = block.PackedState;
                    uint colorBits = block.PackedColors;

                    // Process each window in this block
                    for (int bit = 0; bit < 32; bit++)
                    {
                        int windowIdx = block.BlockIndex * 32 + bit;
                        if (windowIdx >= totalWindows) break;

                        bool isLit = CitySkylineSystem.IsWindowLit(litMask, bit);
                        if (!isLit) continue;

                        // Calculate window position within building
                        int col = windowIdx % windowCols;
                        int row = windowIdx / windowCols;

                        float u = (col + 0.5f) / windowCols;
                        float v = (row + 0.5f) / windowRows;

                        // Interpolate position on building face
                        float3 windowPos = math.lerp(
                            math.lerp(bottomLeft, bottomRight, u),
                            math.lerp(topLeft, topRight, u),
                            v
                        );

                        // Get window color
                        float4 windowColor = CitySkylineSystem.GetWindowColor(config, colorBits, bit);

                        // Apply height-based fade (windows lower on building are dimmer)
                        float fadeFactor = math.smoothstep(HorizonFadeEnd, HorizonFadeStart, v);
                        windowColor.xyz *= fadeFactor * WindowGlow;

                        // This position and color would be sent to GPU for point rendering
                        // In practice, this would populate a compute buffer or instance data
                    }

                    windowBlockIdx++;
                }
            }
        }
    }
}
