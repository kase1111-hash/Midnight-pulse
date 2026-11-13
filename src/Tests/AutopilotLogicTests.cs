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
        public void LaneEscape_SeriousThreat_LeavesAtAnyDistance()
        {
            // A crashed car far up the lane: same rule as ChooseEscapeDirection
            Assert.AreEqual(1, AutopilotLogic.LaneEscape(Threats(0f, 0.6f, 0f, 0f), 1, 4, 200f, 30f, 0.5f));
        }

        [Test]
        public void LaneEscape_MinorHazardClose_DodgesIt()
        {
            // Regression: a loose tyre (severity 0.2) never got past the 0.1
            // escape margin, so the autopilot drove over every one of them
            float threat = AutopilotLogic.HazardThreat(0.2f, 40f, 100f);
            Assert.AreEqual(1, AutopilotLogic.LaneEscape(Threats(0f, threat, 0f, 0f), 1, 4, 40f, 30f, 0.5f));
        }

        [Test]
        public void LaneEscape_MinorHazardFar_NotYet()
        {
            // 100 m at 30 m/s is ~3.3 s away: keep the lane for now
            float threat = AutopilotLogic.HazardThreat(0.2f, 100f, 200f);
            Assert.AreEqual(0, AutopilotLogic.LaneEscape(Threats(0f, threat, 0f, 0f), 1, 4, 100f, 30f, 0.5f));
        }

        [Test]
        public void LaneEscape_MinorHazardEverywhere_Stays()
        {
            // No neighbour has under half our threat: hold the lane
            float threat = AutopilotLogic.HazardThreat(0.2f, 40f, 100f);
            Assert.AreEqual(0, AutopilotLogic.LaneEscape(Threats(threat, threat, threat, 0f), 1, 4, 40f, 30f, 0.5f));
        }

        [Test]
        public void LaneEscape_ClearLane_Stays()
        {
            Assert.AreEqual(0, AutopilotLogic.LaneEscape(Threats(0f, 0f, 0f, 0f), 1, 4, 10f, 30f, 0.5f));
            Assert.AreEqual(0, AutopilotLogic.LaneEscape(Threats(0f, 0f, 0f, 0f), -1, 4, 10f, 30f, 0.5f));
        }

        [Test]
        public void LaneEscape_RefusedSide_TriesTheOtherWhenItIsAWayOutToo()
        {
            // Regression: a request the steering system turned down (traffic in
            // that lane) was never made again, so the car hit what it dodged.
            // Both neighbours clear: the two are asked for in turn
            float threat = AutopilotLogic.HazardThreat(0.3f, 40f, 100f);
            var both = Threats(0f, threat, 0f, 0f);
            Assert.AreEqual(1, AutopilotLogic.LaneEscape(both, 1, 4, 40f, 30f, 0.5f, 0));
            Assert.AreEqual(-1, AutopilotLogic.LaneEscape(both, 1, 4, 40f, 30f, 0.5f, 1));
            Assert.AreEqual(1, AutopilotLogic.LaneEscape(both, 1, 4, 40f, 30f, 0.5f, -1));

            // The other side is no way out (as bad, or the road edge): keep asking for this one
            Assert.AreEqual(1, AutopilotLogic.LaneEscape(Threats(threat, threat, 0f, 0f), 1, 4, 40f, 30f, 0.5f, 1));
            Assert.AreEqual(1, AutopilotLogic.LaneEscape(Threats(threat, 0f, 0f, 0f), 0, 4, 40f, 30f, 0.5f, 1));

            // Serious threats use the same rule with the usual margin
            Assert.AreEqual(-1, AutopilotLogic.LaneEscape(Threats(0f, 0.9f, 0f, 0f), 1, 4, 200f, 30f, 0.5f, 1));
            Assert.AreEqual(1, AutopilotLogic.LaneEscape(Threats(0.85f, 0.9f, 0f, 0f), 1, 4, 200f, 30f, 0.5f, 1));
        }

        [Test]
        public void LaneChangeDuration_QuickWellBelowTheHoldCalmNearIt()
        {
            float hold = 85f;
            float full = HazardPlacement.LaneChangeMaxDuration;

            // Well below the hold speed (attract mode): as quick as the player's
            Assert.AreEqual(HazardPlacement.LaneChangeDuration(25f), AutopilotLogic.LaneChangeDuration(25f, hold), 1e-6f);
            Assert.AreEqual(HazardPlacement.LaneChangeDuration(hold * AutopilotLogic.QuickChangeShare),
                AutopilotLogic.LaneChangeDuration(hold * AutopilotLogic.QuickChangeShare, hold), 1e-5f);

            // Near it and above, the full second. Regression: a car on failed
            // tyres (hold ~63 m/s) held at 50 m/s dodged with the player's
            // 0.75 s change and squealed
            Assert.AreEqual(full, AutopilotLogic.LaneChangeDuration(hold * AutopilotLogic.CalmChangeShare, hold), 1e-6f);
            Assert.AreEqual(full, AutopilotLogic.LaneChangeDuration(hold, hold), 1e-6f);
            Assert.AreEqual(full, AutopilotLogic.LaneChangeDuration(hold * 1.5f, hold), 1e-6f);
            float failedTyresHold = GameFlowLogic.SuspendedRunHoldSpeed(VehicleDynamics.GripShare(1f, true, 1f, false));
            Assert.AreEqual(full, AutopilotLogic.LaneChangeDuration(50f, failedTyresHold), 1e-6f);

            // Never shorter at a higher speed
            float previous = 0f;
            for (float v = 0f; v <= 200f; v += 0.5f)
            {
                float d = AutopilotLogic.LaneChangeDuration(v, hold);
                Assert.GreaterOrEqual(d, previous - 1e-6f, $"{v} m/s");
                previous = d;
            }

            // Bad input still gives a lane change the steering system can run
            foreach (float d in new[] { AutopilotLogic.LaneChangeDuration(float.NaN, hold),
                                        AutopilotLogic.LaneChangeDuration(100f, float.NaN) })
            {
                Assert.GreaterOrEqual(d, HazardPlacement.LaneChangeMinDuration);
                Assert.LessOrEqual(d, full);
            }
        }

        [Test]
        public void LaneChangeCooldown_NeverOutlastsTheLookAhead()
        {
            // A hazard found in the new lane can still be dodged once the
            // cooldown is over, and the look-ahead is inside what it can see
            Assert.LessOrEqual(AutopilotLogic.LaneChangeCooldown, AutopilotLogic.LeaveLaneSeconds);
            Assert.LessOrEqual(AutopilotLogic.LeaveLaneSeconds, AutopilotLogic.DetectionSeconds);
            Assert.Greater(AutopilotLogic.LeaveLaneSeconds, HazardPlacement.LaneChangeMaxDuration);
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
        public void EaseTargetSpeed_AboveTheHold_SettlesFasterThenEasesOn()
        {
            // A run paused at 110 m/s with an 85 m/s hold: SettleDecel down to
            // the hold, ComfortDecel below it (the car keeps pace with the target)
            float eased = 110f;
            for (int i = 0; i < 60; i++)
                eased = AutopilotLogic.EaseTargetSpeed(eased, 35f, eased, 85f, 1f / 60f);
            Assert.AreEqual(110f - AutopilotLogic.SettleDecel, eased, 1e-3f);

            // Crossing the hold inside one frame splits it between the two rates
            float crossing = AutopilotLogic.EaseTargetSpeed(86f, 35f, 86f, 85f, 0.5f);
            float settleTime = 1f / AutopilotLogic.SettleDecel;
            Assert.AreEqual(85f - AutopilotLogic.ComfortDecel * (0.5f - settleTime), crossing, 1e-4f);

            // ...so it is the same at any frame rate
            float slow = 100f, fast = 100f;
            for (int i = 0; i < 30 * 6; i++)
                slow = AutopilotLogic.EaseTargetSpeed(slow, 35f, slow, 85f, 1f / 30f);
            for (int i = 0; i < 144 * 6; i++)
                fast = AutopilotLogic.EaseTargetSpeed(fast, 35f, fast, 85f, 1f / 144f);
            Assert.AreEqual(fast, slow, 1e-2f);

            // At or below the hold, and on the way up, it is the comfort ease
            Assert.AreEqual(AutopilotLogic.EaseTargetSpeed(80f, 35f, 80f, 1f / 60f),
                AutopilotLogic.EaseTargetSpeed(80f, 35f, 80f, 85f, 1f / 60f), 1e-6f);
            Assert.AreEqual(AutopilotLogic.EaseTargetSpeed(20f, 35f, 20f, 1f / 60f),
                AutopilotLogic.EaseTargetSpeed(20f, 35f, 20f, 85f, 1f / 60f), 1e-6f);

            // Never below the goal, never from above a car something else slowed,
            // and bad input reads as the goal
            Assert.AreEqual(85f, AutopilotLogic.EaseTargetSpeed(110f, 85f, 110f, 85f, 10f), 1e-5f);
            Assert.LessOrEqual(AutopilotLogic.EaseTargetSpeed(110f, 85f, 60f, 85f, 1f / 60f), 85f);
            Assert.AreEqual(85f, AutopilotLogic.EaseTargetSpeed(float.NaN, 85f, 100f, 85f, 1f / 60f), 1e-6f);
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
