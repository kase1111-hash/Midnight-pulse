// ============================================================================
// Nightflow - Traffic AI System
// Execution Order: 10 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;
using Nightflow.Utilities;

namespace Nightflow.Systems
{
    /// <summary>
    /// Controls AI traffic vehicles using lane desirability scoring.
    ///
    /// Lane Score Function (from spec):
    /// S_i = w_s·S_speed + w_d·S_density + w_e·S_emergency + w_h·S_hazard + w_p·S_player + w_m·S_merge
    ///
    /// Features:
    /// - Decision hysteresis: ΔS > θ (0.15) required to change
    /// - Commitment lock: 1.2s during lane change
    /// - Traffic yields to emergency vehicles
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ScoringSystem))]
    public partial struct TrafficAISystem : ISystem
    {
        // Scoring weights (from spec)
        private const float WeightSpeed = 0.35f;          // w_s
        private const float WeightDensity = 0.25f;        // w_d
        private const float WeightEmergency = 0.4f;       // w_e
        private const float WeightHazard = 0.3f;          // w_h
        private const float WeightPlayer = 0.15f;         // w_p
        private const float WeightMerge = 0.3f;           // w_m

        // Decision parameters (from spec)
        private const float DecisionInterval = 0.5f;      // seconds between evaluations
        private const float HysteresisThreshold = 0.15f;  // θ - minimum ΔS to change
        private const float CommitmentLock = 1.2f;        // seconds locked during change
        private const float LookAheadDistance = 50f;      // gap detection range
        private const float LaneWidth = 3.6f;

        // Density parameters
        private const float DensityDecay = 0.5f;          // k_d for exponential decay
        private const float SafeDistance = 30f;           // d_safe for hazard scoring

        // Speed control
        private const float AccelRate = 8f;               // m/s²
        private const float DecelRate = 12f;              // m/s²
        private const float MinSpeed = 15f;               // m/s minimum traffic speed

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // =============================================================
            // Gather Player State
            // =============================================================

            float3 playerPos = float3.zero;
            int playerLane = 1;
            float playerSpeed = 0f;

            foreach (var (transform, laneFollower, velocity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<LaneFollower>, RefRO<Velocity>>()
                    .WithAll<PlayerVehicleTag>())
            {
                playerPos = transform.ValueRO.Position;
                playerLane = laneFollower.ValueRO.CurrentLane;
                playerSpeed = velocity.ValueRO.Forward;
                break;
            }

            // =============================================================
            // Gather Emergency Vehicle State
            // =============================================================

            float3 emergencyPos = float3.zero;
            int emergencyLane = -1;
            bool emergencyApproaching = false;
            float emergencyDistance = 999f;

            foreach (var (transform, laneFollower, emergencyAI) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<LaneFollower>, RefRO<EmergencyAI>>()
                    .WithAll<EmergencyVehicleTag>())
            {
                if (emergencyAI.ValueRO.SirenActive)
                {
                    emergencyPos = transform.ValueRO.Position;
                    emergencyLane = laneFollower.ValueRO.CurrentLane;
                    emergencyApproaching = true;
                    break;
                }
            }

            // =============================================================
            // Build Traffic Position List for Density Calculation
            // =============================================================

            var trafficData = new NativeList<TrafficPositionData>(Allocator.Temp);

            foreach (var (transform, laneFollower, velocity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<LaneFollower>, RefRO<Velocity>>()
                    .WithAll<TrafficVehicleTag>())
            {
                trafficData.Add(new TrafficPositionData
                {
                    Position = transform.ValueRO.Position,
                    Lane = laneFollower.ValueRO.CurrentLane,
                    Speed = velocity.ValueRO.Forward
                });
            }

            // =============================================================
            // Process Each Traffic Vehicle
            // =============================================================

            foreach (var (trafficAI, laneScore, velocity, transform, laneFollower, steeringState) in
                SystemAPI.Query<RefRW<TrafficAI>, RefRW<LaneScoreCache>,
                               RefRW<Velocity>, RefRO<WorldTransform>,
                               RefRW<LaneFollower>, RefRW<SteeringState>>()
                    .WithAll<TrafficVehicleTag>())
            {
                float3 myPos = transform.ValueRO.Position;
                int currentLane = laneFollower.ValueRO.CurrentLane;

                // =============================================================
                // Update Timers
                // =============================================================

                // Lane change lock timer
                if (trafficAI.ValueRO.LaneChangeLock)
                {
                    trafficAI.ValueRW.LaneChangeTimer -= deltaTime;
                    if (trafficAI.ValueRO.LaneChangeTimer <= 0)
                    {
                        trafficAI.ValueRW.LaneChangeLock = false;
                    }
                }

                // Decision timer
                trafficAI.ValueRW.DecisionTimer -= deltaTime;

                if (trafficAI.ValueRO.DecisionTimer > 0 || trafficAI.ValueRO.LaneChangeLock)
                {
                    // Still waiting, just do speed control
                    UpdateSpeed(ref velocity.ValueRW, trafficAI.ValueRO.TargetSpeed, deltaTime);
                    continue;
                }

                trafficAI.ValueRW.DecisionTimer = DecisionInterval;

                // =============================================================
                // Calculate Lane Scores
                // =============================================================

                float currentLaneScore = 0f;
                float bestScore = float.MinValue;
                int bestLane = currentLane;

                for (int lane = 0; lane < 4; lane++)
                {
                    // Skip non-adjacent lanes (can only change one lane at a time)
                    if (math.abs(lane - currentLane) > 1 && lane != currentLane)
                        continue;

                    float score = CalculateLaneScore(
                        lane, currentLane, myPos,
                        playerPos, playerLane, playerSpeed,
                        emergencyPos, emergencyLane, emergencyApproaching,
                        ref trafficData, trafficAI.ValueRO.TargetSpeed
                    );

                    // Cache scores
                    if (lane == 0) laneScore.ValueRW.Lane0 = score;
                    else if (lane == 1) laneScore.ValueRW.Lane1 = score;
                    else if (lane == 2) laneScore.ValueRW.Lane2 = score;
                    else if (lane == 3) laneScore.ValueRW.Lane3 = score;

                    if (lane == currentLane)
                        currentLaneScore = score;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestLane = lane;
                    }
                }

                // =============================================================
                // Decision with Hysteresis
                // =============================================================

                float scoreDelta = bestScore - currentLaneScore;

                if (bestLane != currentLane &&
                    scoreDelta > HysteresisThreshold &&
                    !steeringState.ValueRO.ChangingLanes)
                {
                    // Initiate lane change
                    steeringState.ValueRW.ChangingLanes = true;
                    steeringState.ValueRW.LaneChangeTimer = 0f;
                    steeringState.ValueRW.LaneChangeDir = bestLane > currentLane ? 1 : -1;
                    steeringState.ValueRW.LaneChangeDuration = 0.8f; // Slower than player

                    laneFollower.ValueRW.TargetLane = bestLane;

                    // Commitment lock
                    trafficAI.ValueRW.LaneChangeLock = true;
                    trafficAI.ValueRW.LaneChangeTimer = CommitmentLock;
                }

                // =============================================================
                // Emergency Yielding
                // =============================================================

                float targetSpeed = trafficAI.ValueRO.TargetSpeed;

                if (emergencyApproaching)
                {
                    float distToEmergency = math.distance(myPos, emergencyPos);
                    emergencyDistance = distToEmergency;

                    // Calculate urgency
                    float urgency = math.saturate(1f - distToEmergency / 120f);

                    if (urgency > 0.7f)
                    {
                        // Slow down significantly
                        targetSpeed *= (1f - 0.3f * urgency);
                    }
                }

                trafficAI.ValueRW.TargetSpeed = math.max(targetSpeed, MinSpeed);

                // =============================================================
                // Speed Control
                // =============================================================

                UpdateSpeed(ref velocity.ValueRW, trafficAI.ValueRO.TargetSpeed, deltaTime);
            }

            trafficData.Dispose();
        }

        private float CalculateLaneScore(
            int lane, int currentLane, float3 myPos,
            float3 playerPos, int playerLane, float playerSpeed,
            float3 emergencyPos, int emergencyLane, bool emergencyApproaching,
            ref NativeList<TrafficPositionData> trafficData,
            float targetSpeed)
        {
            float score = 0f;

            // =============================================================
            // S_speed: Speed Advantage
            // S_speed = clamp(v_i/v_t, 0, 1)
            // =============================================================

            // Check for slower vehicles ahead in this lane
            float minSpeedAhead = targetSpeed;
            for (int i = 0; i < trafficData.Length; i++)
            {
                var other = trafficData[i];
                if (other.Lane != lane) continue;

                float dz = other.Position.z - myPos.z;
                if (dz > 0 && dz < LookAheadDistance)
                {
                    minSpeedAhead = math.min(minSpeedAhead, other.Speed);
                }
            }

            float speedScore = math.saturate(minSpeedAhead / targetSpeed);
            score += WeightSpeed * speedScore;

            // =============================================================
            // S_density: Lane Density
            // S_density = e^(-k_d × n_i)
            // =============================================================

            int vehiclesInLane = 0;
            for (int i = 0; i < trafficData.Length; i++)
            {
                var other = trafficData[i];
                if (other.Lane == lane)
                {
                    float dist = math.abs(other.Position.z - myPos.z);
                    if (dist < LookAheadDistance)
                        vehiclesInLane++;
                }
            }

            float densityScore = math.exp(-DensityDecay * vehiclesInLane);
            score += WeightDensity * densityScore;

            // =============================================================
            // S_emergency: Emergency Pressure
            // S_emergency = 1 - u
            // =============================================================

            float emergencyScore = 1f;
            if (emergencyApproaching && emergencyLane >= 0)
            {
                float distToEmergency = math.distance(myPos, emergencyPos);
                float urgency = math.saturate(1f - distToEmergency / 120f);

                // Penalize being in emergency lane or adjacent
                if (lane == emergencyLane)
                    emergencyScore = 1f - urgency;
                else if (math.abs(lane - emergencyLane) == 1)
                    emergencyScore = 1f - urgency * 0.5f;
            }
            score += WeightEmergency * emergencyScore;

            // =============================================================
            // S_hazard: Hazard Avoidance
            // S_hazard = clamp(d_h/d_safe, 0, 1)
            // =============================================================

            // TODO: Query actual hazards
            float hazardScore = 1f;
            score += WeightHazard * hazardScore;

            // =============================================================
            // S_player: Player Proximity
            // S_player = clamp(d_p/w_lane, 0.3, 1)
            // =============================================================

            float distToPlayer = math.distance(myPos, playerPos);
            float dz = myPos.z - playerPos.z; // Positive = ahead of player

            float playerScore = 1f;
            if (lane == playerLane && math.abs(dz) < LookAheadDistance)
            {
                // Avoid crowding player's lane
                playerScore = math.clamp(distToPlayer / (LaneWidth * 5f), 0.3f, 1f);
            }
            score += WeightPlayer * playerScore;

            // =============================================================
            // S_merge: Merge Logic (stay in current lane slight preference)
            // =============================================================

            float mergeScore = (lane == currentLane) ? 1f : 0.85f;
            score += WeightMerge * mergeScore;

            return score;
        }

        private void UpdateSpeed(ref Velocity velocity, float targetSpeed, float deltaTime)
        {
            float currentSpeed = velocity.Forward;

            if (currentSpeed < targetSpeed - 1f)
            {
                velocity.Forward += AccelRate * deltaTime;
            }
            else if (currentSpeed > targetSpeed + 1f)
            {
                velocity.Forward -= DecelRate * deltaTime;
            }

            velocity.Forward = math.max(velocity.Forward, MinSpeed);
        }

        private struct TrafficPositionData
        {
            public float3 Position;
            public int Lane;
            public float Speed;
        }

        private const float LookAheadDistance = 50f;
    }
}
