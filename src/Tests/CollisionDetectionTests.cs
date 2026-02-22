// ============================================================================
// Nightflow - Collision Detection Tests
// Validates AABB overlap, broad phase culling, and impact speed calculation
// ============================================================================

using NUnit.Framework;
using Unity.Mathematics;

namespace Nightflow.Tests
{
    /// <summary>
    /// Tests the collision detection math: AABB overlap, broad phase radius,
    /// and impact speed formula: v_impact = max(0, dot(V, -N))
    /// TODO: Add error-path tests for NaN/Infinity inputs and zero-dimension collision boxes
    /// TODO: Add [TestCase] parametrization for AABB and impact speed boundary values
    /// </summary>
    [TestFixture]
    public class CollisionDetectionTests
    {
        // Player collision box (half-extents, mirrors CollisionSystem)
        private const float PlayerHalfWidth = 0.9f;
        private const float PlayerHalfHeight = 0.6f;
        private const float PlayerHalfLength = 2.2f;
        private const float BroadPhaseRadius = 6f;
        private const float MinImpactThreshold = 0.5f;

        // =====================================================================
        // AABB Overlap Tests
        // =====================================================================

        private static bool AABBOverlap(float3 posA, float3 sizeA, float3 posB, float3 sizeB)
        {
            float3 minA = posA - sizeA;
            float3 maxA = posA + sizeA;
            float3 minB = posB - sizeB;
            float3 maxB = posB + sizeB;

            return (minA.x <= maxB.x && maxA.x >= minB.x) &&
                   (minA.y <= maxB.y && maxA.y >= minB.y) &&
                   (minA.z <= maxB.z && maxA.z >= minB.z);
        }

        [Test]
        public void AABB_SamePosition_Overlaps()
        {
            float3 pos = new float3(0, 0, 0);
            float3 size = new float3(1, 1, 1);
            Assert.IsTrue(AABBOverlap(pos, size, pos, size));
        }

        [Test]
        public void AABB_FarApart_NoOverlap()
        {
            float3 posA = new float3(0, 0, 0);
            float3 posB = new float3(100, 0, 0);
            float3 size = new float3(1, 1, 1);
            Assert.IsFalse(AABBOverlap(posA, size, posB, size));
        }

        [Test]
        public void AABB_TouchingEdge_Overlaps()
        {
            // Boxes touching at their edges (minA == maxB is still overlap with <=)
            float3 posA = new float3(0, 0, 0);
            float3 posB = new float3(2, 0, 0);
            float3 size = new float3(1, 1, 1);
            Assert.IsTrue(AABBOverlap(posA, size, posB, size));
        }

        [Test]
        public void AABB_JustBeyondEdge_NoOverlap()
        {
            float3 posA = new float3(0, 0, 0);
            float3 posB = new float3(2.01f, 0, 0);
            float3 size = new float3(1, 1, 1);
            Assert.IsFalse(AABBOverlap(posA, size, posB, size));
        }

        [Test]
        public void AABB_OverlapOnlyXY_NotZ_NoOverlap()
        {
            // Overlap in X and Y but not Z
            float3 posA = new float3(0, 0, 0);
            float3 posB = new float3(0.5f, 0.5f, 10f);
            float3 size = new float3(1, 1, 1);
            Assert.IsFalse(AABBOverlap(posA, size, posB, size));
        }

        [Test]
        public void AABB_PlayerVsHazard_HeadOn()
        {
            float3 playerPos = new float3(0, 0.6f, 0);
            float3 playerSize = new float3(PlayerHalfWidth, PlayerHalfHeight, PlayerHalfLength);

            float3 hazardPos = new float3(0, 0.6f, 3f); // 3m ahead
            float3 hazardSize = new float3(0.5f, 0.5f, 1f);

            // Player extends to z=2.2, hazard from z=2 to z=4 => overlap
            Assert.IsTrue(AABBOverlap(playerPos, playerSize, hazardPos, hazardSize));
        }

