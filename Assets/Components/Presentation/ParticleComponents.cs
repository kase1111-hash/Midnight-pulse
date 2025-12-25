// ============================================================================
// Nightflow - Particle System Components
// ECS-based particle effects for sparks, smoke, speed lines, and flash
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    /// <summary>
    /// Individual particle data for GPU instancing.
    /// </summary>
    public struct Particle : IBufferElementData
    {
        public float3 Position;
        public float3 Velocity;
        public float3 Acceleration;
        public float4 Color;           // RGBA
        public float Size;
        public float Rotation;
        public float RotationSpeed;
        public float Lifetime;
        public float MaxLifetime;
        public float EmissionIntensity;
    }

    /// <summary>
    /// Particle emitter configuration.
    /// </summary>
    public struct ParticleEmitter : IComponentData
    {
        public ParticleType Type;
        public float3 Position;
        public float3 Direction;
        public float3 Spread;          // Cone angle in radians
        public float EmissionRate;      // Particles per second
        public float EmissionAccumulator;
        public bool IsActive;
        public bool IsBurst;            // Emit all at once
        public int BurstCount;
        public int MaxParticles;

        // Particle properties
        public float4 ColorStart;
        public float4 ColorEnd;
        public float SizeStart;
        public float SizeEnd;
        public float SpeedMin;
        public float SpeedMax;
        public float LifetimeMin;
        public float LifetimeMax;
        public float GravityMultiplier;
        public float Drag;
    }

    /// <summary>
    /// Types of particle effects.
    /// </summary>
    public enum ParticleType : byte
    {
        None = 0,
        Spark = 1,          // Collision sparks
        TireSmoke = 2,      // Drift/skid smoke
        SpeedLine = 3,      // High velocity streaks
        CrashFlash = 4,     // Screen flash on impact
        Debris = 5,         // Small debris chunks
        Glow = 6            // Ambient glow particles
    }

    /// <summary>
    /// Tag for spark emitter attached to vehicles.
    /// </summary>
    public struct SparkEmitterTag : IComponentData
    {
        public float3 LocalOffset;      // Offset from vehicle center
        public float IntensityMultiplier;
    }

    /// <summary>
    /// Tag for tire smoke emitter attached to wheel positions.
    /// </summary>
    public struct TireSmokeEmitterTag : IComponentData
    {
        public int WheelIndex;          // 0=FL, 1=FR, 2=RL, 3=RR
        public float SlipThreshold;     // Minimum slip to emit
    }

    /// <summary>
    /// Speed line effect controller (screen-space streaks).
    /// </summary>
    public struct SpeedLineEffect : IComponentData
    {
        public bool IsActive;
        public float Intensity;         // 0-1 based on speed
        public float SpeedThreshold;    // Minimum speed to activate
        public float LineLength;        // Length of speed streaks
        public float LineDensity;       // Number of lines
        public float4 LineColor;        // Usually white/cyan
        public float FadeSpeed;         // How fast lines fade
    }

    /// <summary>
    /// Crash flash effect controller (screen overlay).
    /// </summary>
    public struct CrashFlashEffect : IComponentData
    {
        public bool IsActive;
        public float Intensity;         // Current flash intensity
        public float Duration;          // Total flash duration
        public float Timer;             // Current timer
        public float4 FlashColor;       // Usually white or red
        public CrashFlashPhase Phase;
    }

    /// <summary>
    /// Crash flash animation phases.
    /// </summary>
    public enum CrashFlashPhase : byte
    {
        None = 0,
        FlashIn = 1,        // Quick ramp up
        Hold = 2,           // Brief hold
        FadeOut = 3         // Gradual fade
    }

    /// <summary>
    /// Request to spawn particles at a location.
    /// </summary>
    public struct ParticleSpawnRequest : IBufferElementData
    {
        public ParticleType Type;
        public float3 Position;
        public float3 Direction;
        public float3 Normal;           // Surface normal for sparks
        public float Intensity;         // 0-1 effect strength
        public int Count;               // Number of particles
    }

    /// <summary>
    /// Singleton for particle system configuration.
    /// </summary>
    public struct ParticleSystemConfig : IComponentData
    {
        // Spark settings
        public float4 SparkColorStart;
        public float4 SparkColorEnd;
        public float SparkSizeMin;
        public float SparkSizeMax;
        public float SparkSpeedMin;
        public float SparkSpeedMax;
        public float SparkLifetime;
        public float SparkGravity;

        // Tire smoke settings
        public float4 SmokeColorStart;
        public float4 SmokeColorEnd;
        public float SmokeSizeStart;
        public float SmokeSizeEnd;
        public float SmokeSpeed;
        public float SmokeLifetime;
        public float SmokeDrag;

        // Speed line settings
        public float SpeedLineThreshold;
        public float SpeedLineMaxIntensity;
        public float4 SpeedLineColor;

        // Global settings
        public int MaxTotalParticles;
        public float GlobalIntensityMultiplier;
    }

    /// <summary>
    /// Collision event data for triggering spark effects.
    /// </summary>
    public struct CollisionEvent : IBufferElementData
    {
        public float3 Position;
        public float3 Normal;
        public float3 RelativeVelocity;
        public float Impulse;
        public CollisionType Type;
    }

    /// <summary>
    /// Types of collisions for effect selection.
    /// </summary>
    public enum CollisionType : byte
    {
        None = 0,
        VehicleVehicle = 1,
        VehicleBarrier = 2,
        VehicleHazard = 3,
        Scrape = 4          // Continuous contact
    }

    /// <summary>
    /// Visual drift state for tire smoke triggering.
    /// Separate from VehicleComponents.DriftState for particle effects.
    /// </summary>
    public struct DriftVisualState : IComponentData
    {
        public bool IsDrifting;
        public float DriftAngle;        // Degrees from forward
        public float DriftIntensity;    // 0-1
        public float4 WheelSlip;        // Slip ratio per wheel
        public float DriftTime;         // Time spent drifting
    }
}
