// ============================================================================
// Nightflow - Integration Tests
// Validates multi-system call chains work correctly end-to-end
// ============================================================================

using NUnit.Framework;
using Unity.Mathematics;

namespace Nightflow.Tests
{
    /// <summary>
    /// Integration tests that verify cross-system data flow.
    /// These simulate the call chains: Collision → Damage → Risk → Scoring → Crash.
    /// </summary>
    [TestFixture]
    public class IntegrationTests
    {
        // Constants mirrored from production systems
        private const float DamageScale = 0.04f;
        private const float MaxDamage = 100f;
        private const float BaseRiskCap = 2.0f;
        private const float MinRiskCap = 0.5f;
        private const float RiskCapDamageMultiplier = 0.7f;
        private const float MinRebuildRate = 0.3f;
        private const float FastThreshold = 30f;
        private const float BoostedThreshold = 50f;
        private const float SideMagnetismPenalty = 0.5f;

        // =====================================================================
        // Collision → Damage → Risk Chain
        // =====================================================================

        private static float CalculateDamageEnergy(float impactSpeed, float severity)
        {
            float ed = DamageScale * impactSpeed * impactSpeed * severity;
            return math.min(ed, MaxDamage);
        }

        private static float CalculateRiskCap(float totalDamage)
        {
            float damageRatio = totalDamage / MaxDamage;
            float cap = BaseRiskCap * (1f - RiskCapDamageMultiplier * damageRatio);
            return math.max(MinRiskCap, cap);
        }

        private static float CalculateMagnetReduction(float baseMagnet, float sideDamageRatio)
        {
            return baseMagnet * (1f - SideMagnetismPenalty * sideDamageRatio);
        }

        [Test]
        public void CollisionToDamageToRisk_FullChain()
        {
            // Step 1: Collision produces impact speed
            float3 velocity = new float3(0, 0, 40f);
            float3 normal = new float3(0, 0, -1f);
            float impactSpeed = math.max(0f, math.dot(velocity, -normal));
            Assert.AreEqual(40f, impactSpeed, 0.001f);

            // Step 2: Impact speed produces damage energy
            float severity = 1.0f;
            float damageEnergy = CalculateDamageEnergy(impactSpeed, severity);
            // 0.04 * 40² * 1.0 = 64
            Assert.AreEqual(64f, damageEnergy, 0.001f);

            // Step 3: Accumulated damage reduces risk cap
            float totalDamage = damageEnergy;
            float riskCap = CalculateRiskCap(totalDamage);
            // 2.0 * (1 - 0.7 * 64/100) = 2.0 * (1 - 0.448) = 2.0 * 0.552 = 1.104
            Assert.AreEqual(1.104f, riskCap, 0.01f);
            Assert.Greater(riskCap, MinRiskCap);
        }

        [Test]
        public void CollisionToDamage_LethalImpact_MaxDamage()
        {
            // High-speed impact exceeds MaxDamage cap
            float impactSpeed = 80f;
            float severity = 1.5f;
            float damageEnergy = CalculateDamageEnergy(impactSpeed, severity);
            // 0.04 * 80² * 1.5 = 0.04 * 6400 * 1.5 = 384 → capped to 100
            Assert.AreEqual(MaxDamage, damageEnergy, 0.001f);
        }

        // =====================================================================
        // Damage → Handling Degradation Chain
        // =====================================================================

        [Test]
        public void DamageToHandling_SideDamageReducesMagnetism()
        {
            float baseMagnet = 1.0f;

            // No damage: full magnetism
            float noDamage = CalculateMagnetReduction(baseMagnet, 0f);
            Assert.AreEqual(1.0f, noDamage, 0.001f);

            // Half side damage: reduced magnetism
            float halfDamage = CalculateMagnetReduction(baseMagnet, 0.5f);
            Assert.AreEqual(0.75f, halfDamage, 0.001f);

            // Full side damage: minimum magnetism
            float fullDamage = CalculateMagnetReduction(baseMagnet, 1.0f);
            Assert.AreEqual(0.5f, fullDamage, 0.001f);

            Assert.Greater(noDamage, halfDamage);
            Assert.Greater(halfDamage, fullDamage);
        }

        // =====================================================================
        // Score Accumulation with Risk and Damage
        // =====================================================================

        private static float GetTierMultiplier(float speed)
        {
            if (speed >= BoostedThreshold) return 2.5f;
            if (speed >= FastThreshold) return 1.5f;
            return 1.0f;
        }

        private static float CalculateScore(float distance, float speed, float risk)
        {
            return distance * GetTierMultiplier(speed) * (1f + risk);
        }

