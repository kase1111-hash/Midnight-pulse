// Nightflow - Game Flow Logic Tests
// Validates the continuous-play rules: pilot handoff, idle timeout, crash flow
// phase timing, coasting and run lifecycle defaults.

using NUnit.Framework;
using Nightflow.Components;
using Nightflow.Config;
using Nightflow.Systems;

namespace Nightflow.Tests
{
    [TestFixture]
    public class GameFlowLogicTests
    {
        private const float Dt = 1f / 60f;

        #region Pilot Handoff

        [Test]
        public void EvaluatePilot_MenuOpen_EngagesAutopilot()
        {
            float idle = 3f;
            var decision = GameFlowLogic.EvaluatePilot(
                autopilotEnabled: false, humanInput: false, menuOpen: true,
                crashFlowActive: false, ref idle, Dt);

            Assert.AreEqual(PilotDecision.EngageForMenu, decision);
            Assert.AreEqual(0f, idle);
        }

        [Test]
        public void EvaluatePilot_MenuOpen_InputNeverTakesOver()
        {
            float idle = 0f;
            var decision = GameFlowLogic.EvaluatePilot(
                autopilotEnabled: true, humanInput: true, menuOpen: true,
                crashFlowActive: false, ref idle, Dt);

            Assert.AreEqual(PilotDecision.NoChange, decision);
        }

        [Test]
        public void EvaluatePilot_AutopilotAndInput_PlayerTakesOver()
        {
            float idle = 0f;
            var decision = GameFlowLogic.EvaluatePilot(
                autopilotEnabled: true, humanInput: true, menuOpen: false,
                crashFlowActive: false, ref idle, Dt);

            Assert.AreEqual(PilotDecision.PlayerTakeover, decision);
        }

        [Test]
        public void EvaluatePilot_AutopilotNoInput_NoChange()
        {
            float idle = 0f;
            var decision = GameFlowLogic.EvaluatePilot(
                autopilotEnabled: true, humanInput: false, menuOpen: false,
                crashFlowActive: false, ref idle, Dt);

            Assert.AreEqual(PilotDecision.NoChange, decision);
        }

        [Test]
        public void EvaluatePilot_PlayerIdle_EngagesAfterTimeout()
        {
            float idle = 0f;
            PilotDecision decision = PilotDecision.NoChange;
            int frames = 0;

            while (decision == PilotDecision.NoChange && frames < 10000)
            {
                decision = GameFlowLogic.EvaluatePilot(
                    autopilotEnabled: false, humanInput: false, menuOpen: false,
                    crashFlowActive: false, ref idle, Dt);
                frames++;
            }

            Assert.AreEqual(PilotDecision.EngageForIdle, decision);
            float elapsed = frames * Dt;
            Assert.GreaterOrEqual(elapsed, GameConstants.IdleTimeoutForAutopilot - Dt);
            Assert.LessOrEqual(elapsed, GameConstants.IdleTimeoutForAutopilot + Dt);
            Assert.AreEqual(0f, idle, "idle timer resets when autopilot engages");
        }

        [Test]
        public void EvaluatePilot_PlayerInput_ResetsIdleTimer()
        {
            float idle = GameConstants.IdleTimeoutForAutopilot * 0.9f;
            var decision = GameFlowLogic.EvaluatePilot(
                autopilotEnabled: false, humanInput: true, menuOpen: false,
                crashFlowActive: false, ref idle, Dt);

            Assert.AreEqual(PilotDecision.NoChange, decision);
            Assert.AreEqual(0f, idle);
        }

        [Test]
        public void EvaluatePilot_CrashFlow_NoHandoff()
        {
            float idle = 5f;
            var decision = GameFlowLogic.EvaluatePilot(
                autopilotEnabled: false, humanInput: true, menuOpen: false,
                crashFlowActive: true, ref idle, Dt);

            Assert.AreEqual(PilotDecision.NoChange, decision);
            Assert.AreEqual(0f, idle);
        }

        [Test]
        public void IsHumanInput_RespectsThreshold()
        {
            float t = GameConstants.AutopilotTakeoverThreshold;
            Assert.IsFalse(GameFlowLogic.IsHumanInput(0f, 0f, 0f, false));
            Assert.IsFalse(GameFlowLogic.IsHumanInput(t * 0.5f, t * 0.5f, t * 0.5f, false));
            Assert.IsTrue(GameFlowLogic.IsHumanInput(-t * 2f, 0f, 0f, false));
            Assert.IsTrue(GameFlowLogic.IsHumanInput(0f, t * 2f, 0f, false));
            Assert.IsTrue(GameFlowLogic.IsHumanInput(0f, 0f, t * 2f, false));
            Assert.IsTrue(GameFlowLogic.IsHumanInput(0f, 0f, 0f, true));
        }

        [Test]
        public void AutopilotTargetSpeed_HoldsComfortBand()
        {
            Assert.AreEqual(GameConstants.AutopilotRecoverySpeed,
                GameFlowLogic.AutopilotTargetSpeedFor(GameConstants.MinForwardSpeed), 0.001f);
            Assert.AreEqual(30f, GameFlowLogic.AutopilotTargetSpeedFor(30f), 0.001f);
            Assert.AreEqual(GameFlowLogic.AutopilotComfortMaxSpeed,
                GameFlowLogic.AutopilotTargetSpeedFor(GameConstants.MaxForwardSpeed), 0.001f);
        }

        #endregion

        #region Crash Flow

        private static bool Step(ref CrashFlowPhase phase, ref float timer, bool dismiss,
            out float fade, out float scale)
        {
            timer += Dt;
            return GameFlowLogic.AdvanceCrashPhase(ref phase, ref timer, dismiss, out fade, out scale);
        }

