// ============================================================================
// Nightflow - Game Constants Tests
// Validates centralized constants and helper methods
// ============================================================================

using NUnit.Framework;
using Nightflow.Config;

namespace Nightflow.Tests
{
    [TestFixture]
    public class GameConstantsTests
    {
        // =====================================================================
        // Constant Range Validation
        // =====================================================================

        [Test]
        public void LaneWidth_IsPositive()
        {
            Assert.Greater(GameConstants.LaneWidth, 0f);
        }

        [Test]
        public void RoadWidth_EqualsLaneWidthTimesNumLanes()
        {
            float expected = GameConstants.LaneWidth * GameConstants.DefaultNumLanes;
            Assert.AreEqual(expected, GameConstants.RoadWidth, 0.001f);
        }

        [Test]
        public void MinForwardSpeed_IsLessThanMax()
        {
            Assert.Less(GameConstants.MinForwardSpeed, GameConstants.MaxForwardSpeed);
        }

        [Test]
        public void MaxDamage_IsPositive()
        {
            Assert.Greater(GameConstants.MaxDamage, 0f);
        }

        [Test]
        public void SegmentLength_IsPositive()
        {
            Assert.Greater(GameConstants.SegmentLength, 0f);
        }

        [Test]
        public void SegmentsAhead_GreaterThanZero()
        {
            Assert.Greater(GameConstants.SegmentsAhead, 0);
        }

        [Test]
        public void SegmentsBehind_GreaterThanZero()
        {
            Assert.Greater(GameConstants.SegmentsBehind, 0);
        }

        [Test]
        public void BaseRiskCap_GreaterThanMinRiskCap()
        {
            Assert.Greater(GameConstants.BaseRiskCap, GameConstants.MinRiskCap);
        }

        [Test]
        public void MinRebuildRate_IsPositive()
        {
            Assert.Greater(GameConstants.MinRebuildRate, 0f);
        }

        [Test]
        public void CrashFlashTimings_ArePositive()
        {
            Assert.Greater(GameConstants.CrashFlashInDuration, 0f);
            Assert.Greater(GameConstants.CrashFlashHoldDuration, 0f);
            Assert.Greater(GameConstants.CrashFlashFadeOutDuration, 0f);
        }

        // =====================================================================
        // Unit Conversion Tests
        // =====================================================================

        [Test]
        public void ToKmh_ConvertsCorrectly()
        {
            // 1 m/s = 3.6 km/h
            float result = GameConstants.ToKmh(1f);
            Assert.AreEqual(3.6f, result, 0.001f);
        }

        [Test]
        public void ToKmh_ZeroReturnsZero()
        {
            Assert.AreEqual(0f, GameConstants.ToKmh(0f), 0.001f);
        }

        [Test]
        public void ToMph_ConvertsCorrectly()
        {
            // 1 m/s = 2.237 mph
            float result = GameConstants.ToMph(1f);
            Assert.AreEqual(2.237f, result, 0.001f);
        }

        [Test]
        public void ToMph_ZeroReturnsZero()
        {
            Assert.AreEqual(0f, GameConstants.ToMph(0f), 0.001f);
        }

        [Test]
        public void FromKmh_ConvertsCorrectly()
        {
            // 3.6 km/h = 1 m/s
            float result = GameConstants.FromKmh(3.6f);
            Assert.AreEqual(1f, result, 0.01f);
        }

        [Test]
        public void FromKmh_ZeroReturnsZero()
        {
            Assert.AreEqual(0f, GameConstants.FromKmh(0f), 0.001f);
        }

        [Test]
        public void ToKmh_FromKmh_RoundTrip()
        {
            float originalMs = 25f;
            float kmh = GameConstants.ToKmh(originalMs);
            float backToMs = GameConstants.FromKmh(kmh);
            Assert.AreEqual(originalMs, backToMs, 0.01f);
        }

        [TestCase(10f)]
        [TestCase(30f)]
        [TestCase(80f)]
        public void ToKmh_AlwaysGreaterThanInput(float metersPerSecond)
        {
            // km/h values are always larger than m/s (factor 3.6)
            Assert.Greater(GameConstants.ToKmh(metersPerSecond), metersPerSecond);
        }

        // =====================================================================
        // Lane Center Calculation Tests
        // =====================================================================

        [Test]
        public void GetLaneCenterX_Lane0_IsLeftmost()
        {
            float lane0 = GameConstants.GetLaneCenterX(0);
            float lane1 = GameConstants.GetLaneCenterX(1);
            Assert.Less(lane0, lane1);
        }

        [Test]
        public void GetLaneCenterX_LanesAreEquidistant()
        {
            float lane0 = GameConstants.GetLaneCenterX(0);
            float lane1 = GameConstants.GetLaneCenterX(1);
            float lane2 = GameConstants.GetLaneCenterX(2);

            float gap01 = lane1 - lane0;
            float gap12 = lane2 - lane1;

            Assert.AreEqual(gap01, gap12, 0.001f);
        }

        [Test]
        public void GetLaneCenterX_SpacingEqualsLaneWidth()
        {
            float lane0 = GameConstants.GetLaneCenterX(0);
            float lane1 = GameConstants.GetLaneCenterX(1);

            Assert.AreEqual(GameConstants.LaneWidth, lane1 - lane0, 0.001f);
        }

        [Test]
        public void GetLaneCenterX_SymmetricAroundZero()
        {
            // With 4 lanes, lane 0 and lane 3 should be symmetric around 0
            float lane0 = GameConstants.GetLaneCenterX(0);
            float lane3 = GameConstants.GetLaneCenterX(3);

            Assert.AreEqual(0f, (lane0 + lane3) / 2f, 0.001f);
        }

        [Test]
        public void GetLaneCenterX_DefaultNumLanes_FourLanes()
        {
            // Verify formula: leftEdge + (laneIndex + 0.5) * LaneWidth
            // leftEdge = 0 - (4 * 3.6 * 0.5) = -7.2
            // lane 0 = -7.2 + 0.5 * 3.6 = -7.2 + 1.8 = -5.4
            float expected = -5.4f;
            Assert.AreEqual(expected, GameConstants.GetLaneCenterX(0), 0.001f);
        }

        [Test]
        public void GetLaneCenterX_CustomNumLanes()
        {
            // With 2 lanes: leftEdge = 0 - (2 * 3.6 * 0.5) = -3.6
            // lane 0 = -3.6 + 0.5 * 3.6 = -3.6 + 1.8 = -1.8
            float expected = -1.8f;
            Assert.AreEqual(expected, GameConstants.GetLaneCenterX(0, 2), 0.001f);
        }
    }
}
