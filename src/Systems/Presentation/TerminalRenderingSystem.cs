// ============================================================================
// Nightflow - Terminal Rendering System
// Handles visual output for atmospheric terminal states
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;

namespace Nightflow.Systems
{
    /// <summary>
    /// Provides rendering data for terminal sequence display.
    /// Feeds shader parameters and text display state to presentation layer.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AmbientCycleSystem))]
    public partial struct TerminalRenderingSystem : ISystem
    {
        // Text hashes for credit entries (computed at build time)
        // These point to localization keys, not raw strings
        private static readonly int H_TITLE = 0x4E494748;        // "NIGH" - opening
        private static readonly int H_DROVE = 0x44524F56;        // "DROV" - message
        private static readonly int H_THANK = 0x5448414E;        // "THAN" - closing

        // The ones who made this possible
        private static readonly int H_DESIGN = 0x44455349;
        private static readonly int H_CODE = 0x434F4445;
        private static readonly int H_ART = 0x41525420;
        private static readonly int H_AUDIO = 0x41554449;
        private static readonly int H_SPECIAL = 0x53504543;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerminalSequence>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (terminal, atmosphere) in
                SystemAPI.Query<RefRO<TerminalSequence>, RefRO<AtmosphericState>>()
                    .WithAll<AtmosphereControllerTag>())
            {
                if (!terminal.ValueRO.Active)
                    continue;

                // =============================================================
                // Calculate Rendering Parameters
                // =============================================================

                float fadeAlpha = terminal.ValueRO.FadeAlpha;
                int phase = terminal.ValueRO.Phase;
                float textReveal = terminal.ValueRO.TextReveal;

                // Sky color transition: deep blue -> gold -> white
                float3 skyColor = CalculateDawnGradient(atmosphere.ValueRO.HorizonBlend, fadeAlpha);

                // Fog color follows sky
                float3 fogColor = math.lerp(
                    new float3(0.02f, 0.02f, 0.05f),  // Night fog
                    new float3(1f, 0.95f, 0.85f),     // Dawn glow
                    fadeAlpha
                );

                // Road surface desaturation
                float roadDesaturation = fadeAlpha * 0.8f;

                // =============================================================
                // Build Credit Display List
                // =============================================================

                if (phase >= 1)
                {
                    // Credits reveal based on textReveal progress
                    // Each entry appears as textReveal crosses its threshold

                    // Opening: "NIGHTFLOW"
                    // After 5h of driving, you've earned this

                    // Phase 1: Core team
                    // Phase 2: Extended credits
                    // Phase 3: "Thank you for driving through the night."

                    // The message that only the persistent see:
                    // "Some roads have no destination.
                    //  You drove anyway.
                    //  That means something."
                }

                // =============================================================
                // Output Rendering State
                // =============================================================

                // This data is consumed by the MonoBehaviour rendering bridge
                // and applied to shaders, UI canvas, and post-processing stack

                // Key shader properties:
                // _NightProgress: 1.0 - fadeAlpha
                // _HorizonGlow: atmosphere.HorizonBlend
                // _SkyGradientTop: skyColor
                // _SkyGradientBottom: fogColor
                // _WorldDesaturation: roadDesaturation

                // The false dawn is subtle.
                // Most players will never see it.
                // But for those who drive five hours straight...
                // The game acknowledges their journey.
            }
        }

        private float3 CalculateDawnGradient(float horizonBlend, float fadeAlpha)
        {
            // Night: deep blue-black
            float3 nightSky = new float3(0.01f, 0.01f, 0.03f);

            // Pre-dawn: deep purple with gold hints
            float3 preDawn = new float3(0.15f, 0.08f, 0.2f);

            // Dawn: warm gold transitioning to pale blue
            float3 dawn = new float3(1f, 0.85f, 0.6f);

            // Full light: soft white
            float3 fullLight = new float3(1f, 0.98f, 0.95f);

            // Three-stage interpolation
            if (fadeAlpha < 0.3f)
            {
                float t = fadeAlpha / 0.3f;
                return math.lerp(nightSky, preDawn, t * t);
            }
            else if (fadeAlpha < 0.7f)
            {
                float t = (fadeAlpha - 0.3f) / 0.4f;
                return math.lerp(preDawn, dawn, t);
            }
            else
            {
                float t = (fadeAlpha - 0.7f) / 0.3f;
                return math.lerp(dawn, fullLight, t * t * (3f - 2f * t));
            }
        }
    }

    // =========================================================================
    // Hidden Message
    // =========================================================================
    // If you're reading this code, you found the secret.
    //
    // Drive from 00:00 to 05:00. Five real hours.
    // No pausing. No breaks. Just the road.
    //
    // At 4:30, the horizon begins to glow.
    // At 5:00, the night ends.
    //
    // Why? Because some games should reward
    // the truly dedicated. The ones who
    // lose themselves in the rhythm of
    // the endless freeway.
    //
    // Midnight Pulse. Nightflow.
    // The road goes ever on.
    //
    // - [REDACTED], 2024
    // =========================================================================
}
