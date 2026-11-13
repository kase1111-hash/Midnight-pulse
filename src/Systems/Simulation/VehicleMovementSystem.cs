// ============================================================================
// Nightflow - Vehicle Movement & Drift/Yaw System
// Execution Order: 5 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;
using Nightflow.Utilities;
using Nightflow.Config;

namespace Nightflow.Systems
{
    /// <summary>
    /// Handles forward momentum, yaw (drift, spin, slide) and the car's pose.
    ///
    /// The autopilot drives through this exact same path: AutopilotSystem writes
    /// PlayerInput, so there is no separate AI movement model. A crashed vehicle
    /// coasts (see the crashed branch) so the car never stops, not even during
    /// the crash sequence.
    ///
    /// Momentum (VehicleDynamics): there is no top speed. While the player
    /// drives a run, the car is carried at a pace that keeps climbing; throttle
    /// boosts above it, the brake trims down to a floor under it, and nothing
    /// slows the car on its own. Critical rule: v_f >= v_min (8 m/s), so spins
    /// never stall the run.
    ///
    /// Yaw (VehicleDynamics.StepYaw): the tyres keep the nose in line with
    /// where the car is going; steering and the handbrake twist it; past the
    /// grip limit reported by LaneMagnetismSystem the rear steps out. The
    /// resulting slip angle is what CrashSystem judges loss of control by.
    ///
    /// Lateral motion is handled by LaneMagnetismSystem (which runs first).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LaneMagnetismSystem))]
    public partial struct VehicleMovementSystem : ISystem
    {
        // Yaw damping while a wreck spins down (1/s)
        private const float CrashYawDamping = 0.8f;

        // A wreck's sideways slide dies out quickly (1/s) instead of stopping dead
        private const float CrashLateralDamping = 4f;

        // How quickly the body follows the track frame + yaw (1/s)
        private const float RotationResponse = 20f;
        private const float CrashRotationResponse = 5f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            int steps = VehicleDynamics.SubSteps(deltaTime, out float h);

            // Get current game mode
            GameMode currentMode = GameMode.Nightflow;
            foreach (var modeState in SystemAPI.Query<RefRO<GameModeState>>())
            {
                currentMode = modeState.ValueRO.CurrentMode;
                break;
            }

            // Mode momentum rules: Redline builds harder, Freeflow lets the
            // driver choose the speed (no climb, no floor); none of them cap it
            float paceGrowth = currentMode == GameMode.Redline ? VehicleDynamics.RedlinePaceScale
                : currentMode == GameMode.Freeflow ? 0f
                : 1f;
            float boostScale = currentMode == GameMode.Redline ? VehicleDynamics.RedlineBoostScale : 1f;
            bool useFloor = currentMode != GameMode.Freeflow;

