// ============================================================================
// Nightflow - Lane Magnetism System
// Execution Order: 4 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Applies lane magnetism forces to keep vehicles centered.
    /// Uses critically damped spring model: a_lat = m × (-ω²x - 2ωẋ)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SteeringSystem))]
    public partial struct LaneMagnetismSystem : ISystem
    {
        // Magnetism parameters (from spec)
        private const float Omega = 8.0f;               // Natural frequency
        private const float ReferenceSpeed = 40f;       // m/s
        private const float MaxLateralSpeed = 6f;       // m/s
        private const float EdgeStiffness = 20f;        // Edge force coefficient
        private const float SoftZoneRatio = 0.85f;      // 85% of lane width
        private const float AutopilotMultiplier = 1.5f;
        private const float HandbrakeMultiplier = 0.25f;
        private const float DriftMultiplier = 0.3f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (laneFollower, velocity, input, autopilot, driftState, transform) in
                SystemAPI.Query<RefRW<LaneFollower>, RefRW<Velocity>, RefRO<PlayerInput>,
                               RefRO<Autopilot>, RefRO<DriftState>, RefRW<WorldTransform>>())
            {
                // =============================================================
                // Calculate Magnetism Modulation
                // =============================================================

                // m_input = 1 - |steer| (no steering = full magnetism)
                float mInput = 1f - math.abs(input.ValueRO.Steer);

                // m_auto = 1.5 if autopilot, else 1.0
                float mAuto = autopilot.ValueRO.Enabled ? AutopilotMultiplier : 1f;

                // m_speed = sqrt(v / v_ref), clamped [0.75, 1.25]
                float speed = velocity.ValueRO.Forward;
                float mSpeed = math.clamp(math.sqrt(speed / ReferenceSpeed), 0.75f, 1.25f);

                // m_handbrake = 0.25 if engaged, else 1.0
                float mHandbrake = input.ValueRO.Handbrake ? HandbrakeMultiplier : 1f;

                // m_drift = 0.3 if drifting, else 1.0
                float mDrift = driftState.ValueRO.IsDrifting ? DriftMultiplier : 1f;

                // Combined modulation
                float m = mInput * mAuto * mSpeed * mHandbrake * mDrift;

                // =============================================================
                // Calculate Lane Target Position
                // =============================================================

                // TODO: Get actual lane center from LaneSpline component
                // For now, assume lane center is at lateral offset 0
                float targetLateral = 0f;

                // During lane transition, blend between lanes
                // if (laneTransition.Active)
                // {
                //     float t = transition.Progress / transition.Duration;
                //     float lambda = 3f * t * t - 2f * t * t * t; // smoothstep
                //     targetLateral = math.lerp(fromLaneCenter, toLaneCenter, lambda);
                // }

                // =============================================================
                // Apply Critically Damped Spring
                // =============================================================

                float x = laneFollower.ValueRO.LateralOffset - targetLateral;
                float dx = velocity.ValueRO.Lateral;
                float omega = laneFollower.ValueRO.MagnetStrength;

                // a_lat = m × (-ω²x - 2ωẋ)
                float aLat = m * (-omega * omega * x - 2f * omega * dx);

                // =============================================================
                // Apply Edge Force (Soft Constraint)
                // =============================================================

                // TODO: Get lane width from LaneSpline
                float laneWidth = 3.6f;
                float halfWidth = laneWidth * 0.5f;
                float softZone = halfWidth * SoftZoneRatio;

                float absX = math.abs(laneFollower.ValueRO.LateralOffset);
                if (absX > softZone)
                {
                    float xEdge = absX - softZone;
                    float aEdge = -math.sign(laneFollower.ValueRO.LateralOffset) *
                                  EdgeStiffness * xEdge * xEdge;
                    aLat += aEdge;
                }

                // =============================================================
                // Integrate Lateral Velocity
                // =============================================================

                float newLateralVel = velocity.ValueRO.Lateral + aLat * deltaTime;
                newLateralVel = math.clamp(newLateralVel, -MaxLateralSpeed, MaxLateralSpeed);
                velocity.ValueRW.Lateral = newLateralVel;

                // Update lateral offset
                laneFollower.ValueRW.LateralOffset += newLateralVel * deltaTime;

                // TODO: Update world position based on lane spline + lateral offset
                // float3 laneRight = GetLaneRight(laneFollower.LaneEntity, laneFollower.SplineParameter);
                // transform.ValueRW.Position += laneRight * newLateralVel * deltaTime;
            }
        }
    }
}
