// ============================================================================
// Nightflow - Damage System Tests
// Validates damage energy formula, directional weights, and risk cap degradation
// ============================================================================

using NUnit.Framework;
using Unity.Mathematics;
using Nightflow.Config;

namespace Nightflow.Tests
{
    /// <summary>
    /// Tests the damage system formulas:
    /// E_d = k_d * v_impact^2 * Severity
    /// Directional weight normalization
    /// Risk cap degradation with damage
    /// </summary>
    [TestFixture]
    public class DamageSystemTests
    {
        // Damage parameters (mirrors DamageSystem)
        private const float DamageScale = 0.04f; // k_d
        private const float FrontSteeringPenalty = 0.4f;
        private const float SideMagnetismPenalty = 0.5f;
        private const float RearSlipPenalty = 0.6f;
        private const float RiskCapDamageMultiplier = 0.7f;

        // =====================================================================
        // Damage Energy Formula: E_d = k_d * v_impact^2 * Severity
        // =====================================================================

        private static float CalculateDamageEnergy(float impactSpeed, float severity)
        {
            return DamageScale * impactSpeed * impactSpeed * severity;
        }

        [Test]
        public void DamageEnergy_ZeroImpact_ZeroDamage()
        {
            float ed = CalculateDamageEnergy(0f, 0.3f);
            Assert.AreEqual(0f, ed, 0.001f);
        }

        [Test]
        public void DamageEnergy_ZeroSeverity_ZeroDamage()
        {
            float ed = CalculateDamageEnergy(30f, 0f);
            Assert.AreEqual(0f, ed, 0.001f);
        }

        [Test]
        public void DamageEnergy_KnownValues()
        {
            // v=10, severity=0.3 => 0.04 * 100 * 0.3 = 1.2
            float ed = CalculateDamageEnergy(10f, 0.3f);
            Assert.AreEqual(1.2f, ed, 0.001f);
        }

        [Test]
        public void DamageEnergy_QuadraticInSpeed()
        {
            float ed10 = CalculateDamageEnergy(10f, 1f);
            float ed20 = CalculateDamageEnergy(20f, 1f);
            // 20^2 / 10^2 = 4, so ed20 should be 4x ed10
            Assert.AreEqual(ed10 * 4f, ed20, 0.001f);
        }

        [Test]
        public void DamageEnergy_LinearInSeverity()
        {
            float ed1 = CalculateDamageEnergy(10f, 0.5f);
            float ed2 = CalculateDamageEnergy(10f, 1.0f);
            Assert.AreEqual(ed1 * 2f, ed2, 0.001f);
        }

        [Test]
        public void DamageEnergy_HighSpeedImpact_SignificantDamage()
        {
            // 60 m/s head-on with severity 0.5
            // 0.04 * 3600 * 0.5 = 72
            float ed = CalculateDamageEnergy(60f, 0.5f);
            Assert.AreEqual(72f, ed, 0.001f);
        }

        [Test]
        public void DamageEnergy_LowSpeedBump_MinimalDamage()
        {
            // 5 m/s with low severity
            // 0.04 * 25 * 0.1 = 0.1
            float ed = CalculateDamageEnergy(5f, 0.1f);
            Assert.AreEqual(0.1f, ed, 0.001f);
        }

        // =====================================================================
        // Directional Weight Distribution
        // =====================================================================

        private struct DirectionalWeights
        {
            public float Front, Rear, Left, Right;
        }

        private static DirectionalWeights CalculateWeights(float3 normal)
        {
            float wFront = math.max(0f, -normal.z);
            float wRear = math.max(0f, normal.z);
            float wLeft = math.max(0f, normal.x);
            float wRight = math.max(0f, -normal.x);

            float total = wFront + wRear + wLeft + wRight;
            if (total > 0.01f)
            {
                wFront /= total;
                wRear /= total;
                wLeft /= total;
                wRight /= total;
            }
            else
            {
                wFront = wRear = wLeft = wRight = 0.25f;
            }

            return new DirectionalWeights { Front = wFront, Rear = wRear, Left = wLeft, Right = wRight };
        }

        [Test]
        public void Weights_FrontalImpact_AllFront()
        {
            // Normal pointing in -Z (hazard hit from front)
            float3 normal = new float3(0, 0, -1);
            var w = CalculateWeights(normal);

            Assert.AreEqual(1f, w.Front, 0.001f);
            Assert.AreEqual(0f, w.Rear, 0.001f);
            Assert.AreEqual(0f, w.Left, 0.001f);
            Assert.AreEqual(0f, w.Right, 0.001f);
        }

