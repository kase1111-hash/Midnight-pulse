// ============================================================================
// Nightflow - Unity DOTS Components: Audio
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    /// <summary>
    /// Audio parameter state for MonoBehaviour audio bridge.
    /// Singleton component updated by AudioSystem.
    ///
    /// From spec:
    /// - Engine: Pitch/load blend based on speed
    /// - Tires: Slip/skid layer
    /// - Wind: Rush intensity ∝ speed²
    /// - Environment: Reverb zones (tunnel boomy, overpass ringing, open dry)
    /// </summary>
    public struct AudioState : IComponentData
    {
        // Engine audio
        public float EnginePitch;
        public float EngineVolume;
        public float EngineRPM;
        public float EngineLoad;

        // Tire audio
        public float TireSquealVolume;
        public float TireSquealPitch;
        public float TireSkidVolume;

        // Wind audio (intensity ∝ speed²)
        public float WindVolume;
        public float WindPitch;

        // Impact audio (one-shot triggers)
        public bool ImpactTriggered;
        public float ImpactVolume;
        public float ImpactPitch;
        public float3 ImpactPosition;

        // Siren audio
        public float SirenVolume;
        public float SirenPitch;
        public float3 SirenPosition;
        public bool SirenActive;

        // Environment reverb
        public ReverbZone CurrentZone;
        public float ReverbMix;
        public float ReverbDecay;

        // Music/Ambience
        public float MusicIntensity;
        public float AmbienceVolume;
    }

    /// <summary>
    /// Environment reverb zone types.
    /// </summary>
    public enum ReverbZone : byte
    {
        OpenRoad = 0,     // Dry, minimal reverb
        Tunnel = 1,       // Boomy, long decay
        Overpass = 2,     // Ringing, metallic
        Construction = 3  // Industrial, cluttered
    }

    /// <summary>
    /// Traffic vehicle audio state (for spatial audio).
    /// </summary>
    public struct TrafficAudioState : IComponentData
    {
        public float EngineVolume;
        public float EnginePitch;
        public float DopplerShift;
        public float Distance;
    }

    /// <summary>
    /// Crash audio event data.
    /// </summary>
    public struct CrashAudioEvent : IComponentData
    {
        public bool Triggered;
        public float Intensity;
        public CrashAudioType Type;
        public float3 Position;
    }

    /// <summary>
    /// Types of crash audio.
    /// </summary>
    public enum CrashAudioType : byte
    {
        Impact = 0,
        Scrape = 1,
        Crunch = 2,
        Explosion = 3
    }
}
