// Nightflow - Lane Magnetism Tests
// Validates critically damped spring model and modulation factors

using NUnit.Framework;
using Unity.Mathematics;

namespace Nightflow.Tests
{
    /// <summary>
    /// Tests the lane magnetism spring model: a_lat = m * (-omega^2 * x - 2*omega*dx)
    /// and modulation factor calculations.
    /// </summary>
    [TestFixture]
    public class LaneMagnetismTests
    {
        // Magnetism parameters (mirrors LaneMagnetismSystem)
        private const float BaseOmega = 8.0f;
        private const float ReferenceSpeed = 40f;
        private const float MaxLateralSpeed = 6f;
        private const float EdgeStiffness = 20f;
        private const float SoftZoneRatio = 0.85f;
        private const float LaneWidth = 3.6f;
        private const int DefaultNumLanes = 4;

        // Modulation multipliers
        private const float AutopilotMultiplier = 1.5f;
        private const float HandbrakeMultiplier = 0.25f;
        private const float DriftMultiplier = 0.3f;

        #region Spring Acceleration Formula

        private static float CalculateSpringAcceleration(float modulation, float offset, float lateralVelocity)
        {
            float omega = BaseOmega;
            return modulation * (-omega * omega * offset - 2f * omega * lateralVelocity);
        }

        [Test]
        public void Spring_AtCenter_NoVelocity_ZeroAcceleration()
        {
            float accel = CalculateSpringAcceleration(1f, 0f, 0f);
            Assert.AreEqual(0f, accel, 0.001f);
        }

        [Test]
        public void Spring_OffsetRight_AcceleratesLeft()
        {
            // Positive offset (right of center) should produce negative acceleration (toward center)
            float accel = CalculateSpringAcceleration(1f, 1f, 0f);
            Assert.Less(accel, 0f);
        }

        [Test]
        public void Spring_OffsetLeft_AcceleratesRight()
        {
            // Negative offset (left of center) should produce positive acceleration (toward center)
            float accel = CalculateSpringAcceleration(1f, -1f, 0f);
            Assert.Greater(accel, 0f);
        }

        [Test]
        public void Spring_LargerOffset_StrongerAcceleration()
        {
            float smallOffset = CalculateSpringAcceleration(1f, 0.5f, 0f);
            float largeOffset = CalculateSpringAcceleration(1f, 2.0f, 0f);

            Assert.Less(largeOffset, smallOffset); // Both negative, larger magnitude
            Assert.Greater(math.abs(largeOffset), math.abs(smallOffset));
        }

        [Test]
        public void Spring_CriticallyDamped_DampingTerm()
        {
            // With velocity moving away from center, damping should oppose it
            float offset = 0f; // At center
            float movingRight = 2f; // Moving right

            float accel = CalculateSpringAcceleration(1f, offset, movingRight);
            Assert.Less(accel, 0f); // Should push back left (damping)
        }

        [Test]
        public void Spring_ZeroModulation_NoForce()
        {
            float accel = CalculateSpringAcceleration(0f, 5f, 3f);
            Assert.AreEqual(0f, accel, 0.001f);
        }

        [Test]
        public void Spring_KnownValues_MatchesFormula()
        {
            // m=1.0, x=1.0, dx=0.0
            // a = 1.0 * (-64 * 1.0 - 16 * 0.0) = -64
            float accel = CalculateSpringAcceleration(1f, 1f, 0f);
            Assert.AreEqual(-64f, accel, 0.001f);
        }

        [Test]
        public void Spring_WithDamping_KnownValues()
        {
            // m=1.0, x=0.5, dx=1.0
            // a = 1.0 * (-64 * 0.5 - 16 * 1.0) = -32 - 16 = -48
            float accel = CalculateSpringAcceleration(1f, 0.5f, 1f);
            Assert.AreEqual(-48f, accel, 0.001f);
        }

        #endregion

        #region Modulation Factors

        private static float CalculateInputModulation(float steerInput)
        {
            return 1f - math.abs(steerInput);
        }

        private static float CalculateSpeedModulation(float speed)
        {
            float v = math.max(speed, 1f);
            return math.clamp(math.sqrt(v / ReferenceSpeed), 0.75f, 1.25f);
        }

        [Test]
        public void InputMod_NoSteering_FullMagnetism()
        {
            Assert.AreEqual(1f, CalculateInputModulation(0f), 0.001f);
        }

