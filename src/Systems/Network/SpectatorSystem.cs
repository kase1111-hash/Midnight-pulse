// ============================================================================
// Nightflow - Spectator System
// Live spectator mode with multiple camera options
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Config;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages spectator mode camera and target switching.
    ///
    /// From spec:
    /// - Live spectator mode
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(CameraSystem))]
    public partial struct SpectatorSystem : ISystem
    {
        // Camera parameters
        private const float FreeCamBaseSpeed = 20f;
        private const float FreeCamSprintMultiplier = 3f;
        private const float CinematicTransitionSpeed = 2f;
        private const float OverheadHeight = 50f;
        private const float TracksideDistance = 15f;
        private const float ChaseDistance = 12f;
        private const float ChaseHeight = 4f;

        // Auto-switch parameters
        private const float ActionThreshold = 30f;       // Speed for "action" detection
        private const float CrashSwitchDelay = 0.5f;     // Delay after crash to switch

        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false; // Deferred to v0.3.0 — requires multiplayer infrastructure
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // =============================================================
            // Update Spectator State
            // =============================================================

            foreach (var spectator in SystemAPI.Query<RefRW<SpectatorState>>())
            {
                if (!spectator.ValueRO.IsSpectating) continue;

                ref var spec = ref spectator.ValueRW;
                spec.TimeSinceSwitch += deltaTime;

                // Auto-switch logic
                if (spec.FollowAction && spec.AutoSwitchDelay > 0f &&
                    spec.TimeSinceSwitch > spec.AutoSwitchDelay)
                {
                    // Find most interesting target
                    Entity bestTarget = Entity.Null;
                    float bestScore = 0f;

                    foreach (var (velocity, transform, netPlayer, entity) in
                        SystemAPI.Query<RefRO<Velocity>, RefRO<WorldTransform>, RefRO<NetworkPlayer>>()
                            .WithEntityAccess())
                    {
                        float score = 0f;

                        // Speed contributes to "interesting"
                        float speed = velocity.ValueRO.Forward;
                        score += speed * 0.5f;

                        // Drifting is interesting
                        if (SystemAPI.HasComponent<DriftState>(entity))
                        {
                            var drift = SystemAPI.GetComponent<DriftState>(entity);
                            if (drift.IsDrifting)
                            {
                                score += 50f;
                            }
                        }

                        // Near crashes are very interesting
                        if (SystemAPI.HasComponent<DamageState>(entity))
                        {
                            var damage = SystemAPI.GetComponent<DamageState>(entity);
                            if (damage.Total > 50f)
                            {
                                score += damage.Total;
                            }
                        }

                        // Just crashed is most interesting
                        if (SystemAPI.HasComponent<CrashState>(entity))
                        {
                            var crash = SystemAPI.GetComponent<CrashState>(entity);
                            if (crash.IsCrashed && crash.CrashTime < 3f)
                            {
                                score += 200f;
                            }
                        }

                        // Don't switch to same target
                        if (entity == spec.TargetEntity)
                        {
                            score *= 0.5f;
                        }

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestTarget = entity;
                        }
                    }

                    // Switch if found better target
                    if (bestTarget != Entity.Null && bestTarget != spec.TargetEntity &&
                        bestScore > ActionThreshold)
                    {
                        spec.TargetEntity = bestTarget;
                        spec.TimeSinceSwitch = 0f;
                    }
                }
            }

            // =============================================================
            // Update Spectator Camera Based on Mode
            // =============================================================

            foreach (var (camera, spectator) in
                SystemAPI.Query<RefRW<CameraState>, RefRW<SpectatorState>>()
                    .WithAll<SpectatorCameraTag>())
            {
                if (!spectator.ValueRO.IsSpectating) continue;

                ref var spec = ref spectator.ValueRW;
                ref var cam = ref camera.ValueRW;

                // Get target transform
                float3 targetPos = float3.zero;
                quaternion targetRot = quaternion.identity;
                float targetSpeed = 0f;
                bool hasTarget = false;

                if (spec.TargetEntity != Entity.Null &&
                    SystemAPI.Exists(spec.TargetEntity) &&
                    SystemAPI.HasComponent<WorldTransform>(spec.TargetEntity))
                {
                    var xform = SystemAPI.GetComponent<WorldTransform>(spec.TargetEntity);
                    targetPos = xform.Position;
                    targetRot = xform.Rotation;
                    hasTarget = true;

                    if (SystemAPI.HasComponent<Velocity>(spec.TargetEntity))
                    {
                        var vel = SystemAPI.GetComponent<Velocity>(spec.TargetEntity);
                        targetSpeed = vel.Forward;
                    }
                }

                // Calculate camera position based on mode
                float3 desiredPos = cam.Position;
                quaternion desiredRot = cam.Rotation;
                float desiredFOV = 55f;

                switch (spec.CameraMode)
                {
                    case SpectatorCameraMode.FollowTarget:
                        if (hasTarget)
                        {
                            float3 forward = math.mul(targetRot, new float3(0, 0, 1));
                            desiredPos = targetPos - forward * 10f + new float3(0, 4f, 0);
                            desiredRot = quaternion.LookRotation(
                                math.normalize(targetPos - desiredPos),
                                new float3(0, 1, 0));
                            desiredFOV = math.lerp(55f, 80f, targetSpeed / 70f);
                        }
                        break;

                    case SpectatorCameraMode.Cinematic:
                        if (hasTarget)
                        {
                            // Slow, sweeping camera movement
                            float time = (float)SystemAPI.Time.ElapsedTime;
                            float angle = time * 0.2f;
                            float distance = 15f + math.sin(time * 0.1f) * 5f;
                            float height = 5f + math.sin(time * 0.15f) * 3f;

                            desiredPos = targetPos + new float3(
                                math.cos(angle) * distance,
                                height,
                                math.sin(angle) * distance
                            );
                            desiredRot = quaternion.LookRotation(
                                math.normalize(targetPos - desiredPos),
                                new float3(0, 1, 0));
                            desiredFOV = 45f; // Tighter for cinematic
                        }
                        break;

                    case SpectatorCameraMode.Overhead:
                        if (hasTarget)
                        {
                            desiredPos = targetPos + new float3(0, OverheadHeight, 0);
                            desiredRot = quaternion.LookRotation(
                                new float3(0, -1, 0.001f), // Look down
                                new float3(0, 0, 1));       // Forward as up
                            desiredFOV = 60f;
                        }
                        break;

                    case SpectatorCameraMode.Trackside:
                        if (hasTarget)
                        {
                            // Fixed position, rotate to track target
                            float3 toTarget = targetPos - cam.Position;
                            float dist = math.length(toTarget);

                            // Move to new trackside position if target too far
                            if (dist > 50f)
                            {
                                float3 forward = math.mul(targetRot, new float3(0, 0, 1));
                                float3 right = math.mul(targetRot, new float3(1, 0, 0));
                                desiredPos = targetPos + right * TracksideDistance +
                                             new float3(0, 2f, 0) - forward * 5f;
                            }
                            else
                            {
                                desiredPos = cam.Position; // Stay in place
                            }

                            desiredRot = quaternion.LookRotation(
                                math.normalize(targetPos - desiredPos),
                                new float3(0, 1, 0));
                            desiredFOV = 35f; // Telephoto
                        }
                        break;

                    case SpectatorCameraMode.FreeCam:
                        // Free camera controlled by spectator input
                        desiredPos = spec.FreeCamPosition;
                        desiredRot = spec.FreeCamRotation;
                        desiredFOV = 70f;
                        break;

                    case SpectatorCameraMode.FirstPerson:
                        if (hasTarget)
                        {
                            // Cockpit view
                            float3 forward = math.mul(targetRot, new float3(0, 0, 1));
                            desiredPos = targetPos + forward * 1f + new float3(0, 1f, 0);
                            desiredRot = targetRot;
                            desiredFOV = 90f; // Wide for cockpit
                        }
                        break;

                    case SpectatorCameraMode.Chase:
                        if (hasTarget)
                        {
                            float3 forward = math.mul(targetRot, new float3(0, 0, 1));
                            desiredPos = targetPos - forward * ChaseDistance +
                                         new float3(0, ChaseHeight, 0);
                            desiredRot = quaternion.LookRotation(
                                math.normalize(targetPos - desiredPos),
                                new float3(0, 1, 0));
                            desiredFOV = math.lerp(55f, 90f, targetSpeed / 70f);
                        }
                        break;
                }

                // Smooth camera movement
                float posSmooth = spec.CameraMode == SpectatorCameraMode.FreeCam ? 1f : 0.1f;
                float rotSmooth = spec.CameraMode == SpectatorCameraMode.FreeCam ? 1f : 0.15f;

                cam.Position = math.lerp(cam.Position, desiredPos, posSmooth);
                cam.Rotation = math.slerp(cam.Rotation, desiredRot, rotSmooth);
                cam.FOV = math.lerp(cam.FOV, desiredFOV, 0.1f);
            }
        }
    }

    /// <summary>
    /// Handles spectator input for camera control and target switching.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InputSystem))]
    public partial struct SpectatorInputSystem : ISystem
    {
        // Input parameters
        private const float FreeCamMoveSpeed = 30f;
        private const float FreeCamLookSpeed = 2f;
        private const float TargetSwitchCooldown = 0.5f;

        private float _switchCooldown;

        public void OnCreate(ref SystemState state)
        {
            _switchCooldown = 0f;
            state.Enabled = false; // Deferred to v0.3.0 — requires multiplayer infrastructure
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            _switchCooldown = math.max(0f, _switchCooldown - deltaTime);

            foreach (var (spectator, input) in
                SystemAPI.Query<RefRW<SpectatorState>, RefRO<PlayerInput>>()
                    .WithAll<SpectatorCameraTag>())
            {
                if (!spectator.ValueRO.IsSpectating) continue;

                ref var spec = ref spectator.ValueRW;

                // Free cam movement
                if (spec.CameraMode == SpectatorCameraMode.FreeCam)
                {
                    // Use steer for yaw, calculate pitch from input
                    float yaw = input.ValueRO.Steer * FreeCamLookSpeed * deltaTime;
                    quaternion yawRot = quaternion.RotateY(yaw);
                    spec.FreeCamRotation = math.mul(yawRot, spec.FreeCamRotation);

                    // Forward/back from throttle/brake
                    float3 forward = math.mul(spec.FreeCamRotation, new float3(0, 0, 1));
                    float3 right = math.mul(spec.FreeCamRotation, new float3(1, 0, 0));

                    float moveForward = input.ValueRO.Throttle - input.ValueRO.Brake;
                    spec.FreeCamPosition += forward * moveForward * spec.FreeCamSpeed * deltaTime;

                    // Lateral movement from steer (when not looking)
                    if (math.abs(moveForward) < 0.1f)
                    {
                        spec.FreeCamPosition += right * input.ValueRO.Steer *
                                                spec.FreeCamSpeed * deltaTime;
                    }

                    // Up/down from handbrake + throttle/brake combo
                    if (input.ValueRO.Handbrake)
                    {
                        spec.FreeCamPosition.y += (input.ValueRO.Throttle - input.ValueRO.Brake) *
                                                   spec.FreeCamSpeed * deltaTime;
                    }
                }

                // Target switching with handbrake
                if (input.ValueRO.Handbrake && _switchCooldown <= 0f)
                {
                    _switchCooldown = TargetSwitchCooldown;

                    // Cycle to next player
                    Entity nextTarget = Entity.Null;
                    bool foundCurrent = false;

                    foreach (var (netPlayer, entity) in
                        SystemAPI.Query<RefRO<NetworkPlayer>>()
                            .WithEntityAccess())
                    {
                        if (foundCurrent)
                        {
                            nextTarget = entity;
                            break;
                        }
                        if (entity == spec.TargetEntity)
                        {
                            foundCurrent = true;
                        }
                    }

                    // Wrap to first player if at end
                    if (nextTarget == Entity.Null)
                    {
                        foreach (var (netPlayer, entity) in
                            SystemAPI.Query<RefRO<NetworkPlayer>>()
                                .WithEntityAccess())
                        {
                            nextTarget = entity;
                            break;
                        }
                    }

                    if (nextTarget != Entity.Null)
                    {
                        spec.TargetEntity = nextTarget;
                        spec.TimeSinceSwitch = 0f;
                    }
                }

                // Camera mode switching would be handled by UI/menu input
            }
        }
    }

    /// <summary>
    /// Initializes spectator camera entity and state.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SpectatorInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Create spectator camera entity
            var spectatorEntity = Entity.Null;

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<SpectatorState>>()
                    .WithAll<SpectatorCameraTag>()
                    .WithEntityAccess())
            {
                spectatorEntity = entity;
                break;
            }

            if (spectatorEntity == Entity.Null)
            {
                spectatorEntity = state.EntityManager.CreateEntity();

                state.EntityManager.AddComponentData(spectatorEntity, new SpectatorState
                {
                    IsSpectating = false,
                    TargetEntity = Entity.Null,
                    TargetPlayerId = -1,
                    CameraMode = SpectatorCameraMode.FollowTarget,
                    FreeCamPosition = new float3(0, 10f, 0),
                    FreeCamRotation = quaternion.identity,
                    FreeCamSpeed = 30f,
                    AutoSwitchDelay = 10f,
                    TimeSinceSwitch = 0f,
                    FollowAction = true
                });

                state.EntityManager.AddComponentData(spectatorEntity, new CameraState
                {
                    Mode = CameraMode.Follow,
                    Position = new float3(0, 10f, -20f),
                    Rotation = quaternion.identity,
                    FOV = 55f,
                    FollowDistance = 10f,
                    FollowHeight = 4f,
                    LateralOffset = 0f,
                    Roll = 0f
                });

                // Add spectator tag and empty input
                state.EntityManager.AddComponent<SpectatorCameraTag>(spectatorEntity);
                state.EntityManager.AddComponentData(spectatorEntity, new PlayerInput());

                state.EntityManager.SetName(spectatorEntity, "SpectatorCamera");
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            // One-time init
            state.Enabled = false;
        }
    }
}
