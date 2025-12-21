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
        // Movement parameters (from spec)
        private const float MinForwardSpeed = 8f;         // m/s - CRITICAL: never go below this
        private const float MaxForwardSpeed = 80f;        // m/s
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

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (velocity, input, driftState, damage, transform, laneFollower) in
                SystemAPI.Query<RefRW<Velocity>, RefRO<PlayerInput>, RefRW<DriftState>,
                               RefRO<DamageState>, RefRW<WorldTransform>, RefRO<LaneFollower>>()
                    .WithNone<CrashedTag, AutopilotActiveTag>())
            {
                ref var vel = ref velocity.ValueRW;
                ref var drift = ref driftState.ValueRW;

                // =============================================================
                // Forward Velocity (Throttle/Brake)
                // =============================================================

                float throttle = input.ValueRO.Throttle;
                float brake = input.ValueRO.Brake;

                // Accelerate
                if (throttle > 0.01f)
                {
                    vel.Forward += throttle * Acceleration * deltaTime;
                }

                // Brake
                if (brake > 0.01f)
                {
                    vel.Forward -= brake * BrakeDeceleration * deltaTime;
                }

                // Natural deceleration (aerodynamic drag)
                vel.Forward -= vel.Forward * DragCoefficient;

                // =============================================================
                // Forward Velocity Constraint (CRITICAL)
                // =============================================================
                // v_f >= v_min - spins never stall the run
                // This is the core design rule that allows 360° spins

                vel.Forward = math.clamp(vel.Forward, MinForwardSpeed, MaxForwardSpeed);

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
                    float recoveryTorque = -RecoveryGain * drift.YawOffset;
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
                    // Decay lateral velocity when not drifting
                    vel.Lateral *= (1f - 3f * deltaTime);
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

                // Smooth rotation blend
                float rotBlend = drift.IsDrifting ? 1f : 8f * deltaTime;
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
                velocity.ValueRW.Forward = math.max(velocity.ValueRO.Forward, MinForwardSpeed);

                // Simple forward movement
                float3 forward = math.mul(transform.ValueRO.Rotation, new float3(0, 0, 1));
                transform.ValueRW.Position += forward * velocity.ValueRO.Forward * deltaTime;

                // Decay lateral velocity
                velocity.ValueRW.Lateral *= 0.95f;
            }
        }
    }
}
