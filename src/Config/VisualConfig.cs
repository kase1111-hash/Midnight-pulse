// ============================================================================
// Nightflow - Visual Configuration
// ScriptableObject for colors, shaders, post-processing, and lighting
// ============================================================================

using UnityEngine;

namespace Nightflow.Config
{
    /// <summary>
    /// Master visual configuration containing all rendering parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "VisualConfig", menuName = "Nightflow/Visual Config")]
    public class VisualConfig : ScriptableObject
    {
        [Header("=== COLOR PALETTE ===")]
        public ColorPaletteConfig colorPalette = new ColorPaletteConfig();

        [Header("=== WIREFRAME ===")]
        public WireframeConfig wireframe = new WireframeConfig();

        [Header("=== LIGHTING ===")]
        public LightingConfig lighting = new LightingConfig();

        [Header("=== POST PROCESSING ===")]
        public PostProcessConfig postProcess = new PostProcessConfig();

        [Header("=== PARTICLES ===")]
        public ParticleVisualConfig particles = new ParticleVisualConfig();
    }

    /// <summary>
    /// Neon color palette definitions.
    /// </summary>
    [System.Serializable]
    public class ColorPaletteConfig
    {
        [Header("Primary Colors")]
        [Tooltip("Player vehicle - Cyan")]
        public Color playerCyan = new Color(0f, 1f, 1f, 1f);

        [Tooltip("Traffic vehicles - Magenta")]
        public Color trafficMagenta = new Color(1f, 0f, 1f, 1f);

        [Tooltip("Hazards/Warnings - Orange")]
        public Color hazardOrange = new Color(1f, 0.533f, 0f, 1f);

        [Tooltip("Emergency Red")]
        public Color emergencyRed = new Color(1f, 0f, 0f, 1f);

        [Tooltip("Emergency Blue")]
        public Color emergencyBlue = new Color(0f, 0.4f, 1f, 1f);

        [Header("Secondary Colors")]
        [Tooltip("Lane markings - Blue")]
        public Color laneBlue = new Color(0.267f, 0.533f, 1f, 1f);

        [Tooltip("Streetlights - Warm Sodium")]
        public Color streetlightSodium = new Color(1f, 0.816f, 0.5f, 1f);

        [Tooltip("Tunnel lights - Cool Fluorescent")]
        public Color tunnelFluorescent = new Color(0.9f, 0.95f, 1f, 1f);

        [Header("Background Colors")]
        [Tooltip("Road surface - Dark Gray")]
        public Color roadDark = new Color(0.102f, 0.102f, 0.102f, 1f);

        [Tooltip("Sky - Deep Black")]
        public Color skyBlack = new Color(0.039f, 0.039f, 0.039f, 1f);

        [Header("UI Colors")]
        [Tooltip("UI Warning - Yellow")]
        public Color uiWarningYellow = new Color(1f, 1f, 0f, 1f);

        [Tooltip("UI Damage - Red")]
        public Color uiDamageRed = new Color(1f, 0.2f, 0.2f, 1f);

        [Tooltip("UI Boost - Magenta")]
        public Color uiBoostMagenta = new Color(1f, 0f, 0.8f, 1f);

        [Header("Ghost Vehicle")]
        [Tooltip("Ghost vehicle color (dimmed cyan)")]
        public Color ghostCyan = new Color(0f, 0.6f, 0.6f, 0.5f);
    }

    /// <summary>
    /// Wireframe rendering configuration.
    /// </summary>
    [System.Serializable]
    public class WireframeConfig
    {
        [Header("Line Properties")]
        [Tooltip("Wireframe line thickness")]
        [Range(0.001f, 0.05f)]
        public float lineThickness = 0.008f;

        [Tooltip("Line anti-aliasing width")]
        [Range(0f, 0.01f)]
        public float antiAliasWidth = 0.002f;

        [Header("Glow")]
        [Tooltip("Glow intensity multiplier")]
        [Range(0.5f, 5f)]
        public float glowIntensity = 2f;

        [Tooltip("Glow falloff exponent")]
        [Range(0.5f, 10f)]
        public float glowFalloff = 3f;

        [Tooltip("Player vehicle glow multiplier")]
        [Range(1f, 3f)]
        public float playerGlowMultiplier = 1.5f;

        [Header("Fill")]
        [Tooltip("Interior fill alpha (0 = pure wireframe)")]
        [Range(0f, 0.3f)]
        public float fillAlpha = 0.05f;

        [Tooltip("Fill color multiplier")]
        [Range(0f, 1f)]
        public float fillColorMultiplier = 0.3f;

        [Header("Animation")]
        [Tooltip("Enable wireframe pulse animation")]
        public bool enablePulse = true;

        [Tooltip("Pulse frequency (Hz)")]
        public float pulseFrequency = 1f;

        [Tooltip("Pulse intensity range")]
        [Range(0f, 0.5f)]
        public float pulseIntensity = 0.1f;

        [Header("Distance")]
        [Tooltip("Distance for wireframe fade start")]
        public float fadeStartDistance = 100f;

        [Tooltip("Distance for wireframe fade end")]
        public float fadeEndDistance = 200f;
    }

    /// <summary>
    /// Lighting configuration.
    /// </summary>
    [System.Serializable]
    public class LightingConfig
    {
        [Header("Streetlights")]
        [Tooltip("Streetlight spacing (meters)")]
        public float streetlightSpacing = 40f;

        [Tooltip("Streetlight intensity")]
        [Range(0.5f, 5f)]
        public float streetlightIntensity = 2f;

        [Tooltip("Streetlight range (meters)")]
        public float streetlightRange = 25f;

