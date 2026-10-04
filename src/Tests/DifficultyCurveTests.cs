// Nightflow - Difficulty Curve Tests
// Validates the in-run ramp: gentle start, spec'd full difficulty, a smooth
// (kink-free, jump-free, plateau-free) build-up and a bounded overdrive.

using NUnit.Framework;
using Nightflow.Config;
using Nightflow.Systems;

namespace Nightflow.Tests
{
    [TestFixture]
    public class DifficultyCurveTests
    {
        private const float Dt = 1f / 60f;

        #region Endpoints

        [Test]
        public void Evaluate_RunStart_UsesStartValues()
        {
            var l = DifficultyCurve.Evaluate(0f, 0f);

            Assert.AreEqual(0f, l.Progress);
            Assert.AreEqual(DifficultyCurve.TrafficDensityStart, l.TrafficDensityScale, 1e-5f);
            Assert.AreEqual(0f, l.TrafficSpeedBonus, 1e-5f);
            Assert.AreEqual(DifficultyCurve.HazardRateStart, l.HazardRateScale, 1e-5f);
            Assert.AreEqual(DifficultyCurve.LethalStart, l.LethalScale, 1e-5f);
            Assert.AreEqual(DifficultyCurve.EmergencyStart, l.EmergencyFrequencyScale, 1e-5f);
            Assert.AreEqual(DifficultyCurve.BaseCruiseStart, l.BaseCruiseSpeed, 1e-5f);
        }

        [Test]
        public void Evaluate_SpecFullDifficulty_HitsSpecValues()
        {
            // Spec 10-parameters: full difficulty at 10 km / 5 minutes
            var l = DifficultyCurve.Evaluate(DifficultyCurve.FullDifficultyDistance, DifficultyCurve.FullDifficultyTime);

            Assert.AreEqual(1f, l.Progress, 1e-5f);
            Assert.AreEqual(1f, l.Intensity, 1e-5f);
            Assert.AreEqual(2f, l.TrafficDensityScale, 1e-4f);
            Assert.AreEqual(30f / 3.6f, l.TrafficSpeedBonus, 1e-4f);
            Assert.AreEqual(2.5f, l.HazardRateScale, 1e-4f);
            Assert.AreEqual(3f, l.EmergencyFrequencyScale, 1e-4f);
        }

        [Test]
        public void Evaluate_InvalidInputs_TreatedAsRunStart()
        {
            var nan = DifficultyCurve.Evaluate(float.NaN, float.NaN);
            var neg = DifficultyCurve.Evaluate(-500f, -10f);

            Assert.AreEqual(0f, nan.Progress);
            Assert.AreEqual(0f, neg.Progress);
            Assert.AreEqual(DifficultyCurve.BaseCruiseStart, nan.BaseCruiseSpeed, 1e-5f);
        }

        [Test]
        public void Evaluate_VeryLongRun_ApproachesButNeverExceedsMaxIntensity()
        {
            var l = DifficultyCurve.Evaluate(1_000_000f, 36_000f);

            Assert.LessOrEqual(l.Intensity, DifficultyCurve.MaxIntensity);
            Assert.AreEqual(DifficultyCurve.MaxIntensity, l.Intensity, 1e-3f);
        }

        [Test]
        public void BaseCruise_NeverReachesTopSpeed()
        {
            // Throttle must always have headroom over the "gravity" speed
            float cap = DifficultyCurve.Knob(DifficultyCurve.BaseCruiseStart, DifficultyCurve.BaseCruiseFull,
                DifficultyCurve.MaxIntensity);
            Assert.Less(cap, GameConstants.MaxForwardSpeed);
        }

        [Test]
        public void TrafficAtMaxIntensity_StaysUnderPlayerTopSpeed()
        {
            // Fastest possible spawn: +20% variance, full bonus, hardest adaptive modifier
            float bonus = DifficultyCurve.Knob(0f, DifficultyCurve.TrafficSpeedBonusFull, DifficultyCurve.MaxIntensity);
            float fastest = (22f * 1.2f + bonus) * 1.15f;
            Assert.Less(fastest, GameConstants.MaxForwardSpeed * DifficultyCurve.MaxTrafficSpeedRatio);
        }

        #endregion

        #region Shape: builds steadily, never jumps

