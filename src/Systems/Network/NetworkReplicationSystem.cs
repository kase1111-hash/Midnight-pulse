// ============================================================================
// Nightflow - Network Replication System
// Input-based replication with deterministic simulation
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;
using Nightflow.Config;

namespace Nightflow.Systems
{
    /// <summary>
    /// Captures local player input and prepares it for network transmission.
    /// Runs after InputSystem to capture final input values.
    ///
    /// From spec:
    /// - Network replication via input logs + deterministic seeds
    /// - Replicate ECS state deltas across network
    /// </summary>
    [DisableAutoCreation] // Deferred to v0.3.0 — no transport layer
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InputSystem))]
    public partial struct NetworkInputCaptureSystem : ISystem
    {
        // Input capture parameters
        private const int MaxPendingInputs = 128;
        private const float InputSendRate = 1f / 60f; // 60Hz

        private float _timeSinceLastSend;

        public void OnCreate(ref SystemState state)
        {
            _timeSinceLastSend = 0f;
            state.Enabled = false; // Deferred to v0.3.0 — no transport layer
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get network state
            bool isConnected = false;
            uint currentTick = 0;

            foreach (var netState in SystemAPI.Query<RefRW<NetworkState>>())
            {
                isConnected = netState.ValueRO.IsConnected;
                currentTick = netState.ValueRO.CurrentTick;

                // Increment tick each frame (60Hz)
                netState.ValueRW.CurrentTick++;
                break;
            }

            if (!isConnected) return;

            _timeSinceLastSend += deltaTime;

            // Capture input at fixed rate
            if (_timeSinceLastSend < InputSendRate) return;
            _timeSinceLastSend = 0f;

            // =============================================================
            // Capture Local Player Input
            // =============================================================

            foreach (var (input, networkInput, netPlayer, entity) in
                SystemAPI.Query<RefRO<PlayerInput>, RefRW<NetworkInput>, RefRO<NetworkPlayer>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithEntityAccess())
            {
                if (!netPlayer.ValueRO.IsLocal) continue;

                // Update network input with current tick
                networkInput.ValueRW.Steer = input.ValueRO.Steer;
                networkInput.ValueRW.Throttle = input.ValueRO.Throttle;
                networkInput.ValueRW.Brake = input.ValueRO.Brake;
                networkInput.ValueRW.Handbrake = input.ValueRO.Handbrake;
                networkInput.ValueRW.Tick = currentTick;
                networkInput.ValueRW.SequenceNumber++;
                networkInput.ValueRW.Acknowledged = false;

                // Buffer input for prediction/rollback
                if (SystemAPI.HasBuffer<NetworkInputEntry>(entity))
                {
                    var buffer = SystemAPI.GetBuffer<NetworkInputEntry>(entity);

                    // Remove old acknowledged inputs
                    while (buffer.Length > 0 && buffer[0].Acknowledged)
                    {
                        buffer.RemoveAt(0);
                    }

                    // Add new input if buffer not full
                    if (buffer.Length < MaxPendingInputs)
                    {
                        buffer.Add(new NetworkInputEntry
                        {
                            Tick = currentTick,
                            Steer = input.ValueRO.Steer,
                            Throttle = input.ValueRO.Throttle,
                            Brake = input.ValueRO.Brake,
                            Handbrake = input.ValueRO.Handbrake,
                            Acknowledged = false
                        });
                    }
                }

                // Update prediction state
                foreach (var predState in SystemAPI.Query<RefRW<InputPredictionState>>()
                    .WithAll<PlayerVehicleTag>())
                {
                    predState.ValueRW.NewestSentTick = currentTick;
                    predState.ValueRW.PendingInputCount++;
                }
            }
        }
    }

    /// <summary>
    /// Applies received network inputs to remote player vehicles.
    /// Uses interpolation for smooth visual updates.
    /// </summary>
    [DisableAutoCreation] // Deferred to v0.3.0
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(NetworkInputCaptureSystem))]
    public partial struct NetworkInputApplySystem : ISystem
    {
        // Interpolation parameters
        private const float InterpolationDelay = 0.1f; // 100ms buffer
        private const float MaxExtrapolation = 0.2f;   // Max prediction time
        private const float PositionCorrectionRate = 10f;
        private const float RotationCorrectionRate = 15f;

        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false; // Deferred to v0.3.0 — no transport layer
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get network tick for timing
            uint serverTick = 0;
            foreach (var netState in SystemAPI.Query<RefRO<NetworkState>>())
            {
                serverTick = netState.ValueRO.LastConfirmedTick;
                break;
            }

            // =============================================================
            // Apply Network Input to Remote Players
            // =============================================================

            foreach (var (playerInput, networkInput, netPlayer, entity) in
                SystemAPI.Query<RefRW<PlayerInput>, RefRO<NetworkInput>, RefRO<NetworkPlayer>>()
                    .WithAll<RemotePlayerTag>()
                    .WithEntityAccess())
            {
                // Apply buffered input from network
                if (SystemAPI.HasBuffer<NetworkInputEntry>(entity))
                {
                    var buffer = SystemAPI.GetBuffer<NetworkInputEntry>(entity);

                    // Find input closest to interpolation target tick
                    uint targetTick = serverTick > 6 ? serverTick - 6 : 0; // ~100ms delay at 60Hz

                    int bestIndex = -1;
                    uint bestDiff = uint.MaxValue;

                    for (int i = 0; i < buffer.Length; i++)
                    {
                        uint diff = buffer[i].Tick > targetTick
                            ? buffer[i].Tick - targetTick
                            : targetTick - buffer[i].Tick;

                        if (diff < bestDiff)
                        {
                            bestDiff = diff;
                            bestIndex = i;
                        }
                    }

                    if (bestIndex >= 0)
                    {
                        var entry = buffer[bestIndex];
                        playerInput.ValueRW.Steer = entry.Steer;
                        playerInput.ValueRW.Throttle = entry.Throttle;
                        playerInput.ValueRW.Brake = entry.Brake;
                        playerInput.ValueRW.Handbrake = entry.Handbrake;
                    }
                }
                else
                {
                    // Use latest network input directly
                    playerInput.ValueRW.Steer = networkInput.ValueRO.Steer;
                    playerInput.ValueRW.Throttle = networkInput.ValueRO.Throttle;
                    playerInput.ValueRW.Brake = networkInput.ValueRO.Brake;
                    playerInput.ValueRW.Handbrake = networkInput.ValueRO.Handbrake;
                }
            }

            // =============================================================
            // Apply Network Transform Corrections
            // =============================================================

            foreach (var (transform, netTransform, velocity) in
                SystemAPI.Query<RefRW<WorldTransform>, RefRW<NetworkTransform>, RefRW<Velocity>>()
                    .WithAll<RemotePlayerTag>())
            {
                if (!netTransform.ValueRO.NeedsCorrection) continue;

                ref var net = ref netTransform.ValueRW;
                ref var xform = ref transform.ValueRW;

                // Smooth position correction
                float3 posError = net.NetworkPosition - xform.Position;
                net.PositionError = math.length(posError);

                if (net.PositionError > 0.1f) // Only correct significant errors
                {
                    float correctionT = math.saturate(PositionCorrectionRate * deltaTime);
                    xform.Position = math.lerp(xform.Position, net.NetworkPosition, correctionT);
                }

                // Smooth rotation correction
                float rotError = 1f - math.abs(math.dot(xform.Rotation.value, net.NetworkRotation.value));
                if (rotError > 0.01f)
                {
                    float correctionT = math.saturate(RotationCorrectionRate * deltaTime);
                    xform.Rotation = math.slerp(xform.Rotation, net.NetworkRotation, correctionT);
                }

                // Update velocity for prediction
                velocity.ValueRW.Forward = math.length(net.NetworkVelocity);

                // Clear correction flag when close enough
                if (net.PositionError < 0.05f && rotError < 0.005f)
                {
                    net.NeedsCorrection = false;
                }
            }
        }
    }

    /// <summary>
    /// Captures local player state for network transmission.
    /// Prepares state snapshots for other clients.
    /// </summary>
    [DisableAutoCreation] // Deferred to v0.3.0
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct NetworkStateCaptureSystem : ISystem
    {
        private const float StateSendRate = 1f / 20f; // 20Hz state updates
        private float _timeSinceLastSend;

        public void OnCreate(ref SystemState state)
        {
            _timeSinceLastSend = 0f;
            state.Enabled = false; // Deferred to v0.3.0 — no transport layer
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get network state
            bool isConnected = false;
            uint currentTick = 0;

            foreach (var netState in SystemAPI.Query<RefRO<NetworkState>>())
            {
                isConnected = netState.ValueRO.IsConnected;
                currentTick = netState.ValueRO.CurrentTick;
                break;
            }

            if (!isConnected) return;

            _timeSinceLastSend += deltaTime;
            if (_timeSinceLastSend < StateSendRate) return;
            _timeSinceLastSend = 0f;

            // =============================================================
            // Capture Local Player State
            // =============================================================

            foreach (var (transform, velocity, driftState, netTransform, netPlayer) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>, RefRO<DriftState>,
                               RefRW<NetworkTransform>, RefRO<NetworkPlayer>>()
                    .WithAll<PlayerVehicleTag>())
            {
                if (!netPlayer.ValueRO.IsLocal) continue;

                // Store current state for network transmission
                netTransform.ValueRW.NetworkPosition = transform.ValueRO.Position;
                netTransform.ValueRW.NetworkRotation = transform.ValueRO.Rotation;

                float3 forward = math.mul(transform.ValueRO.Rotation, new float3(0, 0, 1));
                netTransform.ValueRW.NetworkVelocity = forward * velocity.ValueRO.Forward;

                netTransform.ValueRW.NetworkYawOffset = driftState.ValueRO.YawOffset;
                netTransform.ValueRW.StateTick = currentTick;
            }
        }
    }

    /// <summary>
    /// Handles input acknowledgments and rollback for client-side prediction.
    /// </summary>
    [DisableAutoCreation] // Deferred to v0.3.0
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(NetworkStateCaptureSystem))]
    public partial struct NetworkPredictionSystem : ISystem
    {
        // Rollback parameters
        private const float MaxPositionError = 1f;    // Max error before rollback
        private const float MaxRotationError = 0.1f;  // Max rotation error (dot product)

        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false; // Deferred to v0.3.0 — no transport layer
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get confirmed tick from server
            uint confirmedTick = 0;
            foreach (var netState in SystemAPI.Query<RefRO<NetworkState>>())
            {
                confirmedTick = netState.ValueRO.LastConfirmedTick;
                break;
            }

            if (confirmedTick == 0) return;

            // =============================================================
            // Process Input Acknowledgments
            // =============================================================

            foreach (var (predState, entity) in
                SystemAPI.Query<RefRW<InputPredictionState>>()
                    .WithAll<PlayerVehicleTag, NetworkPlayer>()
                    .WithEntityAccess())
            {
                if (!SystemAPI.HasBuffer<NetworkInputEntry>(entity)) continue;

                var buffer = SystemAPI.GetBuffer<NetworkInputEntry>(entity);

                // Mark acknowledged inputs
                int acknowledgedCount = 0;
                for (int i = 0; i < buffer.Length; i++)
                {
                    var entry = buffer[i];
                    if (entry.Tick <= confirmedTick && !entry.Acknowledged)
                    {
                        entry.Acknowledged = true;
                        buffer[i] = entry;
                        acknowledgedCount++;
                    }
                }

                // Update prediction state
                predState.ValueRW.PendingInputCount -= acknowledgedCount;
                predState.ValueRW.PendingInputCount = math.max(0, predState.ValueRO.PendingInputCount);

                // Find oldest pending tick
                uint oldestPending = uint.MaxValue;
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (!buffer[i].Acknowledged && buffer[i].Tick < oldestPending)
                    {
                        oldestPending = buffer[i].Tick;
                    }
                }
                predState.ValueRW.OldestPendingTick = oldestPending;
            }

            // =============================================================
            // Check for Prediction Errors (Rollback Detection)
            // =============================================================

            foreach (var (transform, netTransform, predState) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<NetworkTransform>,
                               RefRW<InputPredictionState>>()
                    .WithAll<PlayerVehicleTag, NetworkPlayer>())
            {
                // Skip if no server state received
                if (netTransform.ValueRO.StateTick == 0) continue;

                // Check position error at confirmed tick
                float posError = math.distance(transform.ValueRO.Position,
                                               netTransform.ValueRO.NetworkPosition);

                // Check rotation error
                float rotError = 1f - math.abs(math.dot(
                    transform.ValueRO.Rotation.value,
                    netTransform.ValueRO.NetworkRotation.value));

                // Trigger rollback if error too large
                if (posError > MaxPositionError || rotError > MaxRotationError)
                {
                    predState.ValueRW.RollbackInProgress = true;
                    predState.ValueRW.RollbackTargetTick = netTransform.ValueRO.StateTick;
                }
            }
        }
    }

    /// <summary>
    /// Initializes network singletons on world creation.
    /// </summary>
    [DisableAutoCreation] // Deferred to v0.3.0 — singleton not consumed by active systems
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct NetworkInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Create network state singleton
            if (!SystemAPI.HasSingleton<NetworkState>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, NetworkState.Default);
                state.EntityManager.SetName(entity, "NetworkState");
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            // One-time init
            state.Enabled = false;
        }
    }

    /// <summary>
    /// Manages network session lifecycle and player connections.
    /// </summary>
    [DisableAutoCreation] // Deferred to v0.3.0
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct NetworkSessionSystem : ISystem
    {
        // Connection parameters
        private const float HeartbeatInterval = 1f;
        private const float ConnectionTimeout = 10f;
        private const float RTTSmoothing = 0.1f;

        private float _heartbeatTimer;

        public void OnCreate(ref SystemState state)
        {
            _heartbeatTimer = 0f;
            state.Enabled = false; // Deferred to v0.3.0 — no transport layer
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var netState in SystemAPI.Query<RefRW<NetworkState>>())
            {
                if (!netState.ValueRO.IsConnected) continue;

                // Update RTT smoothing
                netState.ValueRW.SmoothedRTT = math.lerp(
                    netState.ValueRO.SmoothedRTT,
                    netState.ValueRO.RTT,
                    RTTSmoothing
                );

                // Calculate connection quality
                float rttFactor = math.saturate(1f - netState.ValueRO.SmoothedRTT / 200f);
                float lossFactor = math.saturate(1f - netState.ValueRO.PacketLossPercent / 10f);
                netState.ValueRW.ConnectionQuality = rttFactor * lossFactor;
            }

            // Update player connection states
            _heartbeatTimer += deltaTime;
            if (_heartbeatTimer >= HeartbeatInterval)
            {
                _heartbeatTimer = 0f;

                foreach (var netPlayer in SystemAPI.Query<RefRW<NetworkPlayer>>())
                {
                    // Connection timeout check would go here
                    // (actual network I/O handled by external transport layer)
                }
            }
        }
    }
}
