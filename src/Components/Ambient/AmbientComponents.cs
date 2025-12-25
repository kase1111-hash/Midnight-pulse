// ============================================================================
// Nightflow - Unity DOTS Components: Ambient State
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    /// <summary>
    /// Atmospheric state for visual rendering.
    /// Controls sky gradient, fog density, and horizon color.
    /// </summary>
    public struct AtmosphericState : IComponentData
    {
        /// <summary>Internal cycle accumulator.</summary>
        public double CycleAccumulator;

        /// <summary>Horizon blend factor [0, 1].</summary>
        public float HorizonBlend;

        /// <summary>Sky saturation shift.</summary>
        public float SaturationShift;

        /// <summary>Fog density modifier.</summary>
        public float FogDensity;

        /// <summary>Whether threshold state has been reached.</summary>
        public bool ThresholdReached;

        /// <summary>Secondary phase progression.</summary>
        public float PhaseProgress;
    }

    /// <summary>
    /// Singleton tag for atmosphere controller entity.
    /// </summary>
    public struct AtmosphereControllerTag : IComponentData { }

    /// <summary>
    /// Terminal sequence state.
    /// Activated under specific accumulated conditions.
    /// </summary>
    public struct TerminalSequence : IComponentData
    {
        /// <summary>Whether sequence is active.</summary>
        public bool Active;

        /// <summary>Sequence progress [0, 1].</summary>
        public float Progress;

        /// <summary>Current phase index.</summary>
        public int Phase;

        /// <summary>Fade alpha for overlay.</summary>
        public float FadeAlpha;

        /// <summary>Text reveal progress.</summary>
        public float TextReveal;
    }

    /// <summary>
    /// Credit entry for terminal sequence.
    /// </summary>
    public struct CreditEntry : IBufferElementData
    {
        /// <summary>Role/title hash.</summary>
        public int RoleHash;

        /// <summary>Name hash.</summary>
        public int NameHash;

        /// <summary>Display delay from sequence start.</summary>
        public float Delay;

        /// <summary>Whether this entry has been displayed.</summary>
        public bool Displayed;
    }

    // ============================================================================
    // City Skyline Components
    // ============================================================================

    /// <summary>
    /// Singleton state for the city skyline backdrop.
    /// Controls the distant cityscape that wraps around the horizon.
    /// </summary>
    public struct CitySkylineState : IComponentData
    {
        /// <summary>Distance from player to skyline (meters).</summary>
        public float SkylineDistance;

        /// <summary>Base height of shortest buildings (meters).</summary>
        public float MinBuildingHeight;

        /// <summary>Max height of tallest skyscrapers (meters).</summary>
        public float MaxBuildingHeight;

        /// <summary>Number of buildings around the horizon.</summary>
        public int BuildingCount;

        /// <summary>Time accumulator for window animations.</summary>
        public float AnimationTime;

        /// <summary>Random seed for building generation.</summary>
        public uint RandomSeed;

        /// <summary>Whether skyline needs regeneration.</summary>
        public bool NeedsRegeneration;
    }

    /// <summary>
    /// Tag for the skyline controller entity.
    /// </summary>
    public struct CitySkylineTag : IComponentData { }

    /// <summary>
    /// Individual building in the skyline.
    /// Stored in a dynamic buffer on the skyline entity.
    /// </summary>
    public struct SkylineBuilding : IBufferElementData
    {
        /// <summary>Angle around the horizon (radians, 0 = forward).</summary>
        public float Angle;

        /// <summary>Building width (degrees of arc).</summary>
        public float WidthAngle;

        /// <summary>Building height (normalized 0-1, scaled by max height).</summary>
        public float Height;

        /// <summary>Number of window columns.</summary>
        public int WindowColumns;

        /// <summary>Number of window rows.</summary>
        public int WindowRows;

        /// <summary>Random seed for this building's windows.</summary>
        public uint WindowSeed;

        /// <summary>Building style variant (0-3).</summary>
        public int StyleVariant;
    }

    /// <summary>
    /// Window state for animated lights.
    /// Packed efficiently: each uint32 stores 32 window on/off states.
    /// </summary>
    public struct WindowStateBlock : IBufferElementData
    {
        /// <summary>Building index this block belongs to.</summary>
        public int BuildingIndex;

        /// <summary>Block index within the building.</summary>
        public int BlockIndex;

        /// <summary>Packed on/off state (1 bit per window).</summary>
        public uint PackedState;

        /// <summary>Packed color variant (2 bits per window, 16 windows per uint).</summary>
        public uint PackedColors;

        /// <summary>Timer for next state change in this block.</summary>
        public float NextChangeTime;
    }

    /// <summary>
    /// Configuration for city skyline generation.
    /// </summary>
    public struct SkylineConfig : IComponentData
    {
        /// <summary>Percentage of windows that are lit (0-1).</summary>
        public float WindowLitRatio;

        /// <summary>Percentage of lit windows that are yellow vs other colors.</summary>
        public float YellowWindowRatio;

        /// <summary>Average time between window state changes (seconds).</summary>
        public float WindowToggleInterval;

        /// <summary>Variance in toggle interval.</summary>
        public float ToggleIntervalVariance;

        /// <summary>Silhouette color (dark blue-black).</summary>
        public float4 SilhouetteColor;

        /// <summary>Primary window color (warm yellow).</summary>
        public float4 WindowColorYellow;

        /// <summary>Secondary window color (cool white).</summary>
        public float4 WindowColorWhite;

        /// <summary>Tertiary window color (warm orange).</summary>
        public float4 WindowColorOrange;

        /// <summary>Accent window color (cyan/blue).</summary>
        public float4 WindowColorCyan;
    }

    // ============================================================================
    // Star Field Components
    // ============================================================================

    /// <summary>
    /// Singleton state for the night sky star field.
    /// Provides subtle visual reference points for tracking rotation.
    /// </summary>
    public struct StarFieldState : IComponentData
    {
        /// <summary>Total number of stars in the sky dome.</summary>
        public int StarCount;

        /// <summary>Distance to star dome (meters).</summary>
        public float DomeRadius;

        /// <summary>Minimum elevation angle (radians, above horizon).</summary>
        public float MinElevation;

        /// <summary>Maximum elevation angle (radians).</summary>
        public float MaxElevation;

        /// <summary>Base star brightness (0-1, kept low for subtlety).</summary>
        public float BaseBrightness;

        /// <summary>Random seed for star generation.</summary>
        public uint RandomSeed;

        /// <summary>Time accumulator for twinkling.</summary>
        public float TwinkleTime;
    }

    /// <summary>
    /// Tag for the star field controller entity.
    /// </summary>
    public struct StarFieldTag : IComponentData { }

    /// <summary>
    /// Individual star in the sky dome.
    /// Stored in a dynamic buffer on the star field entity.
    /// </summary>
    public struct StarData : IBufferElementData
    {
        /// <summary>Azimuth angle (radians, 0 = north).</summary>
        public float Azimuth;

        /// <summary>Elevation angle (radians, above horizon).</summary>
        public float Elevation;

        /// <summary>Star brightness multiplier (0-1).</summary>
        public float Brightness;

        /// <summary>Star size multiplier.</summary>
        public float Size;

        /// <summary>Twinkle phase offset.</summary>
        public float TwinklePhase;

        /// <summary>Twinkle speed multiplier.</summary>
        public float TwinkleSpeed;

        /// <summary>Color temperature (0=warm, 1=cool).</summary>
        public float ColorTemperature;
    }

    /// <summary>
    /// Configuration for star field rendering.
    /// </summary>
    public struct StarFieldConfig : IComponentData
    {
        /// <summary>Base opacity for all stars (kept low for subtlety).</summary>
        public float BaseOpacity;

        /// <summary>Twinkle intensity (how much brightness varies).</summary>
        public float TwinkleIntensity;

        /// <summary>Warm star color (reddish-white).</summary>
        public float4 WarmStarColor;

        /// <summary>Cool star color (bluish-white).</summary>
        public float4 CoolStarColor;

        /// <summary>Neutral star color (pure white).</summary>
        public float4 NeutralStarColor;
    }

    // ============================================================================
    // Moon Components
    // ============================================================================

    /// <summary>
    /// Moon phase enumeration for rendering.
    /// </summary>
    public enum MoonPhase
    {
        NewMoon = 0,
        WaxingCrescent = 1,
        FirstQuarter = 2,
        WaxingGibbous = 3,
        FullMoon = 4,
        WaningGibbous = 5,
        LastQuarter = 6,
        WaningCrescent = 7
    }

    /// <summary>
    /// Singleton state for the moon.
    /// Uses real system date/time for accurate phase calculation.
    /// </summary>
    public struct MoonState : IComponentData
    {
        /// <summary>Current moon phase (0-7).</summary>
        public MoonPhase Phase;

        /// <summary>Precise phase progress (0-1 through full lunar cycle).</summary>
        public float PhaseProgress;

        /// <summary>Illumination fraction (0=new, 1=full).</summary>
        public float Illumination;

        /// <summary>Azimuth angle for moon position (radians).</summary>
        public float Azimuth;

        /// <summary>Elevation angle above horizon (radians).</summary>
        public float Elevation;

        /// <summary>Whether moon is currently visible.</summary>
        public bool IsVisible;

        /// <summary>Last system time used for calculation (ticks).</summary>
        public long LastUpdateTicks;
    }

    /// <summary>
    /// Tag for the moon controller entity.
    /// </summary>
    public struct MoonTag : IComponentData { }

    /// <summary>
    /// Configuration for moon rendering.
    /// </summary>
    public struct MoonConfig : IComponentData
    {
        /// <summary>Angular size of moon (degrees).</summary>
        public float AngularSize;

        /// <summary>Distance to moon billboard (meters).</summary>
        public float RenderDistance;

        /// <summary>Base glow radius multiplier.</summary>
        public float GlowRadius;

        /// <summary>Glow intensity (0-1).</summary>
        public float GlowIntensity;

        /// <summary>Full moon halo ring radius multiplier.</summary>
        public float HaloRadius;

        /// <summary>Full moon halo intensity.</summary>
        public float HaloIntensity;

        /// <summary>Moon surface color.</summary>
        public float4 MoonColor;

        /// <summary>Glow color.</summary>
        public float4 GlowColor;

        /// <summary>Halo ring color (for full moon).</summary>
        public float4 HaloColor;
    }
}
