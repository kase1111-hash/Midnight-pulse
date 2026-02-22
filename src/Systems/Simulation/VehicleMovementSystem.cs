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
    /// Handles vehicle forward movement, drift mechanics, and yaw dynamics.
    ///
    /// Critical Rule: v_f >= v_min (8 m/s)
    /// This is why spins never stall the run - forward velocity is always maintained.
    ///
    /// Yaw Dynamics (Explicit Torque Model):
    /// ψ̈ = τ_steer + τ_drift - c_ψ·ψ̇
    ///
    /// Where:
    /// - τ_steer = k_s × s × (v_f/v_ref)
    /// - τ_drift = k_d × sign(s) × √v_f (if handbrake)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LaneMagnetismSystem))]
    public partial struct VehicleMovementSystem : ISystem
    {
        // Movement parameters - centralized in GameConstants
        // MinForwardSpeed (8 m/s) - CRITICAL: never go below this - uses GameConstants.MinForwardSpeed
        // MaxForwardSpeed (80 m/s) - uses GameConstants.MaxForwardSpeed
        private const float Acceleration = 15f;           // m/s²
        private const float BrakeDeceleration = 25f;      // m/s²
        private const float DragCoefficient = 0.01f;      // Natural deceleration
        private const float ReferenceSpeed = 40f;         // m/s

        // Yaw/Drift parameters (from spec)
        private const float SteeringGain = 1.2f;          // k_s
        private const float DriftGain = 2.5f;             // k_d
        private const float YawDamping = 0.8f;            // c_ψ
        private const float MaxYawRate = 6f;              // rad/s
        private const float SlipGain = 1.1f;              // k_slip
        private const float RecoveryGain = 2f;            // k_r

        // Drift state thresholds
        private const float DriftExitYaw = 0.1f;          // radians
        private const float DriftExitRate = 0.5f;         // rad/s

        // Redline mode parameters
        private const float RedlineReferenceSpeed = 50f;      // Speed at which acceleration is halved
        private const float RedlineBaseAcceleration = 20f;    // Higher base accel for Redline
        private const float RedlineDragCoefficient = 0.005f;  // Lower drag for higher speeds

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get current game mode
            GameMode currentMode = GameMode.Nightflow;
            foreach (var modeState in SystemAPI.Query<RefRO<GameModeState>>())
            {
                currentMode = modeState.ValueRO.CurrentMode;
                break;
            }

            bool isRedlineMode = currentMode == GameMode.Redline;

            foreach (var (velocity, input, driftState, damage, transform, laneFollower, componentHealth, failureState) in
                SystemAPI.Query<RefRW<Velocity>, RefRO<PlayerInput>, RefRW<DriftState>,
                               RefRO<DamageState>, RefRW<WorldTransform>, RefRO<LaneFollower>,
                               RefRO<ComponentHealth>, RefRO<ComponentFailureState>>()
                    .WithNone<CrashedTag, AutopilotActiveTag>())
            {
                ref var vel = ref velocity.ValueRW;
                ref var drift = ref driftState.ValueRW;

                // =============================================================
                // Phase 2 Damage: Component Failure Effects
                // =============================================================

                var health = componentHealth.ValueRO;
                var failures = failureState.ValueRO;

                // Engine health affects acceleration
                // At full health (1.0): 100% acceleration
                // At 50% health: 75% acceleration
                // At engine failure: 50% acceleration (limp mode)
                // TODO: Engine limp mode should also cap max RPM and add audio stutter
                float engineModifier = failures.HasFailed(ComponentFailures.Engine)
                    ? 0.5f  // Engine failed: limp mode
                    : 0.5f + (health.Engine * 0.5f);  // Gradual degradation

                // Transmission health affects speed changes and max speed
                // At failure: slower acceleration response
                float transmissionModifier = failures.HasFailed(ComponentFailures.Transmission)
                    ? 0.6f
                    : 0.6f + (health.Transmission * 0.4f);

                // =============================================================
                // Forward Velocity (Throttle/Brake)
                // =============================================================

                float throttle = input.ValueRO.Throttle;
                float brake = input.ValueRO.Brake;

                // Accelerate - with mode-specific behavior and component effects
                if (throttle > 0.01f)
                {
                    // Apply engine and transmission damage modifiers
                    float componentModifier = engineModifier * transmissionModifier;

                    if (isRedlineMode)
                    {
                        // REDLINE MODE: No top speed, but acceleration decreases asymptotically
                        // Formula: effectiveAccel = baseAccel / (1 + speed/referenceSpeed)
                        // At 0 m/s: full acceleration
                        // At 50 m/s: half acceleration
                        // At 100 m/s: 1/3 acceleration
                        // At 150 m/s: 1/4 acceleration ... and so on
                        float accelModifier = 1f / (1f + vel.Forward / RedlineReferenceSpeed);
                        float effectiveAccel = RedlineBaseAcceleration * accelModifier * componentModifier;
                        vel.Forward += throttle * effectiveAccel * deltaTime;
                    }
                    else
                    {
                        // Normal mode acceleration with component damage
                        vel.Forward += throttle * Acceleration * componentModifier * deltaTime;
                    }
                }

                // Brake
                if (brake > 0.01f)
                {
                    vel.Forward -= brake * BrakeDeceleration * deltaTime;
                }

                // Natural deceleration (aerodynamic drag) - reduced in Redline mode
                // Use exponential decay for framerate independence
                float drag = isRedlineMode ? RedlineDragCoefficient : DragCoefficient;
                vel.Forward *= math.exp(-drag * deltaTime);

                // =============================================================
                // Forward Velocity Constraint (CRITICAL)
                // =============================================================
                // v_f >= v_min - spins never stall the run
                // This is the core design rule that allows 360° spins
                //
                // In Redline mode: No upper limit! Push your speed to the limit.
                // In other modes: Capped at MaxForwardSpeed

                float maxSpeed = isRedlineMode ? float.MaxValue : GameConstants.MaxForwardSpeed;
                vel.Forward = math.clamp(vel.Forward, GameConstants.MinForwardSpeed, maxSpeed);

                // =============================================================
                // Yaw Dynamics: ψ̈ = τ_steer + τ_drift - c_ψ·ψ̇
                // =============================================================

                float steer = input.ValueRO.Steer;
                bool handbrake = input.ValueRO.Handbrake;

                // Steering torque: τ_steer = k_s × s × (v_f / v_ref)
                float speedRatio = vel.Forward / ReferenceSpeed;
                float steerTorque = SteeringGain * steer * speedRatio;

                // Apply rear damage penalty to slip gain
                float rearDamage = damage.ValueRO.Rear;
                float effectiveSlipGain = SlipGain * (1f + 0.6f * rearDamage);

                // Phase 2: Tire and suspension affect slip behavior
                // Low tire health = more slipping
                // Suspension failure = unpredictable handling
                float tireModifier = failures.HasFailed(ComponentFailures.Tires)
                    ? 1.8f  // Blown tires: very slippery
                    : 1f + (1f - health.Tires) * 0.5f;  // Gradual degradation

                float suspensionModifier = failures.HasFailed(ComponentFailures.Suspension)
                    ? 1.5f  // Broken suspension: unstable
                    : 1f + (1f - health.Suspension) * 0.3f;

                effectiveSlipGain *= tireModifier * suspensionModifier;

                // Drift torque: τ_drift = k_d × sign(s) × √v_f (if handbrake)
                float driftTorque = 0f;

                if (handbrake && math.abs(steer) > 0.1f)
                {
                    driftTorque = DriftGain * math.sign(steer) * math.sqrt(vel.Forward);
                    drift.IsDrifting = true;
                }
                else if (drift.IsDrifting)
                {
                    // Recovery torque: τ_recover = -k_r × ψ (when handbrake released)
                    // Phase 2: Transmission affects recovery speed
                    float recoveryModifier = failures.HasFailed(ComponentFailures.Transmission)
                        ? 0.4f  // Failed transmission: very slow recovery
                        : 0.4f + (health.Transmission * 0.6f);  // Gradual degradation

                    float recoveryTorque = -RecoveryGain * drift.YawOffset * recoveryModifier;
                    steerTorque += recoveryTorque;

                    // Exit drift state when yaw is small and stable
                    if (math.abs(drift.YawOffset) < DriftExitYaw &&
                        math.abs(drift.YawRate) < DriftExitRate)
                    {
                        drift.IsDrifting = false;
                        drift.YawOffset = 0f;
                    }
                }

                // Total yaw acceleration
                float yawAccel = steerTorque + driftTorque - YawDamping * drift.YawRate;

                // Integrate yaw rate
                drift.YawRate += yawAccel * deltaTime;
                drift.YawRate = math.clamp(drift.YawRate, -MaxYawRate, MaxYawRate);

                // Integrate yaw offset (allows full 360° spins)
                drift.YawOffset += drift.YawRate * deltaTime;

                // =============================================================
                // Drift Slip Angle
                // =============================================================

                if (handbrake || drift.IsDrifting)
                {
                    // β = ψ - arctan(v_l / v_f)
                    float lateralVel = vel.Lateral;
                    float slipAngle = drift.YawOffset - math.atan2(lateralVel, vel.Forward);
                    drift.SlipAngle = slipAngle;

                    // v_l += k_slip × sin(β) × v_f × Δt
                    vel.Lateral += effectiveSlipGain * math.sin(slipAngle) * vel.Forward * deltaTime;

                    // Clamp lateral velocity
                    vel.Lateral = math.clamp(vel.Lateral, -15f, 15f);
                }
                else
                {
                    // Decay lateral velocity when not drifting (framerate-independent)
                    vel.Lateral *= math.exp(-3f * deltaTime);
                    drift.SlipAngle = 0f;
                }

                // Store angular velocity for other systems
                vel.Angular = drift.YawRate;

                // =============================================================
                // Find Current Spline Frame
                // =============================================================

                float playerZ = transform.ValueRO.Position.z;
                float3 forward = new float3(0, 0, 1);
                float3 right = new float3(1, 0, 0);
                float3 up = new float3(0, 1, 0);

                foreach (var (segment, spline) in
                    SystemAPI.Query<RefRO<TrackSegment>, RefRO<HermiteSpline>>()
                        .WithAll<TrackSegmentTag>())
                {
                    if (playerZ >= segment.ValueRO.StartZ && playerZ <= segment.ValueRO.EndZ)
                    {
                        float t = (playerZ - segment.ValueRO.StartZ) /
                                  (segment.ValueRO.EndZ - segment.ValueRO.StartZ);
                        t = math.saturate(t);

                        float3 tangent = SplineUtilities.EvaluateTangent(
                            spline.ValueRO.P0, spline.ValueRO.T0,
                            spline.ValueRO.P1, spline.ValueRO.T1, t);

                        SplineUtilities.BuildFrenetFrame(tangent, out forward, out right, out up);
                        break;
                    }
                }

                // =============================================================
                // Update World Transform
                // =============================================================

                // Forward movement along spline direction
                transform.ValueRW.Position += forward * vel.Forward * deltaTime;

                // Lateral movement handled by LaneMagnetismSystem

                // =============================================================
                // Update Rotation
                // =============================================================

                // Base rotation aligned with track
                quaternion trackRot = quaternion.LookRotation(forward, up);

                // Apply yaw offset from drift
                quaternion yawRot = quaternion.RotateY(drift.YawOffset);
                quaternion targetRot = math.mul(trackRot, yawRot);

                // Smooth rotation blend (framerate-independent)
                float rotBlend = drift.IsDrifting ? 5f * deltaTime : 8f * deltaTime;
                transform.ValueRW.Rotation = math.slerp(
                    transform.ValueRO.Rotation,
                    targetRot,
                    math.saturate(rotBlend)
                );
            }

            // =============================================================
            // Handle Autopilot Vehicles
            // =============================================================

            foreach (var (velocity, autopilot, transform) in
                SystemAPI.Query<RefRW<Velocity>, RefRO<Autopilot>, RefRW<WorldTransform>>()
                    .WithAll<AutopilotActiveTag>())
            {
                if (!autopilot.ValueRO.Enabled)
                    continue;

                // Autopilot maintains steady speed
                float targetSpeed = autopilot.ValueRO.TargetSpeed;

                if (velocity.ValueRO.Forward < targetSpeed)
                {
                    velocity.ValueRW.Forward += 10f * deltaTime;
                }
                else if (velocity.ValueRO.Forward > targetSpeed + 5f)
                {
                    velocity.ValueRW.Forward -= 5f * deltaTime;
                }

                // Ensure minimum speed
                velocity.ValueRW.Forward = math.max(velocity.ValueRO.Forward, GameConstants.MinForwardSpeed);

                // Simple forward movement
                float3 forward = math.mul(transform.ValueRO.Rotation, new float3(0, 0, 1));
                transform.ValueRW.Position += forward * velocity.ValueRO.Forward * deltaTime;

                // Decay lateral velocity (framerate-independent)
                velocity.ValueRW.Lateral *= math.exp(-3f * deltaTime);
            }
        }
    }
}
