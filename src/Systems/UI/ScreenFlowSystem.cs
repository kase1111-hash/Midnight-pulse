// ============================================================================
// Nightflow - Screen Flow System
// Derives UI overlay flags from GameState and exposes the navigation helpers
// that menus, input and the crash flow share
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Config;

namespace Nightflow.Systems.UI
{
    /// <summary>
    /// Keeps UIState overlay flags in sync with GameState every frame.
    ///
    /// The crash flow timing and the pilot handoff live in GameStateSystem;
    /// this system never advances phases or timers, it only mirrors them into
    /// UI-facing flags. Menus and the crash sequence are overlays on a world
    /// that keeps moving, so nothing in here stops time.
    ///
    /// From spec:
    /// - Pause with 5-second cooldown
    /// - Crash flow: impact → shake → fade → summary → reset → autopilot
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Unity.Entities.BeginSimulationEntityCommandBufferSystem))]
    public partial struct ScreenFlowSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameState>();
            state.RequireForUpdate<UIState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<GameState> gameState = SystemAPI.GetSingletonRW<GameState>();
            RefRW<UIState> uiState = SystemAPI.GetSingletonRW<UIState>();

            SyncMenuFlags(ref gameState.ValueRW, ref uiState.ValueRW);
            SyncCrashOverlays(in gameState.ValueRO, ref uiState.ValueRW);
            SyncWarningFlashes(ref state, ref uiState.ValueRW);
        }

        /// <summary>
        /// IsPaused / MenuVisible are derived from CurrentMenu so no caller can
        /// leave them stale; the pause overlay shows only for the pause menu.
        /// </summary>
        [BurstCompile]
        private void SyncMenuFlags(ref GameState gameState, ref UIState uiState)
        {
            bool pauseMenu = gameState.CurrentMenu == MenuState.Pause;
            gameState.IsPaused = pauseMenu;
            gameState.MenuVisible = gameState.CurrentMenu != MenuState.None;
            uiState.ShowPauseMenu = pauseMenu;
        }

        [BurstCompile]
        private void SyncCrashOverlays(in GameState gameState, ref UIState uiState)
        {
            bool summary = gameState.CrashPhase == CrashFlowPhase.Summary;
            uiState.ShowScoreSummary = summary;
            uiState.ShowCrashOverlay = summary;

            // Black fade follows the crash flow; menus clear it
            uiState.OverlayAlpha = gameState.CrashPhase != CrashFlowPhase.None
                ? gameState.FadeAlpha
                : 0f;
        }

        [BurstCompile]
        private void SyncWarningFlashes(ref SystemState state, ref UIState uiState)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;

            // Warning flash timing (2 Hz while a warning is active)
            if (uiState.WarningPriority > 0)
            {
                uiState.WarningFlash = (time * 4f) % 2f < 1f;
            }
            else
            {
                uiState.WarningFlash = false;
            }

            // Damage flash
            if (uiState.CriticalDamage)
            {
                uiState.DamageFlash = (time * 6f) % 2f < 1f;
            }
        }

        // ====================================================================
        // Shared navigation helpers (called from ECS systems and managed UI)
        // ====================================================================

        /// <summary>
        /// Called when a crash occurs to start the crash flow sequence.
        /// </summary>
        public static void TriggerCrash(ref GameState gameState, CrashReason reason, bool queueAutopilot = true)
        {
            if (gameState.CrashPhase != CrashFlowPhase.None)
                return; // Already in crash flow

            gameState.CrashPhase = CrashFlowPhase.Impact;
            gameState.CrashPhaseTimer = 0f;
            gameState.AutopilotQueued = queueAutopilot;

            // A crash closes any pause menu; the summary is the only overlay now
            if (gameState.CurrentMenu == MenuState.Pause)
            {
                gameState.CurrentMenu = MenuState.None;
            }
        }

        /// <summary>
        /// Requests an in-place vehicle reset (performed by CrashSystem this
        /// frame) and runs the Reset → FadeIn tail of the crash flow so the
        /// handoff to the autopilot is covered by the fade.
        /// </summary>
        public static void RequestVehicleReset(ref GameState gameState)
        {
            gameState.CrashPhase = CrashFlowPhase.Reset;
            gameState.CrashPhaseTimer = 0f;
            gameState.FadeAlpha = 1f;
            gameState.VehicleResetPending = true;
            gameState.AutopilotQueued = true;
        }

        /// <summary>
        /// Called to dismiss the score summary and continue to reset.
        /// </summary>
        public static void DismissSummary(ref GameState gameState)
        {
            if (gameState.CrashPhase == CrashFlowPhase.Summary)
            {
                RequestVehicleReset(ref gameState);
            }
        }

        /// <summary>
        /// Restart the run right now: from the summary this is the same as
        /// dismissing it; mid-run it ends the run, resets the car under a quick
        /// fade and hands it to the autopilot until the player moves a control.
        /// </summary>
        public static void RequestRestart(ref GameState gameState)
        {
            if (gameState.CrashPhase == CrashFlowPhase.Summary)
            {
                DismissSummary(ref gameState);
                return;
            }

            if (gameState.CrashPhase != CrashFlowPhase.None)
                return; // Impact/shake/fade already heading for a reset

            gameState.CurrentMenu = MenuState.None;
            gameState.MenuVisible = false;
            gameState.IsPaused = false;
            RequestVehicleReset(ref gameState);
        }

        /// <summary>
        /// Toggle the pause menu if the cooldown allows. Pausing never stops the
        /// car: GameStateSystem hands the wheel to the autopilot while any menu
        /// is open, and gives it back on the first control input after resume.
        /// </summary>
        public static bool TryTogglePause(ref GameState gameState)
        {
            // Can't pause during crash flow
            if (gameState.CrashPhase != CrashFlowPhase.None)
                return false;

            bool paused = gameState.CurrentMenu == MenuState.Pause;

            // Only the pause menu toggles here (other menus have their own back actions)
            if (!paused && gameState.CurrentMenu != MenuState.None)
                return false;

            // Check cooldown
            if (!paused && gameState.PauseCooldown > 0f)
                return false;

            if (paused)
            {
                ResumeFromPause(ref gameState);
            }
            else
            {
                gameState.CurrentMenu = MenuState.Pause;
                gameState.MenuVisible = true;
                gameState.IsPaused = true;
            }

            return true;
        }

        /// <summary>
        /// Close the pause menu and start the pause cooldown.
        /// </summary>
        public static void ResumeFromPause(ref GameState gameState)
        {
            if (gameState.CurrentMenu != MenuState.Pause)
                return;

            gameState.CurrentMenu = MenuState.None;
            gameState.MenuVisible = false;
            gameState.IsPaused = false;
            gameState.TimeScale = 1f;
            gameState.PauseCooldown = GameConstants.PauseCooldownDuration;
        }
    }

    /// <summary>
    /// System to handle UI input for menus and overlays.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ScreenFlowSystem))]
    public partial struct UIInputSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Input handling is done in managed UIController
            // This system could process buffered input commands if needed
        }
    }
}
