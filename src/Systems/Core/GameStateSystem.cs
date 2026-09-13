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

            if (gameState.ValueRO.CrashPhase != CrashFlowPhase.None)
            {
                gameState.ValueRW.CrashPhaseTimer += deltaTime;

                CrashFlowPhase phase = gameState.ValueRO.CrashPhase;
                float timer = gameState.ValueRO.CrashPhaseTimer;

                bool enteredReset = GameFlowLogic.AdvanceCrashPhase(
                    ref phase, ref timer, humanInput,
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
            // Pilot Handoff (autopilot <-> player)
            // =============================================================

            bool menuOpen = gameState.ValueRO.CurrentMenu != MenuState.None;
            bool crashFlowActive = gameState.ValueRO.CrashPhase != CrashFlowPhase.None;

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

                    PilotDecision decision = GameFlowLogic.EvaluatePilot(
                        autopilot.ValueRO.Enabled,
                        autopilot.ValueRO.HumanInputDetected,
                        menuOpen,
                        crashFlowActive,
                        ref idleTimer,
                        deltaTime);

                    gameState.ValueRW.IdleTimer = idleTimer;

                    switch (decision)
                    {
                        case PilotDecision.EngageForMenu:
                            EngageAutopilot(ref autopilot.ValueRW, ref gameState.ValueRW,
                                AutopilotReason.Menu, velocity.ValueRO.Forward);
                            break;

                        case PilotDecision.EngageForIdle:
                            EngageAutopilot(ref autopilot.ValueRW, ref gameState.ValueRW,
                                AutopilotReason.Idle, velocity.ValueRO.Forward);
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
        /// Hands the wheel to the autopilot. The target speed holds the current
        /// speed when it is inside the autopilot's comfort band so the handoff
        /// is invisible; otherwise it eases toward the recovery speed.
        /// </summary>
        private void EngageAutopilot(ref Autopilot autopilot, ref GameState gameState,
            AutopilotReason reason, float currentSpeed)
        {
            autopilot.Enabled = true;
            autopilot.Reason = reason;
            autopilot.TargetSpeed = GameFlowLogic.AutopilotTargetSpeedFor(currentSpeed);
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
