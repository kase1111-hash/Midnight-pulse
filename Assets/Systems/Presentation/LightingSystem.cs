// ============================================================================
// Nightflow - Lighting System
// Execution Order: 3 (Presentation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages dynamic lighting for the neon wireframe aesthetic.
    /// Handles bloom, ambient glow, and light accumulation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(WireframeRenderSystem))]
    public partial struct LightingSystem : ISystem
    {
        // Ambient parameters
        private const float AmbientIntensity = 0.05f;     // Very dark ambient
        private const float GridGlow = 0.1f;              // Subtle grid illumination

        // Bloom parameters
        private const float BloomThreshold = 0.8f;        // When to start blooming
        private const float BloomIntensity = 1.5f;        // Bloom multiplier
        private const float BloomRadius = 0.02f;          // Screen-space radius

        // Light accumulation
        private const float MaxLightAccum = 4f;           // HDR headroom
        private const float LightFalloff = 0.1f;          // Intensity decay rate

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;

            // =============================================================
            // Calculate Scene Lighting State
            // =============================================================

            float totalLightIntensity = 0f;
            int lightCount = 0;

            foreach (var lightEmitter in SystemAPI.Query<RefRO<LightEmitter>>())
            {
                totalLightIntensity += lightEmitter.ValueRO.Intensity;
                lightCount++;
            }

            // Average scene brightness for exposure
            float avgIntensity = lightCount > 0 ? totalLightIntensity / lightCount : AmbientIntensity;

            // =============================================================
            // Update Global Lighting Parameters
            // =============================================================

            // These would typically be passed to shaders via a singleton
            // For now, we're just preparing the data

            // Calculate adaptive bloom based on scene brightness
            float adaptiveBloomIntensity = BloomIntensity * math.saturate(avgIntensity / 2f);

            // =============================================================
            // Update Light Emitter Contributions
            // =============================================================

            foreach (var (lightEmitter, transform) in
                SystemAPI.Query<RefRW<LightEmitter>, RefRO<WorldTransform>>())
            {
                // Calculate world-space light radius based on intensity
                float radius = lightEmitter.ValueRO.Intensity * 5f;
                lightEmitter.ValueRW.Radius = radius;

                // Calculate falloff for shader
                lightEmitter.ValueRW.Falloff = LightFalloff;
            }

            // =============================================================
            // Emergency Light Flashing
            // =============================================================

            foreach (var (lightEmitter, emergencyAI) in
                SystemAPI.Query<RefRW<LightEmitter>, RefRO<EmergencyAI>>()
                    .WithAll<EmergencyVehicleTag>())
            {
                if (!emergencyAI.ValueRO.SirenActive)
                    continue;

                // Create sharp on/off flash pattern
                float flashFreq = 4f; // Hz
                float phase = math.frac(time * flashFreq);

                // Two-phase flash (red-blue alternating)
                bool redPhase = phase < 0.25f || (phase >= 0.5f && phase < 0.75f);
                bool bluePhase = (phase >= 0.25f && phase < 0.5f) || phase >= 0.75f;

                // Sharp intensity transitions
                float flashIntensity = (redPhase || bluePhase) ? 2.5f : 0.3f;
                lightEmitter.ValueRW.Intensity = flashIntensity;
            }

            // =============================================================
            // Hazard Warning Pulses
            // =============================================================

            foreach (var (lightEmitter, hazard) in
                SystemAPI.Query<RefRW<LightEmitter>, RefRO<Hazard>>()
                    .WithAll<HazardTag>())
            {
                // Severity-based pulse frequency
                float pulseFreq = 1f + hazard.ValueRO.Severity * 3f;
                float pulse = math.sin(time * pulseFreq * math.PI * 2f);

                // Triangle wave for sharper pulses
                float sharpPulse = math.abs(math.frac(time * pulseFreq) * 2f - 1f);

                float baseIntensity = hazard.ValueRO.Severity;
                lightEmitter.ValueRW.Intensity = baseIntensity * (0.7f + sharpPulse * 0.3f);
            }
        }
    }
}
