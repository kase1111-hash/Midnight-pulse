// ============================================================================
// Nightflow - Steering & Lane Transition System
// Execution Order: 3 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Config;

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
    [UpdateAfter(typeof(LaneBlockingSystem))]
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

        // Blocking check parameters
        private const float BlockCheckAhead = 20f;
        private const float BlockCheckBehind = 10f;
        // GameConstants.LaneWidth uses GameConstants.GameConstants.LaneWidth

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // =============================================================
            // Build blocker list for lane change safety check
            // =============================================================

            var blockers = new NativeList<BlockerInfo>(Allocator.Temp);

            foreach (var (transform, laneFollower, velocity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<LaneFollower>, RefRO<Velocity>>()
                    .WithAll<TrafficVehicleTag>())
            {
                blockers.Add(new BlockerInfo
                {
                    Z = transform.ValueRO.Position.z,
                    Lane = laneFollower.ValueRO.CurrentLane,
                    Speed = velocity.ValueRO.Forward
                });
            }

            foreach (var (transform, hazard) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Hazard>>()
                    .WithAll<HazardTag>())
            {
                int lane = (int)math.round((transform.ValueRO.Position.x / GameConstants.LaneWidth) + 1.5f);
                blockers.Add(new BlockerInfo
                {
                    Z = transform.ValueRO.Position.z,
                    Lane = math.clamp(lane, 0, 3),
                    Speed = 0f
                });
            }

            // =============================================================
            // Process Player Steering
            // =============================================================

            foreach (var (input, steeringState, laneFollower, velocity, transform, damage, componentHealth, failureState) in
                SystemAPI.Query<RefRO<PlayerInput>, RefRW<SteeringState>,
                               RefRW<LaneFollower>, RefRO<Velocity>, RefRO<WorldTransform>,
                               RefRO<DamageState>, RefRO<ComponentHealth>, RefRO<ComponentFailureState>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag, AutopilotActiveTag>())
            {
                float steerInput = input.ValueRO.Steer;
                float myZ = transform.ValueRO.Position.z;
                float mySpeed = velocity.ValueRO.Forward;

                // =============================================================
                // Phase 2 Damage: Steering Component Effects
                // =============================================================

                var health = componentHealth.ValueRO;
                var failures = failureState.ValueRO;

                // Steering health affects responsiveness
                // At full health: normal steering response
                // At 50% health: reduced max angle, slower response
                // At failure: severely limited steering
                float steeringHealthFactor = failures.HasFailed(ComponentFailures.Steering)
                    ? 0.15f  // Steering failed: severely limited control
                    : 0.3f + (health.Steering * 0.7f);  // Gradual degradation

                // Front damage also reduces steering (from original spec)
                float frontDamageFactor = 1f - (damage.ValueRO.Front * 0.4f);

                // Combined steering modifier
                float steeringModifier = steeringHealthFactor * frontDamageFactor;

                // =============================================================
                // Steering Smoothing
                // =============================================================

                // Apply steering modifier to max angle
                float maxSteerAngle = math.PI * 0.25f * steeringModifier; // Max 45 degrees, reduced by damage
                steeringState.ValueRW.TargetAngle = steerInput * maxSteerAngle;

                // Steering health affects response speed (lower health = slower response)
                float smoothness = steeringState.ValueRO.Smoothness;
                if (smoothness <= 0) smoothness = 8f;
                smoothness *= steeringHealthFactor;  // Damaged steering is slower

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
                            // Check if lane is blocked
                            bool isBlocked = IsLaneBlocked(myZ, mySpeed, targetLane, ref blockers);

                            if (!isBlocked)
                            {
                                // Calculate speed-aware duration per spec: T = T_base × (v / v_ref)
                                // Higher speed → longer duration (harder to change lanes at speed)
                                float speed = velocity.ValueRO.Forward;
                                float duration = BaseDuration * (speed / ReferenceSpeed);
                                duration = math.clamp(duration, MinDuration, MaxDuration);

                                // Start lane change
                                steeringState.ValueRW.ChangingLanes = true;
                                steeringState.ValueRW.LaneChangeTimer = 0f;
                                steeringState.ValueRW.LaneChangeDuration = duration;
                                steeringState.ValueRW.LaneChangeDir = direction;

                                laneFollower.ValueRW.TargetLane = targetLane;
                            }
                            // If blocked, steering input is ignored for lane change
                            // Player can still steer within current lane
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

                steeringState.ValueRW.LaneChangeTimer += deltaTime;

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

            blockers.Dispose();
        }

        private bool IsLaneBlocked(float myZ, float mySpeed, int targetLane,
                                   ref NativeList<BlockerInfo> blockers)
        {
            for (int i = 0; i < blockers.Length; i++)
            {
                var b = blockers[i];
                if (b.Lane != targetLane) continue;

                float dz = b.Z - myZ;

                // Adjust blocking distance based on relative speed
                float relSpeed = mySpeed - b.Speed;
                float ahead = BlockCheckAhead + math.max(0, relSpeed * 1.5f);
                float behind = BlockCheckBehind + math.max(0, -relSpeed * 2f);

                if (dz > -behind && dz < ahead)
                {
                    return true;
                }
            }
            return false;
        }

        private struct BlockerInfo
        {
            public float Z;
            public int Lane;
            public float Speed;
        }
    }
}