        [Test]
        public void CrashFlow_RunsAllPhasesInOrder_WithAutoDismiss()
        {
            var phase = CrashFlowPhase.Impact;
            float timer = 0f;
            var seen = new System.Collections.Generic.List<CrashFlowPhase> { phase };
            bool enteredReset = false;
            int guard = 0;

            while (phase != CrashFlowPhase.None && guard++ < 100000)
            {
                enteredReset |= Step(ref phase, ref timer, dismiss: false, out _, out _);
                if (seen[seen.Count - 1] != phase) seen.Add(phase);
            }

            CollectionAssert.AreEqual(new[]
            {
                CrashFlowPhase.Impact, CrashFlowPhase.ScreenShake, CrashFlowPhase.FadeOut,
                CrashFlowPhase.Summary, CrashFlowPhase.Reset, CrashFlowPhase.FadeIn, CrashFlowPhase.None
            }, seen);
            Assert.IsTrue(enteredReset);
        }

        [Test]
        public void CrashFlow_SummaryIgnoresDismissBeforeMinDuration()
        {
            var phase = CrashFlowPhase.Summary;
            float timer = 0f;

            float half = GameConstants.CrashSummaryMinDuration * 0.5f;
            while (timer < half)
            {
                Step(ref phase, ref timer, dismiss: true, out _, out _);
            }

            Assert.AreEqual(CrashFlowPhase.Summary, phase);
        }

        [Test]
        public void CrashFlow_SummaryDismissAfterMinDuration_EntersReset()
        {
            var phase = CrashFlowPhase.Summary;
            float timer = GameConstants.CrashSummaryMinDuration;

            bool entered = Step(ref phase, ref timer, dismiss: true, out float fade, out _);

            Assert.IsTrue(entered);
            Assert.AreEqual(CrashFlowPhase.Reset, phase);
            Assert.AreEqual(0f, timer);
        }

        [Test]
        public void CrashFlow_SummaryAutoDismisses()
        {
            var phase = CrashFlowPhase.Summary;
            float timer = GameConstants.CrashSummaryAutoDismissDuration;

            bool entered = Step(ref phase, ref timer, dismiss: false, out _, out _);

            Assert.IsTrue(entered);
            Assert.AreEqual(CrashFlowPhase.Reset, phase);
        }

        [Test]
        public void CrashFlow_FadeAlphaCoversResetAndClearsAtEnd()
        {
            var phase = CrashFlowPhase.Reset;
            float timer = 0f;
            Step(ref phase, ref timer, false, out float fadeDuringReset, out _);
            Assert.AreEqual(1f, fadeDuringReset);

            phase = CrashFlowPhase.FadeIn;
            timer = GameConstants.CrashFadeInDuration;
            Step(ref phase, ref timer, false, out float fadeAtEnd, out float scaleAtEnd);
            Assert.AreEqual(CrashFlowPhase.None, phase);
            Assert.AreEqual(0f, fadeAtEnd);
            Assert.AreEqual(1f, scaleAtEnd);
        }

        [Test]
        public void CrashFlow_ImpactUsesSlowMotionHint_NeverZero()
        {
            var phase = CrashFlowPhase.Impact;
            float timer = 0f;
            Step(ref phase, ref timer, false, out _, out float scale);

            Assert.AreEqual(GameConstants.CrashSlowMotionScale, scale, 0.001f);
            Assert.Greater(scale, 0f, "the simulation never stops");
        }

        [Test]
        public void CoastingSpeed_DecaysButNeverStalls()
        {
            float speed = 60f;
            float previous = speed;
            for (int i = 0; i < 60 * 30; i++)
            {
                speed = GameFlowLogic.CoastingSpeed(speed, Dt);
                Assert.LessOrEqual(speed, previous + 0.0001f);
                Assert.GreaterOrEqual(speed, GameConstants.MinForwardSpeed);
                previous = speed;
            }

            Assert.AreEqual(GameConstants.MinForwardSpeed, speed, 0.001f);
        }

        #endregion

        #region Run Lifecycle

        [Test]
        public void NewScoreSession_IsActiveAndZeroed()
        {
            var session = GameFlowLogic.NewScoreSession();
            Assert.IsTrue(session.Active);
            Assert.AreEqual(0f, session.Score);
            Assert.AreEqual(0f, session.Distance);
            Assert.AreEqual(1f, session.Multiplier);
            Assert.AreEqual(0f, session.RiskMultiplier);
        }

        [Test]
        public void NewRiskState_UsesBaseCap()
        {
            var risk = GameFlowLogic.NewRiskState();
            Assert.AreEqual(GameConstants.BaseRiskCap, risk.Cap);
            Assert.AreEqual(0f, risk.Value);
            Assert.IsFalse(risk.BrakePenaltyActive);
        }

        [Test]
        public void NewScoreSummary_HasNoEndReason()
        {
            var summary = GameFlowLogic.NewScoreSummary();
            Assert.AreEqual(CrashReason.None, summary.EndReason);
            Assert.AreEqual(0f, summary.FinalScore);
            Assert.AreEqual(0f, summary.TimeSurvived);
        }

        [Test]
        public void FlowConstants_AreConsistent()
        {
            Assert.Greater(GameConstants.IdleTimeoutForAutopilot, 0f);
            Assert.Greater(GameConstants.CrashSummaryAutoDismissDuration, GameConstants.CrashSummaryMinDuration);
            Assert.GreaterOrEqual(GameConstants.AutopilotRecoverySpeed, GameConstants.MinForwardSpeed);
            Assert.LessOrEqual(GameFlowLogic.AutopilotComfortMaxSpeed, GameConstants.MaxForwardSpeed);
        }

        #endregion
    }
}
