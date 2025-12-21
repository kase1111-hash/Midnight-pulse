// ============================================================================
// Nightflow - Wireframe Render System
// Execution Order: 2 (Presentation Group)
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
    /// Prepares render data for the neon wireframe aesthetic.
    /// Handles edge detection, glow intensity, and color assignment.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraSystem))]
    public partial struct WireframeRenderSystem : ISystem
    {
        // Render parameters
        private const float BaseGlowIntensity = 1f;
        private const float DamageGlowBoost = 0.5f;       // Extra glow when damaged
        private const float SpeedGlowBoost = 0.3f;        // Extra glow at high speed
        private const float PulseSpeed = 2f;              // Hz

        // LOD parameters
        private const float LOD0Distance = 50f;           // Full detail
        private const float LOD1Distance = 100f;          // Reduced detail
        private const float LOD2Distance = 200f;          // Minimal detail

        // Color palette (neon cyberpunk)
        private static readonly float4 PlayerColor = new float4(0f, 1f, 0.8f, 1f);      // Cyan
        private static readonly float4 TrafficColor = new float4(1f, 0.3f, 0.8f, 1f);   // Magenta
        private static readonly float4 EmergencyRed = new float4(1f, 0f, 0f, 1f);        // Red
        private static readonly float4 EmergencyBlue = new float4(0f, 0.3f, 1f, 1f);    // Blue
        private static readonly float4 HazardColor = new float4(1f, 0.6f, 0f, 1f);      // Orange
        private static readonly float4 LaneColor = new float4(0.3f, 0.3f, 1f, 0.5f);    // Dim blue

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;
            float pulse = (math.sin(time * PulseSpeed * math.PI * 2f) + 1f) * 0.5f;

            // Get camera position for LOD
            float3 cameraPos = float3.zero;
            foreach (var camera in SystemAPI.Query<RefRO<CameraState>>())
            {
                cameraPos = camera.ValueRO.Position;
                break;
            }

            // =============================================================
            // Player Vehicle Render Data
            // =============================================================

            foreach (var (transform, velocity, damage, lightEmitter) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>,
                               RefRO<DamageState>, RefRW<LightEmitter>>()
                    .WithAll<PlayerVehicleTag>())
            {
                // Base color with damage tint
                float4 color = PlayerColor;
                float damageAmount = damage.ValueRO.Total;

                // Shift towards orange/red as damage increases
                color = math.lerp(color, HazardColor, damageAmount * 0.5f);

                // Glow intensity based on speed and damage
                float speedNorm = math.saturate(velocity.ValueRO.Forward / 70f);
                float intensity = BaseGlowIntensity
                    + speedNorm * SpeedGlowBoost
                    + damageAmount * DamageGlowBoost;

                // Apply subtle pulse
                intensity *= (0.9f + pulse * 0.1f);

                lightEmitter.ValueRW.Color = color;
                lightEmitter.ValueRW.Intensity = intensity;
            }

            // =============================================================
            // Traffic Vehicle Render Data
            // =============================================================

            foreach (var (transform, lightEmitter) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRW<LightEmitter>>()
                    .WithAll<TrafficVehicleTag>())
            {
                float dist = math.distance(transform.ValueRO.Position, cameraPos);

                // LOD-based intensity
                float lodFactor = 1f - math.saturate((dist - LOD0Distance) / (LOD2Distance - LOD0Distance));
                float intensity = BaseGlowIntensity * lodFactor * 0.7f;

                lightEmitter.ValueRW.Color = TrafficColor;
                lightEmitter.ValueRW.Intensity = intensity;
            }

            // =============================================================
            // Emergency Vehicle Render Data
            // =============================================================

            foreach (var (transform, emergencyAI, lightEmitter) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<EmergencyAI>, RefRW<LightEmitter>>()
                    .WithAll<EmergencyVehicleTag>())
            {
                // Alternating red/blue flash
                float flashPhase = math.frac(time * 4f);
                bool isRed = flashPhase < 0.5f;

                lightEmitter.ValueRW.Color = isRed ? EmergencyRed : EmergencyBlue;
                lightEmitter.ValueRW.Intensity = emergencyAI.ValueRO.SirenActive ? 2f : 0.8f;
                lightEmitter.ValueRW.FlashPhase = flashPhase;
            }

            // =============================================================
            // Hazard Render Data
            // =============================================================

            foreach (var (transform, hazard, lightEmitter) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Hazard>, RefRW<LightEmitter>>()
                    .WithAll<HazardTag>())
            {
                float dist = math.distance(transform.ValueRO.Position, cameraPos);

                // Hazards pulse faster when close
                float hazardPulse = math.frac(time * (3f + (1f - math.saturate(dist / 50f)) * 2f));
                float intensity = hazard.ValueRO.Severity * (0.7f + hazardPulse * 0.3f);

                // Color shifts to red for high severity
                float4 color = math.lerp(HazardColor, EmergencyRed, hazard.ValueRO.Severity);

                lightEmitter.ValueRW.Color = color;
                lightEmitter.ValueRW.Intensity = intensity;
            }

            // =============================================================
            // Lane Line Render Data
            // =============================================================

            foreach (var (transform, lightEmitter) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRW<LightEmitter>>()
                    .WithAll<LaneMarkerTag>())
            {
                float dist = math.distance(transform.ValueRO.Position, cameraPos);

                // Lanes fade with distance
                float fadeStart = 30f;
                float fadeEnd = 150f;
                float alpha = 1f - math.saturate((dist - fadeStart) / (fadeEnd - fadeStart));

                float4 color = LaneColor;
                color.w = alpha * 0.5f;

                lightEmitter.ValueRW.Color = color;
                lightEmitter.ValueRW.Intensity = BaseGlowIntensity * 0.4f;
            }
        }
    }
}
