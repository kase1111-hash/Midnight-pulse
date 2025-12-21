// ============================================================================
// Nightflow - Tunnel System
// Handles tunnel entry/exit, lighting reduction, and camera squeeze
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages tunnel transitions and effects.
    ///
    /// From spec:
    /// - Tunnel flag on segment triggers:
    ///   - Reduce lighting radius
    ///   - Increase reverb (handled by AudioSystem)
    /// - Camera: Tunnel Squeeze effect when entering
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrackGenerationSystem))]
    public partial struct TunnelSystem : ISystem
    {
        // Transition parameters
        private const float TunnelBlendSpeed = 3f;
        private const float TunnelAmbientReduction = 0.3f;

        // Camera squeeze parameters
        private const float SqueezeVignetteBoost = 0.2f;
        private const float SqueezeFOVReduction = 5f;

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

            // =============================================================
            // Detect Current Tunnel State
            // =============================================================

            bool inTunnel = false;
            float tunnelProgress = 0f;
            bool isEntry = false;
            bool isExit = false;

            foreach (var (segment, tunnelData) in
                SystemAPI.Query<RefRO<TrackSegment>, RefRO<TunnelData>>()
                    .WithAll<TunnelTag>())
            {
                if (playerZ >= segment.ValueRO.StartZ && playerZ <= segment.ValueRO.EndZ)
                {
                    inTunnel = true;
                    // Calculate progress within segment
                    float segmentLength = segment.ValueRO.EndZ - segment.ValueRO.StartZ;
                    tunnelProgress = (playerZ - segment.ValueRO.StartZ) / segmentLength;
                    isEntry = tunnelData.ValueRO.IsEntry;
                    isExit = tunnelData.ValueRO.IsExit;
                    break;
                }
            }

            // =============================================================
            // Update TunnelLighting Singleton
            // =============================================================

            foreach (var tunnelLighting in SystemAPI.Query<RefRW<TunnelLighting>>())
            {
                // Smoothly transition tunnel blend
                float targetBlend = inTunnel ? 1f : 0f;

                // Faster blend on entry, slower on exit
                float blendSpeed = inTunnel ? TunnelBlendSpeed : TunnelBlendSpeed * 0.5f;

                tunnelLighting.ValueRW.TunnelBlend = math.lerp(
                    tunnelLighting.ValueRO.TunnelBlend,
                    targetBlend,
                    blendSpeed * deltaTime
                );

                tunnelLighting.ValueRW.IsInTunnel = inTunnel;

                // Adjust lighting based on tunnel state
                if (inTunnel)
                {
                    tunnelLighting.ValueRW.AmbientIntensity = TunnelAmbientReduction;
                    tunnelLighting.ValueRW.AmbientColor = new float3(0.2f, 0.15f, 0.1f); // Warm tunnel light
                }
                else
                {
                    // Restore normal ambient (lerped)
                    tunnelLighting.ValueRW.AmbientIntensity = math.lerp(
                        tunnelLighting.ValueRO.AmbientIntensity, 1f, deltaTime);
                }

                break;
            }

            // =============================================================
            // Update RenderState for Tunnel Effects
            // =============================================================

            foreach (var renderState in SystemAPI.Query<RefRW<RenderState>>())
            {
                float tunnelBlend = 0f;
                foreach (var lighting in SystemAPI.Query<RefRO<TunnelLighting>>())
                {
                    tunnelBlend = lighting.ValueRO.TunnelBlend;
                    break;
                }

                // Increase vignette in tunnel (squeeze effect)
                float baseVignette = 0.3f;
                float tunnelVignette = baseVignette + tunnelBlend * SqueezeVignetteBoost;
                renderState.ValueRW.Vignette = math.lerp(
                    renderState.ValueRO.Vignette,
                    tunnelVignette,
                    5f * deltaTime
                );

                // Reduce ambient intensity in tunnel
                float tunnelAmbient = 0.05f * (1f - tunnelBlend * 0.7f);
                renderState.ValueRW.AmbientIntensity = math.lerp(
                    renderState.ValueRO.AmbientIntensity,
                    math.max(tunnelAmbient, 0.02f),
                    3f * deltaTime
                );

                // Reduce city glow in tunnel
                float tunnelCityGlow = 0.3f * (1f - tunnelBlend * 0.8f);
                renderState.ValueRW.CityGlowIntensity = math.lerp(
                    renderState.ValueRO.CityGlowIntensity,
                    tunnelCityGlow,
                    2f * deltaTime
                );

                break;
            }

            // =============================================================
            // Update Camera for Tunnel Squeeze
            // =============================================================

            foreach (var cameraState in SystemAPI.Query<RefRW<CameraState>>()
                .WithAll<PlayerVehicleTag>())
            {
                float tunnelBlend = 0f;
                foreach (var lighting in SystemAPI.Query<RefRO<TunnelLighting>>())
                {
                    tunnelBlend = lighting.ValueRO.TunnelBlend;
                    break;
                }

                // Apply FOV squeeze on entry
                if (isEntry && tunnelProgress < 0.3f)
                {
                    float squeezeAmount = (0.3f - tunnelProgress) / 0.3f;
                    cameraState.ValueRW.TargetFOV -= SqueezeFOVReduction * squeezeAmount * tunnelBlend;
                }

                break;
            }
        }
    }
}
