// Nightflow - Hazard Placement Tests
// Validates the fairness rule that keeps hazard layouts drivable: no stretch
// of road as long as a lane change may ever become an unavoidable wall.

using NUnit.Framework;
using Nightflow.Systems;

namespace Nightflow.Tests
{
    [TestFixture]
    public class HazardPlacementTests
    {
        private const int Lanes = 4;

        private static int Mask(params int[] lanes)
        {
            int m = 0;
            foreach (int l in lanes) m |= 1 << l;
            return m;
        }

        #region IsPassable

        [Test]
        public void IsPassable_EmptyRoad()
        {
            Assert.IsTrue(HazardPlacement.IsPassable(0, Lanes));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void IsPassable_SingleBlockedLane(int lane)
        {
            Assert.IsTrue(HazardPlacement.IsPassable(Mask(lane), Lanes));
        }

        [Test]
        public void IsPassable_TwoLanesWithFreeNeighbours()
        {
            Assert.IsTrue(HazardPlacement.IsPassable(Mask(1, 2), Lanes));
            Assert.IsTrue(HazardPlacement.IsPassable(Mask(0, 2), Lanes));
            Assert.IsTrue(HazardPlacement.IsPassable(Mask(0, 3), Lanes));
            Assert.IsTrue(HazardPlacement.IsPassable(Mask(1, 3), Lanes));
        }

        [Test]
        public void IsPassable_EdgePairForcesDoubleLaneChange_Rejected()
        {
            // A car in lane 0 would have to cross lane 1 inside one row
            Assert.IsFalse(HazardPlacement.IsPassable(Mask(0, 1), Lanes));
            Assert.IsFalse(HazardPlacement.IsPassable(Mask(2, 3), Lanes));
        }

        [Test]
        public void IsPassable_ThreeOrFourLanes_Rejected()
        {
            Assert.IsFalse(HazardPlacement.IsPassable(Mask(0, 1, 3), Lanes));
            Assert.IsFalse(HazardPlacement.IsPassable(Mask(0, 2, 3), Lanes));
            Assert.IsFalse(HazardPlacement.IsPassable(Mask(0, 1, 2, 3), Lanes));
        }

        #endregion

        #region AllowedLanes / PickLane

        [Test]
        public void AllowedLanes_EmptyRow_AllLanes()
        {
            Assert.AreEqual(Mask(0, 1, 2, 3), HazardPlacement.AllowedLanes(0, Lanes));
        }

        [Test]
        public void AllowedLanes_EdgeBlocked_NeighbourDisallowed()
        {
            // Lane 0 blocked: lane 1 would make {0,1}; 2 and 3 are fine; 0 itself stays allowed
            Assert.AreEqual(Mask(0, 2, 3), HazardPlacement.AllowedLanes(Mask(0), Lanes));
        }

        [Test]
        public void AllowedLanes_RowFull_OnlyAlreadyBlockedLanes()
        {
            // Stacking more hazards into already-blocked lanes never closes the gap
            Assert.AreEqual(Mask(1, 2), HazardPlacement.AllowedLanes(Mask(1, 2), Lanes));
        }

        [Test]
        public void PickLane_OnlyReturnsAllowedLanes()
        {
            int allowed = Mask(0, 3);
            for (float roll = 0f; roll < 1f; roll += 0.01f)
            {
                int lane = HazardPlacement.PickLane(allowed, Lanes, roll);
                Assert.IsTrue(lane == 0 || lane == 3, $"roll {roll} picked lane {lane}");
            }
        }

        [Test]
        public void PickLane_CoversEveryAllowedLane()
        {
            int allowed = Mask(0, 2, 3);
            int seen = 0;
            for (float roll = 0f; roll < 1f; roll += 0.01f)
                seen |= 1 << HazardPlacement.PickLane(allowed, Lanes, roll);

            Assert.AreEqual(allowed, seen);
        }

        [Test]
        public void PickLane_NothingAllowed_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, HazardPlacement.PickLane(0, Lanes, 0.5f));
            Assert.AreEqual(3, HazardPlacement.PickLane(Mask(3), Lanes, 0.9999f));
        }

        [Test]
        public void GreedyPlacement_NeverProducesAWall()
        {
            // Any sequence of picks inside one row stays passable
            var rng = new System.Random(7);
            for (int trial = 0; trial < 2000; trial++)
            {
                int row = 0;
                for (int i = 0; i < 10; i++)
                {
                    int lane = HazardPlacement.PickLane(
                        HazardPlacement.AllowedLanes(row, Lanes), Lanes, (float)rng.NextDouble());
                    if (lane >= 0) row |= 1 << lane;
                    Assert.IsTrue(HazardPlacement.IsPassable(row, Lanes));
                }
            }
        }

        #endregion

        #region SafetyWindow

        [Test]
        public void SafetyWindow_CoversLaneChangeAtSpeed()
        {
            // 80 m/s: lane change clamps to 1.0 s -> 80 m of road, plus margin
            Assert.AreEqual(80f + HazardPlacement.WindowMargin, HazardPlacement.SafetyWindow(80f), 1e-4f);
            // 40 m/s: 0.6 s -> 24 m
            Assert.AreEqual(24f + HazardPlacement.WindowMargin, HazardPlacement.SafetyWindow(40f), 1e-4f);
        }

        [Test]
        public void SafetyWindow_GrowsWithSpeed()
        {
            Assert.Greater(HazardPlacement.SafetyWindow(60f), HazardPlacement.SafetyWindow(30f));
            Assert.AreEqual(HazardPlacement.WindowMargin, HazardPlacement.SafetyWindow(float.NaN), 1e-4f);
        }

        #endregion
    }
}
