// ============================================================================
// Nightflow - Unity DOTS Components: Rendering
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    /// <summary>
    /// Global render state singleton for shader bridge.
    /// Manages wireframe, bloom, and post-processing parameters.
    ///
    /// From spec:
    /// - Entire world rendered in wireframe
    /// - Solid light volumes, bloom, and additive glows
    /// - Night-time city suggested via distant light grids
    /// </summary>
    public struct RenderState : IComponentData
    {
        // Wireframe parameters
        public float WireframeThickness;
        public float WireframeGlow;
        public float EdgeIntensity;

        // Bloom parameters
        public float BloomThreshold;
        public float BloomIntensity;
        public float BloomRadius;
        public float BloomSoftKnee;

        // Motion blur (intensity ∝ speed²)
        public float MotionBlurIntensity;
        public float MotionBlurSamples;

        // Ambient/Environment
        public float AmbientIntensity;
        public float3 AmbientColor;
        public float GridGlowIntensity;

        // City glow (distant illumination)
        public float CityGlowIntensity;
        public float3 CityGlowColor;
        public float HorizonGlow;

        // Exposure/Tonemapping
        public float Exposure;
        public float Contrast;
        public float Saturation;

        // Fog
        public float FogDensity;
        public float FogStart;
        public float FogEnd;
        public float3 FogColor;

        // Screen-space effects
        public float ChromaticAberration;
        public float Vignette;
        public float FilmGrain;
    }

    /// <summary>
    /// Headlight data for player vehicle.
    /// Dynamic lights that illuminate the road ahead.
    /// </summary>
    public struct Headlight : IComponentData
    {
        public float3 Color;
        public float Intensity;
        public float Range;
        public float SpotAngle;
        public float3 LeftOffset;
        public float3 RightOffset;
        public bool HighBeam;
    }

    /// <summary>
    /// Streetlight data for procedurally placed road lights.
    /// </summary>
    public struct Streetlight : IComponentData
    {
        public float3 Color;
        public float Intensity;
        public float Height;
        public float Radius;
        public int Side; // -1 = left, 0 = center, 1 = right
    }

    /// <summary>
    /// Tunnel lighting state for interior segments.
    /// </summary>
    public struct TunnelLighting : IComponentData
    {
        public float3 AmbientColor;
        public float AmbientIntensity;
        public float LightSpacing;
        public float LightIntensity;
        public bool IsInTunnel;
        public float TunnelBlend; // 0 = outside, 1 = fully inside
    }

    /// <summary>
    /// Motion blur state for the current frame.
    /// </summary>
    public struct MotionBlurState : IComponentData
    {
        public float Intensity;
        public float3 VelocityDirection;
        public float Speed;
    }

    /// <summary>
    /// Skybox/environment parameters.
    /// </summary>
    public struct SkyboxState : IComponentData
    {
        public float3 HorizonColor;
        public float3 ZenithColor;
        public float StarIntensity;
        public float MoonPhase;
        public float CloudCover;
        public float AtmosphereScatter;
    }
}
