// Nightflow - Vehicle Dynamics Tests
// Validates momentum (no terminal velocity), grip, lane integration, yaw
// alignment and loss of control. Expected values come from the Python
// reference model the constants were tuned with.

using NUnit.Framework;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Config;
using Nightflow.Systems;
using Nightflow.Utilities;

namespace Nightflow.Tests
{
    [TestFixture]
    public class VehicleDynamicsTests
    {
        private const float Dt = 1f / 60f;
        private const float LaneWidth = GameConstants.LaneWidth;

        #region Momentum

        [Test]
        public void Pace_StartsAtCruiseAndEasesIn()
        {
            Assert.AreEqual(VehicleDynamics.PaceStart, VehicleDynamics.PaceSpeed(0f, 0f), 1e-5f);
            Assert.AreEqual(28.75f, VehicleDynamics.PaceSpeed(30f, 0f), 1e-3f);
            Assert.AreEqual(49f, VehicleDynamics.PaceSpeed(120f, 0f), 1e-3f);
        }

        [Test]
        public void Pace_InvalidInputs_ReadAsRunStart()
        {
            Assert.AreEqual(VehicleDynamics.PaceStart, VehicleDynamics.PaceSpeed(-5f, float.NaN), 1e-5f);
            Assert.AreEqual(VehicleDynamics.PaceStart, VehicleDynamics.PaceSpeed(float.NaN, -1f), 1e-5f);
        }

        [Test]
        public void Pace_DrivingFurtherBuildsMomentumFaster()
        {
            Assert.Greater(VehicleDynamics.PaceSpeed(120f, 8000f), VehicleDynamics.PaceSpeed(120f, 3000f));
            Assert.AreEqual(93.933f, VehicleDynamics.PaceSpeed(240f, 13000f), 1e-2f);
        }

        [Test]
        public void Pace_HasNoTerminalVelocity()
        {
            // The climb never flattens out: every minute adds at least as much as the one before
            float previousGain = 0f;
            for (int minute = 1; minute <= 30; minute++)
            {
                float t = minute * 60f;
                float gain = VehicleDynamics.PaceSpeed(t, 0f) - VehicleDynamics.PaceSpeed(t - 60f, 0f);
                Assert.Greater(gain, previousGain - 1e-3f);
                previousGain = gain;
            }

            Assert.Greater(VehicleDynamics.PaceSpeed(3600f, 0f), 900f);
        }

        [Test]
        public void Pace_GrowthScaleZero_StaysAtStart()
        {
            Assert.AreEqual(VehicleDynamics.PaceStart, VehicleDynamics.PaceSpeed(600f, 50000f, 0f), 1e-5f);
            Assert.Greater(VehicleDynamics.PaceSpeed(120f, 0f, VehicleDynamics.RedlinePaceScale),
                VehicleDynamics.PaceSpeed(120f, 0f));
        }

        [Test]
        public void Pace_IsSmooth_NoFrameJumps()
        {
            float prev = VehicleDynamics.PaceSpeed(0f, 0f);
            float t = 0f, d = 0f;
            for (int i = 0; i < 10 * 60 * 60; i++)
            {
                t += Dt;
                d += prev * Dt;
                float pace = VehicleDynamics.PaceSpeed(t, d);
                Assert.GreaterOrEqual(pace, prev);
                Assert.Less(pace - prev, 0.05f);
                prev = pace;
            }
        }

        [Test]
        public void MomentumFloor_IsShareOfPaceButNeverBelowMinimum()
        {
            Assert.AreEqual(17.5f, VehicleDynamics.MomentumFloor(25f), 1e-4f);
            Assert.AreEqual(70f, VehicleDynamics.MomentumFloor(100f), 1e-4f);
            Assert.AreEqual(GameConstants.MinForwardSpeed, VehicleDynamics.MomentumFloor(5f), 1e-4f);
        }

        [Test]
        public void Boost_ShrinksWithSpeedButNeverReachesZero()
        {
            Assert.AreEqual(VehicleDynamics.BoostAccel, VehicleDynamics.BoostAcceleration(0f), 1e-5f);
            Assert.AreEqual(3.5f, VehicleDynamics.BoostAcceleration(25f), 1e-4f);
            Assert.AreEqual(1.4f, VehicleDynamics.BoostAcceleration(100f), 1e-4f);
            Assert.Greater(VehicleDynamics.BoostAcceleration(1000f), 0f);
        }

        [Test]
        public void StepForward_OffPedals_PulledUpToPaceWithoutOvershoot()
        {
            float v = 20f;
            for (int i = 0; i < 60 * 30; i++)
            {
                v = VehicleDynamics.StepForwardSpeed(v, 40f, 28f, 0f, 0f, 1f, 1f, Dt);
                Assert.LessOrEqual(v, 40f + 1e-4f);
            }

            Assert.AreEqual(40f, v, 0.01f);
        }

        [Test]
        public void StepForward_AbovePace_NothingSlowsTheCar()
        {
            // No drag: momentum you gain is momentum you keep
            float v = 90f;
            for (int i = 0; i < 600; i++)
                v = VehicleDynamics.StepForwardSpeed(v, 40f, 28f, 0f, 0f, 1f, 1f, Dt);

            Assert.AreEqual(90f, v, 1e-4f);
        }

        [Test]
        public void StepForward_FullThrottle_KeepsAcceleratingPastOldCap()
        {
            float v = GameConstants.MaxForwardSpeed;
            float before = v;
            for (int i = 0; i < 60 * 10; i++)
                v = VehicleDynamics.StepForwardSpeed(v, 25f, 17.5f, 1f, 0f, 1f, 1f, Dt);

            Assert.Greater(v, before + 10f);
        }

