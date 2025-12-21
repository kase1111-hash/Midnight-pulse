// ============================================================================
// Nightflow - Unity DOTS Components: Ambient State
// ============================================================================

using Unity.Entities;

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
}
