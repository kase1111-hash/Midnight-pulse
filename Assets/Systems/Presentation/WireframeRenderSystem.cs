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
    ///
    /// From spec:
    /// - Entire world rendered in wireframe
    /// - Solid light volumes, bloom, and additive glows
    /// - Night-time city suggested via distant light grids
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(HeadlightSystem))]
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
        private static readonly float3 PlayerColor = new float3(0f, 1f, 0.8f);       // Cyan
        private static readonly float3 TrafficColor = new float3(1f, 0.3f, 0.8f);    // Magenta
        private static readonly float3 EmergencyRed = new float3(1f, 0f, 0f);         // Red
        private static readonly float3 EmergencyBlue = new float3(0f, 0.3f, 1f);     // Blue
        private static readonly float3 HazardColor = new float3(1f, 0.6f, 0f);       // Orange
        private static readonly float3 LaneColor = new float3(0.3f, 0.3f, 1f);       // Dim blue
        private static readonly float3 StreetlightColor = new float3(1f, 0.8f, 0.5f); // Warm sodium

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

            // Get render state for global parameters
            float wireframeGlow = 1.2f;
            float edgeIntensity = 1f;
            foreach (var renderState in SystemAPI.Query<RefRO<RenderState>>())
            {
                wireframeGlow = renderState.ValueRO.WireframeGlow;
                edgeIntensity = renderState.ValueRO.EdgeIntensity;
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
                float3 color = PlayerColor;
                float damageAmount = math.saturate(damage.ValueRO.Total / 100f);

                // Shift towards orange/red as damage increases
                color = math.lerp(color, HazardColor, damageAmount * 0.5f);

                // Glow intensity based on speed and damage
                float speedNorm = math.saturate(velocity.ValueRO.Forward / 70f);
                float intensity = BaseGlowIntensity
                    + speedNorm * SpeedGlowBoost
                    + damageAmount * DamageGlowBoost;

                // Apply subtle pulse and global glow modifier
                intensity *= (0.9f + pulse * 0.1f) * wireframeGlow;

                lightEmitter.ValueRW.Color = color;
                lightEmitter.ValueRW.Intensity = intensity * edgeIntensity;
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
                lightEmitter.ValueRW.Intensity = emergencyAI.ValueRO.SirenActive ? 2f * wireframeGlow : 0.8f;
                lightEmitter.ValueRW.StrobePhase = flashPhase;
                lightEmitter.ValueRW.Strobe = emergencyAI.ValueRO.SirenActive;
                lightEmitter.ValueRW.StrobeRate = 4f;
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
                float3 color = math.lerp(HazardColor, EmergencyRed, hazard.ValueRO.Severity);

                lightEmitter.ValueRW.Color = color;
                lightEmitter.ValueRW.Intensity = intensity * wireframeGlow;
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

                // Intensity based on alpha (since Color is float3)
                lightEmitter.ValueRW.Color = LaneColor;
                lightEmitter.ValueRW.Intensity = BaseGlowIntensity * 0.4f * alpha;
            }

            // =============================================================
            // Streetlight Render Data
            // =============================================================

            foreach (var (transform, streetlight, lightEmitter) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Streetlight>, RefRW<LightEmitter>>()
                    .WithAll<LightSourceTag>())
            {
                float dist = math.distance(transform.ValueRO.Position, cameraPos);

                // Streetlights have warm glow, fade with distance
                float lodFactor = 1f - math.saturate((dist - LOD0Distance) / (LOD2Distance - LOD0Distance));

                lightEmitter.ValueRW.Color = StreetlightColor;
                lightEmitter.ValueRW.Intensity = streetlight.ValueRO.Intensity * lodFactor;
                lightEmitter.ValueRW.Radius = streetlight.ValueRO.Radius;
            }

            // =============================================================
            // Ghost Vehicle Render Data
            // Semi-transparent, pulsing cyan with trail effect
            // =============================================================

            foreach (var (transform, ghostRender, lightEmitter) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<GhostRenderState>, RefRW<LightEmitter>>()
                    .WithAll<GhostVehicleTag>())
            {
                // Use ghost-specific color and alpha
                float3 ghostColor = ghostRender.ValueRO.WireframeColor;
                float ghostAlpha = ghostRender.ValueRO.Alpha;

                // Apply ghost color with transparency
                lightEmitter.ValueRW.Color = ghostColor;
                lightEmitter.ValueRW.Intensity = BaseGlowIntensity * ghostAlpha * wireframeGlow;

                // Ghost-specific rendering flags
                // (could be used by shader for additive blending)
            }
        }
    }
}
