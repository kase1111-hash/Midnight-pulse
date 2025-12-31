// ============================================================================
// Nightflow - Emergency Vehicle System
// Execution Order: 11 (Simulation Group)
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
    /// Controls emergency vehicle behavior and player detection/avoidance.
    ///
    /// From spec:
    /// Detection: d_f < 0 AND |d_l| < w_detect AND |d_f| < d_max
    /// Urgency: u = clamp(1 - |d_f|/d_max, 0, 1)
    /// Avoidance: x_avoid = dir × k_a × u × w
    /// Escalation: If u > 0.6 AND time > 1.5s → aggressive overtake
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrafficAISystem))]
    public partial struct EmergencyVehicleSystem : ISystem
    {
        // Emergency vehicle parameters
        private const float EmergencySpeed = 45f;         // m/s - faster than traffic
        private const float DetectionDistance = 120f;     // meters behind player (d_max)
        private const float DetectionWidth = 7f;          // meters lateral
        // GameConstants.LaneWidth uses GameConstants.GameConstants.LaneWidth

        // Avoidance parameters
        private const float AvoidStrength = 0.8f;         // k_a
        private const float PlayerMinOverride = 0.3f;     // Minimum player control
        private const float WarningTime = 1.5f;           // seconds before escalation

        // Urgency thresholds
        private const float EscalationUrgency = 0.6f;
        private const float EscalationTime = 1.5f;        // seconds of pressure before aggressive

        // Light boost
        private const float BaseFlashRate = 4f;
        private const float EscalatedFlashRate = 8f;
        private const float LightIntensityBoost = 3f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // =============================================================
            // Get Player State
            // =============================================================

            float3 playerPos = float3.zero;
            int playerLane = 0;
            float playerSpeed = 0f;
            float playerSteer = 0f;
            Entity playerEntity = Entity.Null;

            foreach (var (transform, laneFollower, velocity, input, entity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<LaneFollower>,
                               RefRO<Velocity>, RefRO<PlayerInput>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>()
                    .WithEntityAccess())
            {
                playerPos = transform.ValueRO.Position;
                playerLane = laneFollower.ValueRO.CurrentLane;
                playerSpeed = velocity.ValueRO.Forward;
                playerSteer = input.ValueRO.Steer;
                playerEntity = entity;
                break;
            }

            if (playerEntity == Entity.Null)
                return;

            // =============================================================
            // Update Emergency Vehicles
            // =============================================================

            foreach (var (emergencyAI, velocity, transform, laneFollower, steeringState, lightEmitter) in
                SystemAPI.Query<RefRW<EmergencyAI>, RefRW<Velocity>,
                               RefRW<WorldTransform>, RefRW<LaneFollower>,
                               RefRW<SteeringState>, RefRW<LightEmitter>>()
                    .WithAll<EmergencyVehicleTag>())
            {
                // Maintain emergency speed
                velocity.ValueRW.Forward = EmergencySpeed;

                // =============================================================
                // Calculate Approach Geometry
                // =============================================================

                float3 toPlayer = playerPos - transform.ValueRO.Position;

                // d_f = forward distance (positive = player ahead)
                float dF = toPlayer.z;

                // d_l = lateral distance
                float dL = toPlayer.x;

                emergencyAI.ValueRW.ApproachDistance = dF;

                // =============================================================
                // Calculate Urgency
                // u = clamp(1 - |d_f|/d_max, 0, 1)
                // =============================================================

                float urgency = 0f;
                bool inDetectionCone = false;

                // Detection: d_f > 0 (behind player) AND |d_l| < w_detect AND d_f < d_max
                if (dF > 0 && dF < DetectionDistance && math.abs(dL) < DetectionWidth)
                {
                    inDetectionCone = true;
                    urgency = math.saturate(1f - dF / DetectionDistance);
                }

                emergencyAI.ValueRW.Urgency = urgency;
                emergencyAI.ValueRW.SirenActive = inDetectionCone || dF < 50f;

                // =============================================================
                // Pressure Time & Escalation
                // =============================================================

                if (urgency > EscalationUrgency)
                {
                    emergencyAI.ValueRW.PressureTime += deltaTime;

                    // Check for escalation
                    if (emergencyAI.ValueRO.PressureTime > EscalationTime &&
                        !emergencyAI.ValueRO.AggressiveOvertake)
                    {
                        // Player hasn't yielded - go aggressive
                        emergencyAI.ValueRW.AggressiveOvertake = true;
                    }
                }
                else
                {
                    // Reset pressure if urgency drops
                    emergencyAI.ValueRW.PressureTime = 0f;
                }

                // =============================================================
                // Light/Siren Behavior
                // =============================================================

                if (emergencyAI.ValueRO.SirenActive)
                {
                    lightEmitter.ValueRW.Intensity = 1f + urgency * (LightIntensityBoost - 1f);
                    lightEmitter.ValueRW.StrobeRate = emergencyAI.ValueRO.AggressiveOvertake
                        ? EscalatedFlashRate
                        : BaseFlashRate + urgency * 2f;
                }
                else
                {
                    lightEmitter.ValueRW.Intensity = 0.3f;
                    lightEmitter.ValueRW.StrobeRate = BaseFlashRate;
                }

                // Update strobe phase
                lightEmitter.ValueRW.StrobePhase += lightEmitter.ValueRO.StrobeRate * deltaTime;

                // =============================================================
                // Lane Selection
                // =============================================================

                int currentLane = laneFollower.ValueRO.CurrentLane;

                if (!steeringState.ValueRO.ChangingLanes)
                {
                    int targetLane = currentLane;

                    if (emergencyAI.ValueRO.AggressiveOvertake)
                    {
                        // Aggressive: pick lane away from player
                        targetLane = (playerLane <= 1) ? 3 : 0;
                    }
                    else if (dF > 20f && dF < 100f)
                    {
                        // Approaching: try to pick clear lane
                        // Prefer lane opposite to player
                        if (playerLane == currentLane)
                        {
                            targetLane = (currentLane <= 1) ? currentLane + 1 : currentLane - 1;
                        }
                    }

                    if (targetLane != currentLane && targetLane >= 0 && targetLane <= 3)
                    {
                        steeringState.ValueRW.ChangingLanes = true;
                        steeringState.ValueRW.LaneChangeTimer = 0f;
                        steeringState.ValueRW.LaneChangeDuration = 0.6f;
                        steeringState.ValueRW.LaneChangeDir = targetLane > currentLane ? 1 : -1;
                        laneFollower.ValueRW.TargetLane = targetLane;
                    }
                }

                // =============================================================
                // Lane Change Progress
                // =============================================================

                if (steeringState.ValueRO.ChangingLanes)
                {
                    steeringState.ValueRW.LaneChangeTimer += deltaTime;

                    float t = steeringState.ValueRO.LaneChangeTimer /
                             steeringState.ValueRO.LaneChangeDuration;

                    if (t >= 1f)
                    {
                        steeringState.ValueRW.ChangingLanes = false;
                        steeringState.ValueRW.LaneChangeTimer = 0f;
                        laneFollower.ValueRW.CurrentLane = laneFollower.ValueRO.TargetLane;
                    }
                }

                // =============================================================
                // Position Update
                // =============================================================

                float3 forward = new float3(0, 0, 1);
                transform.ValueRW.Position += forward * velocity.ValueRO.Forward * deltaTime;

                // Lateral movement for lane following
                float targetLateral = (laneFollower.ValueRO.CurrentLane - 1.5f) * GameConstants.LaneWidth;

                if (steeringState.ValueRO.ChangingLanes)
                {
                    float sourceLateral = (laneFollower.ValueRO.CurrentLane - 1.5f) * GameConstants.LaneWidth;
                    float destLateral = (laneFollower.ValueRO.TargetLane - 1.5f) * GameConstants.LaneWidth;

                    float t = steeringState.ValueRO.LaneChangeTimer /
                             steeringState.ValueRO.LaneChangeDuration;
                    float lambda = t * t * (3f - 2f * t); // smoothstep

                    targetLateral = math.lerp(sourceLateral, destLateral, lambda);
                }

                float currentLateral = transform.ValueRO.Position.x;
                float lateralError = targetLateral - currentLateral;
                float lateralSpeed = math.clamp(lateralError * 8f, -6f, 6f);
                transform.ValueRW.Position += new float3(lateralSpeed * deltaTime, 0, 0);
            }

            // =============================================================
            // Update Player Emergency Detection
            // =============================================================

            foreach (var (detection, transform, input) in
                SystemAPI.Query<RefRW<EmergencyDetection>, RefRO<WorldTransform>, RefRO<PlayerInput>>()
                    .WithAll<PlayerVehicleTag>())
            {
                detection.ValueRW.NearestDistance = float.MaxValue;
                detection.ValueRW.ApproachingFromBehind = false;
                detection.ValueRW.Urgency = 0f;
                detection.ValueRW.AvoidanceOffset = 0f;

                foreach (var (emergencyAI, emergencyTransform, emergencyLane) in
                    SystemAPI.Query<RefRO<EmergencyAI>, RefRO<WorldTransform>, RefRO<LaneFollower>>()
                        .WithAll<EmergencyVehicleTag>())
                {
                    float3 toEmergency = emergencyTransform.ValueRO.Position - transform.ValueRO.Position;
                    float dF = -toEmergency.z; // Positive = emergency behind
                    float dL = toEmergency.x;

                    // Check if in detection cone
                    if (dF > 0 && dF < DetectionDistance && math.abs(dL) < DetectionWidth)
                    {
                        if (dF < detection.ValueRO.NearestDistance)
                        {
                            detection.ValueRW.NearestDistance = dF;
                            detection.ValueRW.ApproachingFromBehind = true;
                            detection.ValueRW.EmergencyLane = emergencyLane.ValueRO.CurrentLane;
                            detection.ValueRW.TimeToArrival = dF / EmergencySpeed;

                            // Calculate urgency
                            float urgency = math.saturate(1f - dF / DetectionDistance);
                            detection.ValueRW.Urgency = urgency;

                            // =============================================================
                            // Calculate Avoidance Offset
                            // dir = -sign(d_l)
                            // x_avoid = dir × k_a × u × w
                            // Player override: m_player = clamp(1 - |s|, 0.3, 1)
                            // =============================================================

                            float dir = -math.sign(dL);
                            if (math.abs(dL) < 0.1f)
                            {
                                // Emergency directly behind - pick a side
                                dir = (emergencyLane.ValueRO.CurrentLane <= 1) ? 1f : -1f;
                            }

                            float avoidOffset = dir * AvoidStrength * urgency * GameConstants.LaneWidth;

                            // Apply player steering override
                            float playerOverride = math.clamp(1f - math.abs(input.ValueRO.Steer),
                                                             PlayerMinOverride, 1f);
                            avoidOffset *= playerOverride;

                            detection.ValueRW.AvoidanceOffset = avoidOffset;
                        }
                    }
                }

                // Set warning state
                detection.ValueRW.WarningActive = detection.ValueRO.ApproachingFromBehind &&
                                                   detection.ValueRO.TimeToArrival < WarningTime * 2f;
            }
        }
    }
}
