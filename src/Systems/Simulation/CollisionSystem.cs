// ============================================================================
// Nightflow - Collision Detection System
// Execution Order: 5 (Simulation Group)
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
    /// Detects collisions between player vehicle and hazards.
    /// Uses simple AABB overlap tests for fast detection.
    /// Populates CollisionEvent for processing by ImpulseSystem and DamageSystem.
    ///
    /// From spec:
    /// - v_impact = max(0, -V · N)
    /// - Glancing contacts ignored automatically
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VehicleMovementSystem))]
    [UpdateBefore(typeof(ImpulseSystem))]
    public partial struct CollisionSystem : ISystem
    {
        // Player collision box (half-extents)
        private const float PlayerHalfWidth = 0.9f;      // ~1.8m wide
        private const float PlayerHalfHeight = 0.6f;     // ~1.2m tall
        private const float PlayerHalfLength = 2.2f;     // ~4.4m long

        // Collision detection radius (broad phase)
        private const float BroadPhaseRadius = 6f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // =============================================================
            // Clear Previous Collision Events
            // =============================================================

            foreach (var collision in
                SystemAPI.Query<RefRW<CollisionEvent>>()
                    .WithAll<PlayerVehicleTag>())
            {
                collision.ValueRW.Occurred = false;
                collision.ValueRW.OtherEntity = Entity.Null;
            }

            // =============================================================
            // Get Player State
            // =============================================================

            float3 playerPos = float3.zero;
            float3 playerVelocity = float3.zero;
            quaternion playerRot = quaternion.identity;
            Entity playerEntity = Entity.Null;
            bool playerActive = false;

            foreach (var (transform, velocity, entity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>()
                    .WithEntityAccess())
            {
                playerPos = transform.ValueRO.Position;
                playerRot = transform.ValueRO.Rotation;
                playerEntity = entity;
                playerActive = true;

                // Reconstruct velocity vector
                float3 forward = math.forward(playerRot);
                float3 right = math.mul(playerRot, new float3(1, 0, 0));
                playerVelocity = forward * velocity.ValueRO.Forward + right * velocity.ValueRO.Lateral;
                break;
            }

            if (!playerActive)
                return;

            // =============================================================
            // Check Collisions with Hazards
            // =============================================================

            Entity closestHazard = Entity.Null;
            float3 closestNormal = float3.zero;
            float3 closestContactPoint = float3.zero;
            float closestImpactSpeed = 0f;

            foreach (var (hazardTransform, hazard, collisionShape, entity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRW<Hazard>, RefRO<CollisionShape>>()
                    .WithAll<HazardTag>()
                    .WithEntityAccess())
            {
                // Skip already-hit hazards
                if (hazard.ValueRO.Hit)
                    continue;

                float3 hazardPos = hazardTransform.ValueRO.Position;

                // Broad phase: quick distance check
                float distSq = math.distancesq(playerPos, hazardPos);
                if (distSq > BroadPhaseRadius * BroadPhaseRadius)
                    continue;

                // Narrow phase: AABB collision
                float3 hazardSize = collisionShape.ValueRO.Size;
                float3 playerSize = new float3(PlayerHalfWidth, PlayerHalfHeight, PlayerHalfLength);

                // World-aligned boxes for simplicity
                float3 minA = playerPos - playerSize;
                float3 maxA = playerPos + playerSize;
                float3 minB = hazardPos - hazardSize;
                float3 maxB = hazardPos + hazardSize;

                // AABB overlap test
                bool overlap = (minA.x <= maxB.x && maxA.x >= minB.x) &&
                              (minA.y <= maxB.y && maxA.y >= minB.y) &&
                              (minA.z <= maxB.z && maxA.z >= minB.z);

                if (!overlap)
                    continue;

                // =============================================================
                // Calculate Collision Details
                // =============================================================

                // Calculate collision normal (from hazard toward player)
                float3 toPlayer = playerPos - hazardPos;
                float dist = math.length(toPlayer);

                float3 normal;
                if (dist > 0.01f)
                {
                    normal = toPlayer / dist;
                }
                else
                {
                    // Fallback: use forward direction
                    normal = math.forward(playerRot);
                }

                // Calculate impact speed along normal
                // v_impact = max(0, -V · N)
                // N points toward player, so -N is the impact direction
                float impactSpeed = math.max(0f, math.dot(playerVelocity, -normal));

                // Contact point (approximate as midpoint)
                float3 contactPoint = (playerPos + hazardPos) * 0.5f;

                // Track strongest collision
                if (impactSpeed > closestImpactSpeed)
                {
                    closestHazard = entity;
                    closestNormal = normal;
                    closestContactPoint = contactPoint;
                    closestImpactSpeed = impactSpeed;
                }

                // Mark hazard as hit
                hazard.ValueRW.Hit = true;
            }

            // =============================================================
            // Store Collision Event
            // =============================================================

            if (closestHazard != Entity.Null && closestImpactSpeed > 0.5f)
            {
                foreach (var collision in
                    SystemAPI.Query<RefRW<CollisionEvent>>()
                        .WithAll<PlayerVehicleTag>())
                {
                    collision.ValueRW.Occurred = true;
                    collision.ValueRW.OtherEntity = closestHazard;
                    collision.ValueRW.ImpactSpeed = closestImpactSpeed;
                    collision.ValueRW.Normal = closestNormal;
                    collision.ValueRW.ContactPoint = closestContactPoint;
                }
            }
        }
    }
}
