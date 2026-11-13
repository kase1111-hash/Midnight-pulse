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
    /// AI-driven vehicle control when autopilot is enabled: attract mode under
    /// the menus, after a crash, and when the player goes idle.
    ///
    /// From spec:
    /// - Lane-following at medium speed
    /// - Avoids hazards
    /// - Maintains visual flow
    /// - Menu overlay stays active
    ///
    /// The autopilot drives like a calm human: lane magnetism keeps it centred
    /// (it never steers against the road centre), lane changes go through the
    /// same request/blocked-lane path as the player's, and speed changes ease
    /// in at comfortable rates. Taking the wheel from a fast player it eases
    /// down from that speed instead of braking hard, so the handoff is seamless;
    /// while the player's run is suspended underneath (pause menu, idle) it
    /// keeps the run's speed instead, so resuming never loses momentum.
    ///
    /// A lane change it wants is asked for every frame until SteeringSystem
    /// starts it (a blocked lane, e.g. traffic alongside, only defers it; the
    /// other side is tried in turn when it is a way out too), and its own
    /// changes take a calm AutopilotLogic.LaneChangeDuration. A run paused
    /// above the speed its tyres let it hold is brought down to it at
    /// AutopilotLogic.SettleDecel.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AutopilotSystem : ISystem
    {
        // Speed control. The cruise target is Autopilot.TargetSpeed: the
        // comfort band (20-35 m/s) in attract mode, or the run's own speed
        // (up to GameFlowLogic.SuspendedRunHoldSpeed for the car's grip) while
        // a paused or idle run waits underneath
        private const float MinAutopilotSpeed = 15f;    // m/s (~54 km/h)

        // Steering: a small lean toward the way out of a hazard, always below
        // the lane-change threshold (lane changes are explicit requests)
        private const float MaxLeanSteer = 0.25f;
        private const float SteerSmoothing = 5f;        // 1/s

        // Hazard avoidance (the lane-change cooldown, look-ahead and duration
        // live in AutopilotLogic)
        // TODO: Autopilot should vary its driving style (cautious vs aggressive) based on player's typical playstyle
        private const float MinHazardDetectionRange = 80f;
        private const float HazardAvoidanceStrength = 1.5f;
        private const float LaneChangeThreshold = 0.5f;

        // Emergency response
        private const float EmergencySlowdownFactor = 0.6f;

        // State (single player vehicle)
        private float _smoothedSteer;
        private float _laneChangeTimer;
        private float _easedSpeed;
        private bool _wasEnabled;

        // Lane change asked for last frame (0 = none), and whether one was
        // under way: a request consumed without a change starting was turned
        // down, and the cooldown starts with the change itself
        private int _requestedDirection;
        private bool _wasChangingLanes;

        public void OnCreate(ref SystemState state)
        {
            _smoothedSteer = 0f;
            _laneChangeTimer = 0f;
            _easedSpeed = 0f;
            _wasEnabled = false;
            _requestedDirection = 0;
            _wasChangingLanes = false;
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

            foreach (var (autopilot, input, laneFollower, velocity, steeringState, transform, detection, entity) in
                SystemAPI.Query<RefRO<Autopilot>, RefRW<PlayerInput>, RefRO<LaneFollower>,
                               RefRO<Velocity>, RefRW<SteeringState>, RefRO<WorldTransform>,
                               RefRO<EmergencyDetection>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>()
                    .WithEntityAccess())
            {
                float currentSpeed = velocity.ValueRO.Forward;
                bool changingLanes = steeringState.ValueRO.ChangingLanes;

                if (!autopilot.ValueRO.Enabled)
                {
                    // A request still waiting for SteeringSystem was ours, not the player's
                    if (_requestedDirection != 0 && steeringState.ValueRO.LaneChangeRequested)
                    {
                        steeringState.ValueRW.LaneChangeRequested = false;
                        steeringState.ValueRW.LaneChangeDirection = 0;
                    }
                    _wasEnabled = false;
                    _smoothedSteer = 0f;
                    _requestedDirection = 0;
                    _wasChangingLanes = changingLanes;
                    continue;
                }

                // Taking the wheel: start from the car's speed and lean, so the
                // handoff itself never jolts the car
                if (!_wasEnabled)
                {
                    _wasEnabled = true;
                    _easedSpeed = currentSpeed;
                    _smoothedSteer = input.ValueRO.Steer;
                }

                float3 playerPos = transform.ValueRO.Position;

                // =============================================================
                // Hazard Detection
                // =============================================================

                // Lanes, not world x: the road curves away from x = 0, so a
                // hazard's x says nothing about which lane it is in
                int laneCount = laneFollower.ValueRO.TotalLanes > 0
                    ? laneFollower.ValueRO.TotalLanes
                    : GameConstants.DefaultNumLanes;
                int myLane = AutopilotLogic.DrivingLane(
                    laneFollower.ValueRO.CurrentLane,
                    laneFollower.ValueRO.TargetLane,
                    steeringState.ValueRO.ChangingLanes && !steeringState.ValueRO.LaneChangeAborting);

                // Look a few seconds ahead, not a fixed distance: at speed the
                // fixed range would leave no time to react
                float detectionRange = AutopilotLogic.DetectionRange(currentSpeed, MinHazardDetectionRange);

                float4 laneThreat = float4.zero;
                float nearestHazardDist = detectionRange;

                foreach (var (hazardTransform, hazard) in
                    SystemAPI.Query<RefRO<WorldTransform>, RefRO<Hazard>>()
                        .WithAll<HazardTag>())
                {
                    if (hazard.ValueRO.Hit)
                        continue;

                    // Road heading stays within ~25 deg of +Z, so z is a good
                    // stand-in for distance along the road
                    float forwardDist = hazardTransform.ValueRO.Position.z - playerPos.z;
                    float threat = AutopilotLogic.HazardThreat(
                        hazard.ValueRO.Severity, forwardDist, detectionRange);

                    if (threat <= 0f)
                        continue;

                    laneThreat = AutopilotLogic.AccumulateThreat(laneThreat, hazard.ValueRO.Lane, threat);

                    if (hazard.ValueRO.Lane == myLane && forwardDist < nearestHazardDist)
                        nearestHazardDist = forwardDist;
                }

                float hazardThreat = myLane >= 0 && myLane < AutopilotLogic.MaxLanes ? laneThreat[myLane] : 0f;

                // Way out if the lane ahead is dangerous: the clearer neighbour
                int escapeDirection = AutopilotLogic.ChooseEscapeDirection(laneThreat, myLane, laneCount);

                // =============================================================
                // Emergency Vehicle Response
                // =============================================================
                // Yielding sideways is done by LaneMagnetismSystem through
                // EmergencyDetection.AvoidanceOffset; here we only slow down

                float emergencySpeedMod = 1f;
                if (detection.ValueRO.WarningActive)
                {
                    emergencySpeedMod = 1f - detection.ValueRO.Urgency * (1f - EmergencySlowdownFactor);
                }

                // What the car's own (possibly worn) tyres let it hold: caps a
                // held run's speed and sets how calmly it changes lanes
                float gripShare = 1f;
                if (SystemAPI.HasComponent<ComponentHealth>(entity) &&
                    SystemAPI.HasComponent<ComponentFailureState>(entity))
                {
                    var health = SystemAPI.GetComponent<ComponentHealth>(entity);
                    var failures = SystemAPI.GetComponent<ComponentFailureState>(entity);
                    gripShare = VehicleDynamics.GripShare(
                        health.Tires, failures.HasFailed(ComponentFailures.Tires),
                        health.Suspension, failures.HasFailed(ComponentFailures.Suspension));
                }
                float holdSpeed = GameFlowLogic.SuspendedRunHoldSpeed(gripShare);

                // =============================================================
                // Lane Change Decision
                // =============================================================

                // The cooldown runs from the start of each change
                if (changingLanes && !_wasChangingLanes)
                {
                    _laneChangeTimer = AutopilotLogic.LaneChangeCooldown;
                }
                _wasChangingLanes = changingLanes;

                // Last frame's request was consumed without a change starting:
                // SteeringSystem found that lane blocked
                int refusedDirection = !changingLanes && !steeringState.ValueRO.LaneChangeRequested
                    ? _requestedDirection
                    : 0;
                _requestedDirection = 0;

                // Leave the lane for a serious threat, or for anything at all it
                // would otherwise run over in the next couple of seconds
                int laneEscape = AutopilotLogic.LaneEscape(laneThreat, myLane, laneCount,
                    nearestHazardDist, currentSpeed, LaneChangeThreshold, refusedDirection);
                bool wantsLaneChange = laneEscape != 0 && _laneChangeTimer <= 0f && !changingLanes;

                // =============================================================
                // Speed Control
                // =============================================================

                // Cruise target, slowed for emergencies and hazards close ahead.
                // A held run's speed is capped by what the car's own (possibly
                // worn) tyres can take, so it never slides under the menu
                float cruiseSpeed = math.clamp(autopilot.ValueRO.TargetSpeed, MinAutopilotSpeed, holdSpeed);
                float goalSpeed = cruiseSpeed * emergencySpeedMod;
                if (hazardThreat > 0.3f && nearestHazardDist < detectionRange * 0.5f)
                {
                    goalSpeed *= 1f - hazardThreat * 0.4f;
                }
                goalSpeed = math.clamp(goalSpeed, MinAutopilotSpeed, cruiseSpeed);

                // Ease the working target toward the goal at comfortable rates
                // (shedding a run paused above the hold speed twice as fast),
                // never from above a car that something else already slowed
                // (RESTART resets the car in place without disengaging us)
                _easedSpeed = AutopilotLogic.EaseTargetSpeed(_easedSpeed, goalSpeed, currentSpeed, holdSpeed, deltaTime);

                AutopilotLogic.SpeedControl(currentSpeed, _easedSpeed, out float throttle, out float brake);
                input.ValueRW.Throttle = throttle;
                input.ValueRW.Brake = brake;

                // =============================================================
                // Steering
                // =============================================================

                // Lane magnetism (x1.5 for the autopilot) keeps the car centred in
                // its lane; steering here only leans toward the way out while a
                // hazard closes in (also while a lane change waits for a gap),
                // and always stays below the lane-change threshold so the car
                // never weaves on its own. The lean points where it is asking
                // to go, so a change that starts hands it over the usual way
                int leanDirection = wantsLaneChange ? laneEscape : escapeDirection;
                float leanTarget = 0f;
                if (hazardThreat > 0.2f && !changingLanes && leanDirection != 0)
                {
                    leanTarget = leanDirection * hazardThreat * HazardAvoidanceStrength;
                }
                leanTarget = math.clamp(leanTarget, -MaxLeanSteer, MaxLeanSteer);

                _smoothedSteer = math.lerp(_smoothedSteer, leanTarget,
                    VehicleDynamics.Damp(SteerSmoothing, deltaTime));
                input.ValueRW.Steer = math.clamp(_smoothedSteer, -MaxLeanSteer, MaxLeanSteer);

                // =============================================================
                // Lane Change Execution
                // =============================================================

                if (wantsLaneChange)
                {
                    // Request lane change through steering state (same blocked-lane
                    // checks as the player); asked again next frame if refused
                    steeringState.ValueRW.LaneChangeRequested = true;
                    steeringState.ValueRW.LaneChangeDirection = laneEscape;
                    _requestedDirection = laneEscape;
                }

                // No handbrake during autopilot
                input.ValueRW.Handbrake = false;
            }
        }
    }
}