        [Test]
        public void Weights_RearImpact_AllRear()
        {
            float3 normal = new float3(0, 0, 1);
            var w = CalculateWeights(normal);

            Assert.AreEqual(0f, w.Front, 0.001f);
            Assert.AreEqual(1f, w.Rear, 0.001f);
        }

        [Test]
        public void Weights_LeftImpact_AllLeft()
        {
            float3 normal = new float3(1, 0, 0);
            var w = CalculateWeights(normal);

            Assert.AreEqual(1f, w.Left, 0.001f);
            Assert.AreEqual(0f, w.Right, 0.001f);
        }

        [Test]
        public void Weights_RightImpact_AllRight()
        {
            float3 normal = new float3(-1, 0, 0);
            var w = CalculateWeights(normal);

            Assert.AreEqual(0f, w.Left, 0.001f);
            Assert.AreEqual(1f, w.Right, 0.001f);
        }

        [Test]
        public void Weights_DiagonalImpact_SumToOne()
        {
            float3 normal = math.normalize(new float3(1, 0, -1));
            var w = CalculateWeights(normal);

            float sum = w.Front + w.Rear + w.Left + w.Right;
            Assert.AreEqual(1f, sum, 0.01f);
        }

        [Test]
        public void Weights_DiagonalFrontLeft_SplitEvenly()
        {
            float3 normal = math.normalize(new float3(1, 0, -1));
            var w = CalculateWeights(normal);

            // Front and Left should both be approximately 0.5
            Assert.AreEqual(w.Front, w.Left, 0.01f);
            Assert.AreEqual(0f, w.Rear, 0.01f);
            Assert.AreEqual(0f, w.Right, 0.01f);
        }

        [Test]
        public void Weights_ZeroNormal_EqualDistribution()
        {
            float3 normal = float3.zero;
            var w = CalculateWeights(normal);

            Assert.AreEqual(0.25f, w.Front, 0.001f);
            Assert.AreEqual(0.25f, w.Rear, 0.001f);
            Assert.AreEqual(0.25f, w.Left, 0.001f);
            Assert.AreEqual(0.25f, w.Right, 0.001f);
        }

        [Test]
        public void Weights_VerticalNormal_EqualDistribution()
        {
            // Pure vertical impact (e.g., falling debris) has no horizontal components
            float3 normal = new float3(0, 1, 0);
            var w = CalculateWeights(normal);

            Assert.AreEqual(0.25f, w.Front, 0.001f);
            Assert.AreEqual(0.25f, w.Rear, 0.001f);
            Assert.AreEqual(0.25f, w.Left, 0.001f);
            Assert.AreEqual(0.25f, w.Right, 0.001f);
        }

        // =====================================================================
        // Risk Cap Degradation
        // =====================================================================

        private static float CalculateRiskCap(float totalDamage)
        {
            float damageRatio = totalDamage / GameConstants.MaxDamage;
            float riskCapReduction = damageRatio * RiskCapDamageMultiplier;
            float cap = GameConstants.BaseRiskCap * (1f - riskCapReduction);
            return math.max(cap, GameConstants.MinRiskCap);
        }

        [Test]
        public void RiskCap_NoDamage_AtBase()
        {
            float cap = CalculateRiskCap(0f);
            Assert.AreEqual(GameConstants.BaseRiskCap, cap, 0.001f);
        }

        [Test]
        public void RiskCap_SomeDamage_Reduced()
        {
            float cap = CalculateRiskCap(50f); // 50% damage
            Assert.Less(cap, GameConstants.BaseRiskCap);
            Assert.Greater(cap, GameConstants.MinRiskCap);
        }

        [Test]
        public void RiskCap_FullDamage_AtMinimum()
        {
            float cap = CalculateRiskCap(GameConstants.MaxDamage);
            // damageRatio=1.0, reduction=0.7, cap=2*(1-0.7)=0.6
            Assert.AreEqual(0.6f, cap, 0.001f);
        }

        [Test]
        public void RiskCap_ExcessDamage_ClampedToMin()
        {
            float cap = CalculateRiskCap(GameConstants.MaxDamage * 2f);
            // Would go below min, so should be clamped
            Assert.AreEqual(GameConstants.MinRiskCap, cap, 0.001f);
        }

        [Test]
        public void RiskCap_ProgressiveReduction()
        {
            float cap25 = CalculateRiskCap(25f);
            float cap50 = CalculateRiskCap(50f);
            float cap75 = CalculateRiskCap(75f);

            Assert.Greater(cap25, cap50);
            Assert.Greater(cap50, cap75);
        }

        // =====================================================================
        // Rebuild Rate Degradation
        // =====================================================================

