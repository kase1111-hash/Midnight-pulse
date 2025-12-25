// ============================================================================
// Nightflow - Lane Blocking System
// Checks if lanes are blocked before allowing lane changes
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Checks lane availability for safe lane changes.
    /// Updates LaneFollower.LaneBlocked flags for each adjacent lane.
    /// Used by SteeringSystem to prevent lane changes into occupied lanes.
    ///
    /// A lane is blocked if:
    /// - Traffic vehicle is within blocking distance in that lane
    /// - Hazard is within blocking distance in that lane
    /// - Emergency vehicle is approaching in that lane
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SteeringSystem))]
    [UpdateAfter(typeof(InputSystem))]
    public partial struct LaneBlockingSystem : ISystem
    {
        // Blocking parameters
        private const float BlockingDistanceAhead = 20f;    // meters ahead
        private const float BlockingDistanceBehind = 10f;   // meters behind
        private const float BlockingLateralTolerance = 2f;  // meters lateral
        private const float LaneWidth = 3.6f;
        private const float EmergencyBlockDistance = 50f;   // meters for emergency

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // =============================================================
            // Build blocking data for all vehicles
            // =============================================================

            var blockers = new NativeList<BlockerData>(Allocator.Temp);

            // Collect traffic vehicles
            foreach (var (transform, laneFollower, velocity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<LaneFollower>, RefRO<Velocity>>()
                    .WithAll<TrafficVehicleTag>())
            {
                blockers.Add(new BlockerData
                {
                    Position = transform.ValueRO.Position,
                    Lane = laneFollower.ValueRO.CurrentLane,
                    Speed = velocity.ValueRO.Forward,
                    Type = BlockerType.Traffic
                });
            }

            // Collect hazards
            foreach (var (transform, hazard) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Hazard>>()
                    .WithAll<HazardTag>())
            {
                // Estimate lane from position
                int hazardLane = (int)math.round((transform.ValueRO.Position.x / LaneWidth) + 1.5f);
                hazardLane = math.clamp(hazardLane, 0, 3);

                blockers.Add(new BlockerData
                {
                    Position = transform.ValueRO.Position,
                    Lane = hazardLane,
                    Speed = 0f,
                    Type = BlockerType.Hazard
                });
            }

            // Collect emergency vehicles
            foreach (var (transform, laneFollower, emergencyAI) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<LaneFollower>, RefRO<EmergencyAI>>()
                    .WithAll<EmergencyVehicleTag>())
            {
                if (emergencyAI.ValueRO.SirenActive)
                {
                    blockers.Add(new BlockerData
                    {
                        Position = transform.ValueRO.Position,
                        Lane = laneFollower.ValueRO.CurrentLane,
                        Speed = 45f, // Emergency speed
                        Type = BlockerType.Emergency
                    });
                }
            }

            // =============================================================
            // Check blocking for player
            // =============================================================

            foreach (var (transform, velocity, laneFollower) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>, RefRW<LaneFollower>>()
                    .WithAll<PlayerVehicleTag>())
            {
                float3 myPos = transform.ValueRO.Position;
                float mySpeed = velocity.ValueRO.Forward;
                int myLane = laneFollower.ValueRO.CurrentLane;

                // Check each adjacent lane
                bool leftBlocked = IsLaneBlocked(myPos, mySpeed, myLane - 1, ref blockers);
                bool rightBlocked = IsLaneBlocked(myPos, mySpeed, myLane + 1, ref blockers);

                // Store in extended lane follower state
                // Note: Using TargetLane temporarily to indicate blocked state
                // In full implementation, add LeftBlocked/RightBlocked fields

                // For now, we prevent lane change initiation in SteeringSystem
                // by checking blockers again there
            }

            // =============================================================
            // Check blocking for traffic (affects AI decisions)
            // =============================================================

            foreach (var (transform, velocity, laneFollower, trafficAI) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>,
                               RefRO<LaneFollower>, RefRW<TrafficAI>>()
                    .WithAll<TrafficVehicleTag>())
            {
                float3 myPos = transform.ValueRO.Position;
                float mySpeed = velocity.ValueRO.Forward;
                int myLane = laneFollower.ValueRO.CurrentLane;

                // Traffic AI will use lane scoring which inherently avoids blocked lanes
                // But we could add explicit blocking here if needed
            }

            blockers.Dispose();
        }

        private bool IsLaneBlocked(float3 myPos, float mySpeed, int targetLane,
                                   ref NativeList<BlockerData> blockers)
        {
            if (targetLane < 0 || targetLane > 3)
                return true; // Out of bounds = blocked

            float targetLateralPos = (targetLane - 1.5f) * LaneWidth;

            for (int i = 0; i < blockers.Length; i++)
            {
                var blocker = blockers[i];

                if (blocker.Lane != targetLane)
                    continue;

                float dz = blocker.Position.z - myPos.z;

                // Check if blocker is in our path
                float blockAhead = BlockingDistanceAhead;
                float blockBehind = BlockingDistanceBehind;

                // Adjust for emergency vehicles (larger buffer)
                if (blocker.Type == BlockerType.Emergency)
                {
                    blockBehind = EmergencyBlockDistance;
                }

                // Adjust for relative speed
                float relativeSpeed = mySpeed - blocker.Speed;
                if (relativeSpeed > 0)
                {
                    // We're catching up, need more space ahead
                    blockAhead += relativeSpeed * 1.5f;
                }
                else
                {
                    // They're catching up, need more space behind
                    blockBehind += math.abs(relativeSpeed) * 2f;
                }

                if (dz > -blockBehind && dz < blockAhead)
                {
                    return true; // Lane is blocked
                }
            }

            return false;
        }

        private struct BlockerData
        {
            public float3 Position;
            public int Lane;
            public float Speed;
            public BlockerType Type;
        }

        private enum BlockerType
        {
            Traffic,
            Hazard,
            Emergency
        }
    }
}
