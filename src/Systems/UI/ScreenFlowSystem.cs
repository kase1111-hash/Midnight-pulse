// ============================================================================
// Nightflow - Screen Flow System
// Manages game state transitions, pause, crash flow, and screen overlays
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;

namespace Nightflow.Systems.UI
{
    /// <summary>
    /// Manages game state transitions and crash flow sequence.
    /// Handles pause menu, crash animation, score summary, and reset.
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
        // Crash flow timing (seconds)
        private const float ImpactDuration = 0.2f;
        private const float ShakeDuration = 0.8f;
        private const float FadeDuration = 0.5f;
        private const float SummaryMinDuration = 2.0f;
        private const float ResetDuration = 0.3f;
        private const float FadeInDuration = 0.5f;

        // Pause cooldown
        private const float PauseCooldownDuration = 5.0f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameState>();
            state.RequireForUpdate<UIState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get singletons
            RefRW<GameState> gameState = SystemAPI.GetSingletonRW<GameState>();
            RefRW<UIState> uiState = SystemAPI.GetSingletonRW<UIState>();

            // Update pause cooldown
            if (gameState.ValueRW.PauseCooldown > 0f)
            {
                gameState.ValueRW.PauseCooldown = math.max(0f, gameState.ValueRW.PauseCooldown - deltaTime);
            }

            // Process crash flow
            if (gameState.ValueRO.CrashPhase != CrashFlowPhase.None)
            {
                ProcessCrashFlow(ref gameState.ValueRW, ref uiState.ValueRW, deltaTime);
            }
            else
            {
                // Normal game state updates
                UpdatePauseState(ref gameState.ValueRW, ref uiState.ValueRW);
            }

