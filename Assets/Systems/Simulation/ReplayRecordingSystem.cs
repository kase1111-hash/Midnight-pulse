// ============================================================================
// Nightflow - Replay Recording System
// Records player inputs at fixed timesteps for replay/ghost playback
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Buffers;

namespace Nightflow.Systems
{
    /// <summary>
    /// Records player inputs at fixed timesteps for deterministic replay.
    ///
    /// From spec:
    /// - Record: globalSeed + fixed-timestep input log
    /// - Deterministic via seeded PRNG and pure math
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InputSystem))]
    [UpdateBefore(typeof(AutopilotSystem))]
    public partial struct ReplayRecordingSystem : ISystem
    {
        // Recording parameters
        private const float RecordingInterval = 1f / 60f;   // 60 Hz fixed timestep
        private const int MaxInputsPerRun = 1024;           // Match buffer capacity

        public void OnCreate(ref SystemState state)
        {
            // Require ReplaySystemState singleton
            state.RequireForUpdate<ReplaySystemState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get replay system state singleton
            foreach (var replayState in SystemAPI.Query<RefRW<ReplaySystemState>>())
            {
                if (!replayState.ValueRO.IsRecording)
                    continue;

                // Update recording time
                replayState.ValueRW.RecordingTime += deltaTime;
                replayState.ValueRW.TimeSinceLastRecord += deltaTime;

                // Check if we should record this frame
                if (replayState.ValueRO.TimeSinceLastRecord < RecordingInterval)
                    continue;

                // Record input from player vehicle
                foreach (var (input, transform, velocity, buffer) in
                    SystemAPI.Query<RefRO<PlayerInput>, RefRO<WorldTransform>,
                                   RefRO<Velocity>, DynamicBuffer<InputLogEntry>>()
                        .WithAll<PlayerVehicleTag>()
                        .WithNone<CrashedTag>())
                {
                    // Check buffer capacity
                    if (buffer.Length >= MaxInputsPerRun)
                    {
                        // Buffer full - stop recording
                        replayState.ValueRW.IsRecording = false;
                        continue;
                    }

                    // Create input log entry
                    var entry = new InputLogEntry
                    {
                        Timestamp = replayState.ValueRO.RecordingTime,
                        Steer = input.ValueRO.Steer,
                        Throttle = input.ValueRO.Throttle,
                        Brake = input.ValueRO.Brake,
                        Handbrake = input.ValueRO.Handbrake
                    };

                    // Add to buffer
                    buffer.Add(entry);

                    // Update recording state
                    replayState.ValueRW.InputsRecorded = buffer.Length;
                    replayState.ValueRW.TimeSinceLastRecord = 0f;

                    break; // Only one player vehicle
                }
            }
        }
    }

    /// <summary>
    /// Manages recording lifecycle - start/stop/save.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ReplayRecordingSystem))]
    public partial struct ReplayRecordingControlSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReplaySystemState>();
            state.RequireForUpdate<GameState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get game state for crash detection
            bool crashStarted = false;
            bool crashEnded = false;

            foreach (var gameState in SystemAPI.Query<RefRO<GameState>>())
            {
                crashStarted = gameState.ValueRO.CrashPhase == CrashFlowPhase.Impact;
                crashEnded = gameState.ValueRO.CrashPhase == CrashFlowPhase.FadeIn;
                break;
            }

            foreach (var replayState in SystemAPI.Query<RefRW<ReplaySystemState>>())
            {
                // Start recording when player takes control after crash reset
                if (crashEnded && !replayState.ValueRO.IsRecording)
                {
                    // Check if player has taken control
                    bool playerControlActive = false;
                    foreach (var gameState in SystemAPI.Query<RefRO<GameState>>())
                    {
                        playerControlActive = gameState.ValueRO.PlayerControlActive;
                        break;
                    }

                    // Don't auto-start during autopilot - wait for player input
                }

                // Stop recording on crash
                if (crashStarted && replayState.ValueRO.IsRecording)
                {
                    replayState.ValueRW.IsRecording = false;

                    // The recorded inputs are now available for ghost playback
                    // Copy to ghost vehicle when spawned
                }
            }

            // Auto-start recording when player takes control
            foreach (var (replayState, gameState) in
                SystemAPI.Query<RefRW<ReplaySystemState>, RefRO<GameState>>())
            {
                // Start recording when player first takes control
                if (gameState.ValueRO.PlayerControlActive &&
                    !replayState.ValueRO.IsRecording &&
                    replayState.ValueRO.InputsRecorded == 0)
                {
                    StartRecording(ref replayState.ValueRW, ref state);
                }
            }
        }

        private void StartRecording(ref ReplaySystemState replayState, ref SystemState state)
        {
            replayState.IsRecording = true;
            replayState.RecordingTime = 0f;
            replayState.TimeSinceLastRecord = 0f;
            replayState.InputsRecorded = 0;
            replayState.RecordingInterval = 1f / 60f;
            replayState.MaxInputs = MaxInputsPerRun;

            // Clear existing input buffer on player vehicle
            foreach (var buffer in SystemAPI.Query<DynamicBuffer<InputLogEntry>>()
                .WithAll<PlayerVehicleTag>())
            {
                buffer.Clear();
                break;
            }
        }

        private const int MaxInputsPerRun = 1024;
    }
}