        [TestCase(30f)]
        [TestCase(55f)]
        [TestCase(80f)]
        public void Evaluate_SimulatedRun_IsMonotonicAndJumpFree(float speed)
        {
            // Twenty-minute run at a constant speed, sampled every frame
            var prev = DifficultyCurve.Evaluate(0f, 0f);
            float distance = 0f, time = 0f;

            for (int i = 0; i < 20 * 60 * 60; i++)
            {
                distance += speed * Dt;
                time += Dt;
                var l = DifficultyCurve.Evaluate(distance, time);

                Assert.GreaterOrEqual(l.TrafficDensityScale, prev.TrafficDensityScale - 1e-6f);
                Assert.GreaterOrEqual(l.HazardRateScale, prev.HazardRateScale - 1e-6f);
                Assert.GreaterOrEqual(l.EmergencyFrequencyScale, prev.EmergencyFrequencyScale - 1e-6f);
                Assert.GreaterOrEqual(l.BaseCruiseSpeed, prev.BaseCruiseSpeed - 1e-6f);

                // No frame may move base speed by more than a hair (no steps)
                Assert.Less(l.BaseCruiseSpeed - prev.BaseCruiseSpeed, 0.01f);
                Assert.Less(l.HazardRateScale - prev.HazardRateScale, 0.001f);

                prev = l;
            }
        }

        [Test]
        public void Intensity_EntersOverdrive_WithoutKinkOrPlateau()
        {
            // Slope just below and just above full difficulty match, and are not flat
            const float h = 1e-3f;
            float below = (DifficultyCurve.Intensity(1f) - DifficultyCurve.Intensity(1f - h)) / h;
            float above = (DifficultyCurve.Intensity(1f + h) - DifficultyCurve.Intensity(1f)) / h;

            Assert.AreEqual(DifficultyCurve.HandoverSlope, below, 0.01f);
            Assert.AreEqual(DifficultyCurve.HandoverSlope, above, 0.01f);
        }

        [Test]
        public void Intensity_EasesInGently()
        {
            // First tenth of the ramp delivers under 3% of the difficulty
            Assert.Less(DifficultyCurve.Intensity(0.1f), 0.03f);
            Assert.AreEqual(0f, DifficultyCurve.Intensity(0f));
            Assert.AreEqual(1f, DifficultyCurve.Intensity(1f), 1e-5f);
        }

        [Test]
        public void Intensity_KeepsBuildingWellPastFullDifficulty()
        {
            // Flat-out driver (80 m/s): full at ~3 min, still clearly climbing at 7 and 12 min
            float i3 = DifficultyCurve.Evaluate(80f * 180f, 180f).Intensity;
            float i7 = DifficultyCurve.Evaluate(80f * 420f, 420f).Intensity;
            float i12 = DifficultyCurve.Evaluate(80f * 720f, 720f).Intensity;

            Assert.Greater(i7 - i3, 0.2f);
            Assert.Greater(i12 - i7, 0.08f);
        }

        [Test]
        public void Progress_FastDriverRampsSoonerThanSlowDriver()
        {
            float fast = DifficultyCurve.Progress(80f * 120f, 120f);
            float slow = DifficultyCurve.Progress(30f * 120f, 120f);

            Assert.Greater(fast, slow);
        }

        #endregion

        #region Cruise Assist

        [Test]
        public void CruiseAssist_OffPedals_EasesUpTowardBase()
        {
            float v = DifficultyCurve.ApplyCruiseAssist(30f, 50f, 0f, 0f, Dt);

            Assert.Greater(v, 30f);
            Assert.AreEqual(30f + DifficultyCurve.CruiseAssistAcceleration * Dt, v, 1e-5f);
        }

        [Test]
        public void CruiseAssist_NeverOvershootsBase()
        {
            float v = DifficultyCurve.ApplyCruiseAssist(49.99f, 50f, 0f, 0f, 1f);

            Assert.AreEqual(50f, v, 1e-5f);
        }

        [Test]
        public void CruiseAssist_AboveBase_DoesNotSlowCar()
        {
            Assert.AreEqual(70f, DifficultyCurve.ApplyCruiseAssist(70f, 50f, 0f, 0f, Dt));
        }

        [Test]
        public void CruiseAssist_PedalsHeld_PlayerKeepsControl()
        {
            Assert.AreEqual(30f, DifficultyCurve.ApplyCruiseAssist(30f, 50f, 1f, 0f, Dt));
            Assert.AreEqual(30f, DifficultyCurve.ApplyCruiseAssist(30f, 50f, 0f, 1f, Dt));
        }

        #endregion

        #region Traffic Speed

        [Test]
        public void ClampTrafficSpeed_TrafficNeverOutrunsPlayer()
        {
            float clamped = DifficultyCurve.ClampTrafficSpeed(500f, GameConstants.MaxForwardSpeed);

            Assert.Less(clamped, GameConstants.MaxForwardSpeed);
            Assert.AreEqual(20f, DifficultyCurve.ClampTrafficSpeed(20f, GameConstants.MaxForwardSpeed));
        }

        #endregion
    }
}
