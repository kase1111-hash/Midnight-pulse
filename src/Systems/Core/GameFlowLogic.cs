// ============================================================================
// Nightflow - Game Flow Logic
// Pure, Burst-friendly decision functions shared by GameStateSystem and the
// EditMode tests. No ECS access in here: everything is plain data in/out so
// the continuous-play rules can be unit tested without a World.
// ============================================================================

using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Config;

namespace Nightflow.Systems
{
    /// <summary>
    /// Who should be driving after this frame's pilot arbitration.
    /// </summary>
    public enum PilotDecision : byte
    {
        /// <summary>Keep the current pilot.</summary>
        NoChange = 0,

        /// <summary>Hand the wheel to the autopilot because a menu is open.</summary>
        EngageForMenu = 1,

        /// <summary>Hand the wheel to the autopilot because the player went idle.</summary>
        EngageForIdle = 2,

        /// <summary>The player moved a control: give them the wheel.</summary>
        PlayerTakeover = 3
    }

    /// <summary>
    /// Stateless rules for the "car always moving" flow:
    /// crash-phase timing, pilot handoff and run lifecycle.
    /// </summary>
    public static class GameFlowLogic
    {
        // =====================================================================
        // Pilot arbitration
        // =====================================================================

        /// <summary>
        /// Decides who drives next frame.
        /// </summary>
        /// <param name="autopilotEnabled">Autopilot currently has the wheel.</param>
        /// <param name="humanInput">Player moved a control this frame.</param>
        /// <param name="menuOpen">Any menu overlay is open (main, pause, settings...).</param>
        /// <param name="crashFlowActive">The crash sequence is playing.</param>
        /// <param name="idleTimer">Seconds since the player last touched a control (in/out).</param>
        /// <param name="deltaTime">Frame time (s).</param>
        public static PilotDecision EvaluatePilot(
            bool autopilotEnabled,
            bool humanInput,
            bool menuOpen,
            bool crashFlowActive,
            ref float idleTimer,
            float deltaTime)
        {
            // The crash sequence owns the vehicle; no handoff until it resets.
            if (crashFlowActive)
            {
                idleTimer = 0f;
                return PilotDecision.NoChange;
            }

            // Menus never stop the car: the autopilot drives underneath them
            // and menu navigation keys can never yank the wheel.
            if (menuOpen)
            {
                idleTimer = 0f;
                return autopilotEnabled ? PilotDecision.NoChange : PilotDecision.EngageForMenu;
            }

            if (autopilotEnabled)
            {
                idleTimer = 0f;
                return humanInput ? PilotDecision.PlayerTakeover : PilotDecision.NoChange;
            }

            // Player is driving: track idle time.
            if (humanInput)
            {
                idleTimer = 0f;
                return PilotDecision.NoChange;
            }

            idleTimer += deltaTime;
            if (idleTimer >= GameConstants.IdleTimeoutForAutopilot)
            {
                idleTimer = 0f;
                return PilotDecision.EngageForIdle;
            }

            return PilotDecision.NoChange;
        }

        /// <summary>
        /// Whether processed control values count as the player moving the controls.
        /// </summary>
        public static bool IsHumanInput(float steer, float throttle, float brake, bool handbrake)
        {
            float t = GameConstants.AutopilotTakeoverThreshold;
            return math.abs(steer) > t || throttle > t || brake > t || handbrake;
        }

        /// <summary>
        /// Autopilot cruise target when it takes over from a moving player:
        /// hold the current speed if it is inside the autopilot's comfort band
        /// so the handoff is seamless, otherwise ease toward the recovery speed.
        /// </summary>
        public static float AutopilotTargetSpeedFor(float currentSpeed)
        {
            return math.clamp(currentSpeed, GameConstants.AutopilotRecoverySpeed, AutopilotComfortMaxSpeed);
        }

        /// <summary>Upper bound of the speed the autopilot will hold on handoff (m/s).</summary>
        public const float AutopilotComfortMaxSpeed = 35f;

        // =====================================================================
        // Crash flow
        // =====================================================================

