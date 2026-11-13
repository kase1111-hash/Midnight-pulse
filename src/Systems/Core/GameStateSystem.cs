// ============================================================================
// Nightflow - Game State System
// Owns the crash flow state machine and the pilot handoff (autopilot <-> player)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Config;
using Nightflow.Tags;
using Nightflow.Systems.UI;

namespace Nightflow.Systems
{
    /// <summary>
    /// Single owner of game-flow transitions on the GameState singleton.
    ///
    /// The simulation never pauses. This system decides who is driving:
    /// - Menus open (main, mode select, pause, settings, ...): autopilot drives
    ///   underneath the overlay and menu input can never grab the wheel.
    /// - Player releases every control for IdleTimeoutForAutopilot: autopilot
    ///   takes over and the run is suspended (score frozen, not lost).
    /// - Player moves a control while the autopilot drives: immediate handoff.
    ///   If no run is active (boot, after a crash), a fresh scoring session
    ///   starts at that moment.
    ///
    /// Crash flow: Impact → Shake → FadeOut → Summary → Reset → FadeIn.
    /// CrashSystem starts it (Impact) and performs the vehicle reset when
    /// VehicleResetPending is raised here; everything else is timed here.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AutopilotSystem))]   // InputSystem is OrderFirst, so it already precedes us
    public partial struct GameStateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            RefRW<GameState> gameState = SystemAPI.GetSingletonRW<GameState>();

            // =============================================================
            // Pause Cooldown
            // =============================================================

            if (gameState.ValueRO.PauseCooldown > 0f)
            {
                gameState.ValueRW.PauseCooldown = math.max(0f, gameState.ValueRO.PauseCooldown - deltaTime);
            }

            // =============================================================
            // Human intent this frame (written by InputSystem)
            // =============================================================

            bool humanInput = false;
            foreach (var autopilot in SystemAPI.Query<RefRO<Autopilot>>().WithAll<PlayerVehicleTag>())
            {
                humanInput = autopilot.ValueRO.HumanInputDetected;
                break;
            }

            // =============================================================
            // Crash Flow State Machine
            // =============================================================

            // A driving control still held from the crash must be released
            // before it can dismiss the summary (keys and buttons dismiss it
            // through the menu controller on their own press)
            bool dismissArmed = gameState.ValueRO.DismissArmed;
            bool dismissInput = GameFlowLogic.TakeoverIntent(
                humanInput, gameState.ValueRO.CrashPhase != CrashFlowPhase.Summary, ref dismissArmed);
            gameState.ValueRW.DismissArmed = dismissArmed;

            if (gameState.ValueRO.CrashPhase != CrashFlowPhase.None)
            {
                gameState.ValueRW.CrashPhaseTimer += deltaTime;

                CrashFlowPhase phase = gameState.ValueRO.CrashPhase;
                float timer = gameState.ValueRO.CrashPhaseTimer;

                bool enteredReset = GameFlowLogic.AdvanceCrashPhase(
                    ref phase, ref timer, dismissInput,
                    out float fadeAlpha, out float timeScale);

                gameState.ValueRW.CrashPhase = phase;
                gameState.ValueRW.CrashPhaseTimer = timer;
                gameState.ValueRW.FadeAlpha = fadeAlpha;
                gameState.ValueRW.TimeScale = timeScale;

                if (enteredReset)
                {
                    // CrashSystem performs the actual vehicle reset this frame
                    gameState.ValueRW.VehicleResetPending = true;
                    gameState.ValueRW.AutopilotQueued = true;
                }

                if (phase == CrashFlowPhase.None)
                {
                    // Sequence complete: autopilot already has the wheel
                    gameState.ValueRW.PauseCooldown = GameConstants.PauseCooldownDuration;
                    if (gameState.ValueRO.AutopilotQueued)
                    {
                        gameState.ValueRW.PlayerControlActive = false;
                        gameState.ValueRW.AutopilotQueued = false;
                    }
                }
            }
            else
            {
                gameState.ValueRW.TimeScale = 1f;
                gameState.ValueRW.FadeAlpha = 0f;

                // Safety net: a crashed vehicle with no sequence running (a menu
                // cleared the phase, or a stale save) must still be reset, or the
                // car would sit crashed forever.
                foreach (var crashState in SystemAPI.Query<RefRO<CrashState>>().WithAll<PlayerVehicleTag>())
                {
                    if (crashState.ValueRO.IsCrashed)
                    {
                        ScreenFlowSystem.RequestVehicleReset(ref gameState.ValueRW);
                    }
                    break;
                }
            }

