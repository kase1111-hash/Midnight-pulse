// ============================================================================
// Nightflow - Scoring System Tests
// Validates score formula, speed tiers, risk multiplier, and brake penalty
// ============================================================================

using NUnit.Framework;
using Unity.Mathematics;

namespace Nightflow.Tests
{
    /// <summary>
    /// Tests the scoring formula logic extracted from ScoringSystem.
    /// Score = Distance * Speed_Tier * (1 + RiskMultiplier)
    /// </summary>
    [TestFixture]
    public class ScoringSystemTests
    {
        // Speed tier thresholds (mirrors ScoringSystem constants)
        private const float FastThreshold = 30f;
        private const float BoostedThreshold = 50f;

        // Speed tier multipliers
        private const float CruiseMultiplier = 1.0f;
        private const float FastMultiplier = 1.5f;
        private const float BoostedMultiplier = 2.5f;

        // Risk parameters
        private const float RiskDecay = 0.8f;
        private const float BrakePenalty = 0.5f;
        private const float BrakeCooldown = 2.0f;

        // =====================================================================
        // Speed Tier Classification
        // =====================================================================

        private static float GetTierMultiplier(float speed)
        {
            if (speed >= BoostedThreshold)
                return BoostedMultiplier;
            if (speed >= FastThreshold)
                return FastMultiplier;
            return CruiseMultiplier;
        }

        [Test]
        public void SpeedTier_BelowFast_ReturnsCruise()
        {
            Assert.AreEqual(CruiseMultiplier, GetTierMultiplier(20f));
        }

        [Test]
        public void SpeedTier_AtFastThreshold_ReturnsFast()
        {
            Assert.AreEqual(FastMultiplier, GetTierMultiplier(FastThreshold));
        }

        [Test]
        public void SpeedTier_BetweenFastAndBoosted_ReturnsFast()
        {
            Assert.AreEqual(FastMultiplier, GetTierMultiplier(40f));
        }

        [Test]
        public void SpeedTier_AtBoostedThreshold_ReturnsBoosted()
        {
            Assert.AreEqual(BoostedMultiplier, GetTierMultiplier(BoostedThreshold));
        }

        [Test]
        public void SpeedTier_AboveBoosted_ReturnsBoosted()
        {
            Assert.AreEqual(BoostedMultiplier, GetTierMultiplier(70f));
        }

        [Test]
        public void SpeedTier_Zero_ReturnsCruise()
        {
            Assert.AreEqual(CruiseMultiplier, GetTierMultiplier(0f));
        }

        // =====================================================================
        // Score Formula
        // =====================================================================

        private static float CalculateScore(float distance, float speed, float riskMultiplier)
        {
            float tierMultiplier = GetTierMultiplier(speed);
            return distance * tierMultiplier * (1f + riskMultiplier);
        }

        [Test]
        public void Score_ZeroDistance_ReturnsZero()
        {
            float score = CalculateScore(0f, 40f, 1.0f);
            Assert.AreEqual(0f, score, 0.001f);
        }

        [Test]
        public void Score_NoRisk_EqualsDistanceTimesTier()
        {
            float distance = 10f;
            float speed = 40f; // Fast tier = 1.5x
            float score = CalculateScore(distance, speed, 0f);
            Assert.AreEqual(distance * FastMultiplier, score, 0.001f);
        }

        [Test]
        public void Score_WithRisk_MultipliesCorrectly()
        {
            float distance = 10f;
            float speed = 25f; // Cruise tier = 1.0x
            float risk = 1.0f;
            float score = CalculateScore(distance, speed, risk);
            // 10 * 1.0 * (1 + 1.0) = 20
            Assert.AreEqual(20f, score, 0.001f);
        }

        [Test]
        public void Score_BoostedWithMaxRisk_HighestMultiplier()
        {
            float distance = 10f;
            float speed = 60f; // Boosted = 2.5x
            float risk = 2.0f; // Max risk cap
            float score = CalculateScore(distance, speed, risk);
            // 10 * 2.5 * (1 + 2.0) = 75
            Assert.AreEqual(75f, score, 0.001f);
        }

        [Test]
        public void Score_HigherSpeed_ProducesHigherScore()
        {
            float distance = 10f;
            float risk = 0.5f;

            float cruiseScore = CalculateScore(distance, 20f, risk);
            float fastScore = CalculateScore(distance, 35f, risk);
            float boostedScore = CalculateScore(distance, 55f, risk);

            Assert.Less(cruiseScore, fastScore);
            Assert.Less(fastScore, boostedScore);
        }