        /// <summary>
        /// Advances the crash sequence by one frame.
        /// </summary>
        /// <param name="phase">Current phase (in/out).</param>
        /// <param name="timer">Seconds spent in the current phase (in/out, already incremented by the caller).</param>
        /// <param name="dismissRequested">Player asked to leave the summary (input or menu).</param>
        /// <param name="fadeAlpha">Black overlay alpha for this frame.</param>
        /// <param name="timeScale">Presentation time-scale hint for this frame.</param>
        /// <returns>True when the Reset phase was just entered (vehicle reset must be requested).</returns>
        public static bool AdvanceCrashPhase(
            ref CrashFlowPhase phase,
            ref float timer,
            bool dismissRequested,
            out float fadeAlpha,
            out float timeScale)
        {
            fadeAlpha = 0f;
            timeScale = 1f;
            bool enteredReset = false;

            switch (phase)
            {
                case CrashFlowPhase.Impact:
                    timeScale = GameConstants.CrashSlowMotionScale;
                    if (timer >= GameConstants.CrashImpactDuration)
                    {
                        phase = CrashFlowPhase.ScreenShake;
                        timer = 0f;
                    }
                    break;

                case CrashFlowPhase.ScreenShake:
                    timeScale = math.lerp(
                        GameConstants.CrashSlowMotionScale, 1f,
                        math.saturate(timer / GameConstants.CrashShakeDuration));
                    if (timer >= GameConstants.CrashShakeDuration)
                    {
                        phase = CrashFlowPhase.FadeOut;
                        timer = 0f;
                    }
                    break;

                case CrashFlowPhase.FadeOut:
                    fadeAlpha = math.saturate(timer / GameConstants.CrashFadeOutDuration);
                    if (timer >= GameConstants.CrashFadeOutDuration)
                    {
                        phase = CrashFlowPhase.Summary;
                        timer = 0f;
                        fadeAlpha = 1f;
                    }
                    break;

                case CrashFlowPhase.Summary:
                    fadeAlpha = 1f;
                    if (timer >= GameConstants.CrashSummaryMinDuration &&
                        (dismissRequested || timer >= GameConstants.CrashSummaryAutoDismissDuration))
                    {
                        phase = CrashFlowPhase.Reset;
                        timer = 0f;
                        enteredReset = true;
                    }
                    break;

                case CrashFlowPhase.Reset:
                    fadeAlpha = 1f;
                    if (timer >= GameConstants.CrashResetDuration)
                    {
                        phase = CrashFlowPhase.FadeIn;
                        timer = 0f;
                    }
                    break;

                case CrashFlowPhase.FadeIn:
                    fadeAlpha = 1f - math.saturate(timer / GameConstants.CrashFadeInDuration);
                    if (timer >= GameConstants.CrashFadeInDuration)
                    {
                        phase = CrashFlowPhase.None;
                        timer = 0f;
                        fadeAlpha = 0f;
                    }
                    break;

                case CrashFlowPhase.None:
                default:
                    break;
            }

            return enteredReset;
        }

        /// <summary>
        /// Speed of a crashed vehicle after one frame of coasting. The car
        /// slows during the crash sequence but never drops below the minimum
        /// forward speed, so the world keeps flowing past the wreck.
        /// </summary>
        public static float CoastingSpeed(float currentSpeed, float deltaTime)
        {
            float coasted = currentSpeed * math.exp(-GameConstants.CrashCoastDrag * deltaTime);
            return math.max(coasted, GameConstants.MinForwardSpeed);
        }

        // =====================================================================
        // Run lifecycle
        // =====================================================================

        /// <summary>
        /// A fresh scoring session for a run that starts now.
        /// </summary>
        public static ScoreSession NewScoreSession()
        {
            return new ScoreSession
            {
                Distance = 0f,
                Multiplier = 1f,
                RiskMultiplier = 0f,
                Active = true,
                Score = 0f,
                HighestMultiplier = 1f,
                _k0 = 0
            };
        }

        /// <summary>
        /// Risk state for a run that starts now (undamaged vehicle).
        /// </summary>
        public static RiskState NewRiskState()
        {
            return new RiskState
            {
                Value = 0f,
                Cap = GameConstants.BaseRiskCap,
                RebuildRate = 1f,
                BrakeCooldown = 0f,
                BrakePenaltyActive = false
            };
        }

        /// <summary>
        /// Empty summary for a run that starts now.
        /// </summary>
        public static ScoreSummary NewScoreSummary()
        {
            return new ScoreSummary
            {
                FinalScore = 0f,
                TotalDistance = 0f,
                HighestSpeed = 0f,
                ClosePasses = 0,
                HazardsDodged = 0,
                HazardsHit = 0,
                DriftRecoveries = 0,
                PerfectSegments = 0,
                LaneWeaves = 0,
                Threadings = 0,
                HighestCombo = 0,
                TimeSurvived = 0f,
                EndReason = CrashReason.None
            };
        }
    }
}