            foreach (var (velocity, input, driftState, damage, transform, entity) in
                SystemAPI.Query<RefRW<Velocity>, RefRO<PlayerInput>, RefRW<DriftState>,
                               RefRO<DamageState>, RefRW<WorldTransform>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>()
                    .WithEntityAccess())
            {
                ref var vel = ref velocity.ValueRW;
                ref var drift = ref driftState.ValueRW;

                // =============================================================
                // Phase 2 Damage: Component Failure Effects
                // =============================================================
                // Optional components (added by ComponentHealthInitSystem); the car
                // must move from frame one, so default to full health when absent.

                var health = SystemAPI.HasComponent<ComponentHealth>(entity)
                    ? SystemAPI.GetComponent<ComponentHealth>(entity)
                    : ComponentHealth.FullHealth;
                var failures = SystemAPI.HasComponent<ComponentFailureState>(entity)
                    ? SystemAPI.GetComponent<ComponentFailureState>(entity)
                    : default;

                // Engine health affects acceleration
                // At full health (1.0): 100%; at 50% health: 75%; failed: 50% (limp mode)
                // TODO: Engine limp mode should also add audio stutter
                float engineModifier = failures.HasFailed(ComponentFailures.Engine)
                    ? 0.5f
                    : 0.5f + (health.Engine * 0.5f);

                // Transmission health affects how quickly speed changes
                float transmissionModifier = failures.HasFailed(ComponentFailures.Transmission)
                    ? 0.6f
                    : 0.6f + (health.Transmission * 0.4f);

                // Steering health and front damage weaken steering torque
                float steeringHealthFactor = failures.HasFailed(ComponentFailures.Steering)
                    ? 0.15f
                    : 0.3f + (health.Steering * 0.7f);
                float steerResponse = steeringHealthFactor * (1f - damage.ValueRO.Front * 0.4f);

                // Transmission health slows drift recovery
                float recoveryResponse = failures.HasFailed(ComponentFailures.Transmission)
                    ? 0.4f
                    : 0.4f + (health.Transmission * 0.6f);

                // =============================================================
                // Forward momentum (no terminal velocity)
                // =============================================================

                bool autopilotDriving = !SystemAPI.HasComponent<Autopilot>(entity) ||
                                        SystemAPI.GetComponent<Autopilot>(entity).Enabled;

                // The run's pace carries the car while the player drives; the
                // autopilot manages its own speed with the pedals
                float pace = 0f;
                float floor = GameConstants.MinForwardSpeed;
                if (!autopilotDriving &&
                    SystemAPI.HasComponent<ScoreSession>(entity) &&
                    SystemAPI.HasComponent<ScoreSummary>(entity))
                {
                    var session = SystemAPI.GetComponent<ScoreSession>(entity);
                    if (session.Active)
                    {
                        float runTime = SystemAPI.GetComponent<ScoreSummary>(entity).TimeSurvived;
                        pace = VehicleDynamics.PaceSpeed(runTime, session.Distance, paceGrowth);
                        if (useFloor)
                            floor = VehicleDynamics.MomentumFloor(pace);
                    }
                }

                vel.Forward = VehicleDynamics.StepForwardSpeed(
                    vel.Forward, pace, floor,
                    input.ValueRO.Throttle, input.ValueRO.Brake,
                    engineModifier * transmissionModifier, boostScale, deltaTime);

                // =============================================================
                // Yaw: alignment, steering, drift, slide (sub-stepped)
                // =============================================================

                var traction = SystemAPI.HasComponent<TractionState>(entity)
                    ? SystemAPI.GetComponent<TractionState>(entity)
                    : default;

                var yawInput = new YawInputs
                {
                    ForwardSpeed = vel.Forward,
                    LateralSpeed = vel.Lateral,
                    Steer = input.ValueRO.Steer,
                    Handbrake = input.ValueRO.Handbrake,
                    Usage = traction.Usage,
                    LoadDirection = traction.LoadDirection,
                    SteerResponse = steerResponse,
                    RecoveryResponse = recoveryResponse
                };

                for (int i = 0; i < steps; i++)
                {
                    VehicleDynamics.StepYaw(ref drift, yawInput, h);
                }

                // Store angular velocity for other systems
                vel.Angular = drift.YawRate;

                // =============================================================
                // Advance along the track
                // =============================================================

                FindTrackFrame(ref state, transform.ValueRO.Position.z,
                    out float3 forward, out float3 right, out float3 up);

                // Forward movement along spline direction
                // (lateral movement handled by LaneMagnetismSystem)
                transform.ValueRW.Position += forward * vel.Forward * deltaTime;

                // Body = track frame turned by the yaw offset
                quaternion trackRot = quaternion.LookRotation(forward, up);
                quaternion targetRot = math.mul(trackRot, quaternion.RotateY(drift.YawOffset));
                transform.ValueRW.Rotation = math.slerp(
                    transform.ValueRO.Rotation,
                    targetRot,
                    VehicleDynamics.Damp(RotationResponse, deltaTime));

                SyncWorldVelocity(ref state, entity, forward, right, vel.Forward, vel.Lateral, drift.YawRate);
            }

