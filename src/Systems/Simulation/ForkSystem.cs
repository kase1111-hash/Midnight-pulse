// ============================================================================
// Nightflow - Fork System
// Handles track branching, commitment, and despawn
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages fork segments and player branch commitment.
    ///
    /// From spec:
    /// - At fork point: θ_L = -θ_fork, θ_R = +θ_fork
    /// - Gradual separation: S_fork(t) += R(t) × (d_fork × t²)
    /// - Unchosen path despawns after commit
    /// - Fork magnetism reduction: m_fork(s) = smoothstep(1, 0.7, s/L_fork)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OverpassSystem))]
    public partial struct ForkSystem : ISystem
    {
        // Fork parameters
        private const float CommitThreshold = 0.7f;        // Progress before commitment
        private const float MagnetismReduction = 0.7f;     // Reduced magnetism at fork
        private const float LaneDecisionThreshold = 0.5f;  // Lane index threshold for L/R

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            EntityCommandBuffer ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Get player position and lane info
            float playerZ = 0f;
            int playerLane = 0;
            float lateralOffset = 0f;

            foreach (var (transform, laneFollower) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<LaneFollower>>()
                    .WithAll<PlayerVehicleTag>())
            {
                playerZ = transform.ValueRO.Position.z;
                playerLane = laneFollower.ValueRO.LaneIndex;
                lateralOffset = laneFollower.ValueRO.LateralOffset;
                break;
            }

            // =============================================================
            // Process Fork Segments
            // =============================================================

            foreach (var (segment, forkData, entity) in
                SystemAPI.Query<RefRO<TrackSegment>, RefRW<ForkData>>()
                    .WithAll<ForkSegmentTag>()
                    .WithEntityAccess())
            {
                float segmentStart = segment.ValueRO.StartZ;
                float segmentEnd = segment.ValueRO.EndZ;
                float segmentLength = segmentEnd - segmentStart;

                // Check if player is in this fork segment
                if (playerZ >= segmentStart && playerZ <= segmentEnd)
                {
                    // Calculate progress through fork
                    float forkProgress = (playerZ - segmentStart) / segmentLength;

                    // Update environment state
                    foreach (var envState in SystemAPI.Query<RefRW<EnvironmentState>>()
                        .WithAll<PlayerVehicleTag>())
                    {
                        envState.ValueRW.AtFork = true;
                        envState.ValueRW.ForkProgress = forkProgress;
                        envState.ValueRW.CurrentFork = entity;
                    }

                    // Apply magnetism reduction during fork
                    // m_fork(s) = smoothstep(1, 0.7, s/L_fork)
                    float magnetReduction = Smoothstep(1f, MagnetismReduction, forkProgress);

                    foreach (var laneFollower in SystemAPI.Query<RefRW<LaneFollower>>()
                        .WithAll<PlayerVehicleTag>())
                    {
                        // Temporarily reduce magnetism
                        laneFollower.ValueRW.MagnetStrength *= magnetReduction;
                    }

                    // =============================================================
                    // Determine Branch and Commit
                    // =============================================================

                    if (!forkData.ValueRO.Committed && forkProgress >= CommitThreshold)
                    {
                        // Determine which branch based on player lane and lateral position
                        // Left lanes (0, 1) = left branch, Right lanes (2, 3) = right branch
                        int numLanes = segment.ValueRO.NumLanes;
                        float laneMidpoint = (numLanes - 1) / 2f;

                        // Calculate effective lane position
                        float effectiveLane = playerLane + (lateralOffset / 3.6f);

                        int chosenBranch;
                        if (effectiveLane < laneMidpoint)
                        {
                            chosenBranch = -1; // Left branch
                        }
                        else
                        {
                            chosenBranch = 1; // Right branch
                        }

                        // Commit to branch
                        forkData.ValueRW.Committed = true;
                        forkData.ValueRW.ChosenBranch = chosenBranch;

                        // Despawn unchosen branch (would be linked entities)
                        // Note: Branch entities would be created during fork generation
                        // For now, mark the fork as committed
                    }
                }
                else if (playerZ > segmentEnd && forkData.ValueRO.Committed)
                {
                    // Player has passed the fork - clear fork state
                    foreach (var envState in SystemAPI.Query<RefRW<EnvironmentState>>()
                        .WithAll<PlayerVehicleTag>())
                    {
                        if (envState.ValueRO.CurrentFork == entity)
                        {
                            envState.ValueRW.AtFork = false;
                            envState.ValueRW.ForkProgress = 0f;
                            envState.ValueRW.CurrentFork = Entity.Null;
                        }
                    }
                }
            }

            // =============================================================
            // Update Camera for Fork Decision
            // =============================================================

            foreach (var (cameraState, envState) in
                SystemAPI.Query<RefRW<CameraState>, RefRO<EnvironmentState>>()
                    .WithAll<PlayerVehicleTag>())
            {
                if (envState.ValueRO.AtFork)
                {
                    // Slight camera pull-back at fork to show options
                    float forkPullback = envState.ValueRO.ForkProgress * 2f;
                    cameraState.ValueRW.DistanceOffset += forkPullback * 0.1f;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Smoothstep interpolation.
        /// </summary>
        private float Smoothstep(float from, float to, float t)
        {
            t = math.saturate(t);
            float smooth = t * t * (3f - 2f * t);
            return math.lerp(from, to, smooth);
        }
    }
}