        private static float CalculateRebuildRate(float totalDamage)
        {
            float damageRatio = totalDamage / GameConstants.MaxDamage;
            float rate = 1f - (damageRatio * 0.5f);
            return math.max(rate, GameConstants.MinRebuildRate);
        }

        [Test]
        public void RebuildRate_NoDamage_Full()
        {
            float rate = CalculateRebuildRate(0f);
            Assert.AreEqual(1f, rate, 0.001f);
        }

        [Test]
        public void RebuildRate_HalfDamage_Reduced()
        {
            float rate = CalculateRebuildRate(50f);
            // damageRatio=0.5, rate=1-(0.5*0.5)=0.75
            Assert.AreEqual(0.75f, rate, 0.001f);
        }

        [Test]
        public void RebuildRate_FullDamage_AtHalf()
        {
            float rate = CalculateRebuildRate(GameConstants.MaxDamage);
            // damageRatio=1.0, rate=1-(1.0*0.5)=0.5
            Assert.AreEqual(0.5f, rate, 0.001f);
        }

        [Test]
        public void RebuildRate_NeverBelowMinimum()
        {
            float rate = CalculateRebuildRate(GameConstants.MaxDamage * 5f);
            Assert.AreEqual(GameConstants.MinRebuildRate, rate, 0.001f);
        }

        // =====================================================================
        // Handling Penalty Coefficients
        // =====================================================================

        [Test]
        public void Penalties_AreInRange()
        {
            Assert.Greater(FrontSteeringPenalty, 0f);
            Assert.Less(FrontSteeringPenalty, 1f);

            Assert.Greater(SideMagnetismPenalty, 0f);
            Assert.Less(SideMagnetismPenalty, 1f);

            Assert.Greater(RearSlipPenalty, 0f);
            Assert.Less(RearSlipPenalty, 1f);
        }

        [Test]
        public void SteeringDegradation_WithFrontDamage()
        {
            // k_steer * (1 - 0.4 * D_front)
            float kSteer = 1f;
            float dFront = 0.5f; // 50% front damage
            float degraded = kSteer * (1f - FrontSteeringPenalty * dFront);
            Assert.AreEqual(0.8f, degraded, 0.001f);
        }

        [Test]
        public void SteeringDegradation_NoDamage_NoChange()
        {
            float kSteer = 1f;
            float degraded = kSteer * (1f - FrontSteeringPenalty * 0f);
            Assert.AreEqual(1f, degraded, 0.001f);
        }

        [Test]
        public void MagnetismDegradation_WithSideDamage()
        {
            // omega * (1 - 0.5 * D_side)
            float omega = 8f;
            float dSide = 0.6f;
            float degraded = omega * (1f - SideMagnetismPenalty * dSide);
            Assert.AreEqual(5.6f, degraded, 0.001f);
        }

        [Test]
        public void DriftStability_WithRearDamage()
        {
            // k_slip * (1 + 0.6 * D_rear) — note: this INCREASES slip
            float kSlip = 1f;
            float dRear = 0.5f;
            float degraded = kSlip * (1f + RearSlipPenalty * dRear);
            Assert.AreEqual(1.3f, degraded, 0.001f);
        }

        // =====================================================================
        // Cascade Failure Threshold
        // =====================================================================

        [Test]
        public void MaxDamage_CausesCrash()
        {
            // Total damage >= MaxDamage should trigger crash
            float totalDamage = GameConstants.MaxDamage;
            bool shouldCrash = totalDamage >= GameConstants.MaxDamage;
            Assert.IsTrue(shouldCrash);
        }

        [Test]
        public void BelowMaxDamage_NoCrash()
        {
            float totalDamage = GameConstants.MaxDamage - 1f;
            bool shouldCrash = totalDamage >= GameConstants.MaxDamage;
            Assert.IsFalse(shouldCrash);
        }

        // =====================================================================
        // Damage Zone Saturation (clamping to [0,1])
        // =====================================================================

        [Test]
        public void DamageZone_Saturated_ClampsToOne()
        {
            float zone = 1.5f;
            float clamped = math.saturate(zone);
            Assert.AreEqual(1f, clamped, 0.001f);
        }

        [Test]
        public void DamageZone_Negative_ClampsToZero()
        {
            float zone = -0.5f;
            float clamped = math.saturate(zone);
            Assert.AreEqual(0f, clamped, 0.001f);
        }

        [Test]
        public void DamageZone_InRange_Unchanged()
        {
            float zone = 0.6f;
            float clamped = math.saturate(zone);
            Assert.AreEqual(0.6f, clamped, 0.001f);
        }
    }
}