            // =============================================================
            // Crashed Vehicle: coast through the crash sequence
            // =============================================================
            // The wreck keeps rolling along the track (never below v_min) with
            // its spin damping out, so the world never freezes on screen.
            // Player input is ignored until CrashSystem resets the vehicle.

            foreach (var (velocity, driftState, transform, entity) in
                SystemAPI.Query<RefRW<Velocity>, RefRW<DriftState>, RefRW<WorldTransform>>()
                    .WithAll<PlayerVehicleTag, CrashedTag>()
                    .WithEntityAccess())
            {
                ref var vel = ref velocity.ValueRW;
                ref var drift = ref driftState.ValueRW;

                vel.Forward = GameFlowLogic.CoastingSpeed(vel.Forward, deltaTime);
                vel.Lateral *= math.exp(-CrashLateralDamping * deltaTime);

                // Spin bleeds off; the yaw offset settles wherever the wreck ends up
                drift.YawRate *= math.exp(-CrashYawDamping * deltaTime);
                drift.YawRate = math.clamp(drift.YawRate, -VehicleDynamics.MaxYawRate, VehicleDynamics.MaxYawRate);
                drift.YawOffset = VehicleDynamics.WrapAngle(drift.YawOffset + drift.YawRate * deltaTime);
                drift.IsDrifting = false;
                vel.Angular = drift.YawRate;

                FindTrackFrame(ref state, transform.ValueRO.Position.z,
                    out float3 forward, out float3 right, out float3 up);

                transform.ValueRW.Position += (forward * vel.Forward + right * vel.Lateral) * deltaTime;

                quaternion trackRot = quaternion.LookRotation(forward, up);
                quaternion targetRot = math.mul(trackRot, quaternion.RotateY(drift.YawOffset));
                transform.ValueRW.Rotation = math.slerp(
                    transform.ValueRO.Rotation, targetRot,
                    VehicleDynamics.Damp(CrashRotationResponse, deltaTime));

                SyncWorldVelocity(ref state, entity, forward, right, vel.Forward, vel.Lateral, drift.YawRate);
            }
        }

        /// <summary>
        /// Mirrors the lane-relative velocity into the world-space VehicleVelocity
        /// that the camera, audio (engine/wind/music) and speed-line effects read.
        /// </summary>
        private void SyncWorldVelocity(ref SystemState state, Entity entity,
            float3 forward, float3 right, float forwardSpeed, float lateralSpeed, float yawRate)
        {
            if (!SystemAPI.HasComponent<VehicleVelocity>(entity))
                return;

            SystemAPI.SetComponent(entity, new VehicleVelocity
            {
                Linear = forward * forwardSpeed + right * lateralSpeed,
                Angular = new float3(0f, yawRate, 0f)
            });
        }

        /// <summary>
        /// Frenet frame of the track spline at the given Z. Falls back to the
        /// world axes when no segment covers that Z (segment gap while the
        /// generator catches up), so the car always has a forward direction.
        /// </summary>
        private void FindTrackFrame(ref SystemState state, float playerZ,
            out float3 forward, out float3 right, out float3 up)
        {
            forward = new float3(0, 0, 1);
            right = new float3(1, 0, 0);
            up = new float3(0, 1, 0);

            foreach (var (segment, spline) in
                SystemAPI.Query<RefRO<TrackSegment>, RefRO<HermiteSpline>>()
                    .WithAll<TrackSegmentTag>())
            {
                if (playerZ >= segment.ValueRO.StartZ && playerZ <= segment.ValueRO.EndZ)
                {
                    float length = segment.ValueRO.EndZ - segment.ValueRO.StartZ;
                    if (math.abs(length) < 0.001f)
                        break;

                    float t = math.saturate((playerZ - segment.ValueRO.StartZ) / length);

                    float3 tangent = SplineUtilities.EvaluateTangent(
                        spline.ValueRO.P0, spline.ValueRO.T0,
                        spline.ValueRO.P1, spline.ValueRO.T1, t);

                    SplineUtilities.BuildFrenetFrame(tangent, out forward, out right, out up);
                    break;
                }
            }
        }
    }
}
