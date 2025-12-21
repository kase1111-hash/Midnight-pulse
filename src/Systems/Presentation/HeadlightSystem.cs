// ============================================================================
// Nightflow - Headlight System
// Dynamic headlight rendering for player vehicle
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages player vehicle headlights.
    /// Calculates light cone positions and intensities.
    ///
    /// From spec:
    /// - Headlights: Dynamic, raytraced (if available)
    /// - Illuminates road ahead
    /// - High beam toggle for tunnels
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraSystem))]
    [UpdateBefore(typeof(WireframeRenderSystem))]
    public partial struct HeadlightSystem : ISystem
    {
        // Headlight parameters
        private const float HighBeamRangeMultiplier = 1.5f;
        private const float HighBeamIntensityMultiplier = 1.3f;
        private const float TunnelAutoHighBeam = 0.8f;

        // Speed-based adjustments
        private const float SpeedIntensityBoost = 0.3f;
        private const float SpeedRangeBoost = 20f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get tunnel state for auto high-beam
            bool inTunnel = false;
            float tunnelBlend = 0f;

            foreach (var tunnelLighting in SystemAPI.Query<RefRO<TunnelLighting>>())
            {
                inTunnel = tunnelLighting.ValueRO.IsInTunnel;
                tunnelBlend = tunnelLighting.ValueRO.TunnelBlend;
                break;
            }

            // Update player headlights
            foreach (var (headlight, velocity, transform) in
                SystemAPI.Query<RefRW<Headlight>, RefRO<Velocity>, RefRO<WorldTransform>>()
                    .WithAll<PlayerVehicleTag>())
            {
                float speed = velocity.ValueRO.Forward;
                float speedNorm = math.saturate(speed / 70f);

                // Speed-based intensity boost
                float intensityBoost = speedNorm * SpeedIntensityBoost;
                float rangeBoost = speedNorm * SpeedRangeBoost;

                // Auto high-beam in tunnels
                bool useHighBeam = headlight.ValueRO.HighBeam;
                if (tunnelBlend > TunnelAutoHighBeam)
                {
                    useHighBeam = true;
                }

                // Calculate effective values
                float effectiveIntensity = headlight.ValueRO.Intensity + intensityBoost;
                float effectiveRange = headlight.ValueRO.Range + rangeBoost;

                if (useHighBeam)
                {
                    effectiveIntensity *= HighBeamIntensityMultiplier;
                    effectiveRange *= HighBeamRangeMultiplier;
                }

                // Update headlight state (for shader bridge)
                // The actual light positions are calculated from transform + offsets
                headlight.ValueRW.Intensity = effectiveIntensity;
                headlight.ValueRW.Range = effectiveRange;
            }

            // Update render state with motion blur based on speed
            foreach (var (renderState, velocity) in
                SystemAPI.Query<RefRW<RenderState>>()
                    .WithNone<PlayerVehicleTag>())
            {
                // Get player speed
                float playerSpeed = 0f;
                foreach (var vel in SystemAPI.Query<RefRO<Velocity>>().WithAll<PlayerVehicleTag>())
                {
                    playerSpeed = vel.ValueRO.Forward;
                    break;
                }

                // Motion blur intensity ∝ speed² (per spec)
                float speedNorm = math.saturate(playerSpeed / 70f);
                float motionBlurIntensity = speedNorm * speedNorm * 0.5f;

                renderState.ValueRW.MotionBlurIntensity = motionBlurIntensity;

                // Chromatic aberration increases with speed
                renderState.ValueRW.ChromaticAberration = speedNorm * 0.02f;

                // Vignette slightly increases at high speed
                renderState.ValueRW.Vignette = 0.3f + speedNorm * 0.15f;
            }
        }
    }
}