        [Test]
        public void InputMod_FullSteerLeft_ZeroMagnetism()
        {
            Assert.AreEqual(0f, CalculateInputModulation(-1f), 0.001f);
        }

        [Test]
        public void InputMod_FullSteerRight_ZeroMagnetism()
        {
            Assert.AreEqual(0f, CalculateInputModulation(1f), 0.001f);
        }

        [Test]
        public void InputMod_HalfSteer_HalfMagnetism()
        {
            Assert.AreEqual(0.5f, CalculateInputModulation(0.5f), 0.001f);
        }

        [Test]
        public void SpeedMod_AtReference_ReturnsOne()
        {
            float mod = CalculateSpeedModulation(ReferenceSpeed);
            Assert.AreEqual(1f, mod, 0.001f);
        }

        [Test]
        public void SpeedMod_VeryLow_ClampedToMin()
        {
            float mod = CalculateSpeedModulation(1f);
            Assert.GreaterOrEqual(mod, 0.75f);
        }

        [Test]
        public void SpeedMod_VeryHigh_ClampedToMax()
        {
            float mod = CalculateSpeedModulation(200f);
            Assert.LessOrEqual(mod, 1.25f);
        }

        [Test]
        public void SpeedMod_Zero_ClampedToMin()
        {
            // Speed clamped to 1 internally, then sqrt(1/40) = 0.158 < 0.75
            float mod = CalculateSpeedModulation(0f);
            Assert.AreEqual(0.75f, mod, 0.001f);
        }

        [Test]
        public void AutopilotMultiplier_IsGreaterThanOne()
        {
            Assert.Greater(AutopilotMultiplier, 1f);
        }

        [Test]
        public void HandbrakeMultiplier_ReducesMagnetism()
        {
            Assert.Less(HandbrakeMultiplier, 1f);
        }

        [Test]
        public void DriftMultiplier_ReducesMagnetism()
        {
            Assert.Less(DriftMultiplier, 1f);
        }

        #endregion

        #region Combined Modulation

        private static float CombineModulation(float baseMagnet, float mInput, bool autopilot,
            float speed, bool handbrake, bool drifting)
        {
            float mAuto = autopilot ? AutopilotMultiplier : 1f;
            float mSpeed = CalculateSpeedModulation(speed);
            float mHandbrake = handbrake ? HandbrakeMultiplier : 1f;
            float mDrift = drifting ? DriftMultiplier : 1f;

            return baseMagnet * mInput * mAuto * mSpeed * mHandbrake * mDrift;
        }

        [Test]
        public void CombinedMod_AllDefault_EqualsBaseMagnet()
        {
            float result = CombineModulation(1f, 1f, false, ReferenceSpeed, false, false);
            Assert.AreEqual(1f, result, 0.01f);
        }

        [Test]
        public void CombinedMod_Autopilot_IncreasesModulation()
        {
            float normal = CombineModulation(1f, 1f, false, ReferenceSpeed, false, false);
            float autop = CombineModulation(1f, 1f, true, ReferenceSpeed, false, false);
            Assert.Greater(autop, normal);
        }

        [Test]
        public void CombinedMod_Handbrake_ReducesModulation()
        {
            float normal = CombineModulation(1f, 1f, false, ReferenceSpeed, false, false);
            float hbrake = CombineModulation(1f, 1f, false, ReferenceSpeed, true, false);
            Assert.Less(hbrake, normal);
        }

        [Test]
        public void CombinedMod_Drifting_ReducesModulation()
        {
            float normal = CombineModulation(1f, 1f, false, ReferenceSpeed, false, false);
            float drift = CombineModulation(1f, 1f, false, ReferenceSpeed, false, true);
            Assert.Less(drift, normal);
        }

        #endregion

        #region Edge Force

        private static float CalculateEdgeForce(float lateralPosition, int numLanes)
        {
            float halfRoadWidth = (numLanes * LaneWidth) * 0.5f;
            float softEdge = halfRoadWidth * SoftZoneRatio;

            float absLateral = math.abs(lateralPosition);
            if (absLateral <= softEdge)
                return 0f;

            float xEdge = absLateral - softEdge;
            return -math.sign(lateralPosition) * EdgeStiffness * xEdge * xEdge;
        }

        [Test]
        public void EdgeForce_InsideSoftZone_NoForce()
        {
            float force = CalculateEdgeForce(0f, DefaultNumLanes);
            Assert.AreEqual(0f, force, 0.001f);
        }

