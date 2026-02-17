// ============================================================================
// Nightflow - Lane Magnetism System
// Execution Order: 4 (Simulation Group)
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
    /// Applies lane magnetism forces to keep vehicles centered.
    /// Uses critically damped spring model: a_lat = m × (-ω²x - 2ωẋ)
    ///
    /// Magnetism modulation factors:
    /// - m_input: Reduces when steering (allows manual control)
    /// - m_auto: Increases during autopilot (stronger centering)
    /// - m_speed: Scales with sqrt(speed/reference)
    /// - m_handbrake: Reduces during handbrake (allows drift)
    /// - m_drift: Reduces when drifting
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SteeringSystem))]
    public partial struct LaneMagnetismSystem : ISystem
    {
        // Magnetism parameters (from spec)
        private const float BaseOmega = 8.0f;             // Natural frequency
        private const float ReferenceSpeed = 40f;         // m/s
        private const float MaxLateralSpeed = 6f;         // m/s
        private const float EdgeStiffness = 20f;          // Edge force coefficient
        private const float SoftZoneRatio = 0.85f;        // 85% of lane width
        // GameConstants.LaneWidth uses GameConstants.GameConstants.LaneWidth (meters)

        // Modulation multipliers
        private const float AutopilotMultiplier = 1.5f;
        private const float HandbrakeMultiplier = 0.25f;
        private const float DriftMultiplier = 0.3f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // =============================================================
            // Get Current Track Spline Data
            // =============================================================

            // Find the track segment the player is on
            // We'll use this for proper lane frame calculation

            foreach (var (laneFollower, velocity, input, autopilot, driftState, steeringState, transform, detection) in
                SystemAPI.Query<RefRW<LaneFollower>, RefRW<Velocity>, RefRO<PlayerInput>,
                               RefRO<Autopilot>, RefRO<DriftState>, RefRO<SteeringState>,
                               RefRW<WorldTransform>, RefRO<EmergencyDetection>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                float playerZ = transform.ValueRO.Position.z;

                // Find the spline segment this vehicle is on
                HermiteSpline currentSpline = default;
                TrackSegment currentSegment = default;
                bool foundSegment = false;

                foreach (var (segment, spline) in
                    SystemAPI.Query<RefRO<TrackSegment>, RefRO<HermiteSpline>>()
                        .WithAll<TrackSegmentTag>())
                {
                    if (playerZ >= segment.ValueRO.StartZ && playerZ <= segment.ValueRO.EndZ)
                    {
                        currentSpline = spline.ValueRO;
                        currentSegment = segment.ValueRO;
                        foundSegment = true;
                        break;
                    }
                }

                if (!foundSegment)
                    continue;

                // =============================================================
                // Calculate Spline Parameter and Frame
                // =============================================================

                // Approximate t parameter based on Z position
                // Guard against division by zero for zero-length segments
                float segmentLength = currentSegment.EndZ - currentSegment.StartZ;
                if (math.abs(segmentLength) < 0.001f)
                    continue;

                float segmentProgress = (playerZ - currentSegment.StartZ) / segmentLength;
                segmentProgress = math.saturate(segmentProgress);

                // Get spline frame at current position
                SplineUtilities.BuildFrameAtT(
                    currentSpline.P0, currentSpline.T0,
                    currentSpline.P1, currentSpline.T1,
                    segmentProgress,
                    out float3 splinePos, out float3 forward, out float3 right, out float3 up);

                // Update spline T for other systems
                laneFollower.ValueRW.SplineParameter = segmentProgress;

                // =============================================================
                // Calculate Lane Center Position
                // =============================================================

                // Lane indices: 0, 1, 2, 3 (left to right)
                // Offsets from center: -1.5w, -0.5w, +0.5w, +1.5w
                int currentLane = laneFollower.ValueRO.CurrentLane;
                int targetLane = laneFollower.ValueRO.TargetLane;

                float currentLaneOffset = (currentLane - 1.5f) * GameConstants.LaneWidth;
                float targetLaneOffset = (targetLane - 1.5f) * GameConstants.LaneWidth;

                // Calculate target lateral position
                float targetLateral = currentLaneOffset;

                // If changing lanes, blend using smoothstep
                if (steeringState.ValueRO.ChangingLanes)
                {
                    float t = steeringState.ValueRO.LaneChangeTimer /
                             steeringState.ValueRO.LaneChangeDuration;
                    float lambda = SplineUtilities.Smoothstep(t);
                    targetLateral = math.lerp(currentLaneOffset, targetLaneOffset, lambda);
                }

                // Apply emergency vehicle avoidance offset
                // This nudges the target position to make room for approaching emergencies
                targetLateral += detection.ValueRO.AvoidanceOffset;

                // =============================================================
                // Calculate Magnetism Modulation
                // =============================================================

                // m_input = 1 - |steer| (no steering = full magnetism)
                float mInput = 1f - math.abs(input.ValueRO.Steer);

                // m_auto = 1.5 if autopilot, else 1.0
                float mAuto = autopilot.ValueRO.Enabled ? AutopilotMultiplier : 1f;

                // m_speed = sqrt(v / v_ref), clamped [0.75, 1.25]
                float speed = math.max(velocity.ValueRO.Forward, 1f);
                float mSpeed = math.clamp(math.sqrt(speed / ReferenceSpeed), 0.75f, 1.25f);

                // m_handbrake = 0.25 if engaged, else 1.0
                float mHandbrake = input.ValueRO.Handbrake ? HandbrakeMultiplier : 1f;

                // m_drift = 0.3 if drifting, else 1.0
                float mDrift = driftState.ValueRO.IsDrifting ? DriftMultiplier : 1f;

                // Combined modulation (can be reduced by damage)
                float baseMagnet = laneFollower.ValueRO.MagnetStrength;
                float m = baseMagnet * mInput * mAuto * mSpeed * mHandbrake * mDrift;

                // =============================================================
                // Calculate Current Lateral Position
                // =============================================================

                // Project vehicle position onto lane frame
                float3 vehiclePos = transform.ValueRO.Position;
                float3 toVehicle = vehiclePos - splinePos;

                // Lateral offset from spline center
                float currentLateral = math.dot(toVehicle, right);

                // Update stored lateral offset
                laneFollower.ValueRW.LateralOffset = currentLateral;

                // =============================================================
                // Apply Critically Damped Spring
                // =============================================================

                // x_error = current - target
                float x = currentLateral - targetLateral;
                float dx = velocity.ValueRO.Lateral;
                float omega = BaseOmega;

                // a_lat = m × (-ω²x - 2ωẋ)
                float aLat = m * (-omega * omega * x - 2f * omega * dx);

                // =============================================================
                // Apply Edge Force (Soft Constraint)
                // =============================================================

                // Edge forces keep vehicle from leaving the road entirely
                float halfRoadWidth = (currentSegment.NumLanes * GameConstants.LaneWidth) * 0.5f;
                float softEdge = halfRoadWidth * SoftZoneRatio;

                float absLateral = math.abs(currentLateral);
                if (absLateral > softEdge)
                {
                    float xEdge = absLateral - softEdge;
                    float aEdge = -math.sign(currentLateral) * EdgeStiffness * xEdge * xEdge;
                    aLat += aEdge;
                }

                // =============================================================
                // Integrate Lateral Velocity
                // =============================================================

                float newLateralVel = velocity.ValueRO.Lateral + aLat * deltaTime;
                newLateralVel = math.clamp(newLateralVel, -MaxLateralSpeed, MaxLateralSpeed);
                velocity.ValueRW.Lateral = newLateralVel;

                // =============================================================
                // Update World Position
                // =============================================================

                // Calculate ideal position on spline + lateral offset
                float3 idealPos = splinePos + right * currentLateral + up * 0.5f; // 0.5m vehicle height

                // Blend toward ideal position (don't teleport)
                float positionBlend = m * 2f * deltaTime;
                float3 lateralCorrection = right * (newLateralVel * deltaTime);

                transform.ValueRW.Position += lateralCorrection;

                // Align vehicle rotation with track (gradually)
                quaternion targetRot = quaternion.LookRotation(forward, up);

                // Apply yaw offset from drift state
                if (math.abs(driftState.ValueRO.YawOffset) > 0.01f)
                {
                    quaternion yawOffset = quaternion.RotateY(driftState.ValueRO.YawOffset);
                    targetRot = math.mul(targetRot, yawOffset);
                }

                // mDrift is 0.3 when drifting, 1.0 when not — use mDrift directly
                // so normal driving gets full blend and drifting gets reduced blend
                float rotationBlend = mDrift * 5f * deltaTime;
                transform.ValueRW.Rotation = math.slerp(
                    transform.ValueRO.Rotation,
                    targetRot,
                    rotationBlend
                );
            }
        }
    }
}