            // =============================================================
            // Run status the menus read, and a pause held over a slide
            // =============================================================

            bool losingControl = false;
            bool runSuspended = false;
            foreach (var (autopilot, scoreSession, entity) in
                SystemAPI.Query<RefRO<Autopilot>, RefRO<ScoreSession>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithEntityAccess())
            {
                runSuspended = scoreSession.ValueRO.Active && autopilot.ValueRO.Enabled;
                if (!autopilot.ValueRO.Enabled)
                {
                    bool sliding = SystemAPI.HasComponent<TractionState>(entity)
                        && SystemAPI.GetComponent<TractionState>(entity).Sliding;
                    float slip = SystemAPI.HasComponent<DriftState>(entity)
                        ? SystemAPI.GetComponent<DriftState>(entity).SlipAngle
                        : 0f;
                    losingControl = GameFlowLogic.LosingControl(sliding, slip);
                }
                break;
            }

            gameState.ValueRW.RunSuspended = runSuspended;
            gameState.ValueRW.PlayerLosingControl = losingControl;

            // The autopilot under the pause menu has far more grip than the
            // player, so a pause asked for mid-slide waits until the car is back
            // in hand (a crash or another menu drops it)
            GameFlowLogic.UpdateHeldPause(ref gameState.ValueRW, losingControl, deltaTime);

            // =============================================================
            // Pilot Handoff (autopilot <-> player)
            // =============================================================

            bool menuOpen = gameState.ValueRO.CurrentMenu != MenuState.None;
            bool crashFlowActive = gameState.ValueRO.CrashPhase != CrashFlowPhase.None;

