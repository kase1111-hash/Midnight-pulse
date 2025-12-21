// ============================================================================
// Nightflow - Vehicle Movement & Drift/Yaw System
// Execution Order: 5 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Handles vehicle forward movement, drift mechanics, and yaw dynamics.
    /// Enforces forward velocity constraint: v_f >= v_min
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LaneMagnetismSystem))]
    public partial struct VehicleMovementSystem : ISystem
    {
        // Movement parameters (from spec)
        private const float MinForwardSpeed = 8f;       // m/s - forward constraint
        private const float MaxForwardSpeed = 80f;      // m/s
        private const float Acceleration = 15f;         // m/s²
        private const float BrakeDeceleration = 25f;    // m/s²
        private const float ReferenceSpeed = 40f;       // m/s

        // Drift/Yaw parameters
        private const float SteeringGain = 1.2f;        // k_s
        private const float DriftGain = 2.5f;           // k_d
        private const float YawDamping = 0.8f;          // c_ψ
        private const float MaxYawRate = 6f;            // rad/s
        private const float SlipGain = 1.1f;            // k_slip
        private const float RecoveryGain = 2f;          // k_r

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (velocity, input, driftState, transform, steeringState) in
                SystemAPI.Query<RefRW<Velocity>, RefRO<PlayerInput>,
                               RefRW<DriftState>, RefRW<WorldTransform>, RefRO<SteeringState>>())
            {
                ref var vel = ref velocity.ValueRW;
                ref var drift = ref driftState.ValueRW;

                // =============================================================
                // Forward Velocity (Throttle/Brake)
                // =============================================================

                float throttle = input.ValueRO.Throttle;
                float brake = input.ValueRO.Brake;

                // Accelerate
                if (throttle > 0)
                {
                    vel.Forward += throttle * Acceleration * deltaTime;
                }

                // Brake
                if (brake > 0)
                {
                    vel.Forward -= brake * BrakeDeceleration * deltaTime;
                }

                // Natural deceleration (drag)
                vel.Forward -= vel.Forward * 0.01f * deltaTime;

                // Clamp forward speed
                vel.Forward = math.clamp(vel.Forward, MinForwardSpeed, MaxForwardSpeed);

                // =============================================================
                // Yaw Dynamics: ψ̈ = τ_steer + τ_drift - c_ψ·ψ̇
                // =============================================================

                float steer = input.ValueRO.Steer;
                bool handbrake = input.ValueRO.Handbrake;

                // Steering torque: τ_steer = k_s × s × (v_f / v_ref)
                float steerTorque = SteeringGain * steer * (vel.Forward / ReferenceSpeed);

                // Drift torque: τ_drift = k_d × sign(s) × √v_f (if handbrake)
                float driftTorque = 0f;
                if (handbrake)
                {
                    driftTorque = DriftGain * math.sign(steer) * math.sqrt(vel.Forward);
                    drift.IsDrifting = true;
                }
                else if (drift.IsDrifting)
                {
                    // Recovery torque: τ_recover = -k_r × ψ
                    float recoveryTorque = -RecoveryGain * drift.YawOffset;
                    steerTorque += recoveryTorque;

                    // Exit drift state when yaw is small
                    if (math.abs(drift.YawOffset) < 0.1f && math.abs(drift.YawRate) < 0.5f)
                    {
                        drift.IsDrifting = false;
                    }
                }

                // Yaw acceleration
                float yawAccel = steerTorque + driftTorque - YawDamping * drift.YawRate;

                // Integrate yaw rate
                drift.YawRate += yawAccel * deltaTime;
                drift.YawRate = math.clamp(drift.YawRate, -MaxYawRate, MaxYawRate);

                // Integrate yaw offset (allow full spins)
                drift.YawOffset += drift.YawRate * deltaTime;

                // =============================================================
                // Drift Slip Angle
                // =============================================================

                if (handbrake)
                {
                    // β = ψ - arctan(v_l / v_f)
                    drift.SlipAngle = drift.YawOffset - math.atan2(vel.Lateral, vel.Forward);

                    // v_l += k_slip × sin(β) × v_f × Δt
                    vel.Lateral += SlipGain * math.sin(drift.SlipAngle) * vel.Forward * deltaTime;
                }

                // =============================================================
                // Forward Velocity Constraint (Critical)
                // =============================================================

                // v_f >= v_min - spins never stall the run
                if (vel.Forward < MinForwardSpeed)
                {
                    vel.Forward = MinForwardSpeed;
                }

                // Store angular velocity
                vel.Angular = drift.YawRate;

                // =============================================================
                // Update World Transform
                // =============================================================

                // TODO: Get lane frame for proper position update
                // For now, simple forward movement
                float3 forward = math.mul(transform.ValueRO.Rotation, new float3(0, 0, 1));
                float3 right = math.mul(transform.ValueRO.Rotation, new float3(1, 0, 0));

                transform.ValueRW.Position += forward * vel.Forward * deltaTime;
                transform.ValueRW.Position += right * vel.Lateral * deltaTime;

                // Apply yaw rotation
                quaternion yawRot = quaternion.RotateY(drift.YawRate * deltaTime);
                transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, yawRot);
            }
        }
    }
}
