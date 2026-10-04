// Nightflow - Adaptive Difficulty Logic Tests
// Validates cross-run skill tracking: which runs count, how a run is folded
// into the profile, and that the difficulty target responds to skill.

using NUnit.Framework;
using Nightflow.Components;
using Nightflow.Systems;

namespace Nightflow.Tests
{
    [TestFixture]
    public class AdaptiveDifficultyLogicTests
    {
        private static DifficultyProfile WarmProfile()
        {
            var p = DifficultyProfile.CreateDefault();
            p.SessionPlayTime = AdaptiveDifficultyLogic.WarmUpTime + 1f;
            p.RunsCompleted = AdaptiveDifficultyLogic.WarmUpRuns;
            return p;
        }

        #region Which runs count

        [Test]
        public void ShouldCountRun_RealRun()
        {
            Assert.IsTrue(AdaptiveDifficultyLogic.ShouldCountRun(true, 45f));
        }

        [Test]
        public void ShouldCountRun_NoRunInProgress_Ignored()
        {
            Assert.IsFalse(AdaptiveDifficultyLogic.ShouldCountRun(false, 45f));
        }

        [Test]
        public void ShouldCountRun_FalseStart_Ignored()
        {
            Assert.IsFalse(AdaptiveDifficultyLogic.ShouldCountRun(true, 1f));
        }

        [Test]
        public void RunAverageMultiplier_IsScorePerMeter()
        {
            Assert.AreEqual(2.5f, AdaptiveDifficultyLogic.RunAverageMultiplier(25000f, 10000f), 1e-4f);
            Assert.AreEqual(1f, AdaptiveDifficultyLogic.RunAverageMultiplier(0f, 0f));
            Assert.AreEqual(1f, AdaptiveDifficultyLogic.RunAverageMultiplier(float.NaN, 100f));
        }

        #endregion

        #region CompleteRun

        [Test]
        public void CompleteRun_CountsRunAndStartsCooldown()
        {
            var p = DifficultyProfile.CreateDefault();
            AdaptiveDifficultyLogic.CompleteRun(ref p, 2f, 60f, 3000f, 5, 5);

            Assert.AreEqual(1, p.RunsCompleted);
            Assert.AreEqual(AdaptiveDifficultyLogic.AdjustmentCooldown, p.AdjustmentCooldown);
            Assert.AreEqual(3000f, p.SessionBestDistance);
        }

        [Test]
        public void CompleteRun_ShortRun_BuildsStrugglingStreak()
        {
            var p = DifficultyProfile.CreateDefault();
            for (int i = 0; i < 3; i++)
                AdaptiveDifficultyLogic.CompleteRun(ref p, 1.2f, 12f, 300f, 0, 4);

            Assert.AreEqual(3, p.StrugglingStreak);
            Assert.AreEqual(0, p.HighPerformanceStreak);
            Assert.Less(p.HazardAvoidanceRate, 0.5f);
        }

        [Test]
        public void CompleteRun_StrongRun_BuildsHighStreakAndResetsStruggling()
        {
            var p = DifficultyProfile.CreateDefault();
            p.StrugglingStreak = 2;
            AdaptiveDifficultyLogic.CompleteRun(ref p, 3.5f, 200f, 15000f, 40, 2);

            Assert.AreEqual(1, p.HighPerformanceStreak);
            Assert.AreEqual(0, p.StrugglingStreak);
        }

        [Test]
        public void CompleteRun_NoHazards_LeavesAvoidanceRate()
        {
            var p = DifficultyProfile.CreateDefault();
            float before = p.HazardAvoidanceRate;
            AdaptiveDifficultyLogic.CompleteRun(ref p, 2f, 60f, 3000f, 0, 0);

            Assert.AreEqual(before, p.HazardAvoidanceRate);
        }

        #endregion

        #region Target & smoothing

        [Test]
        public void TargetDifficulty_DuringWarmUp_IsNormal()
        {
            var p = DifficultyProfile.CreateDefault();
            p.SkillRating = 1f;
            Assert.AreEqual(1f, AdaptiveDifficultyLogic.TargetDifficulty(p));
        }

        [Test]
        public void Struggling_EasesOff_Dominating_RampsUp()
        {
            var weak = WarmProfile();
            var strong = WarmProfile();
            for (int i = 0; i < 4; i++)
            {
                AdaptiveDifficultyLogic.CompleteRun(ref weak, 1.1f, 15f, 300f, 1, 6);
                AdaptiveDifficultyLogic.CompleteRun(ref strong, 4f, 240f, 18000f, 60, 1);
            }
            weak.SkillRating = AdaptiveDifficultyLogic.SkillRating(weak);
            strong.SkillRating = AdaptiveDifficultyLogic.SkillRating(strong);

            float weakTarget = AdaptiveDifficultyLogic.TargetDifficulty(weak);
            float strongTarget = AdaptiveDifficultyLogic.TargetDifficulty(strong);

            Assert.Less(weakTarget, 0.9f);
            Assert.Greater(strongTarget, 1.2f);
            Assert.GreaterOrEqual(weakTarget, AdaptiveDifficultyLogic.MinDifficulty);
            Assert.LessOrEqual(strongTarget, AdaptiveDifficultyLogic.MaxDifficulty);
        }

        [Test]
        public void StepModifier_HeldDuringCooldown()
        {
            Assert.AreEqual(1f, AdaptiveDifficultyLogic.StepModifier(1f, 1.5f, 2f, 1f / 60f));
        }

        [Test]
        public void StepModifier_MovesSmoothlyTowardTarget()
        {
            float m = 1f;
            float prev = m;
            for (int i = 0; i < 60 * 60; i++)
            {
                m = AdaptiveDifficultyLogic.StepModifier(m, 1.4f, 0f, 1f / 60f);
                Assert.Less(m - prev, 0.001f, "no visible jumps frame to frame");
                prev = m;
            }
            Assert.AreEqual(1.4f, m, 0.01f);
        }

        #endregion
    }
}
