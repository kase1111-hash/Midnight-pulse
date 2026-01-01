// ============================================================================
// Nightflow - Raytracing Components
// Components for RT reflections and screen-space fallback
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    /// <summary>
    /// Global raytracing state singleton.
    /// Controls RT quality, fallback mode, and reflection parameters.
    /// </summary>
    public struct RaytracingState : IComponentData
    {
        /// <summary>Whether hardware raytracing is available and enabled.</summary>
        public bool RTEnabled;

        /// <summary>Raytracing quality level (0 = off/SSR, 1 = low, 2 = medium, 3 = high).</summary>
        public int QualityLevel;

        /// <summary>Number of rays per pixel for reflections.</summary>
        public int RaysPerPixel;

        /// <summary>Maximum ray bounce count.</summary>
        public int MaxBounces;

        /// <summary>Reflection intensity multiplier.</summary>
        public float ReflectionIntensity;

        /// <summary>Global wetness level [0, 1] affecting surface reflectivity.</summary>
        public float WetnessLevel;

        /// <summary>Whether using screen-space reflections as fallback.</summary>
        public bool UseSSRFallback;

        /// <summary>SSR quality (ray march steps).</summary>
        public int SSRSteps;

        /// <summary>SSR max distance.</summary>
        public float SSRMaxDistance;

        /// <summary>Default state with RT disabled (SSR fallback).</summary>
        public static RaytracingState Default => new RaytracingState
        {
            RTEnabled = false,
            QualityLevel = 0,
            RaysPerPixel = 1,
            MaxBounces = 1,
            ReflectionIntensity = 1f,
            WetnessLevel = 0.3f,
            UseSSRFallback = true,
            SSRSteps = 32,
            SSRMaxDistance = 100f
        };

        /// <summary>Low quality RT settings.</summary>
        public static RaytracingState RTLow => new RaytracingState
        {
            RTEnabled = true,
            QualityLevel = 1,
            RaysPerPixel = 1,
            MaxBounces = 1,
            ReflectionIntensity = 1f,
            WetnessLevel = 0.3f,
            UseSSRFallback = false,
            SSRSteps = 0,
            SSRMaxDistance = 0f
        };

        /// <summary>High quality RT settings.</summary>
        public static RaytracingState RTHigh => new RaytracingState
        {
            RTEnabled = true,
            QualityLevel = 3,
            RaysPerPixel = 4,
            MaxBounces = 2,
            ReflectionIntensity = 1.2f,
            WetnessLevel = 0.3f,
            UseSSRFallback = false,
            SSRSteps = 0,
            SSRMaxDistance = 0f
        };
    }

    /// <summary>
    /// Raytraced light source with reflection data.
    /// Extends LightEmitter with RT-specific parameters.
    /// </summary>
    public struct RTLight : IComponentData
    {
        /// <summary>Light color (linear RGB).</summary>
        public float3 Color;

        /// <summary>Light intensity for RT (can exceed 1 for HDR).</summary>
        public float Intensity;

        /// <summary>Light position in world space.</summary>
        public float3 Position;

        /// <summary>Light direction (for spotlights).</summary>
        public float3 Direction;

        /// <summary>Spot angle in degrees (0 = point light).</summary>
        public float SpotAngle;

        /// <summary>Light attenuation radius.</summary>
        public float Radius;

        /// <summary>Whether this light casts reflections.</summary>
        public bool CastsReflections;

        /// <summary>Shadow softness for RT shadows.</summary>
        public float ShadowSoftness;

        /// <summary>Light type for RT dispatch.</summary>
        public RTLightType LightType;
    }

    /// <summary>
    /// Types of lights for RT processing.
    /// </summary>
    public enum RTLightType : byte
    {
        Point = 0,
        Spot = 1,
        Directional = 2,
        Area = 3
    }

    /// <summary>
    /// Surface reflection properties for RT.
    /// Attached to entities that should receive reflections.
    /// </summary>
    public struct RTSurface : IComponentData
    {
        /// <summary>Base reflectivity [0, 1].</summary>
        public float Reflectivity;

        /// <summary>Surface roughness [0, 1]. 0 = mirror, 1 = diffuse.</summary>
        public float Roughness;

        /// <summary>Metallic [0, 1]. Affects fresnel.</summary>
        public float Metallic;

        /// <summary>Local wetness override (-1 = use global).</summary>
        public float WetnessOverride;

        /// <summary>Whether surface is currently in shadow.</summary>
        public bool InShadow;

        /// <summary>Accumulated reflected light color.</summary>
        public float3 ReflectedLight;

        /// <summary>Accumulated reflected light intensity.</summary>
        public float ReflectedIntensity;
    }

    /// <summary>
    /// Road surface reflection state.
    /// Tracks wet road reflections for headlights and emergency lights.
    /// </summary>
    public struct RoadReflection : IComponentData
    {
        /// <summary>Road segment start Z.</summary>
        public float StartZ;

        /// <summary>Road segment end Z.</summary>
        public float EndZ;

        /// <summary>Wetness level for this segment [0, 1].</summary>
        public float Wetness;

        /// <summary>Accumulated headlight reflection color.</summary>
        public float3 HeadlightReflection;

        /// <summary>Accumulated emergency light reflection color.</summary>
        public float3 EmergencyReflection;

        /// <summary>Total reflection intensity for bloom.</summary>
        public float TotalReflectionIntensity;

        /// <summary>Whether reflections are active on this segment.</summary>
        public bool HasReflections;
    }

    /// <summary>
    /// Tunnel reflection state for light bounce.
    /// </summary>
    public struct TunnelReflection : IComponentData
    {
        /// <summary>Tunnel segment ID.</summary>
        public int SegmentID;

        /// <summary>Wall reflectivity.</summary>
        public float WallReflectivity;

        /// <summary>Ceiling reflectivity.</summary>
        public float CeilingReflectivity;

        /// <summary>Floor reflectivity (wet surface).</summary>
        public float FloorReflectivity;

        /// <summary>Accumulated bounce light from walls.</summary>
        public float3 BounceLight;

        /// <summary>Bounce light intensity.</summary>
        public float BounceIntensity;

        /// <summary>Number of light bounces calculated.</summary>
        public int BounceCount;
    }

    /// <summary>
    /// Per-frame reflection probe for local area reflections.
    /// </summary>
    public struct ReflectionProbe : IComponentData
    {
        /// <summary>Probe position in world space.</summary>
        public float3 Position;

        /// <summary>Probe influence radius.</summary>
        public float Radius;

        /// <summary>Blending factor with other probes.</summary>
        public float BlendWeight;

        /// <summary>Dominant reflection color from probe.</summary>
        public float3 ReflectionColor;

        /// <summary>Average scene luminance in probe area.</summary>
        public float AverageLuminance;

        /// <summary>Whether probe is inside a tunnel.</summary>
        public bool InTunnel;

        /// <summary>Whether probe needs update this frame.</summary>
        public bool NeedsUpdate;
    }

    /// <summary>
    /// Screen-space reflection parameters (SSR fallback).
    /// </summary>
    public struct SSRState : IComponentData
    {
        /// <summary>SSR enabled state.</summary>
        public bool Enabled;

        /// <summary>Number of ray march steps.</summary>
        public int Steps;

        /// <summary>Step size for ray marching.</summary>
        public float StepSize;

        /// <summary>Maximum ray distance.</summary>
        public float MaxDistance;

        /// <summary>Thickness for hit detection.</summary>
        public float Thickness;

        /// <summary>Fade at screen edges.</summary>
        public float EdgeFade;

        /// <summary>Reflection intensity.</summary>
        public float Intensity;

        /// <summary>Roughness-based blur.</summary>
        public float RoughnessBlur;

        /// <summary>Default SSR settings.</summary>
        public static SSRState Default => new SSRState
        {
            Enabled = true,
            Steps = 32,
            StepSize = 0.5f,
            MaxDistance = 100f,
            Thickness = 0.5f,
            EdgeFade = 0.1f,
            Intensity = 0.8f,
            RoughnessBlur = 1f
        };
    }

    /// <summary>
    /// Emergency vehicle light reflection data.
    /// Tracks bounced red/blue lights on road and surroundings.
    /// </summary>
    public struct EmergencyLightReflection : IComponentData
    {
        /// <summary>Red light current intensity.</summary>
        public float RedIntensity;

        /// <summary>Blue light current intensity.</summary>
        public float BlueIntensity;

        /// <summary>Red light reflection on road.</summary>
        public float3 RedReflection;

        /// <summary>Blue light reflection on road.</summary>
        public float3 BlueReflection;

        /// <summary>Current flash phase [0, 1].</summary>
        public float FlashPhase;

        /// <summary>Distance to emergency vehicle.</summary>
        public float Distance;

        /// <summary>Whether reflections are visible.</summary>
        public bool Visible;

        /// <summary>Reflection spread based on wetness.</summary>
        public float ReflectionSpread;
    }
}