        [Test]
        public void StepForward_BrakeStopsAtMomentumFloor()
        {
            float pace = 100f;
            float floor = VehicleDynamics.MomentumFloor(pace);
            float v = 100f;
            for (int i = 0; i < 60 * 5; i++)
                v = VehicleDynamics.StepForwardSpeed(v, pace, floor, 0f, 1f, 1f, 1f, Dt);

            Assert.AreEqual(floor, v, 1e-3f);
        }

        [TestCase(0f)]
        [TestCase(120f)]
        public void StepForward_HeldBrake_TrailsTheRisingFloor(float brakeFrom)
        {
            // Regression: a held brake skipped the pull, so the speed froze
            // while the floor kept rising with the pace (a permanent cruise control)
            float v = 20f, t = 0f, d = 0f;
            float worstLag = 0f;
            for (int i = 0; i < 60 * 300; i++)
            {
                float pace = VehicleDynamics.PaceSpeed(t, d);
                float floor = VehicleDynamics.MomentumFloor(pace);
                float brake = t >= brakeFrom ? 1f : 0f;
                v = VehicleDynamics.StepForwardSpeed(v, pace, floor, 0f, brake, 1f, 1f, Dt);
                d += v * Dt;
                t += Dt;

                if (t > brakeFrom + 5f)
                    worstLag = math.max(worstLag, floor - v);
            }

            Assert.Less(worstLag, 0.5f);
        }

        [Test]
        public void StepForward_HeldBrake_NeverPullsWithoutAPace()
        {
            // Autopilot / no run: pace 0 disables every pull, the brake still trims
            float v = VehicleDynamics.StepForwardSpeed(20f, 0f, GameConstants.MinForwardSpeed, 0f, 1f, 1f, 1f, Dt);
            Assert.Less(v, 20f);

            float slow = VehicleDynamics.StepForwardSpeed(GameConstants.MinForwardSpeed, 0f,
                GameConstants.MinForwardSpeed, 0f, 1f, 1f, 1f, Dt);
            Assert.AreEqual(GameConstants.MinForwardSpeed, slow, 1e-5f);
        }

        [Test]
        public void StepForward_NeverBelowMinimumSpeed()
        {
            float v = VehicleDynamics.StepForwardSpeed(2f, 0f, 0f, 0f, 1f, 1f, 1f, Dt);
            Assert.AreEqual(GameConstants.MinForwardSpeed, v, 1e-5f);
        }

        [Test]
        public void StepForward_DamagedEngine_BuildsMoreSlowly()
        {
            float healthy = VehicleDynamics.StepForwardSpeed(50f, 25f, 17.5f, 1f, 0f, 1f, 1f, Dt);
            float limp = VehicleDynamics.StepForwardSpeed(50f, 25f, 17.5f, 1f, 0f, 0.5f, 1f, Dt);
            Assert.Greater(healthy, limp);
            Assert.Greater(limp, 50f);
        }

        #endregion

        #region Grip

        [Test]
        public void Grip_FallsWithSpeed_HalfAtLiftSpeed()
        {
            float g0 = Grip(0f);
            Assert.AreEqual(VehicleDynamics.GripBase, g0, 1e-4f);
            Assert.AreEqual(VehicleDynamics.GripBase * 0.5f, Grip(VehicleDynamics.LiftSpeed), 1e-3f);
            Assert.AreEqual(21.189f, Grip(100f), 1e-2f);

            float prev = g0;
            for (float v = 5f; v < 400f; v += 5f)
            {
                float g = Grip(v);
                Assert.Less(g, prev);
                prev = g;
            }
        }

        [Test]
        public void Grip_WearHandbrakeAndAutopilot()
        {
            Assert.AreEqual(22.4f, VehicleDynamics.GripLimit(0f, 0f, false, 1f, false, false, false), 1e-3f);
            Assert.AreEqual(14.08f, VehicleDynamics.GripLimit(0f, 1f, true, 1f, true, false, false), 1e-3f);
            Assert.AreEqual(Grip(100f) * VehicleDynamics.HandbrakeGripFactor,
                VehicleDynamics.GripLimit(100f, 1f, false, 1f, false, true, false), 1e-3f);
            Assert.AreEqual(Grip(100f) * VehicleDynamics.AutopilotGripFactor,
                VehicleDynamics.GripLimit(100f, 1f, false, 1f, false, false, true), 1e-3f);
        }

        [Test]
        public void LaneLoad_NegligibleSlowGrowsWithSpeed()
        {
            Assert.AreEqual(0f, VehicleDynamics.LaneLoadFactor(15f), 1e-6f);
            Assert.Less(VehicleDynamics.LaneLoadFactor(30f), VehicleDynamics.LaneLoadFactor(80f));
            Assert.AreEqual(1f, VehicleDynamics.LaneLoadFactor(100f), 1e-5f);
        }

        [Test]
        public void ResolveLateral_WithinGrip_SpringGetsWhatItAsks()
        {
            Assert.AreEqual(12f, VehicleDynamics.ResolveLateralAcceleration(12f, 5f, 0.9f), 1e-5f);
        }

        [Test]
        public void ResolveLateral_PastLimitOnCurve_CarRunsWide()
        {
            // Right-hand curve needs +30 m/s^2 but usage is 1.5: the car drifts left (outward)
            float a = VehicleDynamics.ResolveLateralAcceleration(0f, 30f, 1.5f);
            Assert.AreEqual(-10f, a, 1e-4f);
        }

        [Test]
        public void SmoothUsage_BuildsOverTimeAndIgnoresNaN()
        {
            float u = VehicleDynamics.SmoothUsage(0f, 2f, Dt);
            Assert.Greater(u, 0f);
            Assert.Less(u, 2f);
            Assert.AreEqual(0f, VehicleDynamics.SmoothUsage(float.NaN, float.NaN, Dt), 1e-6f);
        }

        #endregion

