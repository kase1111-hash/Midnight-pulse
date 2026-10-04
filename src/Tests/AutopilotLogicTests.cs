// Nightflow - Autopilot Logic Tests
// Validates lane-based hazard reading: threat falloff, which lane counts as
// "mine", and picking the clearer neighbouring lane as the way out.

using NUnit.Framework;
using Unity.Mathematics;
using Nightflow.Systems;

namespace Nightflow.Tests
{
    [TestFixture]
    public class AutopilotLogicTests
    {
        private const float Range = 80f;

        private static float4 Threats(float l0, float l1, float l2, float l3)
        {
            return new float4(l0, l1, l2, l3);
        }

        #region HazardThreat

        [Test]
        public void HazardThreat_CloserIsWorse()
        {
            float near = AutopilotLogic.HazardThreat(1f, 10f, Range);
            float far = AutopilotLogic.HazardThreat(1f, 70f, Range);

            Assert.Greater(near, far);
            Assert.AreEqual(0.875f, near, 1e-5f);
        }

        [Test]
        public void HazardThreat_BehindOrOutOfRange_IsZero()
        {
            Assert.AreEqual(0f, AutopilotLogic.HazardThreat(1f, -5f, Range));
            Assert.AreEqual(0f, AutopilotLogic.HazardThreat(1f, 0f, Range));
            Assert.AreEqual(0f, AutopilotLogic.HazardThreat(1f, Range, Range));
            Assert.AreEqual(0f, AutopilotLogic.HazardThreat(1f, float.NaN, Range));
        }

        #endregion

        #region Lanes

        [Test]
        public void DrivingLane_MidChange_WatchesTargetLane()
        {
            Assert.AreEqual(2, AutopilotLogic.DrivingLane(1, 2, changingLanes: true));
            Assert.AreEqual(1, AutopilotLogic.DrivingLane(1, 2, changingLanes: false));
        }

        [Test]
        public void AccumulateThreat_KeepsWorstPerLane_IgnoresBadLanes()
        {
            float4 t = float4.zero;
            t = AutopilotLogic.AccumulateThreat(t, 1, 0.3f);
            t = AutopilotLogic.AccumulateThreat(t, 1, 0.7f);
            t = AutopilotLogic.AccumulateThreat(t, 1, 0.5f);
            t = AutopilotLogic.AccumulateThreat(t, 7, 1f);
            t = AutopilotLogic.AccumulateThreat(t, -1, 1f);

            Assert.AreEqual(0.7f, t[1], 1e-6f);
            Assert.AreEqual(0f, t[0]);
            Assert.AreEqual(0f, t[3]);
        }

        [Test]
        public void HazardInNeighbourLane_DoesNotThreatenMyLane()
        {
            // On a curve the old world-x check could put this hazard "in my lane"
            float4 t = AutopilotLogic.AccumulateThreat(float4.zero, 2, 0.9f);
            Assert.AreEqual(0f, t[1]);
        }

        #endregion

        #region ChooseEscapeDirection

        [Test]
        public void Escape_PicksTheClearerNeighbour()
        {
            Assert.AreEqual(1, AutopilotLogic.ChooseEscapeDirection(Threats(0.6f, 0.9f, 0f, 0f), 1, 4));
            Assert.AreEqual(-1, AutopilotLogic.ChooseEscapeDirection(Threats(0f, 0.9f, 0.6f, 0f), 1, 4));
        }

        [Test]
        public void Escape_EdgeLane_OnlyGoesInward()
        {
            Assert.AreEqual(1, AutopilotLogic.ChooseEscapeDirection(Threats(0.9f, 0f, 0f, 0f), 0, 4));
            Assert.AreEqual(-1, AutopilotLogic.ChooseEscapeDirection(Threats(0f, 0f, 0f, 0.9f), 3, 4));
        }

        [Test]
        public void Escape_TiePrefersRoadCentre()
        {
            // Lane 1: left is the edge (lane 0), right is central (lane 2)
            Assert.AreEqual(1, AutopilotLogic.ChooseEscapeDirection(Threats(0f, 0.9f, 0f, 0f), 1, 4));
            // Lane 2: left is central (lane 1), right is the edge (lane 3)
            Assert.AreEqual(-1, AutopilotLogic.ChooseEscapeDirection(Threats(0f, 0f, 0.9f, 0f), 2, 4));
        }

        [Test]
        public void Escape_NoSaferNeighbour_Stays()
        {
            // Neighbours as bad as (or barely better than) staying: brake instead of weaving
            Assert.AreEqual(0, AutopilotLogic.ChooseEscapeDirection(Threats(0.9f, 0.9f, 0.85f, 0f), 1, 4));
        }

        [Test]
        public void Escape_InvalidLane_Stays()
        {
            Assert.AreEqual(0, AutopilotLogic.ChooseEscapeDirection(Threats(0f, 0f, 0f, 0f), -1, 4));
            Assert.AreEqual(0, AutopilotLogic.ChooseEscapeDirection(Threats(0f, 0f, 0f, 0f), 5, 4));
        }

        [Test]
        public void Escape_RespectsFewerLanes()
        {
            // Two-lane road: lane 1 is the right edge, never escapes to "lane 2"
            Assert.AreEqual(-1, AutopilotLogic.ChooseEscapeDirection(Threats(0f, 0.9f, 0f, 0f), 1, 2));
        }

        #endregion
    }
}