        [Test]
        public void AABB_PlayerVsHazard_SideSwipe()
        {
            float3 playerPos = new float3(0, 0.6f, 0);
            float3 playerSize = new float3(PlayerHalfWidth, PlayerHalfHeight, PlayerHalfLength);

            // Hazard at side, slightly overlapping
            float3 hazardPos = new float3(1.2f, 0.6f, 0f);
            float3 hazardSize = new float3(0.5f, 0.5f, 1f);

            // Player extends to x=0.9, hazard from x=0.7 to x=1.7 => overlap
            Assert.IsTrue(AABBOverlap(playerPos, playerSize, hazardPos, hazardSize));
        }

        [Test]
        public void AABB_PlayerVsHazard_NearMiss()
        {
            float3 playerPos = new float3(0, 0.6f, 0);
            float3 playerSize = new float3(PlayerHalfWidth, PlayerHalfHeight, PlayerHalfLength);

            // Hazard just outside player bounds
            float3 hazardPos = new float3(2f, 0.6f, 0f);
            float3 hazardSize = new float3(0.5f, 0.5f, 1f);

            // Player extends to x=0.9, hazard from x=1.5 to x=2.5 => no overlap
            Assert.IsFalse(AABBOverlap(playerPos, playerSize, hazardPos, hazardSize));
        }

        // =====================================================================
        // Broad Phase Tests
        // =====================================================================

        private static bool BroadPhaseCheck(float3 playerPos, float3 hazardPos)
        {
            float distSq = math.distancesq(playerPos, hazardPos);
            return distSq <= BroadPhaseRadius * BroadPhaseRadius;
        }

        [Test]
        public void BroadPhase_SamePosition_Passes()
        {
            Assert.IsTrue(BroadPhaseCheck(float3.zero, float3.zero));
        }

        [Test]
        public void BroadPhase_ExactlyAtRadius_Passes()
        {
            float3 player = float3.zero;
            float3 hazard = new float3(BroadPhaseRadius, 0, 0);
            Assert.IsTrue(BroadPhaseCheck(player, hazard));
        }

        [Test]
        public void BroadPhase_BeyondRadius_Fails()
        {
            float3 player = float3.zero;
            float3 hazard = new float3(BroadPhaseRadius + 1f, 0, 0);
            Assert.IsFalse(BroadPhaseCheck(player, hazard));
        }

        [Test]
        public void BroadPhase_DiagonalDistance()
        {
            float3 player = float3.zero;
            // sqrt(4^2 + 4^2 + 0^2) = 5.66 < 6
            float3 hazard = new float3(4f, 4f, 0f);
            Assert.IsTrue(BroadPhaseCheck(player, hazard));
        }

        [Test]
        public void BroadPhase_DiagonalBeyond()
        {
            float3 player = float3.zero;
            // sqrt(5^2 + 5^2 + 0^2) = 7.07 > 6
            float3 hazard = new float3(5f, 5f, 0f);
            Assert.IsFalse(BroadPhaseCheck(player, hazard));
        }

        // =====================================================================
        // Impact Speed Tests
        // =====================================================================

        private static float CalculateImpactSpeed(float3 velocity, float3 normal)
        {
            return math.max(0f, math.dot(velocity, -normal));
        }

        [Test]
        public void ImpactSpeed_HeadOnCollision_FullSpeed()
        {
            // Moving forward (0,0,1) into wall with normal facing toward player (0,0,1)
            float3 velocity = new float3(0, 0, 30f);
            float3 normal = new float3(0, 0, 1f); // Points toward player

            float impact = CalculateImpactSpeed(velocity, normal);
            // dot((0,0,30), (0,0,-1)) = -30, max(0, -30) => but -normal = (0,0,-1)
            // Actually: dot(velocity, -normal) = dot((0,0,30), (0,0,-1)) = -30
            // max(0, -30) = 0 ...
            // Wait - normal points FROM hazard TOWARD player, so -normal is INTO the hazard
            // If player moves forward and hazard is ahead, normal points backward (0,0,-1)
            // because toPlayer = playerPos - hazardPos would be negative Z

            // Let's reconsider: hazard is ahead, so normal = playerPos - hazardPos = (0,0,-1) normalized
            float3 normalCorrected = new float3(0, 0, -1f); // Hazard ahead, normal points backward
            float impactCorrected = CalculateImpactSpeed(velocity, normalCorrected);
            // dot((0,0,30), (0,0,1)) = 30
            Assert.AreEqual(30f, impactCorrected, 0.001f);
        }

