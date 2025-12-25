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
}
