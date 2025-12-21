// ============================================================================
// Nightflow - Emergency Vehicle System
// Execution Order: 11 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Controls emergency vehicle behavior and player detection/avoidance.
    /// Emergency vehicles create "fear corridor" that traffic and player must avoid.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TrafficAISystem))]
    public partial struct EmergencyVehicleSystem : ISystem
    {
        // Emergency vehicle parameters
        private const float EmergencySpeed = 45f;         // m/s - faster than traffic
        private const float SirenRadius = 80f;            // meters - audio/visual range
        private const float FearCorridorWidth = 2.5f;     // lanes to clear
        private const float ApproachWarningTime = 3f;     // seconds ahead to warn

        // Detection parameters
        private const float DetectionRange = 150f;        // meters behind player
        private const float OvertakeBuffer = 30f;         // meters past player before despawn

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get player state
            float3 playerPos = float3.zero;
            int playerLane = 0;
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
            // Update Emergency Vehicles
            // =============================================================

            foreach (var (emergencyAI, velocity, transform, laneFollower, lightEmitter) in
                SystemAPI.Query<RefRW<EmergencyAI>, RefRW<Velocity>,
                               RefRW<WorldTransform>, RefRW<LaneFollower>, RefRW<LightEmitter>>()
                    .WithAll<EmergencyVehicleTag>())
            {
                // Maintain emergency speed
                velocity.ValueRW.Forward = EmergencySpeed;

                // Update approach distance to player
                float distToPlayer = playerPos.z - transform.ValueRO.Position.z;
                emergencyAI.ValueRW.ApproachDistance = distToPlayer;

                // =============================================================
                // Siren State
                // =============================================================

                bool inRange = math.abs(distToPlayer) < SirenRadius;
                emergencyAI.ValueRW.SirenActive = inRange;
                lightEmitter.ValueRW.Intensity = inRange ? 1f : 0.3f;

                // Flash pattern (alternate left/right)
                float flashPhase = (float)SystemAPI.Time.ElapsedTime * 8f;
                lightEmitter.ValueRW.FlashPhase = flashPhase;

                // =============================================================
                // Lane Selection (Approach from optimal lane)
                // =============================================================

                if (distToPlayer > 50f)
                {
                    // Approaching from behind - pick lane away from player
                    int targetLane = (playerLane <= 1) ? 3 : 0;
                    if (laneFollower.ValueRO.CurrentLane != targetLane)
                    {
                        laneFollower.ValueRW.TargetLane = targetLane;
                    }
                }

                // =============================================================
                // Position Update
                // =============================================================

                float3 forward = new float3(0, 0, 1);
                transform.ValueRW.Position += forward * velocity.ValueRO.Forward * deltaTime;
            }

            // =============================================================
            // Update Player Emergency Detection
            // =============================================================

            foreach (var (detection, transform) in
                SystemAPI.Query<RefRW<EmergencyDetection>, RefRO<WorldTransform>>()
                    .WithAll<PlayerVehicleTag>())
            {
                detection.ValueRW.NearestDistance = float.MaxValue;
                detection.ValueRW.ApproachingFromBehind = false;

                foreach (var (emergencyAI, emergencyTransform, emergencyLane) in
                    SystemAPI.Query<RefRO<EmergencyAI>, RefRO<WorldTransform>, RefRO<LaneFollower>>()
                        .WithAll<EmergencyVehicleTag>())
                {
                    float dist = transform.ValueRO.Position.z - emergencyTransform.ValueRO.Position.z;

                    if (dist > 0 && dist < detection.ValueRO.NearestDistance)
                    {
                        detection.ValueRW.NearestDistance = dist;
                        detection.ValueRW.ApproachingFromBehind = true;
                        detection.ValueRW.EmergencyLane = emergencyLane.ValueRO.CurrentLane;
                        detection.ValueRW.TimeToArrival = dist / EmergencySpeed;
                    }
                }

                // Set warning state
                detection.ValueRW.WarningActive = detection.ValueRO.ApproachingFromBehind &&
                                                   detection.ValueRO.TimeToArrival < ApproachWarningTime;
            }
        }
    }
}
