// ============================================================================
// Nightflow - Steering & Lane Transition System
// Execution Order: 3 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Processes steering input and manages lane transitions for player.
    /// Applies smoothing to steering angle.
    /// Handles lane change trigger, progress, and abort logic.
    ///
    /// Lane Change Trigger (from spec):
    /// - Steering exceeds threshold: |s| > 0.35
    /// - Steering direction matches lane direction
    /// - Target lane exists and is not blocked
    /// - Not already transitioning
    ///
    /// Transition: λ(t) = 3t² - 2t³ (smoothstep)
    /// Duration: T = clamp(T_base × (v/v_ref), T_min, T_max)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AutopilotSystem))]
    public partial struct SteeringSystem : ISystem
    {
        // Lane change parameters (from spec)
        private const float SteerTriggerThreshold = 0.35f;
        private const float AbortThreshold = 0.7f;
        private const float BaseDuration = 0.6f;
        private const float MinDuration = 0.45f;
        private const float MaxDuration = 1.0f;
        private const float ReferenceSpeed = 40f;         // m/s
        private const int NumLanes = 4;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // =============================================================
            // Process Player Steering
            // =============================================================

            foreach (var (input, steeringState, laneFollower, velocity) in
                SystemAPI.Query<RefRO<PlayerInput>, RefRW<SteeringState>,
                               RefRW<LaneFollower>, RefRO<Velocity>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag, AutopilotActiveTag>())
            {
                float steerInput = input.ValueRO.Steer;

                // =============================================================
                // Steering Smoothing
                // =============================================================

                steeringState.ValueRW.TargetAngle = steerInput * math.PI * 0.25f; // Max 45 degrees

                float smoothness = steeringState.ValueRO.Smoothness;
                if (smoothness <= 0) smoothness = 8f;

                float currentAngle = steeringState.ValueRO.CurrentAngle;
                float targetAngle = steeringState.ValueRO.TargetAngle;

                // Exponential smoothing
                float blendFactor = 1f - math.exp(-deltaTime * smoothness);
                steeringState.ValueRW.CurrentAngle = math.lerp(currentAngle, targetAngle, blendFactor);

                // =============================================================
                // Lane Change Progress
                // =============================================================

                if (steeringState.ValueRO.ChangingLanes)
                {
                    steeringState.ValueRW.LaneChangeTimer += deltaTime;

                    float duration = steeringState.ValueRO.LaneChangeDuration;
                    if (duration <= 0) duration = BaseDuration;

                    float t = steeringState.ValueRO.LaneChangeTimer / duration;

                    // Check for abort (counter-steer)
                    int dir = steeringState.ValueRO.LaneChangeDir;
                    bool counterSteer = (dir > 0 && steerInput < -AbortThreshold) ||
                                       (dir < 0 && steerInput > AbortThreshold);

                    if (counterSteer && t < 0.5f)
                    {
                        // Abort: reverse direction
                        steeringState.ValueRW.LaneChangeDir = -dir;
                        steeringState.ValueRW.LaneChangeTimer = duration * (1f - t);

                        // Swap target back to current
                        laneFollower.ValueRW.TargetLane = laneFollower.ValueRO.CurrentLane;
                    }
                    else if (t >= 1f)
                    {
                        // Lane change complete
                        steeringState.ValueRW.ChangingLanes = false;
                        steeringState.ValueRW.LaneChangeTimer = 0f;
                        laneFollower.ValueRW.CurrentLane = laneFollower.ValueRO.TargetLane;
                    }
                }
                else
                {
                    // =============================================================
                    // Check for Lane Change Trigger
                    // =============================================================

                    if (math.abs(steerInput) > SteerTriggerThreshold)
                    {
                        int direction = steerInput > 0 ? 1 : -1;
                        int currentLane = laneFollower.ValueRO.CurrentLane;
                        int targetLane = currentLane + direction;

                        // Check lane bounds
                        if (targetLane >= 0 && targetLane < NumLanes)
                        {
                            // Calculate speed-aware duration
                            float speed = velocity.ValueRO.Forward;
                            float duration = BaseDuration * (ReferenceSpeed / math.max(speed, 10f));
                            duration = math.clamp(duration, MinDuration, MaxDuration);

                            // Start lane change
                            steeringState.ValueRW.ChangingLanes = true;
                            steeringState.ValueRW.LaneChangeTimer = 0f;
                            steeringState.ValueRW.LaneChangeDuration = duration;
                            steeringState.ValueRW.LaneChangeDir = direction;

                            laneFollower.ValueRW.TargetLane = targetLane;
                        }
                    }
                }

                // =============================================================
                // Steering Attenuation During Lane Change
                // =============================================================

                if (steeringState.ValueRO.ChangingLanes)
                {
                    // s_effective = s × (1 - λ)
                    // Reduce steering influence during transition
                    float t = steeringState.ValueRO.LaneChangeTimer /
                             math.max(steeringState.ValueRO.LaneChangeDuration, 0.1f);
                    float lambda = t * t * (3f - 2f * t); // smoothstep

                    float attenuation = 1f - lambda * 0.5f;
                    steeringState.ValueRW.CurrentAngle *= attenuation;
                }
            }

            // =============================================================
            // Complete Traffic Lane Changes
            // =============================================================

            foreach (var (steeringState, laneFollower) in
                SystemAPI.Query<RefRW<SteeringState>, RefRW<LaneFollower>>()
                    .WithAll<TrafficVehicleTag>())
            {
                if (!steeringState.ValueRO.ChangingLanes)
                    continue;

                float duration = steeringState.ValueRO.LaneChangeDuration;
                if (duration <= 0) duration = 0.8f;

                float t = steeringState.ValueRO.LaneChangeTimer / duration;

                if (t >= 1f)
                {
                    // Lane change complete
                    steeringState.ValueRW.ChangingLanes = false;
                    steeringState.ValueRW.LaneChangeTimer = 0f;
                    laneFollower.ValueRW.CurrentLane = laneFollower.ValueRO.TargetLane;
                }
            }
        }
    }
}
