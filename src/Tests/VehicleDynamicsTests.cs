// Nightflow - Vehicle Dynamics Tests
// Validates momentum (no terminal velocity), grip, lane integration, yaw
// alignment and loss of control. Expected values come from the Python
// reference model the constants were tuned with.

using NUnit.Framework;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Config;
using Nightflow.Systems;

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
        public void TightCorner_HoldsAt80_SpinsOutAt90()
        {
            // R = 300 m is the tightest corner the track generator makes
            var safe = RunLaneChange(80f, 1f / 300f, Dt, 4f, 0);
            Assert.IsFalse(safe.LostControl);
            Assert.AreEqual(LaneCentre(1), safe.Lateral.Offset, 0.1f);

            var tooFast = RunLaneChange(90f, 1f / 300f, Dt, 4f, 0);
            Assert.IsTrue(tooFast.LostControl);
            Assert.Less(tooFast.Lateral.Offset, LaneCentre(1));   // ran wide (outward = left)
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

        #region Simulation helpers

        private struct LaneChangeResult
        {
            public LateralState Lateral;
            public float MaxUsage;
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
        /// constant speed: lane 1 to lane 1 + direction (0 = hold the lane) on a
        /// road of constant curvature.
        /// </summary>
        private static LaneChangeResult RunLaneChange(float speed, float curvature, float frameTime,
            float seconds, int direction)
        {
            float from = LaneCentre(1);
            float to = LaneCentre(1 + direction);
            float duration = HazardPlacement.LaneChangeDuration(speed);
            float magnetism = math.clamp(math.sqrt(speed / 40f), 0.75f, 1.25f);
            float grip = Grip(speed);

            var lateral = new LateralState { Offset = from };
            var drift = new DriftState();
            var result = new LaneChangeResult();

            float timer = 0f;
            bool changing = direction != 0;
            int frames = (int)math.round(seconds / frameTime);

            for (int f = 0; f < frames; f++)
            {
                float target = to;
                float targetVelocity = 0f;
                if (changing)
                {
                    float blend = VehicleDynamics.LaneBlend(timer, duration, out float rate);
                    target = math.lerp(from, to, blend);
                    targetVelocity = (to - from) * rate;
                    timer += frameTime;
                    if (timer >= duration)
                        changing = false;
                }

                var input = new LateralInputs
                {
                    TargetOffset = target,
                    TargetVelocity = targetVelocity,
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
                    VehicleDynamics.StepLateral(ref lateral, input, h);
                    input.TargetOffset += targetVelocity * h;
                }

                var yaw = YawInput(speed, 0f, false);
                yaw.LateralSpeed = lateral.Velocity;
                yaw.Usage = lateral.Usage;
                yaw.LoadDirection = lateral.LoadDirection;

                float slip = 0f;
                for (int i = 0; i < n; i++)
                    slip = VehicleDynamics.StepYaw(ref drift, yaw, h);

                result.MaxUsage = math.max(result.MaxUsage, lateral.Usage);
                if (VehicleDynamics.IsLostControl(slip, speed))
                    result.LostControl = true;
            }

            result.Lateral = lateral;
            return result;
        }

        #endregion
    }
}
