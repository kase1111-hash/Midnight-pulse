// ============================================================================
// Nightflow - Autopilot System
// Execution Order: 2 (Simulation Group)
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
    /// AI-driven vehicle control when autopilot is enabled.
    /// Activates after crash, score save, or player request.
    ///
    /// From spec:
    /// - Lane-following at medium speed
    /// - Avoids hazards
    /// - Maintains visual flow
    /// - Menu overlay stays active
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InputSystem))]
    public partial struct AutopilotSystem : ISystem
    {
        // Speed control
        private const float CruiseSpeed = 25f;          // m/s (~90 km/h)
        private const float MaxAutopilotSpeed = 35f;    // m/s (~126 km/h)
        private const float MinAutopilotSpeed = 15f;    // m/s (~54 km/h)

        // Steering control
        private const float LaneCenteringGain = 0.8f;
        private const float SteerSmoothing = 5f;

        // Hazard avoidance
        private const float HazardDetectionRange = 80f;
        private const float HazardAvoidanceStrength = 1.5f;
        private const float LaneChangeThreshold = 0.5f;

        // Emergency response
        private const float EmergencySlowdownFactor = 0.6f;
        private const float EmergencyYieldOffset = 3f;

        // State
        private float _smoothedSteer;
        private float _laneChangeTimer;
        private int _targetLane;

        public void OnCreate(ref SystemState state)
        {
            _smoothedSteer = 0f;
            _laneChangeTimer = 0f;
            _targetLane = -1;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update lane change cooldown
            if (_laneChangeTimer > 0)
            {
                _laneChangeTimer -= deltaTime;
            }

            foreach (var (autopilot, input, laneFollower, velocity, steeringState, transform, detection) in
                SystemAPI.Query<RefRO<Autopilot>, RefRW<PlayerInput>, RefRO<LaneFollower>,
                               RefRO<Velocity>, RefRW<SteeringState>, RefRO<WorldTransform>,
                               RefRO<EmergencyDetection>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                if (!autopilot.ValueRO.Enabled)
                    continue;

                float3 playerPos = transform.ValueRO.Position;
                float currentSpeed = velocity.ValueRO.Forward;
                float targetSpeed = autopilot.ValueRO.TargetSpeed;

                // =============================================================
                // Hazard Detection
                // =============================================================

                float hazardThreat = 0f;
                float hazardLateralDir = 0f;
                float nearestHazardDist = HazardDetectionRange;

                foreach (var (hazardTransform, hazard) in
                    SystemAPI.Query<RefRO<WorldTransform>, RefRO<Hazard>>()
                        .WithAll<HazardTag>())
                {
                    float3 toHazard = hazardTransform.ValueRO.Position - playerPos;
                    float forwardDist = toHazard.z;
                    float lateralDist = toHazard.x;

                    // Only consider hazards ahead
                    if (forwardDist > 0 && forwardDist < HazardDetectionRange)
                    {
                        // Check if in our lane (roughly)
                        if (math.abs(lateralDist) < GameConstants.LaneWidth)
                        {
                            float threat = hazard.ValueRO.Severity *
                                (1f - forwardDist / HazardDetectionRange);

                            if (threat > hazardThreat)
                            {
                                hazardThreat = threat;
                                hazardLateralDir = math.sign(lateralDist);
                                nearestHazardDist = forwardDist;
                            }
                        }
                    }
                }

                // =============================================================
                // Emergency Vehicle Response
                // =============================================================

                float emergencySpeedMod = 1f;
                float emergencySteerMod = 0f;

                if (detection.ValueRO.WarningActive)
                {
                    float urgency = detection.ValueRO.Urgency;

                    // Slow down
                    emergencySpeedMod = 1f - urgency * (1f - EmergencySlowdownFactor);

                    // Yield to side (use avoidance offset)
                    emergencySteerMod = detection.ValueRO.AvoidanceOffset * 0.3f;
                }

                // =============================================================
                // Lane Change Decision
                // =============================================================

                bool shouldChangeLane = false;
                int laneChangeDirection = 0;

                if (hazardThreat > LaneChangeThreshold && _laneChangeTimer <= 0)
                {
                    // Decide which way to go
                    int currentLane = laneFollower.ValueRO.CurrentLane;
                    int laneCount = laneFollower.ValueRO.TotalLanes;

                    // Prefer going opposite of hazard lateral position
                    if (hazardLateralDir > 0 && currentLane > 0)
                    {
                        // Hazard is to our right, go left
                        laneChangeDirection = -1;
                        shouldChangeLane = true;
                    }
                    else if (hazardLateralDir < 0 && currentLane < laneCount - 1)
                    {
                        // Hazard is to our left, go right
                        laneChangeDirection = 1;
                        shouldChangeLane = true;
                    }
                    else if (hazardLateralDir == 0)
                    {
                        // Hazard dead ahead - pick safest lane
                        if (currentLane > 0)
                        {
                            laneChangeDirection = -1;
                            shouldChangeLane = true;
                        }
                        else if (currentLane < laneCount - 1)
                        {
                            laneChangeDirection = 1;
                            shouldChangeLane = true;
                        }
                    }

                    if (shouldChangeLane)
                    {
                        _laneChangeTimer = 3f; // Cooldown between lane changes
                    }
                }

                // =============================================================
                // Speed Control
                // =============================================================

                // Adjust target speed based on threats
                float effectiveTargetSpeed = targetSpeed * emergencySpeedMod;

                // Slow down for hazards
                if (hazardThreat > 0.3f && nearestHazardDist < 40f)
                {
                    float hazardSlowdown = 1f - (hazardThreat * 0.4f);
                    effectiveTargetSpeed *= hazardSlowdown;
                }

                // Clamp to safe range
                effectiveTargetSpeed = math.clamp(
                    effectiveTargetSpeed,
                    MinAutopilotSpeed,
                    MaxAutopilotSpeed
                );

                // Apply throttle/brake
                if (currentSpeed < effectiveTargetSpeed - 1f)
                {
                    input.ValueRW.Throttle = math.saturate(
                        (effectiveTargetSpeed - currentSpeed) / 10f
                    );
                    input.ValueRW.Brake = 0f;
                }
                else if (currentSpeed > effectiveTargetSpeed + 2f)
                {
                    input.ValueRW.Throttle = 0f;
                    input.ValueRW.Brake = math.saturate(
                        (currentSpeed - effectiveTargetSpeed) / 15f
                    );
                }
                else
                {
                    // Cruise - gentle throttle
                    input.ValueRW.Throttle = 0.3f;
                    input.ValueRW.Brake = 0f;
                }

                // =============================================================
                // Steering Control
                // =============================================================

                // Lane centering
                float lateralOffset = laneFollower.ValueRO.LateralOffset;
                float steerToCenter = -lateralOffset * LaneCenteringGain;

                // Add emergency yield
                steerToCenter += emergencySteerMod;

                // Add hazard avoidance (away from hazard)
                if (hazardThreat > 0.2f && !shouldChangeLane)
                {
                    float avoidSteer = -hazardLateralDir * hazardThreat * HazardAvoidanceStrength;
                    steerToCenter += avoidSteer;
                }

                // Smooth steering
                _smoothedSteer = math.lerp(
                    _smoothedSteer,
                    steerToCenter,
                    SteerSmoothing * deltaTime
                );

                input.ValueRW.Steer = math.clamp(_smoothedSteer, -1f, 1f);

                // =============================================================
                // Lane Change Execution
                // =============================================================

                if (shouldChangeLane && !steeringState.ValueRO.ChangingLanes)
                {
                    // Request lane change through steering state
                    steeringState.ValueRW.LaneChangeRequested = true;
                    steeringState.ValueRW.LaneChangeDirection = laneChangeDirection;
                }

                // No handbrake during autopilot
                input.ValueRW.Handbrake = false;
            }
        }
    }
}
