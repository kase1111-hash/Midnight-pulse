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
        /// Whether this frame's control input may take the wheel from the
        /// autopilot. While a menu is open or the crash sequence plays, takeover
        /// is disarmed; it re-arms on the first frame with every control
        /// released. So the Space/Enter that started a run, the S/W used to
        /// navigate a menu, or the key that dismissed the summary never grabs
        /// the wheel with a handbrake or brake the player didn't mean: the run
        /// starts on a fresh, deliberate input.
        /// </summary>
        /// <param name="humanInput">Player moved a control this frame.</param>
        /// <param name="blocked">A menu is open or the crash sequence is playing.</param>
        /// <param name="armed">Persistent arm state (in/out).</param>
        public static bool TakeoverIntent(bool humanInput, bool blocked, ref bool armed)
        {
            if (blocked)
            {
                armed = false;
                return false;
            }

            if (!armed)
            {
                if (!humanInput)
                    armed = true;
                return false;
            }

            return humanInput;
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

        /// <summary>
        /// Autopilot cruise target on handoff. With a run suspended underneath
        /// it (pause menu, idle) the autopilot keeps the run's momentum, up to
        /// SuspendedRunMaxSpeed, so the player picks the run up where they left
        /// it and a pause can never be used to shed speed; with no run it
        /// settles into the comfort band.
        /// </summary>
        public static float AutopilotTargetSpeedFor(float currentSpeed, bool runSuspended)
        {
            if (!runSuspended)
                return AutopilotTargetSpeedFor(currentSpeed);

            return math.clamp(currentSpeed, GameConstants.AutopilotRecoverySpeed, SuspendedRunMaxSpeed);
        }

        /// <summary>Upper bound of the speed the autopilot will hold on handoff (m/s).</summary>
        public const float AutopilotComfortMaxSpeed = 35f;

        /// <summary>
        /// Fastest a suspended run is held under the autopilot (m/s, ~360 km/h):
        /// well inside the autopilot's own grip on the generated road.
        /// </summary>
        public const float SuspendedRunMaxSpeed = 100f;

        // =====================================================================
        // Pause
        // =====================================================================

        /// <summary>Slip angle (rad) beyond which the car counts as getting away from the driver.</summary>
        public const float PauseSlipLimit = 0.2f;

        /// <summary>
        /// Whether the player's car is getting away from them (tyres past the
        /// limit, or the nose well off the direction of travel). A pause asked
        /// for now waits until the car is back in hand: the autopilot drives
        /// under the pause menu with far more grip, and must not catch a slide
        /// the driver lost.
        /// </summary>
        public static bool LosingControl(bool sliding, float slipAngle)
        {
            return sliding || math.abs(slipAngle) > PauseSlipLimit;
        }

        /// <summary>
        /// After a held pause opens on its own, a pause/resume press this soon
        /// (s) is ignored: it was a second press of the same request, not a
        /// request to resume the pause that just opened.
        /// </summary>
        public const float HeldPauseResumeGuard = 0.5f;

        /// <summary>
        /// The pause key. Opens the pause menu (cooldown permitting) or resumes
        /// from it. While the car is getting away from the driver the request is
        /// held instead (a latch: pressing again keeps it held) and
        /// UpdateHeldPause opens it once the car is back in hand. Returns true
        /// when GameState changed.
        /// </summary>
        public static bool TogglePause(ref GameState gameState)
        {
            // Can't pause during crash flow
            if (gameState.CrashPhase != CrashFlowPhase.None)
                return false;

            bool paused = gameState.CurrentMenu == MenuState.Pause;

            // Only the pause menu toggles here (other menus have their own back actions)
            if (!paused && gameState.CurrentMenu != MenuState.None)
                return false;

            if (paused)
            {
                if (gameState.PauseResumeGuard > 0f)
                    return false;
                ResumeFromPause(ref gameState);
                return true;
            }

            // Check cooldown
            if (gameState.PauseCooldown > 0f)
                return false;

            if (gameState.PlayerLosingControl)
            {
                if (gameState.PauseRequested)
                    return false;
                gameState.PauseRequested = true;
                return true;
            }

            OpenPause(ref gameState, false);
            return true;
        }

        /// <summary>
        /// Opens the pause menu. <paramref name="held"/>: a held request is
        /// opening on its own, so a stray second press is ignored for a moment.
        /// </summary>
        public static void OpenPause(ref GameState gameState, bool held)
        {
            gameState.CurrentMenu = MenuState.Pause;
            gameState.MenuVisible = true;
            gameState.IsPaused = true;
            gameState.PauseRequested = false;
            gameState.PauseResumeGuard = held ? HeldPauseResumeGuard : 0f;
        }

        /// <summary>Closes the pause menu and starts the pause cooldown.</summary>
        public static void ResumeFromPause(ref GameState gameState)
        {
            if (gameState.CurrentMenu != MenuState.Pause)
                return;

            gameState.CurrentMenu = MenuState.None;
            gameState.MenuVisible = false;
            gameState.IsPaused = false;
            gameState.TimeScale = 1f;
            gameState.PauseCooldown = GameConstants.PauseCooldownDuration;
            gameState.PauseResumeGuard = 0f;
        }

        /// <summary>
        /// One frame of a held pause request: dropped by a crash or another
        /// menu, opened as soon as the car is back in hand. Also runs down the
        /// resume guard.
        /// </summary>
        public static void UpdateHeldPause(ref GameState gameState, bool losingControl, float deltaTime)
        {
            if (gameState.PauseResumeGuard > 0f)
                gameState.PauseResumeGuard = math.max(0f, gameState.PauseResumeGuard - math.max(deltaTime, 0f));

            if (!gameState.PauseRequested)
                return;

            if (gameState.CrashPhase != CrashFlowPhase.None || gameState.CurrentMenu != MenuState.None)
            {
                gameState.PauseRequested = false;
                return;
            }

            if (!losingControl)
                OpenPause(ref gameState, true);
        }

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
        /// <param name="timeScale">Slow-motion time scale for this frame (applied by TimeScaleDriver).</param>
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
