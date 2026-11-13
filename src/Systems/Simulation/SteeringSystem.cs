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
        // Lane change parameters (from spec). The trigger threshold lives in
        // VehicleDynamics (the in-lane lean hands over to a lane change there)
        // and the speed-aware duration in HazardPlacement (hazard fairness uses
        // the same lane-change time).
        private const float AbortThreshold = 0.7f;
        private const float BaseDuration = HazardPlacement.LaneChangeBaseDuration;
        private const int NumLanes = 4;

        // Blocking check parameters. The window ahead is the road the car
        // closes on the blocker while the change is under way (relative speed x
        // lane-change time), the same window HazardPlacement keeps two lanes
        // free in. It scales with speed, but unlike a fixed number of seconds
        // it never refuses a change because of something far down the road.
        private const float BlockCheckAhead = 12f;
        private const float BlockCheckBehind = 10f;
        private const float BlockBehindSeconds = 1.5f;    // faster cars closing from behind

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
                blockers.Add(new BlockerInfo
                {
                    Z = transform.ValueRO.Position.z,
                    Lane = math.clamp(hazard.ValueRO.Lane, 0, 3),
                    Speed = 0f
                });
            }

            // =============================================================
            // Process Player Steering
            // =============================================================

            foreach (var (input, steeringState, laneFollower, velocity, transform, damage, entity) in
                SystemAPI.Query<RefRO<PlayerInput>, RefRW<SteeringState>,
                               RefRW<LaneFollower>, RefRO<Velocity>, RefRO<WorldTransform>,
                               RefRO<DamageState>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>()
                    .WithEntityAccess())
            {
                float steerInput = input.ValueRO.Steer;
                float myZ = transform.ValueRO.Position.z;
                float mySpeed = velocity.ValueRO.Forward;

                // =============================================================
                // Phase 2 Damage: Steering Component Effects
                // =============================================================
                // Optional components; default to full health when absent

                var health = SystemAPI.HasComponent<ComponentHealth>(entity)
                    ? SystemAPI.GetComponent<ComponentHealth>(entity)
                    : ComponentHealth.FullHealth;
                var failures = SystemAPI.HasComponent<ComponentFailureState>(entity)
                    ? SystemAPI.GetComponent<ComponentFailureState>(entity)
                    : default;

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
                    // A change is already in flight; drop any queued request
                    steeringState.ValueRW.LaneChangeRequested = false;

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
                        // Abort: run the same blend backwards from where the car
                        // is (LaneMagnetismSystem blends with 1 - λ while
                        // aborting). Mirroring the timer keeps the lateral target
                        // continuous (smoothstep is symmetric: 1 - λ(1-t) == λ(t)),
                        // so the car reverses smoothly instead of snapping back,
                        // and the lanes are left alone so CurrentLane never names
                        // a lane the car didn't reach
                        steeringState.ValueRW.LaneChangeAborting = !steeringState.ValueRO.LaneChangeAborting;
                        steeringState.ValueRW.LaneChangeDir = -dir;
                        steeringState.ValueRW.LaneChangeTimer = duration * (1f - t);
                    }
                    else if (t >= 1f)
                    {
                        // Lane change complete; an aborted one ends where it started
                        steeringState.ValueRW.ChangingLanes = false;
                        steeringState.ValueRW.LaneChangeTimer = 0f;
                        if (steeringState.ValueRO.LaneChangeAborting)
                            laneFollower.ValueRW.TargetLane = laneFollower.ValueRO.CurrentLane;
                        else
                            laneFollower.ValueRW.CurrentLane = laneFollower.ValueRO.TargetLane;
                        steeringState.ValueRW.LaneChangeAborting = false;
                    }
                }
                else
                {
                    // =============================================================
                    // Check for Lane Change Trigger
                    // =============================================================
                    // Either the driver steers past the threshold, or the autopilot
                    // queued an explicit request (hazard avoidance). Both go
                    // through the same blocked-lane safety check.

                    bool wantsChange = false;
                    int direction = 0;

                    if (steeringState.ValueRO.LaneChangeRequested)
                    {
                        direction = math.clamp(steeringState.ValueRO.LaneChangeDirection, -1, 1);
                        wantsChange = direction != 0;
                        steeringState.ValueRW.LaneChangeRequested = false;
                        steeringState.ValueRW.LaneChangeDirection = 0;
                    }
                    else if (math.abs(steerInput) > VehicleDynamics.LaneChangeSteerThreshold)
                    {
                        direction = steerInput > 0 ? 1 : -1;
                        wantsChange = true;
                    }

                    if (wantsChange)
                    {
                        int currentLane = laneFollower.ValueRO.CurrentLane;
                        int targetLane = currentLane + direction;

                        // Check lane bounds
                        if (targetLane >= 0 && targetLane < NumLanes)
                        {
                            // Speed-aware duration per spec: T = clamp(T_base × (v / v_ref), T_min, T_max)
                            // Higher speed → longer duration (harder to change lanes at speed)
                            float duration = HazardPlacement.LaneChangeDuration(mySpeed);

                            // Check if lane is blocked
                            bool isBlocked = IsLaneBlocked(myZ, mySpeed, duration, targetLane, ref blockers);

                            if (!isBlocked)
                            {
                                // Start lane change
                                steeringState.ValueRW.ChangingLanes = true;
                                steeringState.ValueRW.LaneChangeAborting = false;
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

            // Traffic lane changes are advanced and completed by
            // TrafficMovementSystem alone (advancing them here as well ran
            // every traffic lane change at double speed)

            blockers.Dispose();
        }

        private bool IsLaneBlocked(float myZ, float mySpeed, float laneChangeDuration, int targetLane,
                                   ref NativeList<BlockerInfo> blockers)
        {
            for (int i = 0; i < blockers.Length; i++)
            {
                var b = blockers[i];
                if (b.Lane != targetLane) continue;

                float dz = b.Z - myZ;

                // Road closed on the blocker while the change is under way
                float relSpeed = mySpeed - b.Speed;
                float ahead = BlockCheckAhead + math.max(0f, relSpeed) * laneChangeDuration;
                float behind = BlockCheckBehind + math.max(0f, -relSpeed) * BlockBehindSeconds;

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