        #region Lane integration

        [TestCase(40f)]
        [TestCase(80f)]
        [TestCase(100f)]
        public void LaneChange_ArrivesInNewLaneAndSettles(float speed)
        {
            var result = RunLaneChange(speed, 0f, Dt, 3f, 1);

            Assert.AreEqual(LaneCentre(2), result.Lateral.Offset, 0.01f);
            Assert.AreEqual(0f, result.Lateral.Velocity, 0.05f);
            Assert.IsFalse(result.LostControl);
        }

        [Test]
        public void LaneChange_IsQuick_NotTheOldCrawl()
        {
            // Regression: lateral motion was scaled by dt twice and took ~18 s
            var result = RunLaneChange(60f, 0f, Dt, 1.2f, 1);
            Assert.AreEqual(LaneCentre(2), result.Lateral.Offset, 0.25f);
        }

        [Test]
        public void LaneChange_LoadGrowsWithSpeed_SlidesOnlyAtExtremeSpeed()
        {
            Assert.Less(RunLaneChange(80f, 0f, Dt, 3f, 1).MaxUsage, VehicleDynamics.TractionWarningUsage);
            Assert.Greater(RunLaneChange(120f, 0f, Dt, 3f, 1).MaxUsage, 1f);
            Assert.IsFalse(RunLaneChange(120f, 0f, Dt, 3f, 1).LostControl);
            Assert.IsTrue(RunLaneChange(140f, 0f, Dt, 3f, 1).LostControl);
        }

        [Test]
        public void LaneChange_IsFrameRateIndependent()
        {
            var slow = RunLaneChange(100f, 0f, 1f / 30f, 3f, 1);
            var fast = RunLaneChange(100f, 0f, 1f / 144f, 3f, 1);

            Assert.AreEqual(fast.Lateral.Offset, slow.Lateral.Offset, 0.01f);
            Assert.AreEqual(fast.MaxUsage, slow.MaxUsage, 0.05f);
        }

