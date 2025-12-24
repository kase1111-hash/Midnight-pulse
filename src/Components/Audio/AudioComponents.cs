// ============================================================================
// Nightflow - Audio System Components
// ECS-based audio management for engine, collisions, sirens, ambient, and music
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    // ========================================================================
    // ENGINE & VEHICLE AUDIO
    // ========================================================================

    /// <summary>
    /// Engine audio state for a vehicle.
    /// Controls layered engine sounds with RPM-based pitch modulation.
    /// </summary>
    public struct EngineAudio : IComponentData
    {
        public bool IsActive;
        public float RPM;                   // Current engine RPM (0-8000)
        public float TargetRPM;             // Target RPM for smoothing
        public float ThrottleInput;         // 0-1 throttle position
        public float Load;                  // Engine load factor (0-1)

        // Volume levels per layer
        public float IdleVolume;
        public float LowRPMVolume;
        public float MidRPMVolume;
        public float HighRPMVolume;

        // Pitch multipliers
        public float BasePitch;             // Base pitch (1.0 = normal)
        public float CurrentPitch;          // Current interpolated pitch

        // State
        public EngineState State;
        public float StateTimer;
    }

    /// <summary>
    /// Engine state for sound selection.
    /// </summary>
    public enum EngineState : byte
    {
        Off = 0,
        Idle = 1,
        Accelerating = 2,
        Cruising = 3,
        Decelerating = 4,
        Redline = 5
    }

    /// <summary>
    /// Tire audio state for rolling and skid sounds.
    /// </summary>
    public struct TireAudio : IComponentData
    {
        public bool IsActive;
        public float Speed;                 // Vehicle speed in m/s
        public float SlipRatio;             // Combined slip for skid sounds
        public float SurfaceType;           // 0=asphalt, 1=concrete, etc.

        // Volume levels
        public float RollVolume;            // Tire rolling volume
        public float SkidVolume;            // Skid/squeal volume

        // Per-wheel slip for stereo positioning
        public float4 WheelSlip;            // FL, FR, RL, RR
    }

    /// <summary>
    /// Wind audio that scales with speed squared.
    /// </summary>
    public struct WindAudio : IComponentData
    {
        public bool IsActive;
        public float Speed;                 // Vehicle speed
        public float Volume;                // Current wind volume
        public float Pitch;                 // Current wind pitch
        public float TurbulenceAmount;      // Random fluctuation intensity
    }

    // ========================================================================
    // COLLISION & IMPACT AUDIO
    // ========================================================================

    /// <summary>
    /// Request to play a collision sound.
    /// </summary>
    public struct CollisionAudioEvent : IBufferElementData
    {
        public float3 Position;
        public float3 Normal;
        public float Impulse;               // Collision force
        public CollisionAudioType Type;
        public float Delay;                 // Optional delay before playing
    }

    /// <summary>
    /// Types of collision sounds.
    /// </summary>
    public enum CollisionAudioType : byte
    {
        None = 0,
        LightImpact = 1,        // Cones, small debris
        MediumImpact = 2,       // Traffic sideswipe
        HeavyImpact = 3,        // Barrier, crashed car
        MetalScrape = 4,        // Continuous grinding
        GlassShatter = 5        // Severe crash
    }

    /// <summary>
    /// Continuous scraping sound state.
    /// </summary>
    public struct ScrapeAudio : IComponentData
    {
        public bool IsActive;
        public float3 ContactPoint;
        public float Intensity;             // 0-1 scrape intensity
        public float Volume;
        public float Pitch;
        public float Duration;              // Time scraping
    }

    // ========================================================================
    // EMERGENCY VEHICLE AUDIO
    // ========================================================================

    /// <summary>
    /// Siren audio state with doppler effect support.
    /// </summary>
    public struct SirenAudio : IComponentData
    {
        public bool IsActive;
        public SirenType Type;
        public float3 Position;
        public float3 Velocity;

        // Doppler effect
        public float DopplerShift;          // Calculated pitch shift
        public float Distance;              // Distance to listener
        public float RelativeVelocity;      // Velocity toward/away from listener

        // Siren pattern
        public float Phase;                 // Current phase in siren cycle
        public float Frequency;             // Siren warble frequency

        // Volume
        public float Volume;
        public float TargetVolume;
        public float FadeSpeed;
    }

    /// <summary>
    /// Siren type for different emergency vehicles.
    /// </summary>
    public enum SirenType : byte
    {
        None = 0,
        Police = 1,             // Wail siren
        Ambulance = 2,          // Yelp siren
        Fire = 3                // Air horn
    }

    // ========================================================================
    // ENVIRONMENT & AMBIENT AUDIO
    // ========================================================================

    /// <summary>
    /// Reverb zone for tunnels and overpasses.
    /// </summary>
    public struct ReverbZone : IComponentData
    {
        public ReverbType Type;
        public float3 Center;
        public float3 Size;
        public float BlendDistance;         // Distance to blend reverb
        public float CurrentBlend;          // Current reverb blend (0-1)

        // Reverb parameters
        public float DecayTime;
        public float EarlyReflections;
        public float LateReverb;
        public float Diffusion;
    }

    /// <summary>
    /// Reverb types for different environments.
    /// </summary>
    public enum ReverbType : byte
    {
        None = 0,
        OpenRoad = 1,           // Minimal reverb
        Tunnel = 2,             // Long decay, metallic
        Overpass = 3,           // Short decay, concrete
        Urban = 4               // Medium decay, diffuse
    }

    /// <summary>
    /// Ambient audio layer state.
    /// </summary>
    public struct AmbientAudio : IComponentData
    {
        public bool IsActive;
        public AmbientType Type;
        public float Volume;
        public float TargetVolume;
        public float FadeSpeed;
        public float Pitch;
    }

    /// <summary>
    /// Types of ambient audio.
    /// </summary>
    public enum AmbientType : byte
    {
        None = 0,
        OpenRoad = 1,           // Quiet night ambience
        DistantTraffic = 2,     // Highway hum
        TunnelDrone = 3,        // Low tunnel resonance
        CityAmbience = 4        // Distant city sounds
    }

    // ========================================================================
    // MUSIC SYSTEM
    // ========================================================================

    /// <summary>
    /// Dynamic music state with intensity layers.
    /// </summary>
    public struct MusicState : IComponentData
    {
        public bool IsPlaying;
        public MusicTrack CurrentTrack;
        public float Intensity;             // 0-1 intensity level
        public float TargetIntensity;
        public float IntensitySmoothing;

        // Layer volumes
        public float BaseLayerVolume;
        public float LowIntensityVolume;
        public float HighIntensityVolume;
        public float StingerVolume;

        // Timing
        public float BPM;
        public float CurrentBeat;
        public float MeasurePosition;

        // Transitions
        public MusicTransition PendingTransition;
        public float TransitionProgress;
    }

    /// <summary>
    /// Music tracks.
    /// </summary>
    public enum MusicTrack : byte
    {
        None = 0,
        MainGameplay = 1,       // Primary driving music
        Terminal = 2,           // End credits ambience
        Menu = 3                // Pause/menu music
    }

    /// <summary>
    /// Music transition types.
    /// </summary>
    public enum MusicTransition : byte
    {
        None = 0,
        Crossfade = 1,          // Smooth crossfade
        BeatSync = 2,           // Wait for beat to transition
        Immediate = 3,          // Instant change
        FadeOut = 4             // Fade to silence
    }

    /// <summary>
    /// Music intensity trigger events.
    /// </summary>
    public struct MusicIntensityEvent : IBufferElementData
    {
        public float IntensityDelta;        // Change in intensity
        public float Duration;              // How long to maintain
        public MusicIntensityReason Reason;
    }

    /// <summary>
    /// Reasons for music intensity changes.
    /// </summary>
    public enum MusicIntensityReason : byte
    {
        None = 0,
        SpeedBoost = 1,
        NearMiss = 2,
        Collision = 3,
        EmergencyClose = 4,
        HighMultiplier = 5,
        LowDamage = 6
    }

    // ========================================================================
    // UI AUDIO
    // ========================================================================

    /// <summary>
    /// Request to play a UI sound.
    /// </summary>
    public struct UIAudioEvent : IBufferElementData
    {
        public UISoundType Type;
        public float Volume;
        public float Pitch;
        public float Delay;
    }

    /// <summary>
    /// Types of UI sounds.
    /// </summary>
    public enum UISoundType : byte
    {
        None = 0,
        ScoreTick = 1,          // Score increment
        MultiplierUp = 2,       // Multiplier increase
        MultiplierLost = 3,     // Multiplier reset
        DamageWarning = 4,      // Damage beep
        NearMiss = 5,           // Close pass whoosh
        LaneChange = 6,         // Subtle swoosh
        MenuSelect = 7,         // Button click
        MenuBack = 8,           // Back/cancel
        Pause = 9,              // Pause sound
        Unpause = 10,           // Resume sound
        HighScore = 11,         // New high score
        GameOver = 12           // Crash/game over
    }

    // ========================================================================
    // AUDIO SYSTEM CONFIG
    // ========================================================================

    /// <summary>
    /// Global audio configuration singleton.
    /// </summary>
    public struct AudioConfig : IComponentData
    {
        // Master volumes
        public float MasterVolume;
        public float MusicVolume;
        public float SFXVolume;
        public float EngineVolume;
        public float AmbientVolume;

        // Doppler settings
        public float DopplerScale;
        public float SpeedOfSound;

        // Distance attenuation
        public float MinDistance;
        public float MaxDistance;
        public float RolloffFactor;

        // Engine audio settings
        public float EngineRPMSmoothing;
        public float EnginePitchRange;      // Min to max pitch range

        // Music settings
        public float MusicIntensitySmoothing;
        public float MusicCrossfadeDuration;
    }

    /// <summary>
    /// Audio listener position (usually camera).
    /// </summary>
    public struct AudioListener : IComponentData
    {
        public float3 Position;
        public float3 Velocity;
        public float3 Forward;
        public float3 Up;
    }

    /// <summary>
    /// One-shot audio request for positional sounds.
    /// </summary>
    public struct OneShotAudioRequest : IBufferElementData
    {
        public int ClipID;                  // Reference to audio clip
        public float3 Position;
        public float Volume;
        public float Pitch;
        public float MinDistance;
        public float MaxDistance;
        public float Delay;
        public bool Is3D;
    }

    /// <summary>
    /// Tag for entities that emit audio.
    /// </summary>
    public struct AudioEmitter : IComponentData
    {
        public float3 Position;
        public float3 Velocity;
        public float Volume;
        public float MinDistance;
        public float MaxDistance;
        public bool Is3D;
    }
}
