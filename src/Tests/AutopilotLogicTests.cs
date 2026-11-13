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

        #region Speed Handoff

        [Test]
        public void EaseTargetSpeed_FastHandoffEasesDownComfortably()
        {
            // Taking the wheel at 100 m/s toward a 35 m/s cruise: never faster than ComfortDecel
            float target = 100f;
            float prev = target;
            for (int i = 0; i < 60; i++)
            {
                target = AutopilotLogic.EaseTargetSpeed(target, 35f, 1f / 60f);
                Assert.LessOrEqual(prev - target, AutopilotLogic.ComfortDecel / 60f + 1e-4f);
                prev = target;
            }

            Assert.AreEqual(100f - AutopilotLogic.ComfortDecel, target, 1e-3f);
        }

        [Test]
        public void EaseTargetSpeed_ReachesGoalWithoutOvershoot()
        {
            Assert.AreEqual(35f, AutopilotLogic.EaseTargetSpeed(35.01f, 35f, 1f), 1e-5f);
            Assert.AreEqual(20f, AutopilotLogic.EaseTargetSpeed(19.99f, 20f, 1f), 1e-5f);
            Assert.AreEqual(20f, AutopilotLogic.EaseTargetSpeed(float.NaN, 20f, 1f / 60f), 1e-5f);
        }

        [Test]
        public void EaseTargetSpeed_CarAlreadySlowed_NeverChasesBackUp()
        {
            // Regression: RESTART from the pause menu resets the car to 20 m/s in
            // place under an engaged autopilot whose working target was still ~86
            float eased = AutopilotLogic.EaseTargetSpeed(86f, 20f, 20f, 1f / 60f);
            Assert.LessOrEqual(eased, 20f + 1e-4f);
            AutopilotLogic.SpeedControl(20f, eased, out float throttle, out _);
            Assert.AreEqual(0f, throttle, 1e-6f);
        }

        [Test]
        public void EaseTargetSpeed_CarAlongsideTheEase_Unchanged()
        {
            // A normal fast handoff: the braking car sits just above the eased
            // target, so the car speed changes nothing
            float withCar = AutopilotLogic.EaseTargetSpeed(80f, 35f, 80.4f, 1f / 60f);
            float without = AutopilotLogic.EaseTargetSpeed(80f, 35f, 1f / 60f);
            Assert.AreEqual(without, withCar, 1e-6f);

            // Easing up toward a faster goal ignores the car speed too
            Assert.AreEqual(AutopilotLogic.EaseTargetSpeed(20f, 35f, 1f / 60f),
                AutopilotLogic.EaseTargetSpeed(20f, 35f, 18f, 1f / 60f), 1e-6f);
        }

        [Test]
        public void DetectionRange_LooksSecondsAheadAtSpeed()
        {
            Assert.AreEqual(Range, AutopilotLogic.DetectionRange(20f, Range), 1e-5f);
            Assert.AreEqual(100f * AutopilotLogic.DetectionSeconds, AutopilotLogic.DetectionRange(100f, Range), 1e-3f);
        }

        [Test]
        public void SpeedControl_TracksTargetWithoutDrag()
        {
            AutopilotLogic.SpeedControl(20f, 30f, out float throttle, out float brake);
            Assert.Greater(throttle, 0f);
            Assert.AreEqual(0f, brake);

            AutopilotLogic.SpeedControl(40f, 30f, out throttle, out brake);
            Assert.AreEqual(0f, throttle);
            Assert.Greater(brake, 0f);

            // Inside the deadband: coast (nothing slows the car on its own)
            AutopilotLogic.SpeedControl(30.2f, 30f, out throttle, out brake);
            Assert.AreEqual(0f, throttle);
            Assert.AreEqual(0f, brake);
        }

        #endregion
    }
}