        [Test]
        public void ScoreWithDamagePenalty_RiskCapReducesMaxScore()
        {
            float speed = 55f; // Boosted
            float distance = 10f;

            // No damage: risk can reach 2.0
            float maxRiskNoDamage = CalculateRiskCap(0f);
            float highScore = CalculateScore(distance, speed, maxRiskNoDamage);

            // Heavy damage: risk capped lower
            float maxRiskHeavyDamage = CalculateRiskCap(80f);
            float lowScore = CalculateScore(distance, speed, maxRiskHeavyDamage);

            Assert.Greater(highScore, lowScore);
            Assert.AreEqual(BaseRiskCap, maxRiskNoDamage, 0.001f);
            Assert.Less(maxRiskHeavyDamage, maxRiskNoDamage);
        }

        // =====================================================================
        // Crash Condition Integration
        // =====================================================================

        private static bool ShouldCrash_TotalDamage(float totalDamage, float threshold)
        {
            return totalDamage >= threshold;
        }

        private static bool ShouldCrash_CompoundFailure(float yawRate, float speed, float damage,
            float yawThreshold, float speedThreshold, float damageThreshold)
        {
            return math.abs(yawRate) > yawThreshold &&
                   speed < speedThreshold &&
                   damage > damageThreshold;
        }

        [Test]
        public void CrashConditions_MultiHitAccumulation()
        {
            float totalDamage = 0f;
            float crashThreshold = MaxDamage;

            // Three medium hits
            totalDamage += CalculateDamageEnergy(25f, 1.0f); // 0.04 * 625 = 25
            Assert.IsFalse(ShouldCrash_TotalDamage(totalDamage, crashThreshold));

            totalDamage += CalculateDamageEnergy(30f, 1.0f); // 0.04 * 900 = 36
            Assert.IsFalse(ShouldCrash_TotalDamage(totalDamage, crashThreshold));

            totalDamage += CalculateDamageEnergy(35f, 1.0f); // 0.04 * 1225 = 49 → total = 110, capped hits
            // 25 + 36 + 49 = 110, but each hit capped at MaxDamage individually
            // totalDamage = 25 + 36 + 49 = 110
            Assert.IsTrue(ShouldCrash_TotalDamage(totalDamage, crashThreshold));
        }

        [Test]
        public void CrashConditions_CompoundFailure_RequiresAllConditions()
        {
            // All conditions met: crash
            Assert.IsTrue(ShouldCrash_CompoundFailure(
                yawRate: 3.0f, speed: 5f, damage: 60f,
                yawThreshold: 2.0f, speedThreshold: 10f, damageThreshold: 50f));

            // Yaw below threshold: no crash
            Assert.IsFalse(ShouldCrash_CompoundFailure(
                yawRate: 1.0f, speed: 5f, damage: 60f,
                yawThreshold: 2.0f, speedThreshold: 10f, damageThreshold: 50f));

            // Speed above threshold: no crash
            Assert.IsFalse(ShouldCrash_CompoundFailure(
                yawRate: 3.0f, speed: 15f, damage: 60f,
                yawThreshold: 2.0f, speedThreshold: 10f, damageThreshold: 50f));

            // Damage below threshold: no crash
            Assert.IsFalse(ShouldCrash_CompoundFailure(
                yawRate: 3.0f, speed: 5f, damage: 30f,
                yawThreshold: 2.0f, speedThreshold: 10f, damageThreshold: 50f));
        }

        // =====================================================================
        // Track Generation Determinism
        // =====================================================================

        private static uint NextRandom(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        private static float RandomFloat(ref uint state)
        {
            return (NextRandom(ref state) & 0xFFFF) / 65535f;
        }

        [Test]
        public void TrackGeneration_SameSeed_ProducesSameSequence()
        {
            uint seed1 = 12345u;
            uint seed2 = 12345u;

            float[] seq1 = new float[100];
            float[] seq2 = new float[100];

            for (int i = 0; i < 100; i++)
            {
                seq1[i] = RandomFloat(ref seed1);
                seq2[i] = RandomFloat(ref seed2);
            }

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(seq1[i], seq2[i], 0.0001f,
                    $"Determinism broken at index {i}");
            }
        }

        [Test]
        public void TrackGeneration_DifferentSeeds_ProduceDifferentSequences()
        {
            uint seed1 = 12345u;
            uint seed2 = 54321u;

            float val1 = RandomFloat(ref seed1);
            float val2 = RandomFloat(ref seed2);

            Assert.AreNotEqual(val1, val2);
        }
    }
}
