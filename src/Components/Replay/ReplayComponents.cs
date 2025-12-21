// ============================================================================
// Nightflow - Unity DOTS Components: Replay System
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    /// <summary>
    /// State for a ghost vehicle replaying recorded inputs.
    /// Attached to GhostVehicle entities.
    ///
    /// From spec:
    /// - Record: globalSeed + fixed-timestep input log
    /// - Second PlayerVehicle entity driven by log (identical sim)
    /// - Deterministic via seeded PRNG and pure math
    /// </summary>
    public struct ReplayState : IComponentData
    {
        /// <summary>World seed used for this replay (determinism).</summary>
        public uint GlobalSeed;

        /// <summary>Unique identifier for this recorded run.</summary>
        public int RunId;

        /// <summary>Current playback time (seconds from start).</summary>
        public float PlaybackTime;

        /// <summary>Current index in the InputLogEntry buffer.</summary>
        public int CurrentInputIndex;

        /// <summary>Whether playback is active.</summary>
        public bool IsPlaying;

        /// <summary>Whether playback has completed.</summary>
        public bool IsComplete;

        /// <summary>Loop playback when complete.</summary>
        public bool Loop;

        /// <summary>Playback speed multiplier (1.0 = normal).</summary>
        public float PlaybackSpeed;

        /// <summary>Total recorded duration.</summary>
        public float TotalDuration;

        /// <summary>Starting position for replay reset.</summary>
        public float3 StartPosition;

        /// <summary>Starting rotation for replay reset.</summary>
        public quaternion StartRotation;
    }

    /// <summary>
    /// Singleton for global replay system state.
    /// Manages recording and available replays.
    /// </summary>
    public struct ReplaySystemState : IComponentData
    {
        /// <summary>Whether currently recording player input.</summary>
        public bool IsRecording;

        /// <summary>Current recording time.</summary>
        public float RecordingTime;

        /// <summary>Fixed timestep for recording (e.g., 1/60).</summary>
        public float RecordingInterval;

        /// <summary>Time since last input was recorded.</summary>
        public float TimeSinceLastRecord;

        /// <summary>Number of inputs recorded this session.</summary>
        public int InputsRecorded;

        /// <summary>Maximum inputs to record (buffer limit).</summary>
        public int MaxInputs;

        /// <summary>Entity reference to the ghost vehicle (if spawned).</summary>
        public Entity GhostVehicle;

        /// <summary>Whether a ghost is currently active.</summary>
        public bool GhostActive;

        /// <summary>Global seed for current recording session.</summary>
        public uint CurrentSeed;
    }

    /// <summary>
    /// Ghost vehicle rendering state for visual effects.
    /// </summary>
    public struct GhostRenderState : IComponentData
    {
        /// <summary>Ghost transparency (0 = invisible, 1 = fully visible).</summary>
        public float Alpha;

        /// <summary>Base alpha value.</summary>
        public float BaseAlpha;

        /// <summary>Whether to render trail effect.</summary>
        public bool ShowTrail;

        /// <summary>Trail length in seconds.</summary>
        public float TrailLength;

        /// <summary>Trail fade rate.</summary>
        public float TrailFade;

        /// <summary>Wireframe color for ghost.</summary>
        public float3 WireframeColor;

        /// <summary>Pulse effect phase (0-1).</summary>
        public float PulsePhase;

        /// <summary>Pulse speed (Hz).</summary>
        public float PulseSpeed;
    }

}

namespace Nightflow.Buffers
{
    /// <summary>
    /// Trail point for ghost vehicle trail rendering.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct GhostTrailPoint : IBufferElementData
    {
        /// <summary>World position of this trail point.</summary>
        public float3 Position;

        /// <summary>Vehicle rotation at this point.</summary>
        public quaternion Rotation;

        /// <summary>Time this point was recorded.</summary>
        public float Timestamp;

        /// <summary>Alpha at this point (fades over trail length).</summary>
        public float Alpha;
    }
}
