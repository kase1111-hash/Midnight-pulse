// ============================================================================
// Nightflow - Traffic Movement System
// Handles movement for AI traffic vehicles
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Utilities;

namespace Nightflow.Systems
{
    /// <summary>
    /// Moves traffic vehicles along the track spline.
    /// Simpler physics than player - no drift, just smooth lane following.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrafficAISystem))]
    public partial struct TrafficMovementSystem : ISystem
    {
        // Movement parameters
        private const float LaneWidth = 3.6f;
        private const float LaneMagnetism = 10f;          // Stronger than player for smooth AI
        private const float MaxLateralSpeed = 4f;         // Slower lane changes than player
        private const float RotationSpeed = 5f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (velocity, transform, laneFollower, steeringState) in
                SystemAPI.Query<RefRW<Velocity>, RefRW<WorldTransform>,
                               RefRW<LaneFollower>, RefRW<SteeringState>>()
                    .WithAll<TrafficVehicleTag>())
            {
                float myZ = transform.ValueRO.Position.z;

                // =============================================================
                // Find Current Track Segment
                // =============================================================

                HermiteSpline currentSpline = default;
                TrackSegment currentSegment = default;
                bool foundSegment = false;

                foreach (var (segment, spline) in
                    SystemAPI.Query<RefRO<TrackSegment>, RefRO<HermiteSpline>>()
                        .WithAll<TrackSegmentTag>())
                {
                    if (myZ >= segment.ValueRO.StartZ && myZ <= segment.ValueRO.EndZ)
                    {
                        currentSpline = spline.ValueRO;
                        currentSegment = segment.ValueRO;
                        foundSegment = true;
                        break;
                    }
                }

                if (!foundSegment)
                {
                    // Just move forward if no segment found
                    transform.ValueRW.Position += new float3(0, 0, velocity.ValueRO.Forward * deltaTime);
                    continue;
                }

                // =============================================================
                // Calculate Spline Frame
                // =============================================================

                float segmentProgress = (myZ - currentSegment.StartZ) /
                                        (currentSegment.EndZ - currentSegment.StartZ);
                segmentProgress = math.saturate(segmentProgress);

                SplineUtilities.BuildFrameAtT(
                    currentSpline.P0, currentSpline.T0,
                    currentSpline.P1, currentSpline.T1,
                    segmentProgress,
                    out float3 splinePos, out float3 forward, out float3 right, out float3 up);

                laneFollower.ValueRW.SplineT = segmentProgress;

                // =============================================================
                // Calculate Target Lane Position
                // =============================================================

                int currentLane = laneFollower.ValueRO.CurrentLane;
                int targetLane = laneFollower.ValueRO.TargetLane;

                float currentLaneOffset = (currentLane - 1.5f) * LaneWidth;
                float targetLaneOffset = (targetLane - 1.5f) * LaneWidth;

                // Lane change interpolation
                float targetLateral = currentLaneOffset;

                if (steeringState.ValueRO.ChangingLanes)
                {
                    steeringState.ValueRW.LaneChangeTimer += deltaTime;

                    float duration = steeringState.ValueRO.LaneChangeDuration;
                    if (duration <= 0) duration = 0.8f;

                    float t = steeringState.ValueRO.LaneChangeTimer / duration;

                    if (t >= 1f)
                    {
                        // Lane change complete
                        steeringState.ValueRW.ChangingLanes = false;
                        steeringState.ValueRW.LaneChangeTimer = 0f;
                        laneFollower.ValueRW.CurrentLane = targetLane;
                        targetLateral = targetLaneOffset;
                    }
                    else
                    {
                        // Smoothstep interpolation
                        float lambda = SplineUtilities.Smoothstep(t);
                        targetLateral = math.lerp(currentLaneOffset, targetLaneOffset, lambda);
                    }
                }

                // =============================================================
                // Lane Magnetism (Simpler than player)
                // =============================================================

                float3 vehiclePos = transform.ValueRO.Position;
                float3 toVehicle = vehiclePos - splinePos;
                float currentLateral = math.dot(toVehicle, right);

                laneFollower.ValueRW.LateralOffset = currentLateral;

                // Simple spring toward target
                float lateralError = currentLateral - targetLateral;
                float lateralAccel = -LaneMagnetism * lateralError - 4f * velocity.ValueRO.Lateral;

                velocity.ValueRW.Lateral += lateralAccel * deltaTime;
                velocity.ValueRW.Lateral = math.clamp(velocity.ValueRO.Lateral, -MaxLateralSpeed, MaxLateralSpeed);

                // =============================================================
                // Update Position
                // =============================================================

                // Forward movement
                transform.ValueRW.Position += forward * velocity.ValueRO.Forward * deltaTime;

                // Lateral movement
                transform.ValueRW.Position += right * velocity.ValueRO.Lateral * deltaTime;

                // Keep on track height
                float3 idealPos = splinePos + right * currentLateral + up * 0.5f;
                float heightError = idealPos.y - vehiclePos.y;
                transform.ValueRW.Position += new float3(0, heightError * 5f * deltaTime, 0);

                // =============================================================
                // Update Rotation
                // =============================================================

                quaternion targetRot = quaternion.LookRotation(forward, up);
                transform.ValueRW.Rotation = math.slerp(
                    transform.ValueRO.Rotation,
                    targetRot,
                    RotationSpeed * deltaTime
                );
            }
        }
    }
}