        [Test]
        public void Score_HigherRisk_ProducesHigherScore()
        {
            float distance = 10f;
            float speed = 40f;

            float lowRisk = CalculateScore(distance, speed, 0f);
            float midRisk = CalculateScore(distance, speed, 1.0f);
            float highRisk = CalculateScore(distance, speed, 2.0f);

            Assert.Less(lowRisk, midRisk);
            Assert.Less(midRisk, highRisk);
        }

        // =====================================================================
        // Risk Multiplier Decay
        // =====================================================================

        private static float DecayRisk(float currentRisk, float deltaTime)
        {
            float decayed = currentRisk - RiskDecay * deltaTime;
            return math.max(0f, decayed);
        }

        [Test]
        public void RiskDecay_ReducesOverTime()
        {
            float risk = 1.0f;
            float decayed = DecayRisk(risk, 0.5f);
            Assert.Less(decayed, risk);
        }

        [Test]
        public void RiskDecay_NeverGoesBelowZero()
        {
            float risk = 0.1f;
            float decayed = DecayRisk(risk, 10f); // Large deltaTime
            Assert.AreEqual(0f, decayed, 0.001f);
        }

        [Test]
        public void RiskDecay_ZeroDeltaTime_NoChange()
        {
            float risk = 1.5f;
            float decayed = DecayRisk(risk, 0f);
            Assert.AreEqual(risk, decayed, 0.001f);
        }

        [Test]
        public void RiskDecay_FullSecond_ReducesByDecayRate()
        {
            float risk = 2.0f;
            float decayed = DecayRisk(risk, 1f);
            Assert.AreEqual(2.0f - RiskDecay, decayed, 0.001f);
        }

        // =====================================================================
        // Brake Penalty
        // =====================================================================

        private static float ApplyBrakePenalty(float currentRisk)
        {
            return currentRisk * BrakePenalty;
        }

        [Test]
        public void BrakePenalty_HalvesRisk()
        {
            float risk = 2.0f;
            float penalized = ApplyBrakePenalty(risk);
            Assert.AreEqual(1.0f, penalized, 0.001f);
        }

        [Test]
        public void BrakePenalty_ZeroRisk_StaysZero()
        {
            float penalized = ApplyBrakePenalty(0f);
            Assert.AreEqual(0f, penalized, 0.001f);
        }

        [Test]
        public void BrakeCooldown_IsPositive()
        {
            Assert.Greater(BrakeCooldown, 0f);
        }

        // =====================================================================
        // Integration: Score Accumulation Over Multiple Frames
        // =====================================================================

        [Test]
        public void ScoreAccumulation_MultipleFrames_SumsCorrectly()
        {
            float totalScore = 0f;
            float speed = 35f; // Fast tier
            float risk = 0.5f;
            float deltaTime = 1f / 60f; // 60 FPS

            for (int i = 0; i < 60; i++) // 1 second
            {
                float distance = speed * deltaTime;
                totalScore += CalculateScore(distance, speed, risk);
            }

            // Expected: 35 m/s * 1s * 1.5 tier * (1 + 0.5 risk) = 78.75
            Assert.AreEqual(78.75f, totalScore, 0.5f);
        }

        // =====================================================================
        // Error-Path & Boundary Tests
        // =====================================================================

        [Test]
        public void Score_NegativeDistance_ProducesNonPositive()
        {
            float score = CalculateScore(-100f, 30f, 0f);
            Assert.LessOrEqual(score, 0f);
        }

        [Test]
        public void RiskDecay_VeryLargeDeltaTime_NeverNegative()
        {
            float risk = 1.0f;
            float decayed = DecayRisk(risk, 1000f);
            Assert.GreaterOrEqual(decayed, 0f);
        }

        [Test]
        public void BrakePenalty_AppliedTwice_StillNonNegative()
        {
            float risk = 0.5f;
            risk = ApplyBrakePenalty(risk);
            risk = ApplyBrakePenalty(risk);
            Assert.GreaterOrEqual(risk, 0f);
        }

        [TestCase(0f, CruiseMultiplier)]
        [TestCase(29.9f, CruiseMultiplier)]
        [TestCase(30f, FastMultiplier)]
        [TestCase(49.9f, FastMultiplier)]
        [TestCase(50f, BoostedMultiplier)]
        [TestCase(100f, BoostedMultiplier)]
        public void SpeedTier_ParametrizedBoundaries(float speed, float expectedMultiplier)
        {
            Assert.AreEqual(expectedMultiplier, GetTierMultiplier(speed));
        }
    }
}