        [Test]
        public void SustainedTightArc_HoldsAt80_SpinsOutAt90()
        {
            // A constant R = 300 m arc for 4 s: a worst case the generator never
            // builds (it reaches R = 300 m only for a metre or two at segment
            // joins, see GeneratedBend_*), kept as a regression on the grip model
            var safe = RunLaneChange(80f, 1f / 300f, Dt, 4f, 0);
            Assert.IsFalse(safe.LostControl);
            Assert.AreEqual(LaneCentre(1), safe.Lateral.Offset, 0.1f);

            var tooFast = RunLaneChange(90f, 1f / 300f, Dt, 4f, 0);
            Assert.IsTrue(tooFast.LostControl);
            Assert.Less(tooFast.Lateral.Offset, LaneCentre(1));   // ran wide (outward = left)
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        public void GeneratedBend_GripsTo95_SlidesAt100_LostAt120(float frameTime)
        {
            // The tightest bend the track generator can make, read the way
            // LaneMagnetismSystem reads it (t = progress in world z)
            Assert.Less(HoldLaneThroughGeneratedBend(95f, frameTime).MaxUsage, 1f);

            var sliding = HoldLaneThroughGeneratedBend(100f, frameTime);
            Assert.Greater(sliding.MaxUsage, 1f);
            Assert.IsFalse(sliding.LostControl);

            Assert.IsFalse(HoldLaneThroughGeneratedBend(110f, frameTime).LostControl);
            Assert.IsTrue(HoldLaneThroughGeneratedBend(120f, frameTime).LostControl);
        }

        [Test]
        public void SuspendedRunHoldSpeed_IsWellInsideTheAutopilotsGrip()
        {
            // The autopilot keeps a paused or idle run at up to
            // SuspendedRunMaxSpeed; it must never be near a slide there
            float v = GameFlowLogic.SuspendedRunMaxSpeed;
            Assert.Less(HoldLaneThroughGeneratedBend(v, Dt, autopilot: true).MaxUsage, 0.7f);
            Assert.Less(RunLaneChange(v, 0f, Dt, 3f, 1, autopilot: true).MaxUsage, 0.7f);
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        public void LaneChange_HeldIntoABlockedLane_NoHarderThanATap(float frameTime)
        {
            // Regression: the in-lane lean's rate was fed to the spring's damper,
            // so a change started from a full lean (key held into a blocked lane
            // until it cleared) kicked the car back toward the old lane first
            // and slid ~20 m/s earlier than a tapped change
            foreach (float v in new[] { 80f, 100f, 110f })
            {
                var tapped = RunLaneChange(v, 0f, frameTime, 3f, 1);
                var held = RunLaneChange(v, 0f, frameTime, 3f, 1, heldSteer: 1f, leanedAtStart: true);

                Assert.LessOrEqual(held.MaxUsage, tapped.MaxUsage + 0.02f, $"{v} m/s");
                Assert.IsFalse(held.LostControl, $"{v} m/s");
                Assert.Greater(held.MinOffset, LaneCentre(1) + VehicleDynamics.InLaneOffsetMax - 0.05f,
                    $"{v} m/s: ran back toward the old lane");
            }
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        public void LaneChange_KeyHeldIntoTheEdgeLane_NoHarderThanATap(float frameTime)
        {
            // The lean comes back on as the change completes with the key held
            foreach (float v in new[] { 80f, 100f, 110f })
            {
                var tapped = RunLaneChange(v, 0f, frameTime, 3f, 1, fromLane: 2);
                var held = RunLaneChange(v, 0f, frameTime, 3f, 1, heldSteer: 1f, fromLane: 2);
                Assert.LessOrEqual(held.MaxUsage, tapped.MaxUsage + 0.02f, $"{v} m/s");
                Assert.IsFalse(held.LostControl, $"{v} m/s");
            }
        }

        [Test]
        public void InLaneOffset_LeansUpToThresholdThenHolds()
        {
            Assert.AreEqual(0f, VehicleDynamics.InLaneOffset(0f), 1e-6f);
            Assert.AreEqual(VehicleDynamics.InLaneOffsetMax, VehicleDynamics.InLaneOffset(1f), 1e-6f);
            Assert.AreEqual(-VehicleDynamics.InLaneOffsetMax * 0.5f,
                VehicleDynamics.InLaneOffset(-VehicleDynamics.LaneChangeSteerThreshold * 0.5f), 1e-5f);
        }

        [Test]
        public void LaneBlend_SmoothstepWithRate()
        {
            float start = VehicleDynamics.LaneBlend(0f, 1f, out float r0);
            float mid = VehicleDynamics.LaneBlend(0.5f, 1f, out float rMid);
            float end = VehicleDynamics.LaneBlend(1f, 1f, out float rEnd);

            Assert.AreEqual(0f, start, 1e-6f);
            Assert.AreEqual(0f, r0, 1e-6f);
            Assert.AreEqual(0.5f, mid, 1e-6f);
            Assert.AreEqual(1.5f, rMid, 1e-5f);
            Assert.AreEqual(1f, end, 1e-6f);
            Assert.AreEqual(0f, rEnd, 1e-6f);
        }

        [TestCase(0.1f)]
        [TestCase(0.25f)]
        [TestCase(0.45f)]
        public void LaneBlend_AbortCarriesOnFromWhereTheCarIs(float progressAtAbort)
        {
            const float duration = 0.8f;

            // Just before the abort, heading for the new lane
            float before = VehicleDynamics.LaneBlend(progressAtAbort * duration, duration, false, out float rateBefore);

            // SteeringSystem mirrors the timer and flags the abort
            float mirrored = duration * (1f - progressAtAbort);
            float after = VehicleDynamics.LaneBlend(mirrored, duration, true, out float rateAfter);

            Assert.AreEqual(before, after, 1e-5f, "lateral target must not jump on an abort");
            Assert.Greater(rateBefore, 0f);
            Assert.Less(rateAfter, 0f, "an aborted change heads back to the original lane");
            Assert.AreEqual(rateBefore, -rateAfter, 1e-4f);

            // ...and finishes exactly back in the original lane
            Assert.AreEqual(0f, VehicleDynamics.LaneBlend(duration, duration, true, out _), 1e-6f);
        }

        [Test]
        public void EdgeAcceleration_PushesBackOnlyPastSoftEdge()
        {
            float half = GameConstants.RoadWidth * 0.5f;
            Assert.AreEqual(0f, VehicleDynamics.EdgeAcceleration(5f, half), 1e-6f);
            Assert.Less(VehicleDynamics.EdgeAcceleration(7f, half), 0f);
            Assert.Greater(VehicleDynamics.EdgeAcceleration(-7f, half), 0f);
        }

        #endregion

        #region Yaw

        [Test]
        public void Yaw_SteeringAtSpeed_LeansThenStraightensWhenReleased()
        {
            // Regression: yaw used to integrate steering with no restoring force,
            // leaving the car permanently crooked after every lane change
            var drift = new DriftState();
            var input = YawInput(100f, 1f, false);
            for (int i = 0; i < 240; i++)
                VehicleDynamics.StepYaw(ref drift, input, 1f / 120f);

            Assert.AreEqual(0.075f, drift.YawOffset, 0.01f);

            input.Steer = 0f;
            for (int i = 0; i < 480; i++)
                VehicleDynamics.StepYaw(ref drift, input, 1f / 120f);

            Assert.AreEqual(0f, drift.YawOffset, 1e-3f);
        }

        [Test]
        public void Yaw_HandbrakeDriftAtLowSpeed_SurvivableAndRecovers()
        {
            var drift = new DriftState();
            float worst = HoldHandbrake(ref drift, 30f, 0.8f);

            Assert.IsTrue(drift.IsDrifting);
            Assert.Greater(worst, 0.5f);
            Assert.IsFalse(VehicleDynamics.IsLostControl(worst, 30f));

            var release = YawInput(30f, 0f, false);
            for (int i = 0; i < 480; i++)
                VehicleDynamics.StepYaw(ref drift, release, 1f / 120f);

            Assert.IsFalse(drift.IsDrifting);
            Assert.AreEqual(0f, drift.YawOffset, 1e-3f);
        }

        [Test]
        public void Yaw_HandbrakeAtHighSpeed_LosesControl()
        {
            var drift = new DriftState();
            float worst = HoldHandbrake(ref drift, 70f, 0.8f);
            Assert.IsTrue(VehicleDynamics.IsLostControl(worst, 70f));
        }

        [Test]
        public void Yaw_PastGripLimit_RearStepsOutTowardLoad()
        {
            var drift = new DriftState();
            var input = YawInput(90f, 0f, false);
            input.Usage = 1.6f;
            input.LoadDirection = 1f;
            for (int i = 0; i < 60; i++)
                VehicleDynamics.StepYaw(ref drift, input, 1f / 120f);

            Assert.Greater(drift.YawOffset, 0.05f);
        }

        [Test]
        public void Yaw_StaysWrapped()
        {
            var drift = new DriftState { YawOffset = 3.1f, YawRate = VehicleDynamics.MaxYawRate };
            var input = YawInput(30f, 1f, true);
            for (int i = 0; i < 600; i++)
            {
                VehicleDynamics.StepYaw(ref drift, input, 1f / 120f);
                Assert.LessOrEqual(math.abs(drift.YawOffset), math.PI + 1e-4f);
            }
        }

        #endregion

        #region Loss of control

        [Test]
        public void LostControl_SlowSpinsNeverEndTheRun()
        {
            Assert.Greater(VehicleDynamics.LostControlSlip(20f), math.PI);
            Assert.Greater(VehicleDynamics.LostControlSlip(VehicleDynamics.SpinSafeSpeed), math.PI);
            Assert.IsFalse(VehicleDynamics.IsLostControl(math.PI - 0.01f, 30f));
        }

        [Test]
        public void LostControl_ThresholdTightensWithSpeed()
        {
            Assert.AreEqual(2.0458f, VehicleDynamics.LostControlSlip(55f), 1e-3f);
            Assert.AreEqual(VehicleDynamics.MaxSafeSlipAtSpeed, VehicleDynamics.LostControlSlip(75f), 1e-4f);
            Assert.AreEqual(VehicleDynamics.MaxSafeSlipAtSpeed, VehicleDynamics.LostControlSlip(300f), 1e-4f);
            Assert.IsTrue(VehicleDynamics.IsLostControl(-1f, 90f));

            float prev = VehicleDynamics.LostControlSlip(0f);
            for (float v = 1f; v < 120f; v += 1f)
            {
                float s = VehicleDynamics.LostControlSlip(v);
                Assert.LessOrEqual(s, prev + 1e-5f);
                prev = s;
            }
        }

        #endregion

        #region Helpers

        [Test]
        public void WrapAngle_KeepsAnglesInRange()
        {
            Assert.AreEqual(0.5f, VehicleDynamics.WrapAngle(0.5f), 1e-6f);
            Assert.AreEqual(-math.PI + 0.5f, VehicleDynamics.WrapAngle(3f * math.PI + 0.5f), 1e-4f);
            Assert.AreEqual(math.PI - 0.5f, VehicleDynamics.WrapAngle(-3f * math.PI - 0.5f), 1e-4f);
            Assert.AreEqual(0f, VehicleDynamics.WrapAngle(float.NaN), 1e-6f);
        }

        [Test]
        public void SubSteps_CoverTheFrameWithSmallSteps()
        {
            Assert.AreEqual(2, VehicleDynamics.SubSteps(1f / 60f, out float h60));
            Assert.AreEqual(1f / 120f, h60, 1e-6f);
            Assert.AreEqual(4, VehicleDynamics.SubSteps(1f / 30f, out _));
            Assert.AreEqual(1, VehicleDynamics.SubSteps(1f / 240f, out float h240));
            Assert.AreEqual(1f / 240f, h240, 1e-6f);
            Assert.AreEqual(0, VehicleDynamics.SubSteps(0f, out _));
            Assert.AreEqual(0, VehicleDynamics.SubSteps(float.NaN, out _));

            // A long hitch is clamped rather than integrated in one huge step
            Assert.AreEqual(VehicleDynamics.MaxSubSteps, VehicleDynamics.SubSteps(0.5f, out float hHitch));
            Assert.LessOrEqual(hHitch, VehicleDynamics.MaxSubStep + 1e-6f);
        }

        [Test]
        public void Damp_IsFrameRateIndependent()
        {
            // Two half frames blend exactly as much as one whole frame
            float half = VehicleDynamics.Damp(5f, Dt * 0.5f);
            float twoHalves = 1f - (1f - half) * (1f - half);
            Assert.AreEqual(VehicleDynamics.Damp(5f, Dt), twoHalves, 1e-6f);
            Assert.AreEqual(0f, VehicleDynamics.Damp(5f, -1f), 1e-6f);
        }

        [Test]
        public void SpeedFeel_KeepsBuildingWithoutReachingOne()
        {
            Assert.AreEqual(0f, VehicleDynamics.SpeedFeel(5f), 1e-6f);
            float prev = 0f;
            for (float v = 15f; v < 300f; v += 5f)
            {
                float f = VehicleDynamics.SpeedFeel(v);
                Assert.Greater(f, prev);
                Assert.Less(f, 1f);
                prev = f;
            }
        }

        #endregion

        #region Lane target modifiers

        [Test]
        public void Avoidance_MovesAtMostAvoidanceRate()
        {
            // EmergencyVehicleSystem's request jumps (cone entry, the vehicle
            // crossing behind the car); the lane target must not
            float a = VehicleDynamics.StepAvoidance(0f, 2.5f, LaneCentre(1), 7.2f, Dt);
            Assert.AreEqual(VehicleDynamics.AvoidanceRate * Dt, a, 1e-5f);

            float flipped = VehicleDynamics.StepAvoidance(2.5f, -2.5f, LaneCentre(1), 7.2f, Dt);
            Assert.AreEqual(2.5f - VehicleDynamics.AvoidanceRate * Dt, flipped, 1e-5f);

            // Reaches the request in time, and the same at any frame rate
            float slow = 0f, fast = 0f;
            for (int i = 0; i < 30 * 3; i++)
                slow = VehicleDynamics.StepAvoidance(slow, 2.5f, LaneCentre(1), 7.2f, 1f / 30f);
            for (int i = 0; i < 144 * 3; i++)
                fast = VehicleDynamics.StepAvoidance(fast, 2.5f, LaneCentre(1), 7.2f, 1f / 144f);
            Assert.AreEqual(2.5f, slow, 1e-4f);
            Assert.AreEqual(2.5f, fast, 1e-4f);
        }

        [Test]
        public void Avoidance_NeverAimsPastTheSoftEdge()
        {
            // Edge lane yielding outward: stops short of the soft edge (the
            // barrier is not grip-limited, a target past it loads the tyres)
            float halfRoad = GameConstants.RoadWidth * 0.5f;
            float limit = halfRoad * VehicleDynamics.EdgeSoftZoneRatio - VehicleDynamics.AvoidanceEdgeMargin;
            float a = 0f;
            for (int i = 0; i < 60 * 5; i++)
                a = VehicleDynamics.StepAvoidance(a, -2.88f, LaneCentre(0), halfRoad, Dt);
            Assert.AreEqual(-limit - LaneCentre(0), a, 1e-4f);

            // Yielding inward from the edge lane is not limited
            float inward = 0f;
            for (int i = 0; i < 60 * 5; i++)
                inward = VehicleDynamics.StepAvoidance(inward, 2.88f, LaneCentre(0), halfRoad, Dt);
            Assert.AreEqual(2.88f, inward, 1e-4f);
        }

        [Test]
        public void Avoidance_LaneChangeTowardTheEdge_TotalStaysInsideAndContinuous()
        {
            // Regression: a yield already applied toward the edge unwound at its
            // rate while a lane change moved the base target into the edge lane,
            // so the total target ran past the soft edge
            float halfRoad = GameConstants.RoadWidth * 0.5f;
            float limit = halfRoad * VehicleDynamics.EdgeSoftZoneRatio - VehicleDynamics.AvoidanceEdgeMargin;
            float avoid = 2.2f;
            float previousTotal = LaneCentre(2) + avoid;
            for (int i = 0; i <= 60; i++)
            {
                float blend = VehicleDynamics.LaneBlend(i * Dt, 1f, out _);
                float baseTarget = math.lerp(LaneCentre(2), LaneCentre(3), blend);
                avoid = VehicleDynamics.StepAvoidance(avoid, 2.2f, baseTarget, halfRoad, Dt);
                float total = baseTarget + avoid;

                Assert.LessOrEqual(total, limit + 1e-4f, $"frame {i}");
                Assert.Less(math.abs(total - previousTotal), 0.2f, $"frame {i}: target jumped");
                previousTotal = total;
            }
        }

        [Test]
        public void YieldDuringLaneChange_FadesWithTheBlend()
        {
            Assert.AreEqual(2f, VehicleDynamics.YieldDuringLaneChange(2f, 0f, 0f, out float v0), 1e-6f);
            Assert.AreEqual(0f, v0, 1e-6f);
            Assert.AreEqual(1f, VehicleDynamics.YieldDuringLaneChange(2f, 0.5f, 1.5f, out float vMid), 1e-6f);
            Assert.AreEqual(-3f, vMid, 1e-6f);
            Assert.AreEqual(0f, VehicleDynamics.YieldDuringLaneChange(2f, 1f, 0f, out _), 1e-6f);
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        public void Yield_LaneChangeIntoTheEdgeLane_NoHarderThanAPlainChange(float frameTime)
        {
            // Regression: yielding toward the edge for an ambulance and then
            // moving over into the edge lane ran the target into the soft edge
            // while the spring still chased the lane change's speed (usage 1.5
            // at 100 m/s against 0.87 for the same change on its own)
            float halfRoad = GameConstants.RoadWidth * 0.5f;
            float limit = halfRoad * VehicleDynamics.EdgeSoftZoneRatio - VehicleDynamics.AvoidanceEdgeMargin;
            foreach (float v in new[] { 90f, 100f, 110f })
            {
                var plain = RunLaneChange(v, 0f, frameTime, 3f, 1, fromLane: 2);
                var yielding = RunLaneChange(v, 0f, frameTime, 3f, 1, fromLane: 2, yieldRequest: 2.2f);

                Assert.LessOrEqual(yielding.MaxUsage, plain.MaxUsage + 0.05f, $"{v} m/s");
                Assert.IsFalse(yielding.LostControl, $"{v} m/s");
                Assert.LessOrEqual(yielding.MaxTarget, limit + 1e-3f, $"{v} m/s");
                Assert.AreEqual(LaneCentre(3) + VehicleDynamics.StepAvoidance(0f, 2.2f, LaneCentre(3), halfRoad, 10f),
                    yielding.Lateral.Offset, 0.05f, $"{v} m/s: settles in the edge lane");
            }
        }

        [Test]
        public void Avoidance_NoRequest_NeverMovesTheTarget()
        {
            // A lean already outside the limit is left alone, and NaN reads as 0
            float halfRoad = GameConstants.RoadWidth * 0.5f;
            float beyond = -(halfRoad * VehicleDynamics.EdgeSoftZoneRatio);
            Assert.AreEqual(0f, VehicleDynamics.StepAvoidance(0f, 0f, beyond, halfRoad, Dt), 1e-6f);
            Assert.AreEqual(0f, VehicleDynamics.StepAvoidance(0f, float.NaN, LaneCentre(1), halfRoad, Dt), 1e-6f);
            Assert.AreEqual(0f, VehicleDynamics.StepAvoidance(float.NaN, 0f, LaneCentre(1), halfRoad, Dt), 1e-6f);
        }

        [Test]
        public void ForkMagnetism_EasesToFloorAcrossTheForkAndEndsWithIt()
        {
            Assert.AreEqual(1f, VehicleDynamics.ForkMagnetismScale(false, 0.9f), 1e-6f);
            Assert.AreEqual(1f, VehicleDynamics.ForkMagnetismScale(true, 0f), 1e-6f);
            Assert.AreEqual(0.85f, VehicleDynamics.ForkMagnetismScale(true, 0.5f), 1e-5f);
            Assert.AreEqual(VehicleDynamics.ForkMagnetism, VehicleDynamics.ForkMagnetismScale(true, 1f), 1e-6f);
            Assert.AreEqual(1f, VehicleDynamics.ForkMagnetismScale(true, float.NaN), 1e-6f);
        }

        [Test]
        public void LeanAt_ExactExponential()
        {
            Assert.AreEqual(0.9f, VehicleDynamics.LeanAt(0.9f, 0.9f, 1f), 1e-6f);
            float half = VehicleDynamics.LeanAt(0f, 0.9f, Dt * 0.5f);
            Assert.AreEqual(VehicleDynamics.LeanAt(0f, 0.9f, Dt), VehicleDynamics.LeanAt(half, 0.9f, Dt * 0.5f), 1e-6f);
            Assert.AreEqual(0.9f, VehicleDynamics.LeanAt(float.NaN, 0.9f, Dt), 1e-6f);
        }

        #endregion

        #region Simulation helpers

        private struct LaneChangeResult
        {
            public LateralState Lateral;
            public float MaxUsage;
            public float MinOffset;
            public float MaxTarget;
            public float MaxOffset;
            public bool LostControl;
        }

        private static float Grip(float speed)
        {
            return VehicleDynamics.GripLimit(speed, 1f, false, 1f, false, false, false);
        }

        private static float LaneCentre(int lane)
        {
            return (lane - 1.5f) * LaneWidth;
        }

        private static YawInputs YawInput(float speed, float steer, bool handbrake)
        {
            return new YawInputs
            {
                ForwardSpeed = speed,
                LateralSpeed = 0f,
                Steer = steer,
                Handbrake = handbrake,
                Usage = 0f,
                LoadDirection = 0f,
                SteerResponse = 1f,
                RecoveryResponse = 1f
            };
        }

        private static float HoldHandbrake(ref DriftState drift, float speed, float seconds)
        {
            var input = YawInput(speed, 1f, true);
            float worst = 0f;
            int steps = (int)(seconds * 120f);
            for (int i = 0; i < steps; i++)
            {
                float slip = VehicleDynamics.StepYaw(ref drift, input, 1f / 120f);
                worst = math.max(worst, math.abs(slip));
            }
            return worst;
        }

        /// <summary>
        /// Mirrors LaneMagnetismSystem + VehicleMovementSystem for one car at a
        /// constant speed: fromLane to fromLane + direction (0 = hold the lane)
        /// on a road of constant curvature, optionally with a steer key held the
        /// whole time (leaning the car while it is not changing lanes) and
        /// starting from a settled lean (held into a blocked lane until it
        /// cleared), or driven by the autopilot (more grip and magnetism).
        /// </summary>
        private static LaneChangeResult RunLaneChange(float speed, float curvature, float frameTime,
            float seconds, int direction, float heldSteer = 0f, bool leanedAtStart = false,
            int fromLane = 1, bool autopilot = false, float yieldRequest = 0f)
        {
            float from = LaneCentre(fromLane);
            float to = LaneCentre(fromLane + direction);
            float duration = HazardPlacement.LaneChangeDuration(speed);
            float magnetism = math.clamp(math.sqrt(speed / 40f), 0.75f, 1.25f) * (autopilot ? 1.5f : 1f);
            float grip = VehicleDynamics.GripLimit(speed, 1f, false, 1f, false, false, autopilot);
            float lean = leanedAtStart ? VehicleDynamics.InLaneOffset(heldSteer) : 0f;

            // An emergency yield already settled before the change starts
            float halfRoad = GameConstants.RoadWidth * 0.5f;
            float avoid = 0f;
            for (int i = 0; i < 600; i++)
                avoid = VehicleDynamics.StepAvoidance(avoid, yieldRequest, from + lean, halfRoad, 1f / 60f);
            float applied = avoid, yieldShare = 1f;
            bool wasChanging = false;

            var lateral = new LateralState { Offset = from + lean + avoid };
            var drift = new DriftState();
            var result = new LaneChangeResult { MinOffset = lateral.Offset, MaxOffset = lateral.Offset, MaxTarget = lateral.Offset };

            float timer = 0f;
            bool changing = direction != 0;
            int frames = (int)math.round(seconds / frameTime);

            for (int f = 0; f < frames; f++)
            {
                float target = to;
                float targetVelocity = 0f;
                float blend = 0f, rate = 0f;
                bool changingThisFrame = changing;
                if (changing)
                {
                    blend = VehicleDynamics.LaneBlend(timer, duration, out rate);
                    target = math.lerp(from, to, blend);
                    targetVelocity = (to - from) * rate;
                    timer += frameTime;
                    if (timer >= duration)
                        changing = false;
                }

                // In-lane lean: position only, never fed to the spring's damper,
                // following its exact path through the frame
                float desiredLean = changing ? 0f : VehicleDynamics.InLaneOffset(heldSteer);
                float leanStart = lean;
                lean = VehicleDynamics.LeanAt(leanStart, desiredLean, frameTime);

                // Emergency yield, as LaneMagnetismSystem applies it: a lane
                // change takes it over (fades with the blend, rate fed)
                if (!changingThisFrame && wasChanging)
                    avoid *= yieldShare;
                wasChanging = changingThisFrame;
                float appliedStart = applied;
                avoid = VehicleDynamics.StepAvoidance(avoid, yieldRequest, target + lean, halfRoad, frameTime);
                applied = avoid;
                float feed = targetVelocity;
                if (changingThisFrame)
                {
                    applied = VehicleDynamics.YieldDuringLaneChange(avoid, blend, rate, out float fade);
                    yieldShare = 1f - blend;
                    feed += fade;
                }

                var input = new LateralInputs
                {
                    TargetOffset = target + lean + applied,
                    TargetVelocity = feed,
                    Magnetism = magnetism,
                    CurveAccel = VehicleDynamics.CurveAcceleration(speed, curvature),
                    Grip = grip,
                    ForwardSpeed = speed,
                    HalfRoadWidth = GameConstants.RoadWidth * 0.5f,
                    SlipCoupling = 0f,
                    Yaw = drift.YawOffset
                };

                int n = VehicleDynamics.SubSteps(frameTime, out float h);
                for (int i = 0; i < n; i++)
                {
                    input.TargetOffset = target + targetVelocity * (i * h)
                        + VehicleDynamics.LeanAt(leanStart, desiredLean, (i + 1) * h)
                        + math.lerp(appliedStart, applied, (i + 1) / (float)n);
                    result.MaxTarget = math.max(result.MaxTarget, input.TargetOffset);
                    VehicleDynamics.StepLateral(ref lateral, input, h);
                }

                var yaw = YawInput(speed, 0f, false);
                yaw.LateralSpeed = lateral.Velocity;
                yaw.Usage = lateral.Usage;
                yaw.LoadDirection = lateral.LoadDirection;

                float slip = 0f;
                for (int i = 0; i < n; i++)
                    slip = VehicleDynamics.StepYaw(ref drift, yaw, h);

                result.MaxUsage = math.max(result.MaxUsage, lateral.Usage);
                result.MinOffset = math.min(result.MinOffset, lateral.Offset);
                result.MaxOffset = math.max(result.MaxOffset, lateral.Offset);
                if (!autopilot && VehicleDynamics.IsLostControl(slip, speed))
                    result.LostControl = true;
            }

            result.Lateral = lateral;
            return result;
        }

        /// <summary>
        /// The tightest bend TrackGenerationSystem can build: segments of
        /// SegmentLength with Hermite tangents at half the chord
        /// (TangentAlpha 0.5), every one turning the same way at the full
        /// MaxYawChange, the yaw cut back until the 11-sample peak curvature is
        /// 1 / MinCurveRadius. The heading starts turned left so the chain stays
        /// within the generator's ~25 degrees of +Z.
        /// </summary>
        private static void TightestGeneratedBend(int count,
            out float3[] p0, out float3[] t0, out float3[] p1, out float3[] t1)
        {
            const float length = GameConstants.SegmentLength;
            const float tangentAlpha = 0.5f;
            const float maxYaw = 0.15f;
            const float minRadius = 300f;

            p0 = new float3[count];
            t0 = new float3[count];
            p1 = new float3[count];
            t1 = new float3[count];

            float3 position = float3.zero;
            float3 tangent = math.mul(quaternion.RotateY(-0.2f), new float3(0f, 0f, 1f));

            for (int k = 0; k < count; k++)
            {
                float yaw = maxYaw;
                float3 end = float3.zero, ta = float3.zero, tb = float3.zero;
                for (int pass = 0; pass < 2; pass++)
                {
                    float3 endTangent = math.mul(quaternion.RotateY(yaw), tangent);
                    end = position + math.normalize(tangent + endTangent) * length;
                    ta = tangent * length * tangentAlpha;
                    tb = endTangent * length * tangentAlpha;

                    float peak = 0f;
                    for (int i = 0; i <= 10; i++)
                        peak = math.max(peak, SplineUtilities.EvaluateCurvature(position, ta, end, tb, i / 10f));

                    if (pass == 0 && peak > 1f / minRadius)
                        yaw *= (1f / minRadius) / peak;
                    else
                        break;
                }

                p0[k] = position;
                t0[k] = ta;
                p1[k] = end;
                t1[k] = tb;

                position = end;
                tangent = math.normalize(tb);
            }
        }

        /// <summary>
        /// A car holding its lane at a constant speed through the tightest
        /// generated bend, reading the curvature the way LaneMagnetismSystem
        /// does (t = progress through the segment in world z).
        /// </summary>
        private static LaneChangeResult HoldLaneThroughGeneratedBend(float speed, float frameTime, bool autopilot = false)
        {
            TightestGeneratedBend(6, out var p0, out var t0, out var p1, out var t1);

            float magnetism = math.clamp(math.sqrt(speed / 40f), 0.75f, 1.25f) * (autopilot ? 1.5f : 1f);
            float grip = VehicleDynamics.GripLimit(speed, 1f, false, 1f, false, false, autopilot);
            float lane = LaneCentre(1);

            var lateral = new LateralState { Offset = lane };
            var drift = new DriftState();
            var result = new LaneChangeResult { MinOffset = lane, MaxOffset = lane };

            float z = p0[0].z;
            int segment = 0;
            while (segment < p0.Length)
            {
                float t = math.saturate((z - p0[segment].z) / (p1[segment].z - p0[segment].z));
                float curvature = SplineUtilities.EvaluateSignedCurvature(
                    p0[segment], t0[segment], p1[segment], t1[segment], t);
                SplineUtilities.BuildFrameAtT(p0[segment], t0[segment], p1[segment], t1[segment], t,
                    out float3 _, out float3 forward, out float3 _, out float3 _);

                var input = new LateralInputs
                {
                    TargetOffset = lane,
                    TargetVelocity = 0f,
                    Magnetism = magnetism,
                    CurveAccel = VehicleDynamics.CurveAcceleration(speed, curvature),
                    Grip = grip,
                    ForwardSpeed = speed,
                    HalfRoadWidth = GameConstants.RoadWidth * 0.5f,
                    SlipCoupling = 0f,
                    Yaw = drift.YawOffset
                };

                int n = VehicleDynamics.SubSteps(frameTime, out float h);
                for (int i = 0; i < n; i++)
                    VehicleDynamics.StepLateral(ref lateral, input, h);

                var yaw = YawInput(speed, 0f, false);
                yaw.LateralSpeed = lateral.Velocity;
                yaw.Usage = lateral.Usage;
                yaw.LoadDirection = lateral.LoadDirection;

                float slip = 0f;
                for (int i = 0; i < n; i++)
                    slip = VehicleDynamics.StepYaw(ref drift, yaw, h);

                result.MaxUsage = math.max(result.MaxUsage, lateral.Usage);
                if (!autopilot && VehicleDynamics.IsLostControl(slip, speed))
                    result.LostControl = true;

                z += speed * forward.z * frameTime;
                while (segment < p0.Length && z > p1[segment].z)
                    segment++;
            }

            result.Lateral = lateral;
            return result;
        }

        #endregion
    }
}
