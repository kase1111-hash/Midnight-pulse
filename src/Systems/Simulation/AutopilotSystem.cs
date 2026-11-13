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
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AutopilotSystem : ISystem
    {
        // Speed control. The cruise target is Autopilot.TargetSpeed: the
        // comfort band (20-35 m/s) in attract mode, or the run's own speed
        // (up to GameFlowLogic.SuspendedRunMaxSpeed) while a paused or idle
        // run waits underneath
        private const float MinAutopilotSpeed = 15f;    // m/s (~54 km/h)

        // Steering: a small lean toward the way out of a hazard, always below
        // the lane-change threshold (lane changes are explicit requests)
        private const float MaxLeanSteer = 0.25f;
        private const float SteerSmoothing = 5f;        // 1/s

        // Hazard avoidance
        // TODO: Autopilot should vary its driving style (cautious vs aggressive) based on player's typical playstyle
        private const float MinHazardDetectionRange = 80f;
        private const float HazardAvoidanceStrength = 1.5f;
        private const float LaneChangeThreshold = 0.5f;
        private const float LaneChangeCooldown = 3f;    // s

        // Emergency response
        private const float EmergencySlowdownFactor = 0.6f;

        // State (single player vehicle)
        private float _smoothedSteer;
        private float _laneChangeTimer;
        private float _easedSpeed;
        private bool _wasEnabled;

        public void OnCreate(ref SystemState state)
        {
            _smoothedSteer = 0f;
            _laneChangeTimer = 0f;
            _easedSpeed = 0f;
            _wasEnabled = false;
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
                float currentSpeed = velocity.ValueRO.Forward;

                if (!autopilot.ValueRO.Enabled)
                {
                    _wasEnabled = false;
                    _smoothedSteer = 0f;
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

                // =============================================================
                // Lane Change Decision
                // =============================================================

                bool shouldChangeLane = false;
                if (hazardThreat > LaneChangeThreshold && _laneChangeTimer <= 0 && escapeDirection != 0)
                {
                    shouldChangeLane = true;
                    _laneChangeTimer = LaneChangeCooldown;
                }

                // =============================================================
                // Speed Control
                // =============================================================

                // Cruise target, slowed for emergencies and hazards close ahead
                float cruiseSpeed = math.clamp(autopilot.ValueRO.TargetSpeed,
                    MinAutopilotSpeed, GameFlowLogic.SuspendedRunMaxSpeed);
                float goalSpeed = cruiseSpeed * emergencySpeedMod;
                if (hazardThreat > 0.3f && nearestHazardDist < detectionRange * 0.5f)
                {
                    goalSpeed *= 1f - hazardThreat * 0.4f;
                }
                goalSpeed = math.clamp(goalSpeed, MinAutopilotSpeed, cruiseSpeed);

                // Ease the working target toward the goal at comfortable rates,
                // never from above a car that something else already slowed
                // (RESTART resets the car in place without disengaging us)
                _easedSpeed = AutopilotLogic.EaseTargetSpeed(_easedSpeed, goalSpeed, currentSpeed, deltaTime);

                AutopilotLogic.SpeedControl(currentSpeed, _easedSpeed, out float throttle, out float brake);
                input.ValueRW.Throttle = throttle;
                input.ValueRW.Brake = brake;

                // =============================================================
                // Steering
                // =============================================================

                // Lane magnetism (x1.5 for the autopilot) keeps the car centred in
                // its lane; steering here only leans toward the way out while a
                // hazard closes in, and always stays below the lane-change
                // threshold so the car never weaves on its own
                float leanTarget = 0f;
                if (hazardThreat > 0.2f && !shouldChangeLane && escapeDirection != 0)
                {
                    leanTarget = escapeDirection * hazardThreat * HazardAvoidanceStrength;
                }
                leanTarget = math.clamp(leanTarget, -MaxLeanSteer, MaxLeanSteer);

                _smoothedSteer = math.lerp(_smoothedSteer, leanTarget,
                    VehicleDynamics.Damp(SteerSmoothing, deltaTime));
                input.ValueRW.Steer = math.clamp(_smoothedSteer, -MaxLeanSteer, MaxLeanSteer);

                // =============================================================
                // Lane Change Execution
                // =============================================================

                if (shouldChangeLane && !steeringState.ValueRO.ChangingLanes)
                {
                    // Request lane change through steering state (same blocked-lane checks as the player)
                    steeringState.ValueRW.LaneChangeRequested = true;
                    steeringState.ValueRW.LaneChangeDirection = escapeDirection;
                }

                // No handbrake during autopilot
                input.ValueRW.Handbrake = false;
            }
        }
    }
}