        [Test]
        public void EdgeForce_BeyondSoftEdgeRight_PushesLeft()
        {
            // Half road = 4 * 3.6 * 0.5 = 7.2, soft edge = 7.2 * 0.85 = 6.12
            float force = CalculateEdgeForce(7f, DefaultNumLanes);
            Assert.Less(force, 0f); // Negative = pushes left (toward center)
        }

        [Test]
        public void EdgeForce_BeyondSoftEdgeLeft_PushesRight()
        {
            float force = CalculateEdgeForce(-7f, DefaultNumLanes);
            Assert.Greater(force, 0f); // Positive = pushes right (toward center)
        }

        [Test]
        public void EdgeForce_FurtherOut_StrongerForce()
        {
            float force1 = CalculateEdgeForce(6.5f, DefaultNumLanes);
            float force2 = CalculateEdgeForce(7.0f, DefaultNumLanes);

            // Both negative, but force2 should have larger magnitude
            Assert.Greater(math.abs(force2), math.abs(force1));
        }

        #endregion

        #region Lateral Velocity Clamping

        [Test]
        public void LateralVelocity_ClampedToMax()
        {
            float vel = 10f; // Exceeds MaxLateralSpeed
            float clamped = math.clamp(vel, -MaxLateralSpeed, MaxLateralSpeed);
            Assert.AreEqual(MaxLateralSpeed, clamped, 0.001f);
        }

        [Test]
        public void LateralVelocity_NegativeClampedToMin()
        {
            float vel = -10f;
            float clamped = math.clamp(vel, -MaxLateralSpeed, MaxLateralSpeed);
            Assert.AreEqual(-MaxLateralSpeed, clamped, 0.001f);
        }

        [Test]
        public void LateralVelocity_WithinRange_Unchanged()
        {
            float vel = 3f;
            float clamped = math.clamp(vel, -MaxLateralSpeed, MaxLateralSpeed);
            Assert.AreEqual(vel, clamped, 0.001f);
        }

        #endregion

        #region Error-Path & Boundary Tests

        [Test]
        public void Spring_NegativeModulation_ReversesForce()
        {
            // Negative modulation shouldn't occur in production but test behavior
            float normal = CalculateSpringAcceleration(1f, 1f, 0f);
            float negMod = CalculateSpringAcceleration(-1f, 1f, 0f);
            Assert.AreEqual(-normal, negMod, 0.001f);
        }

        [Test]
        public void Spring_ExtremeOffset_ProducesLargeForce()
        {
            float accel = CalculateSpringAcceleration(1f, 100f, 0f);
            // -64 * 100 = -6400
            Assert.AreEqual(-6400f, accel, 0.1f);
        }

        [Test]
        public void InputMod_OverSteer_ProducesNegative()
        {
            // Steer input > 1.0 produces negative modulation
            float mod = CalculateInputModulation(1.5f);
            Assert.Less(mod, 0f);
        }

        [Test]
        public void SpeedMod_NegativeSpeed_ClampedToMin()
        {
            // Negative speed is nonsensical — should be clamped
            float mod = CalculateSpeedModulation(-10f);
            Assert.AreEqual(0.75f, mod, 0.001f);
        }

        [Test]
        public void EdgeForce_AtExactSoftEdge_ZeroForce()
        {
            float halfRoad = (DefaultNumLanes * LaneWidth) * 0.5f;
            float softEdge = halfRoad * SoftZoneRatio;
            float force = CalculateEdgeForce(softEdge, DefaultNumLanes);
            Assert.AreEqual(0f, force, 0.001f);
        }

        [Test]
        public void EdgeForce_ZeroLanes_ZeroSoftEdge()
        {
            // Edge case: 0 lanes means soft edge = 0, any position triggers force
            float force = CalculateEdgeForce(1f, 0);
            Assert.Less(force, 0f);
        }

        [Test]
        public void CombinedMod_AllModifiersActive_StillPositive()
        {
            float result = CombineModulation(1f, 0.5f, true, ReferenceSpeed, true, true);
            Assert.Greater(result, 0f);
        }

        [TestCase(0f, 0f)]
        [TestCase(1f, -64f)]
        [TestCase(-1f, 64f)]
        [TestCase(0.5f, -32f)]
        public void Spring_ParametrizedOffset(float offset, float expectedAccel)
        {
            float accel = CalculateSpringAcceleration(1f, offset, 0f);
            Assert.AreEqual(expectedAccel, accel, 0.01f);
        }

        #endregion
    }
}
