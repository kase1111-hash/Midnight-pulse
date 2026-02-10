// ============================================================================
// Nightflow - City Lighting System
// Dynamic city lights with distance-based rendering
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages dynamic city lighting for procedural buildings.
    /// GPU-light: uses vertex colors and simple emission.
    ///
    /// From spec:
    /// - Dynamic city lights based on time/distance
    ///
    /// Features:
    /// - Window lights with random on/off patterns
    /// - Flicker effects for realism
    /// - Distance-based light intensity
    /// - Haze/fog for depth
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(BuildingFadeSystem))]
    public partial struct CityLightingSystem : ISystem
    {
        // Lighting parameters
        private const float WindowFlickerSpeed = 2f;
        private const float WindowFlickerAmount = 0.15f;
        private const float DistanceGlowFalloff = 0.003f;
        private const float MaxGlowDistance = 300f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get camera position for distance calculations
            float3 cameraPos = float3.zero;
            foreach (var camera in SystemAPI.Query<RefRO<CameraState>>())
            {
                cameraPos = camera.ValueRO.Position;
                break;
            }

            // Get city lighting state
            CityLightingState lighting = CityLightingState.Default;
            foreach (var lightState in SystemAPI.Query<RefRO<CityLightingState>>())
            {
                lighting = lightState.ValueRO;
                break;
            }

            // =============================================================
            // Update Window Lights
            // =============================================================

            foreach (var (windowState, buildingDef, lod, transform) in
                SystemAPI.Query<RefRW<WindowLightState>, RefRO<BuildingDefinition>,
                               RefRO<BuildingLOD>, RefRO<WorldTransform>>()
                    .WithAll<BuildingTag>())
            {
                if (!lod.ValueRO.IsVisible) continue;
                if (lod.ValueRO.CurrentLOD > 1) continue; // No window detail at LOD2+

                ref var window = ref windowState.ValueRW;

                // Update flicker phase
                window.FlickerPhase += deltaTime * lighting.FlickerSpeed;
                if (window.FlickerPhase > math.PI * 2f)
                {
                    window.FlickerPhase -= math.PI * 2f;
                }

                // Calculate distance-based intensity
                float distance = lod.ValueRO.DistanceToCamera;
                float distanceFactor = 1f - math.saturate(distance / MaxGlowDistance);
                distanceFactor = distanceFactor * distanceFactor; // Quadratic falloff

                // Base glow with flicker
                float baseGlow = lighting.WindowGlowBase * distanceFactor;
                float flicker = math.sin(window.FlickerPhase * WindowFlickerSpeed +
                               window.PatternSeed * 0.1f) * WindowFlickerAmount;

                window.GlowIntensity = baseGlow * (1f + flicker);

                // Occasionally toggle windows on/off
                if (math.frac(time * 0.1f + window.PatternSeed * 0.001f) < 0.001f)
                {
                    // Random window toggle
                    uint toggleBit = Hash(window.PatternSeed + (uint)(time * 100)) % 64;
                    window.LitFlags ^= (1UL << (int)toggleBit);
                }
            }

            // =============================================================
            // Apply Lighting to Building Mesh Colors
            // =============================================================

            foreach (var (windowState, buildingDef, lod, meshData, entity) in
                SystemAPI.Query<RefRO<WindowLightState>, RefRO<BuildingDefinition>,
                               RefRO<BuildingLOD>, RefRW<VehicleMeshData>>()
                    .WithAll<BuildingTag>()
                    .WithEntityAccess())
            {
                if (!meshData.ValueRO.IsGenerated) continue;
                if (!SystemAPI.HasBuffer<MeshVertex>(entity)) continue;
                if (!lod.ValueRO.IsVisible) continue;

                var vertices = SystemAPI.GetBuffer<MeshVertex>(entity);
                if (vertices.Length == 0) continue;

                // Calculate final glow color
                float glow = windowState.ValueRO.GlowIntensity;
                float3 glowColor = buildingDef.ValueRO.WindowColor * glow;

                // Apply haze based on distance
                float distance = lod.ValueRO.DistanceToCamera;
                float hazeFactor = 1f - math.exp(-distance * lighting.HazeDensity);
                float3 hazeColor = lighting.HazeColor;

                // Update vertex colors for window vertices (if present)
                // Window vertices are at the end of the buffer (after box geometry)
                if (lod.ValueRO.CurrentLOD == 0 && vertices.Length > 24)
                {
                    // LOD0 has window vertices starting at index 24
                    for (int i = 24; i < vertices.Length; i++)
                    {
                        var vertex = vertices[i];

                        // Blend window color with haze
                        float3 finalColor = math.lerp(glowColor, hazeColor, hazeFactor * 0.5f);
                        vertex.Color = new float4(finalColor, vertex.Color.w * glow);

                        vertices[i] = vertex;
                    }
                }

                // Apply ambient haze to building silhouette
                for (int i = 0; i < math.min(24, vertices.Length); i++)
                {
                    var vertex = vertices[i];

                    // Blend wireframe color with haze
                    float3 baseColor = buildingDef.ValueRO.WireframeColor;
                    float3 finalColor = math.lerp(baseColor, hazeColor, hazeFactor * 0.3f);

                    // Add subtle ambient glow
                    finalColor += lighting.AmbientColor * lighting.AmbientGlow * (1f - hazeFactor);

                    vertex.Color = new float4(finalColor, meshData.ValueRO.WireframeColor.w);
                    vertices[i] = vertex;
                }
            }

            // =============================================================
            // Update Impostor Lighting
            // =============================================================

            foreach (var (impostor, lod) in
                SystemAPI.Query<RefRW<BuildingImpostor>, RefRO<BuildingLOD>>()
                    .WithAll<ImpostorTag>())
            {
                if (!lod.ValueRO.IsVisible) continue;

                float distance = lod.ValueRO.DistanceToCamera;

                // Distant glow effect
                float glowFactor = 1f - math.saturate(distance / MaxGlowDistance);
                impostor.ValueRW.WindowGlow = lighting.WindowGlowBase * glowFactor * 0.5f;

                // Apply haze to silhouette color
                float hazeFactor = 1f - math.exp(-distance * lighting.HazeDensity);
                impostor.ValueRW.SilhouetteColor = math.lerp(
                    impostor.ValueRO.SilhouetteColor,
                    lighting.HazeColor,
                    hazeFactor * 0.4f
                );
            }
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
    }

    /// <summary>
    /// Manages city ambient lighting contribution.
    /// Adds glow to the overall scene based on visible city density.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CityLightingSystem))]
    public partial struct CityAmbientSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Calculate total city light contribution
            float totalGlow = 0f;
            int visibleCount = 0;

            foreach (var (windowState, lod) in
                SystemAPI.Query<RefRO<WindowLightState>, RefRO<BuildingLOD>>()
                    .WithAll<BuildingTag>())
            {
                if (!lod.ValueRO.IsVisible) continue;

                totalGlow += windowState.ValueRO.GlowIntensity;
                visibleCount++;
            }

            // Average glow contribution
            float avgGlow = visibleCount > 0 ? totalGlow / visibleCount : 0f;

            // Update ambient light state
            foreach (var lightState in SystemAPI.Query<RefRW<CityLightingState>>())
            {
                // Smooth transition of ambient glow
                float targetAmbient = avgGlow * 0.5f + 0.1f;
                lightState.ValueRW.AmbientGlow = math.lerp(
                    lightState.ValueRO.AmbientGlow,
                    targetAmbient,
                    SystemAPI.Time.DeltaTime * 2f
                );
            }
        }
    }

    /// <summary>
    /// Creates atmospheric depth with distance haze.
    /// Enhances the sense of scale for the city skyline.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CityAmbientSystem))]
    public partial struct CityHazeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;

            foreach (var lightState in SystemAPI.Query<RefRW<CityLightingState>>())
            {
                ref var lighting = ref lightState.ValueRW;

                // Subtle haze color variation over time
                float hueShift = math.sin(time * 0.05f) * 0.05f;
                float3 baseHaze = new float3(0.15f, 0.1f, 0.2f);

                lighting.HazeColor = baseHaze + new float3(hueShift, 0f, -hueShift * 0.5f);

                // Slight density variation
                float densityVariation = math.sin(time * 0.02f) * 0.0005f;
                lighting.HazeDensity = 0.002f + densityVariation;
            }
        }
    }

    /// <summary>
    /// Adds random lights to rooftops and building details.
    /// Sparse lights for visual interest without GPU cost.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct RooftopLightSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;

            // Only process buildings with rooftop details
            foreach (var (buildingDef, windowState, lod) in
                SystemAPI.Query<RefRO<BuildingDefinition>, RefRW<WindowLightState>,
                               RefRO<BuildingLOD>>()
                    .WithAll<BuildingTag>())
            {
                if (!buildingDef.ValueRO.HasRooftopDetail) continue;
                if (!lod.ValueRO.IsVisible) continue;
                if (lod.ValueRO.CurrentLOD > 0) continue; // Only at LOD0

                // Aircraft warning light blink
                float blinkPhase = math.frac(time * 0.5f + windowState.ValueRO.PatternSeed * 0.01f);
                bool lightOn = blinkPhase < 0.1f;

                // This would be applied to rooftop vertex colors
                // Implementation depends on mesh structure
            }
        }
    }

    /// <summary>
    /// Coordinates city lighting with existing game lighting systems.
    /// Ensures city lights blend with reflections and headlights.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ReflectionSystem))]
    public partial struct CityRTIntegrationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get RT state
            bool rtEnabled = false;
            float reflectionIntensity = 1f;

            foreach (var rtState in SystemAPI.Query<RefRO<ReflectionState>>())
            {
                rtEnabled = rtState.ValueRO.RTEnabled;
                reflectionIntensity = rtState.ValueRO.ReflectionIntensity;
                break;
            }

            // Adjust city lighting for RT compatibility
            foreach (var lightState in SystemAPI.Query<RefRW<CityLightingState>>())
            {
                if (rtEnabled)
                {
                    // Reduce window glow when RT is active (RT handles reflections)
                    lightState.ValueRW.WindowGlowBase = 0.6f * reflectionIntensity;
                }
                else
                {
                    // Higher glow when no RT (compensate for lack of reflections)
                    lightState.ValueRW.WindowGlowBase = 0.9f;
                }
            }

            // City buildings can contribute to road reflections
            foreach (var (probe, probeTransform) in
                SystemAPI.Query<RefRW<ReflectionProbe>, RefRO<WorldTransform>>())
            {
                // Accumulate city light contribution to reflection probes
                float cityContribution = 0f;

                foreach (var (windowState, lod, buildingTransform) in
                    SystemAPI.Query<RefRO<WindowLightState>, RefRO<BuildingLOD>,
                                   RefRO<WorldTransform>>()
                        .WithAll<BuildingTag>())
                {
                    if (!lod.ValueRO.IsVisible) continue;

                    float dist = math.distance(
                        probeTransform.ValueRO.Position,
                        buildingTransform.ValueRO.Position
                    );

                    if (dist < probe.ValueRO.Radius)
                    {
                        float contribution = windowState.ValueRO.GlowIntensity *
                                           (1f - dist / probe.ValueRO.Radius);
                        cityContribution += contribution * 0.1f;
                    }
                }

                // Add subtle city reflection
                probe.ValueRW.AverageLuminance += cityContribution;
            }
        }
    }
}