            // A key still held from a menu or the summary must be released
            // before input can take the wheel from the autopilot
            bool takeoverArmed = gameState.ValueRO.TakeoverArmed;
            bool takeoverInput = GameFlowLogic.TakeoverIntent(
                humanInput, menuOpen || crashFlowActive, ref takeoverArmed);
            gameState.ValueRW.TakeoverArmed = takeoverArmed;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            try
            {
                foreach (var (autopilot, velocity, scoreSession, riskState, summary, speedTier, entity) in
                    SystemAPI.Query<RefRW<Autopilot>, RefRO<Velocity>, RefRW<ScoreSession>,
                                   RefRW<RiskState>, RefRW<ScoreSummary>, RefRW<SpeedTier>>()
                        .WithAll<PlayerVehicleTag>()
                        .WithEntityAccess())
                {
                    float idleTimer = gameState.ValueRO.IdleTimer;

                    // While the autopilot drives, only a deliberate (armed) input
                    // takes over; while the player drives, any input counts as activity
                    bool pilotInput = autopilot.ValueRO.Enabled
                        ? takeoverInput
                        : autopilot.ValueRO.HumanInputDetected;

                    PilotDecision decision = GameFlowLogic.EvaluatePilot(
                        autopilot.ValueRO.Enabled,
                        pilotInput,
                        menuOpen,
                        crashFlowActive,
                        ref idleTimer,
                        deltaTime);

                    gameState.ValueRW.IdleTimer = idleTimer;

                    switch (decision)
                    {
                        case PilotDecision.EngageForMenu:
                            EngageAutopilot(ref autopilot.ValueRW, ref gameState.ValueRW,
                                AutopilotReason.Menu, velocity.ValueRO.Forward,
                                scoreSession.ValueRO.Active);
                            break;

                        case PilotDecision.EngageForIdle:
                            EngageAutopilot(ref autopilot.ValueRW, ref gameState.ValueRW,
                                AutopilotReason.Idle, velocity.ValueRO.Forward,
                                scoreSession.ValueRO.Active);
                            break;

                        case PilotDecision.PlayerTakeover:
                            autopilot.ValueRW.Enabled = false;
                            autopilot.ValueRW.Reason = AutopilotReason.None;
                            gameState.ValueRW.PlayerControlActive = true;

                            if (!scoreSession.ValueRO.Active)
                            {
                                // No run in progress (boot or post-crash): the moment
                                // the player moves the controls, a fresh run begins.
                                StartNewRun(ref state, ref scoreSession.ValueRW, ref riskState.ValueRW,
                                    ref summary.ValueRW, ref speedTier.ValueRW);
                            }
                            break;

                        case PilotDecision.NoChange:
                        default:
                            break;
                    }

                    // Keep the informational tag in sync for UI/queries
                    bool hasTag = SystemAPI.HasComponent<AutopilotActiveTag>(entity);
                    if (autopilot.ValueRO.Enabled && !hasTag)
                    {
                        ecb.AddComponent<AutopilotActiveTag>(entity);
                    }
                    else if (!autopilot.ValueRO.Enabled && hasTag)
                    {
                        ecb.RemoveComponent<AutopilotActiveTag>(entity);
                    }

                    break; // single player vehicle
                }

                ecb.Playback(state.EntityManager);
            }
            finally
            {
                ecb.Dispose();
            }
        }

        /// <summary>
        /// Hands the wheel to the autopilot. With a run suspended underneath
        /// (pause, idle) it holds the run's speed (up to
        /// GameFlowLogic.SuspendedRunMaxSpeed) so the player resumes with the
        /// momentum they left; otherwise it holds the current speed when that
        /// is inside its comfort band and eases toward it when not.
        /// </summary>
        private void EngageAutopilot(ref Autopilot autopilot, ref GameState gameState,
            AutopilotReason reason, float currentSpeed, bool runSuspended)
        {
            autopilot.Enabled = true;
            autopilot.Reason = reason;
            autopilot.TargetSpeed = GameFlowLogic.AutopilotTargetSpeedFor(currentSpeed, runSuspended);
            gameState.PlayerControlActive = false;
            gameState.IdleTimer = 0f;
        }

        /// <summary>
        /// Starts a fresh scoring run: zeroes score, risk, summary and speed
        /// tier, re-arms replay recording and counts the run for the session.
        /// </summary>
        private void StartNewRun(ref SystemState state, ref ScoreSession scoreSession,
            ref RiskState riskState, ref ScoreSummary summary, ref SpeedTier speedTier)
        {
            scoreSession = GameFlowLogic.NewScoreSession();
            riskState = GameFlowLogic.NewRiskState();
            summary = GameFlowLogic.NewScoreSummary();
            speedTier.Tier = 0;
            speedTier.Multiplier = 1f;

            // Re-arm the input recorder so the new run gets its own ghost log
            foreach (var replay in SystemAPI.Query<RefRW<ReplaySystemState>>())
            {
                replay.ValueRW.IsRecording = false;
                replay.ValueRW.InputsRecorded = 0;
                replay.ValueRW.RecordingTime = 0f;
                replay.ValueRW.TimeSinceLastRecord = 0f;
                break;
            }

            if (SystemAPI.TryGetSingletonRW<GameSessionState>(out var session))
            {
                session.ValueRW.RunCount++;
                session.ValueRW.SessionActive = true;
            }

            // Clear the previous run's summary banner
            if (SystemAPI.TryGetSingletonRW<ScoreSummaryDisplay>(out var display))
            {
                display.ValueRW.IsNewHighScore = false;
                display.ValueRW.LeaderboardRank = 0;
            }
        }
    }
}