        [Test]
        public void ImpactSpeed_GlancingContact_LowSpeed()
        {
            // Moving mostly lateral, hitting a hazard on the side
            float3 velocity = new float3(5f, 0, 30f); // Mostly forward
            float3 normal = new float3(1f, 0, 0);     // Hit from right side

            float impact = CalculateImpactSpeed(velocity, normal);
            // dot((5,0,30), (-1,0,0)) = -5, max(0, -5) = 0
            Assert.AreEqual(0f, impact, 0.001f);
        }

        [Test]
        public void ImpactSpeed_MovingAway_Zero()
        {
            // Moving away from hazard
            float3 velocity = new float3(0, 0, -30f); // Moving backward
            float3 normal = new float3(0, 0, -1f);    // Hazard ahead

            float impact = CalculateImpactSpeed(velocity, normal);
            // dot((0,0,-30), (0,0,1)) = -30, max(0, -30) = 0
            Assert.AreEqual(0f, impact, 0.001f);
        }

        [Test]
        public void ImpactSpeed_SideImpact()
        {
            // Moving laterally into a side hazard
            float3 velocity = new float3(10f, 0, 20f);
            float3 normal = new float3(-1f, 0, 0); // Hazard is to the left, normal points right

            float impact = CalculateImpactSpeed(velocity, normal);
            // dot((10,0,20), (1,0,0)) = 10
            Assert.AreEqual(10f, impact, 0.001f);
        }

        [Test]
        public void ImpactSpeed_NeverNegative()
        {
            float3 velocity = new float3(0, 0, -50f);
            float3 normal = new float3(0, 0, -1f);
            float impact = CalculateImpactSpeed(velocity, normal);
            Assert.GreaterOrEqual(impact, 0f);
        }

        // =====================================================================
        // Collision Normal Calculation
        // =====================================================================

        private static float3 CalculateNormal(float3 playerPos, float3 hazardPos)
        {
            float3 toPlayer = playerPos - hazardPos;
            float dist = math.length(toPlayer);
            if (dist > 0.01f)
                return toPlayer / dist;
            return new float3(0, 0, 1); // Fallback
        }

        [Test]
        public void Normal_HazardAhead_PointsBackward()
        {
            float3 player = new float3(0, 0, 0);
            float3 hazard = new float3(0, 0, 5);
            float3 normal = CalculateNormal(player, hazard);

            Assert.Less(normal.z, 0f); // Points toward negative Z (toward player behind hazard)
        }

        [Test]
        public void Normal_HazardLeft_PointsRight()
        {
            float3 player = new float3(0, 0, 0);
            float3 hazard = new float3(-5, 0, 0);
            float3 normal = CalculateNormal(player, hazard);

            Assert.Greater(normal.x, 0f); // Points toward positive X
        }

        [Test]
        public void Normal_IsUnitLength()
        {
            float3 player = new float3(3, 1, 7);
            float3 hazard = new float3(1, 2, 4);
            float3 normal = CalculateNormal(player, hazard);

            Assert.AreEqual(1f, math.length(normal), 0.001f);
        }

        [Test]
        public void Normal_SamePosition_ReturnsFallback()
        {
            float3 pos = new float3(5, 0, 10);
            float3 normal = CalculateNormal(pos, pos);

            // Fallback is (0,0,1)
            Assert.AreEqual(0f, normal.x, 0.001f);
            Assert.AreEqual(0f, normal.y, 0.001f);
            Assert.AreEqual(1f, normal.z, 0.001f);
        }

        // =====================================================================
        // Minimum Impact Threshold
        // =====================================================================

