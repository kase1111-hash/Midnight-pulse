// ============================================================================
// Nightflow - Traffic AI System
// Execution Order: 10 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Controls AI traffic vehicles using lane desirability scoring.
    /// D_lane = w_s × S_speed + w_g × S_gap + w_p × S_player + w_e × S_emergency
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ScoringSystem))]
    public partial struct TrafficAISystem : ISystem
    {
        // Scoring weights
        private const float WeightSpeed = 0.3f;           // w_s
        private const float WeightGap = 0.4f;             // w_g
        private const float WeightPlayer = 0.2f;          // w_p
        private const float WeightEmergency = 0.1f;       // w_e

        // Decision parameters
        private const float DecisionInterval = 0.5f;      // seconds between evaluations
        private const float MinGapForChange = 15f;        // meters required
        private const float LookAheadDistance = 50f;      // gap detection range
        private const float PlayerProximityBonus = 20f;   // meters for player avoidance

        // Speed parameters
        private const float SpeedVariance = 0.15f;        // ±15% from flow speed
        private const float FlowSpeed = 25f;              // m/s base traffic speed

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float time = (float)SystemAPI.Time.ElapsedTime;

            // Get player position for proximity scoring
            float3 playerPos = float3.zero;
            int playerLane = 0;
            foreach (var (transform, laneFollower) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<LaneFollower>>()
                    .WithAll<PlayerVehicleTag>())
            {
                playerPos = transform.ValueRO.Position;
                playerLane = laneFollower.ValueRO.CurrentLane;
                break;
            }

            foreach (var (trafficAI, laneScore, velocity, transform, laneFollower, steeringState) in
                SystemAPI.Query<RefRW<TrafficAI>, RefRW<LaneScoreCache>,
                               RefRW<Velocity>, RefRO<WorldTransform>,
                               RefRW<LaneFollower>, RefRW<SteeringState>>()
                    .WithAll<TrafficVehicleTag>())
            {
                // =============================================================
                // Update Decision Timer
                // =============================================================

                trafficAI.ValueRW.DecisionTimer -= deltaTime;

                if (trafficAI.ValueRO.DecisionTimer > 0)
                    continue;

                trafficAI.ValueRW.DecisionTimer = DecisionInterval;

                int currentLane = laneFollower.ValueRO.CurrentLane;
                int numLanes = 4; // TODO: Get from track data

                // =============================================================
                // Score Each Lane
                // =============================================================

                float bestScore = float.MinValue;
                int bestLane = currentLane;

                for (int lane = 0; lane < numLanes; lane++)
                {
                    float score = 0f;

                    // S_speed: Can we maintain target speed?
                    // TODO: Check for slower vehicles ahead in this lane
                    float speedScore = 1f; // Placeholder
                    score += WeightSpeed * speedScore;

                    // S_gap: Is there room to merge?
                    // TODO: Spatial query for gap detection
                    float gapScore = (lane == currentLane) ? 1f : 0.5f; // Placeholder
                    score += WeightGap * gapScore;

                    // S_player: Avoid getting too close to player
                    float distToPlayer = math.distance(transform.ValueRO.Position, playerPos);
                    float laneDiff = math.abs(lane - playerLane);
                    float playerScore = (distToPlayer > PlayerProximityBonus || laneDiff > 0) ? 1f : 0.3f;
                    score += WeightPlayer * playerScore;

                    // S_emergency: Clear path for emergency vehicles
                    // TODO: Check for approaching emergency vehicles
                    float emergencyScore = 1f; // Placeholder
                    score += WeightEmergency * emergencyScore;

                    // Penalize lane changes slightly
                    if (lane != currentLane)
                    {
                        score *= 0.9f;
                    }

                    // Cache scores for debugging
                    if (lane == 0) laneScore.ValueRW.Lane0 = score;
                    else if (lane == 1) laneScore.ValueRW.Lane1 = score;
                    else if (lane == 2) laneScore.ValueRW.Lane2 = score;
                    else if (lane == 3) laneScore.ValueRW.Lane3 = score;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestLane = lane;
                    }
                }

                // =============================================================
                // Execute Lane Change Decision
                // =============================================================

                if (bestLane != currentLane && !steeringState.ValueRO.ChangingLanes)
                {
                    // Initiate lane change
                    steeringState.ValueRW.ChangingLanes = true;
                    steeringState.ValueRW.LaneChangeTimer = 0f;
                    steeringState.ValueRW.LaneChangeDir = bestLane > currentLane ? 1 : -1;

                    laneFollower.ValueRW.TargetLane = bestLane;
                }

                // =============================================================
                // Maintain Target Speed
                // =============================================================

                // Add slight variance to avoid synchronized traffic
                float speedOffset = math.sin(time * 0.5f + trafficAI.ValueRO.DecisionTimer * 10f) * SpeedVariance;
                float targetSpeed = FlowSpeed * (1f + speedOffset);

                trafficAI.ValueRW.TargetSpeed = targetSpeed;

                // Simple speed control
                if (velocity.ValueRO.Forward < targetSpeed)
                {
                    velocity.ValueRW.Forward += 5f * deltaTime;
                }
                else if (velocity.ValueRO.Forward > targetSpeed + 2f)
                {
                    velocity.ValueRW.Forward -= 3f * deltaTime;
                }
            }
        }
    }
}
