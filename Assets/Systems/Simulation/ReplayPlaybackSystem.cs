// ============================================================================
// Nightflow - Replay Playback System
// Drives ghost vehicles from recorded input logs
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Buffers;
using Nightflow.Archetypes;

namespace Nightflow.Systems
{
    /// <summary>
    /// Plays back recorded inputs to drive ghost vehicles.
    /// Interpolates between logged inputs for smooth playback.
    ///
    /// From spec:
    /// - Second PlayerVehicle entity driven by log (identical sim)
    /// - Deterministic via seeded PRNG and pure math
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SteeringSystem))]
    public partial struct ReplayPlaybackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReplaySystemState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Process each ghost vehicle
            foreach (var (replayState, input, transform, velocity, buffer, trailBuffer) in
                SystemAPI.Query<RefRW<ReplayState>, RefRW<PlayerInput>, RefRW<WorldTransform>,
                               RefRW<Velocity>, DynamicBuffer<InputLogEntry>,
                               DynamicBuffer<GhostTrailPoint>>()
                    .WithAll<GhostVehicleTag>())
            {
                if (!replayState.ValueRO.IsPlaying || replayState.ValueRO.IsComplete)
                    continue;

                // Advance playback time
                float playbackSpeed = replayState.ValueRO.PlaybackSpeed;
                replayState.ValueRW.PlaybackTime += deltaTime * playbackSpeed;

                float currentTime = replayState.ValueRO.PlaybackTime;

                // Check for playback completion
                if (currentTime >= replayState.ValueRO.TotalDuration)
                {
                    if (replayState.ValueRO.Loop)
                    {
                        // Reset to beginning
                        ResetPlayback(ref replayState.ValueRW, ref transform.ValueRW,
                                     ref velocity.ValueRW);
                    }
                    else
                    {
                        replayState.ValueRW.IsComplete = true;
                        replayState.ValueRW.IsPlaying = false;
                        continue;
                    }
                }

                // Find input entries surrounding current time
                int bufferLength = buffer.Length;
                if (bufferLength == 0)
                    continue;

                int currentIndex = replayState.ValueRO.CurrentInputIndex;

                // Advance index to find correct position
                while (currentIndex < bufferLength - 1 &&
                       buffer[currentIndex + 1].Timestamp <= currentTime)
                {
                    currentIndex++;
                }

                replayState.ValueRW.CurrentInputIndex = currentIndex;

                // Interpolate between entries
                InputLogEntry entry0 = buffer[currentIndex];
                InputLogEntry entry1 = currentIndex < bufferLength - 1
                    ? buffer[currentIndex + 1]
                    : entry0;

                float t = 0f;
                float timeDiff = entry1.Timestamp - entry0.Timestamp;
                if (timeDiff > 0.0001f)
                {
                    t = math.saturate((currentTime - entry0.Timestamp) / timeDiff);
                }

                // Apply interpolated input
                input.ValueRW.Steer = math.lerp(entry0.Steer, entry1.Steer, t);
                input.ValueRW.Throttle = math.lerp(entry0.Throttle, entry1.Throttle, t);
                input.ValueRW.Brake = math.lerp(entry0.Brake, entry1.Brake, t);
                input.ValueRW.Handbrake = t < 0.5f ? entry0.Handbrake : entry1.Handbrake;

                // =============================================================
                // Trail Recording
                // =============================================================

                // Add trail point at regular intervals
                const float TrailSampleInterval = 0.1f;
                bool shouldAddTrail = trailBuffer.Length == 0 ||
                    (currentTime - trailBuffer[trailBuffer.Length - 1].Timestamp) >= TrailSampleInterval;

                if (shouldAddTrail && trailBuffer.Length < trailBuffer.Capacity)
                {
                    trailBuffer.Add(new GhostTrailPoint
                    {
                        Position = transform.ValueRO.Position,
                        Rotation = transform.ValueRO.Rotation,
                        Timestamp = currentTime,
                        Alpha = 1f
                    });
                }

                // Trim old trail points
                const float MaxTrailAge = 2f;
                float cutoffTime = currentTime - MaxTrailAge;
                while (trailBuffer.Length > 0 && trailBuffer[0].Timestamp < cutoffTime)
                {
                    trailBuffer.RemoveAt(0);
                }
            }
        }

        private void ResetPlayback(ref ReplayState replayState, ref WorldTransform transform,
                                   ref Velocity velocity)
        {
            replayState.PlaybackTime = 0f;
            replayState.CurrentInputIndex = 0;
            replayState.IsComplete = false;

            // Reset to starting position/rotation
            transform.Position = replayState.StartPosition;
            transform.Rotation = replayState.StartRotation;

            // Reset velocity
            velocity.Forward = 0f;
            velocity.Lateral = 0f;
            velocity.Angular = 0f;
        }
    }

    /// <summary>
    /// Spawns and manages ghost vehicle lifecycle.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ReplayPlaybackSystem))]
    public partial struct GhostSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReplaySystemState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityManager entityManager = state.EntityManager;
            EntityCommandBuffer ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var replaySystemState in SystemAPI.Query<RefRW<ReplaySystemState>>())
            {
                // Check if we should spawn a ghost
                if (!replaySystemState.ValueRO.GhostActive &&
                    replaySystemState.ValueRO.InputsRecorded > 10) // Need minimum inputs
                {
                    // Get player position and inputs for ghost
                    float3 playerPos = float3.zero;
                    quaternion playerRot = quaternion.identity;
                    uint seed = replaySystemState.ValueRO.CurrentSeed;

                    foreach (var (transform, buffer) in
                        SystemAPI.Query<RefRO<WorldTransform>, DynamicBuffer<InputLogEntry>>()
                            .WithAll<PlayerVehicleTag>())
                    {
                        if (buffer.Length > 0)
                        {
                            // Use first recorded position
                            playerPos = transform.ValueRO.Position;
                            playerRot = transform.ValueRO.Rotation;

                            // Create ghost entity
                            Entity ghost = ecb.CreateEntity(EntityArchetypes.GhostVehicle);

                            // Set starting position (offset slightly to side)
                            float3 ghostPos = playerPos + new float3(-2f, 0f, 0f);

                            ecb.SetComponent(ghost, new WorldTransform
                            {
                                Position = ghostPos,
                                Rotation = playerRot
                            });

                            ecb.SetComponent(ghost, new ReplayState
                            {
                                GlobalSeed = seed,
                                RunId = (int)(seed % 10000),
                                PlaybackTime = 0f,
                                CurrentInputIndex = 0,
                                IsPlaying = true,
                                IsComplete = false,
                                Loop = true,
                                PlaybackSpeed = 1f,
                                TotalDuration = buffer[buffer.Length - 1].Timestamp,
                                StartPosition = ghostPos,
                                StartRotation = playerRot
                            });

                            ecb.SetComponent(ghost, new GhostRenderState
                            {
                                Alpha = 0.5f,
                                BaseAlpha = 0.5f,
                                ShowTrail = true,
                                TrailLength = 2f,
                                TrailFade = 0.8f,
                                WireframeColor = new float3(0.3f, 0.8f, 1f), // Cyan ghost
                                PulsePhase = 0f,
                                PulseSpeed = 1f
                            });

                            // Copy input log to ghost
                            DynamicBuffer<InputLogEntry> ghostBuffer =
                                ecb.SetBuffer<InputLogEntry>(ghost);
                            for (int i = 0; i < buffer.Length; i++)
                            {
                                ghostBuffer.Add(buffer[i]);
                            }

                            replaySystemState.ValueRW.GhostVehicle = ghost;
                            replaySystemState.ValueRW.GhostActive = true;
                        }
                        break;
                    }
                }
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }
}