            // Sync UI overlay states
            SyncUIOverlays(ref gameState.ValueRO, ref uiState.ValueRW);
        }

        [BurstCompile]
        private void ProcessCrashFlow(ref GameState gameState, ref UIState uiState, float deltaTime)
        {
            // Advance timer
            gameState.CrashPhaseTimer += deltaTime;

            switch (gameState.CrashPhase)
            {
                case CrashFlowPhase.Impact:
                    // Slow-motion impact
                    gameState.TimeScale = 0.2f;
                    if (gameState.CrashPhaseTimer >= ImpactDuration)
                    {
                        TransitionToPhase(ref gameState, CrashFlowPhase.ScreenShake);
                    }
                    break;

                case CrashFlowPhase.ScreenShake:
                    // Extended shake effect
                    gameState.TimeScale = 0.5f;
                    if (gameState.CrashPhaseTimer >= ShakeDuration)
                    {
                        TransitionToPhase(ref gameState, CrashFlowPhase.FadeOut);
                    }
                    break;

                case CrashFlowPhase.FadeOut:
                    // Fade to black
                    gameState.TimeScale = 1f;
                    float fadeProgress = math.saturate(gameState.CrashPhaseTimer / FadeDuration);
                    gameState.FadeAlpha = fadeProgress;
                    uiState.OverlayAlpha = fadeProgress;

                    if (gameState.CrashPhaseTimer >= FadeDuration)
                    {
                        TransitionToPhase(ref gameState, CrashFlowPhase.Summary);
                    }
                    break;

                case CrashFlowPhase.Summary:
                    // Show score summary
                    gameState.FadeAlpha = 1f;
                    uiState.ShowScoreSummary = true;
                    uiState.ShowCrashOverlay = true;

                    // Wait for minimum duration or player input (handled elsewhere)
                    if (gameState.CrashPhaseTimer >= SummaryMinDuration)
                    {
                        // Mark that summary can be dismissed
                        // Actual dismissal happens via menu selection
                    }
                    break;

                case CrashFlowPhase.Reset:
                    // Reset vehicle position
                    uiState.ShowScoreSummary = false;
                    uiState.ShowCrashOverlay = false;

                    if (gameState.CrashPhaseTimer >= ResetDuration)
                    {
                        TransitionToPhase(ref gameState, CrashFlowPhase.FadeIn);
                    }
                    break;

                case CrashFlowPhase.FadeIn:
                    // Fade back in
                    float fadeInProgress = math.saturate(gameState.CrashPhaseTimer / FadeInDuration);
                    gameState.FadeAlpha = 1f - fadeInProgress;
                    uiState.OverlayAlpha = 1f - fadeInProgress;

                    if (gameState.CrashPhaseTimer >= FadeInDuration)
                    {
                        CompleteCrashFlow(ref gameState);
                    }
                    break;
            }
        }

        [BurstCompile]
        private void TransitionToPhase(ref GameState gameState, CrashFlowPhase newPhase)
        {
            gameState.CrashPhase = newPhase;
            gameState.CrashPhaseTimer = 0f;
        }

        [BurstCompile]
        private void CompleteCrashFlow(ref GameState gameState)
        {
            gameState.CrashPhase = CrashFlowPhase.None;
            gameState.CrashPhaseTimer = 0f;
            gameState.FadeAlpha = 0f;
            gameState.TimeScale = 1f;

            // Enable autopilot after crash
            if (gameState.AutopilotQueued)
            {
                gameState.PlayerControlActive = false;
                gameState.AutopilotQueued = false;
            }

            // Start pause cooldown
            gameState.PauseCooldown = PauseCooldownDuration;
        }

        [BurstCompile]
        private void UpdatePauseState(ref GameState gameState, ref UIState uiState)
        {
            // Pause is toggled from input system, we just sync menu state
            if (gameState.IsPaused)
            {
                gameState.CurrentMenu = MenuState.Pause;
                gameState.MenuVisible = true;
                uiState.ShowPauseMenu = true;
            }
            else if (gameState.CurrentMenu == MenuState.Pause)
            {
                gameState.CurrentMenu = MenuState.None;
                gameState.MenuVisible = false;
                uiState.ShowPauseMenu = false;
            }
        }

        [BurstCompile]
        private void SyncUIOverlays(ref GameState gameState, ref UIState uiState)
        {
            // Overlay alpha from fade
            if (gameState.CrashPhase != CrashFlowPhase.None)
            {
                uiState.OverlayAlpha = gameState.FadeAlpha;
            }

            // Warning flash timing
            if (uiState.WarningPriority > 0)
            {
                // Flash at 2 Hz when warning active
                float time = (float)SystemAPI.Time.ElapsedTime;
                uiState.WarningFlash = (time * 4f) % 2f < 1f;
            }
            else
            {
                uiState.WarningFlash = false;
            }

            // Damage flash
            if (uiState.CriticalDamage)
            {
                float time = (float)SystemAPI.Time.ElapsedTime;
                uiState.DamageFlash = (time * 6f) % 2f < 1f;
            }
        }

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
        }

        /// <summary>
        /// Called to dismiss summary and continue to reset.
        /// </summary>
        public static void DismissSummary(ref GameState gameState)
        {
            if (gameState.CrashPhase == CrashFlowPhase.Summary)
            {
                gameState.CrashPhase = CrashFlowPhase.Reset;
                gameState.CrashPhaseTimer = 0f;
            }
        }

        /// <summary>
        /// Toggle pause state if cooldown allows.
        /// </summary>
        public static bool TryTogglePause(ref GameState gameState)
        {
            // Can't pause during crash flow
            if (gameState.CrashPhase != CrashFlowPhase.None)
                return false;

            // Check cooldown
            if (gameState.PauseCooldown > 0f && !gameState.IsPaused)
                return false;

            gameState.IsPaused = !gameState.IsPaused;

            if (gameState.IsPaused)
            {
                gameState.TimeScale = 0f;
            }
            else
            {
                gameState.TimeScale = 1f;
                gameState.PauseCooldown = PauseCooldownDuration;
            }

            return true;
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
