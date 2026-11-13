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
    /// Moves the player car across the road: a critically damped lane spring
    /// toward its lane (or the lane-change blend between two lanes), limited by
    /// tyre grip. Writes the TractionState the rest of the game reads to know
    /// how close the car is to letting go.
    ///
    /// Spring: a_lat = m × (-ω²·(x - x_t) - 2ω·(ẋ - ẋ_t))
    ///
    /// Target x_t: lane centre, blended with smoothstep during a lane change,
    /// plus the smoothed analog in-lane lean (steering below the lane-change
    /// threshold) and the rate-limited emergency-vehicle yield. The spring
    /// tracks the lane-change blend's velocity too, so lane changes follow it
    /// without lag; the lean and the yield move the target's position only, so
    /// switching them on or off (a lane change starting with the key held, an
    /// emergency vehicle leaving the cone) never steps the spring's damper.
    ///
    /// Magnetism modulation m:
    /// - m_auto: 1.5 during autopilot (stronger centering)
    /// - m_speed: sqrt(v/v_ref), clamped [0.75, 1.25]
    /// - m_handbrake: 0.25 while the handbrake is held (allows drift)
    /// - m_drift: 0.3 while drifting
    /// - m_fork: eases to 0.7 across a fork, and back to 1 at 1/s after it
    /// - m_damage: LaneFollower.MagnetStrength, reduced by side damage
    ///
    /// Grip: the curve (v²κ) plus a speed-weighted share of the spring load the
    /// tyres. Past the limit the car runs wide of the lane and
    /// VehicleMovementSystem lets the rear step out (see VehicleDynamics).
    ///
    /// Integration is sub-stepped (VehicleDynamics.SubSteps), so the result is
    /// the same at 30, 60 or 144 fps.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SteeringSystem))]
    public partial struct LaneMagnetismSystem : ISystem
    {
        // Magnetism parameters (from spec)
        private const float ReferenceSpeed = 40f;         // m/s

        // Modulation multipliers
        private const float AutopilotMultiplier = 1.5f;
        private const float HandbrakeMultiplier = 0.25f;
        private const float DriftMultiplier = 0.3f;

        // Ride height above the road surface and how quickly the car settles onto it
        private const float RideHeight = 0.5f;            // m
        private const float RideHeightResponse = 10f;     // 1/s

        // Drift slip degradation (rear damage, tyres, suspension)
        private const float RearSlipPenalty = 0.6f;

        // Single player car: smoothed in-lane lean and emergency yield (m)
        private float _lean;
        private float _avoid;

        // The yield as applied to the target last frame, and how much of it a
        // lane change in flight was still applying (it fades with the blend)
        private float _avoidApplied;
        private float _changeYieldShare;
        private bool _wasChangingLanes;

        // Fork magnetism deficit (0 = none), eased back after a fork
        private float _forkDeficit;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            int steps = VehicleDynamics.SubSteps(deltaTime, out float h);
            if (steps == 0)
                return;

            // The wreck is skipped below; whatever the car was leaning into,
            // yielding to or recovering from belongs to the run that just ended,
            // so the reset car settles straight into its lane
            foreach (var _ in SystemAPI.Query<RefRO<CrashState>>().WithAll<PlayerVehicleTag, CrashedTag>())
            {
                _lean = 0f;
                _avoid = 0f;
                _avoidApplied = 0f;
                _changeYieldShare = 0f;
                _wasChangingLanes = false;
                _forkDeficit = 0f;
                break;
            }

            foreach (var (laneFollower, velocity, input, driftState, steeringState, transform, entity) in
                SystemAPI.Query<RefRW<LaneFollower>, RefRW<Velocity>, RefRO<PlayerInput>,
                               RefRO<DriftState>, RefRO<SteeringState>, RefRW<WorldTransform>>()
                    .WithAll<PlayerVehicleTag, Autopilot, EmergencyDetection>()
                    .WithNone<CrashedTag>()
                    .WithEntityAccess())
            {
                // Read-only companions fetched by entity to keep the query at 6 types
                var autopilot = SystemAPI.GetComponentRO<Autopilot>(entity);
                var detection = SystemAPI.GetComponentRO<EmergencyDetection>(entity);
                var environment = SystemAPI.HasComponent<EnvironmentState>(entity)
                    ? SystemAPI.GetComponent<EnvironmentState>(entity)
                    : default;

                float playerZ = transform.ValueRO.Position.z;

                // =============================================================
                // Find the track segment under the car
                // =============================================================

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

                // Guard against division by zero for zero-length segments
                float segmentLength = currentSegment.EndZ - currentSegment.StartZ;
                if (math.abs(segmentLength) < 0.001f)
                    continue;

                float segmentProgress = math.saturate((playerZ - currentSegment.StartZ) / segmentLength);

                SplineUtilities.BuildFrameAtT(
                    currentSpline.P0, currentSpline.T0,
                    currentSpline.P1, currentSpline.T1,
                    segmentProgress,
                    out float3 splinePos, out float3 forward, out float3 right, out float3 up);

                float curvature = SplineUtilities.EvaluateSignedCurvature(
                    currentSpline.P0, currentSpline.T0,
                    currentSpline.P1, currentSpline.T1,
                    segmentProgress);

                laneFollower.ValueRW.SplineParameter = segmentProgress;

                // =============================================================
                // Lane target (position and velocity)
                // =============================================================

                // Lane indices: 0, 1, 2, 3 (left to right)
                // Offsets from center: -1.5w, -0.5w, +0.5w, +1.5w
                float currentLaneOffset = (laneFollower.ValueRO.CurrentLane - 1.5f) * GameConstants.LaneWidth;
                float targetLaneOffset = (laneFollower.ValueRO.TargetLane - 1.5f) * GameConstants.LaneWidth;

                float targetLateral = currentLaneOffset;
                float targetVelocity = 0f;

                bool changingLanes = steeringState.ValueRO.ChangingLanes;
                float blend = 0f;
                float blendRate = 0f;
                if (changingLanes)
                {
                    // An aborted change runs the same blend backwards to CurrentLane
                    blend = VehicleDynamics.LaneBlend(
                        steeringState.ValueRO.LaneChangeTimer,
                        steeringState.ValueRO.LaneChangeDuration,
                        steeringState.ValueRO.LaneChangeAborting,
                        out blendRate);

                    targetLateral = math.lerp(currentLaneOffset, targetLaneOffset, blend);
                    targetVelocity = (targetLaneOffset - currentLaneOffset) * blendRate;
                }

                // Steering leans the car inside its lane (up to InLaneOffsetMax
                // at the lane-change threshold, holding there beyond it, e.g.
                // into a blocked lane or at the road edge); smoothed so it hands
                // over to a lane change (or back) without the target jumping.
                // Position only: its rate steps whenever the lean switches on or
                // off, and fed to the spring's damper that step would kick the
                // tyres (a held-key lane change would slide ~20 m/s early)
                float steer = input.ValueRO.Steer;
                float desiredLean = !changingLanes ? VehicleDynamics.InLaneOffset(steer) : 0f;
                float leanStart = _lean;
                _lean = VehicleDynamics.LeanAt(leanStart, desiredLean, deltaTime);

                // Emergency vehicle avoidance nudges the target to make room,
                // rate-limited and kept on the road (position only, as above).
                // A lane change in flight owns the car's sideways motion: the
                // yield holds still until it ends (still moving, its rate would
                // add to the change's and ask for more lateral speed than the
                // tyres give). A change the way the yield points takes it over:
                // it fades out with the blend, and when the change ends the
                // state keeps exactly what was still applied (none after a
                // completed change, all of it after an aborted one), so the
                // target never jumps. A yield pointing back at the lane being
                // left stays applied: fading it would add its width to the
                // change, and it sits on the inner side of the new lane, so it
                // can never reach the edge
                if (!changingLanes && _wasChangingLanes)
                    _avoid *= _changeYieldShare;
                _wasChangingLanes = changingLanes;

                float halfRoadWidth = currentSegment.NumLanes * GameConstants.LaneWidth * 0.5f;
                float avoidStart = _avoidApplied;
                if (!changingLanes)
                {
                    _avoid = VehicleDynamics.StepAvoidance(_avoid, detection.ValueRO.AvoidanceOffset,
                        targetLateral + _lean, halfRoadWidth, deltaTime);
                }

                // The spring's velocity target: the blend's rate plus the yield
                // fading with it (both smooth); the position path below moves the
                // blend at its rate and the applied yield along its own path
                float targetVelocityFeed = targetVelocity;
                _avoidApplied = _avoid;
                _changeYieldShare = 1f;
                if (changingLanes && VehicleDynamics.LaneChangeTakesYield(_avoid, targetLaneOffset - currentLaneOffset))
                {
                    _avoidApplied = VehicleDynamics.YieldDuringLaneChange(_avoid, blend, blendRate,
                        out float fadeVelocity);
                    _changeYieldShare = 1f - blend;
                    targetVelocityFeed += fadeVelocity;
                }

                // =============================================================
                // Magnetism modulation
                // =============================================================

                bool handbrake = input.ValueRO.Handbrake;
                bool drifting = driftState.ValueRO.IsDrifting;
                float speed = math.max(velocity.ValueRO.Forward, 0f);

                float mAuto = autopilot.ValueRO.Enabled ? AutopilotMultiplier : 1f;
                float mSpeed = math.clamp(math.sqrt(math.max(speed, 1f) / ReferenceSpeed), 0.75f, 1.25f);
                float mHandbrake = handbrake ? HandbrakeMultiplier : 1f;
                float mDrift = drifting ? DriftMultiplier : 1f;
                _forkDeficit = VehicleDynamics.StepForkMagnetism(_forkDeficit,
                    environment.AtFork, environment.ForkProgress, deltaTime);
                float mFork = 1f - _forkDeficit;
                float magnetism = laneFollower.ValueRO.MagnetStrength * mAuto * mSpeed * mHandbrake * mDrift * mFork;

                // =============================================================
                // Grip (tyres, suspension, handbrake, speed)
                // =============================================================

                var health = SystemAPI.HasComponent<ComponentHealth>(entity)
                    ? SystemAPI.GetComponent<ComponentHealth>(entity)
                    : ComponentHealth.FullHealth;
                var failures = SystemAPI.HasComponent<ComponentFailureState>(entity)
                    ? SystemAPI.GetComponent<ComponentFailureState>(entity)
                    : default;

                float grip = VehicleDynamics.GripLimit(speed,
                    health.Tires, failures.HasFailed(ComponentFailures.Tires),
                    health.Suspension, failures.HasFailed(ComponentFailures.Suspension),
                    handbrake, autopilot.ValueRO.Enabled);

                // Handbrake drift: the velocity follows the nose (k_slip), worse
                // with rear damage, worn tyres and suspension
                float slipCoupling = 0f;
                if (handbrake || drifting)
                {
                    float rearDamage = SystemAPI.HasComponent<DamageState>(entity)
                        ? SystemAPI.GetComponent<DamageState>(entity).Rear
                        : 0f;
                    float tireModifier = failures.HasFailed(ComponentFailures.Tires)
                        ? 1.8f
                        : 1f + (1f - health.Tires) * 0.5f;
                    float suspensionModifier = failures.HasFailed(ComponentFailures.Suspension)
                        ? 1.5f
                        : 1f + (1f - health.Suspension) * 0.3f;
                    slipCoupling = VehicleDynamics.SlipGain * (1f + RearSlipPenalty * rearDamage)
                        * tireModifier * suspensionModifier;
                }

                // =============================================================
                // Integrate (sub-stepped, frame-rate independent)
                // =============================================================

                bool hasTraction = SystemAPI.HasComponent<TractionState>(entity);
                var traction = hasTraction ? SystemAPI.GetComponent<TractionState>(entity) : default;

                float3 vehiclePos = transform.ValueRO.Position;
                float currentLateral = math.dot(vehiclePos - splinePos, right);

                var lateral = new LateralState
                {
                    Offset = currentLateral,
                    Velocity = velocity.ValueRO.Lateral,
                    Usage = traction.Usage,
                    LoadDirection = traction.LoadDirection
                };

                var lateralInput = new LateralInputs
                {
                    TargetOffset = targetLateral + _lean + _avoidApplied,
                    TargetVelocity = targetVelocityFeed,
                    Magnetism = magnetism,
                    CurveAccel = VehicleDynamics.CurveAcceleration(speed, curvature),
                    Grip = grip,
                    ForwardSpeed = speed,
                    HalfRoadWidth = halfRoadWidth,
                    SlipCoupling = slipCoupling,
                    Yaw = driftState.ValueRO.YawOffset
                };

                for (int i = 0; i < steps; i++)
                {
                    // The lane blend moves at its rate; the lean and the yield
                    // follow their exact paths through the frame, so one 30 fps
                    // frame and four 120 Hz frames give the spring the same target
                    lateralInput.TargetOffset = targetLateral + targetVelocity * (i * h)
                        + VehicleDynamics.LeanAt(leanStart, desiredLean, (i + 1) * h)
                        + math.lerp(avoidStart, _avoidApplied, (i + 1) / (float)steps);
                    VehicleDynamics.StepLateral(ref lateral, lateralInput, h);
                }

                // =============================================================
                // Write back
                // =============================================================

                float3 position = vehiclePos + right * (lateral.Offset - currentLateral);

                // Settle onto the road surface (the spline pitches; the car
                // advances along its tangent and would otherwise drift off it)
                float roadHeight = (splinePos + right * lateral.Offset + up * RideHeight).y;
                position.y = math.lerp(position.y, roadHeight, VehicleDynamics.Damp(RideHeightResponse, deltaTime));

                transform.ValueRW.Position = position;
                velocity.ValueRW.Lateral = lateral.Velocity;
                laneFollower.ValueRW.LateralOffset = lateral.Offset;

                if (hasTraction)
                {
                    traction.Usage = lateral.Usage;
                    traction.Grip = grip;
                    traction.CurveAccel = lateralInput.CurveAccel;
                    traction.LoadDirection = lateral.LoadDirection;
                    traction.Sliding = lateral.Usage > 1f;
                    traction.SlideTime = traction.Sliding ? traction.SlideTime + deltaTime : 0f;
                    SystemAPI.SetComponent(entity, traction);
                }
            }
        }
    }
}