        [Header("Vehicle Lights")]
        [Tooltip("Headlight intensity")]
        [Range(0.5f, 5f)]
        public float headlightIntensity = 3f;

        [Tooltip("Headlight range (meters)")]
        public float headlightRange = 30f;

        [Tooltip("Taillight intensity")]
        [Range(0.5f, 3f)]
        public float taillightIntensity = 1.5f;

        [Header("Emergency Lights")]
        [Tooltip("Police strobe intensity")]
        [Range(1f, 5f)]
        public float policeStrobeIntensity = 4f;

        [Tooltip("Strobe frequency (Hz)")]
        public float strobeFrequency = 8f;

        [Header("Tunnel Lights")]
        [Tooltip("Tunnel light spacing (meters)")]
        public float tunnelLightSpacing = 15f;

        [Tooltip("Tunnel light intensity")]
        [Range(0.5f, 3f)]
        public float tunnelLightIntensity = 1.5f;

        [Header("Ambient")]
        [Tooltip("Ambient light intensity")]
        [Range(0f, 0.5f)]
        public float ambientIntensity = 0.1f;

        [Tooltip("Horizon glow intensity")]
        [Range(0f, 1f)]
        public float horizonGlowIntensity = 0.3f;
    }

    /// <summary>
    /// Post-processing configuration.
    /// </summary>
    [System.Serializable]
    public class PostProcessConfig
    {
        [Header("Bloom")]
        [Tooltip("Enable bloom")]
        public bool bloomEnabled = true;

        [Tooltip("Bloom threshold")]
        [Range(0.5f, 2f)]
        public float bloomThreshold = 0.8f;

        [Tooltip("Bloom intensity")]
        [Range(0.5f, 5f)]
        public float bloomIntensity = 1.5f;

        [Tooltip("Bloom scatter (spread)")]
        [Range(0f, 1f)]
        public float bloomScatter = 0.7f;

        [Header("Motion Blur")]
        [Tooltip("Enable motion blur")]
        public bool motionBlurEnabled = true;

        [Tooltip("Motion blur intensity")]
        [Range(0f, 1f)]
        public float motionBlurIntensity = 0.3f;

        [Tooltip("Speed for max blur (km/h)")]
        public float motionBlurMaxSpeed = 250f;

        [Tooltip("Blur sample count")]
        [Range(4, 16)]
        public int motionBlurSamples = 8;

        [Header("Film Grain")]
        [Tooltip("Enable film grain")]
        public bool filmGrainEnabled = true;

        [Tooltip("Grain intensity")]
        [Range(0f, 0.5f)]
        public float grainIntensity = 0.1f;

        [Tooltip("Grain size")]
        [Range(0.5f, 3f)]
        public float grainSize = 1.2f;

        [Header("Scanlines")]
        [Tooltip("Enable scanlines")]
        public bool scanlinesEnabled = true;

        [Tooltip("Scanline intensity")]
        [Range(0f, 0.3f)]
        public float scanlineIntensity = 0.08f;

        [Tooltip("Scanline density")]
        public float scanlineDensity = 300f;

        [Header("Vignette")]
        [Tooltip("Enable vignette")]
        public bool vignetteEnabled = true;

        [Tooltip("Vignette intensity")]
        [Range(0f, 1f)]
        public float vignetteIntensity = 0.3f;

        [Tooltip("Vignette smoothness")]
        [Range(0.1f, 1f)]
        public float vignetteSmoothness = 0.5f;

        [Header("Chromatic Aberration")]
        [Tooltip("Enable chromatic aberration")]
        public bool chromaticAberrationEnabled = true;

        [Tooltip("Aberration intensity")]
        [Range(0f, 0.05f)]
        public float chromaticAberrationIntensity = 0.01f;

        [Header("Color Grading")]
        [Tooltip("Saturation adjustment")]
        [Range(0.5f, 1.5f)]
        public float saturation = 1.1f;

        [Tooltip("Contrast adjustment")]
        [Range(0.8f, 1.2f)]
        public float contrast = 1.05f;

        [Tooltip("Color temperature shift")]
        [Range(-20f, 20f)]
        public float colorTemperature = -5f;
    }

    /// <summary>
    /// Particle visual configuration.
    /// </summary>
    [System.Serializable]
    public class ParticleVisualConfig
    {
        [Header("Sparks")]
        [Tooltip("Spark emission intensity")]
        [Range(1f, 10f)]
        public float sparkEmissionIntensity = 4f;

        [Tooltip("Spark color start (orange)")]
        public Color sparkColorStart = new Color(1f, 0.7f, 0f, 1f);

        [Tooltip("Spark color end (red)")]
        public Color sparkColorEnd = new Color(1f, 0.2f, 0f, 0f);

        [Header("Smoke")]
        [Tooltip("Smoke neon tint strength")]
        [Range(0f, 0.5f)]
        public float smokeNeonTint = 0.15f;

        [Tooltip("Smoke base color")]
        public Color smokeColor = new Color(0.3f, 0.35f, 0.4f, 0.5f);

        [Header("Speed Lines")]
        [Tooltip("Speed line emission intensity")]
        [Range(0.5f, 5f)]
        public float speedLineEmission = 2f;

        [Tooltip("Speed line primary color")]
        public Color speedLineColor = new Color(0.9f, 1f, 1f, 0.8f);

        [Header("Crash Flash")]
        [Tooltip("Flash white color")]
        public Color flashWhite = new Color(1f, 1f, 1f, 1f);

        [Tooltip("Flash red color")]
        public Color flashRed = new Color(1f, 0.2f, 0.1f, 0.8f);

        [Tooltip("Flash chromatic aberration")]
        [Range(0f, 0.05f)]
        public float flashChromaticAberration = 0.02f;
    }
}