        [Test]
        public void MinImpactThreshold_IsPositive()
        {
            Assert.Greater(MinImpactThreshold, 0f);
        }

        [Test]
        public void MinImpactThreshold_BelowThreshold_NoEvent()
        {
            float impactSpeed = 0.3f;
            bool shouldRegister = impactSpeed > MinImpactThreshold;
            Assert.IsFalse(shouldRegister);
        }

        [Test]
        public void MinImpactThreshold_AboveThreshold_EventRegistered()
        {
            float impactSpeed = 1.0f;
            bool shouldRegister = impactSpeed > MinImpactThreshold;
            Assert.IsTrue(shouldRegister);
        }

        // =====================================================================
        // Error-Path & Boundary Tests
        // =====================================================================

        [Test]
        public void AABB_ZeroDimensionBox_NoOverlapWithSeparation()
        {
            float3 posA = new float3(0, 0, 0);
            float3 posB = new float3(5, 0, 0);
            float3 zeroSize = new float3(0, 0, 0);
            float3 normalSize = new float3(1, 1, 1);
            Assert.IsFalse(AABBOverlap(posA, zeroSize, posB, normalSize));
        }

        [Test]
        public void AABB_ZeroDimensionBox_OverlapAtSamePosition()
        {
            float3 pos = new float3(0, 0, 0);
            float3 zeroSize = new float3(0, 0, 0);
            // Zero-size box at same position: min == max on all axes, overlap is <=
            Assert.IsTrue(AABBOverlap(pos, zeroSize, pos, zeroSize));
        }

        [Test]
        public void Normal_VerySmallDistance_UsesFallback()
        {
            // Distance less than 0.01f threshold
            float3 player = new float3(0, 0, 0);
            float3 hazard = new float3(0.005f, 0, 0);
            float3 normal = CalculateNormal(player, hazard);
            // dist ≈ 0.005 < 0.01, should return fallback (0,0,1)
            Assert.AreEqual(0f, normal.x, 0.001f);
            Assert.AreEqual(0f, normal.y, 0.001f);
            Assert.AreEqual(1f, normal.z, 0.001f);
        }

        [Test]
        public void ImpactSpeed_ExtremeVelocity_StillValid()
        {
            float3 velocity = new float3(0, 0, 1000f);
            float3 normal = new float3(0, 0, -1f);
            float impact = CalculateImpactSpeed(velocity, normal);
            Assert.AreEqual(1000f, impact, 0.001f);
            Assert.IsTrue(float.IsFinite(impact));
        }

        [TestCase(0f, 0f, 0f, 6f, 0f, 0f, true)]     // Exactly at radius on X
        [TestCase(0f, 0f, 0f, 0f, 6f, 0f, true)]     // Exactly at radius on Y
        [TestCase(0f, 0f, 0f, 0f, 0f, 6f, true)]     // Exactly at radius on Z
        [TestCase(0f, 0f, 0f, 6.01f, 0f, 0f, false)]  // Just beyond radius
        public void BroadPhase_BoundaryDistances(
            float px, float py, float pz, float hx, float hy, float hz, bool expected)
        {
            float3 player = new float3(px, py, pz);
            float3 hazard = new float3(hx, hy, hz);
            Assert.AreEqual(expected, BroadPhaseCheck(player, hazard));
        }

        [TestCase(0f, 0f, 30f, 0f, 0f, -1f, 30f)]    // Head-on
        [TestCase(10f, 0f, 0f, -1f, 0f, 0f, 10f)]     // Side impact from left
        [TestCase(0f, 0f, -20f, 0f, 0f, 1f, 0f)]      // Moving away
        [TestCase(0f, 0f, 0f, 0f, 0f, -1f, 0f)]       // Stationary
        public void ImpactSpeed_ParametrizedCases(
            float vx, float vy, float vz, float nx, float ny, float nz, float expected)
        {
            float3 velocity = new float3(vx, vy, vz);
            float3 normal = new float3(nx, ny, nz);
            float impact = CalculateImpactSpeed(velocity, normal);
            Assert.AreEqual(expected, impact, 0.01f);
        }
    }
}
