// ============================================================================
// Nightflow - Menu Navigation System
// Manages title screen, main menu, and game flow transitions
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;

namespace Nightflow.Systems.UI
{
    /// <summary>
    /// Manages the complete game flow from title screen through gameplay and back.
    /// Handles transitions: Title → Mode Select → Play → Pause → Crash → Summary → Title
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ScreenFlowSystem))]
    public partial struct MenuNavigationSystem : ISystem
    {
        // Main menu items
        private const int MenuItemPlay = 0;
        private const int MenuItemLeaderboard = 1;
        private const int MenuItemSettings = 2;
        private const int MenuItemCredits = 3;
        private const int MenuItemQuit = 4;
        private const int MainMenuItemCount = 5;

        // Timing
        private const float TitleAnimationDuration = 2.0f;
        private const float PressStartBlinkRate = 1.5f;
        private const float TransitionFadeDuration = 0.4f;

        // Input cooldown
        private float inputCooldown;
        private const float InputCooldownTime = 0.15f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameState>();
            state.RequireForUpdate<UIState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update input cooldown
            if (inputCooldown > 0f)
            {
                inputCooldown = math.max(0f, inputCooldown - deltaTime);
            }

            // Get singletons
            RefRW<GameState> gameState = SystemAPI.GetSingletonRW<GameState>();
            RefRW<UIState> uiState = SystemAPI.GetSingletonRW<UIState>();

            // Get main menu state if available
            bool hasMainMenuState = SystemAPI.TryGetSingletonRW<MainMenuState>(out var mainMenuState);
            bool hasSessionState = SystemAPI.TryGetSingletonRW<GameSessionState>(out var sessionState);

            // Process based on current menu state
            switch (gameState.ValueRO.CurrentMenu)
            {
                case MenuState.MainMenu:
                    if (hasMainMenuState)
                    {
                        ProcessMainMenu(ref gameState.ValueRW, ref uiState.ValueRW,
                            ref mainMenuState.ValueRW, deltaTime);
                    }
                    break;

                case MenuState.ModeSelect:
                    ProcessModeSelect(ref gameState.ValueRW, ref uiState.ValueRW, deltaTime);
                    break;

                case MenuState.Leaderboard:
                    ProcessLeaderboard(ref gameState.ValueRW, ref uiState.ValueRW, deltaTime);
                    break;

                case MenuState.Settings:
                    ProcessSettings(ref gameState.ValueRW, ref uiState.ValueRW, deltaTime);
                    break;

                case MenuState.Credits:
                    ProcessCredits(ref gameState.ValueRW, ref uiState.ValueRW, deltaTime);
                    break;

                case MenuState.ScoreSummary:
                    ProcessScoreSummary(ref gameState.ValueRW, ref uiState.ValueRW,
                        hasSessionState ? ref sessionState.ValueRW : ref sessionState.ValueRW, deltaTime);
                    break;

                case MenuState.None:
                    // In gameplay - nothing to process here
                    break;
            }

            // Update session state
            if (hasSessionState)
            {
                UpdateSessionState(ref gameState.ValueRO, ref sessionState.ValueRW);
            }
        }

        [BurstCompile]
        private void ProcessMainMenu(ref GameState gameState, ref UIState uiState,
            ref MainMenuState mainMenu, float deltaTime)
        {
            // Update display time
            mainMenu.DisplayTime += deltaTime;

            // Title animation
            if (!mainMenu.TitleAnimationComplete && mainMenu.DisplayTime >= TitleAnimationDuration)
            {
                mainMenu.TitleAnimationComplete = true;
            }

            // "Press Start" blink
            mainMenu.BlinkTimer += deltaTime;
            if (mainMenu.BlinkTimer >= (1f / PressStartBlinkRate))
            {
                mainMenu.BlinkTimer = 0f;
                mainMenu.ShowPressStart = !mainMenu.ShowPressStart;
            }

            // Sync to UIState
            uiState.ShowMainMenu = true;
            uiState.ShowPressStart = !mainMenu.InputReceived && mainMenu.ShowPressStart;
            uiState.MainMenuSelection = mainMenu.SelectedIndex;

            // Menu is paused state
            gameState.TimeScale = 0f;
            gameState.IsPaused = true;
            gameState.MenuVisible = true;
        }

        [BurstCompile]
        private void ProcessModeSelect(ref GameState gameState, ref UIState uiState, float deltaTime)
        {
            uiState.ShowModeSelect = true;
            uiState.ShowMainMenu = false;

            // Mode select uses existing mode selection UI
            gameState.TimeScale = 0f;
            gameState.IsPaused = true;
            gameState.MenuVisible = true;
        }

        [BurstCompile]
        private void ProcessLeaderboard(ref GameState gameState, ref UIState uiState, float deltaTime)
        {
            uiState.ShowMainMenu = false;

            // Leaderboard visibility is handled by LeaderboardState
            gameState.TimeScale = 0f;
            gameState.IsPaused = true;
            gameState.MenuVisible = true;
        }

        [BurstCompile]
        private void ProcessSettings(ref GameState gameState, ref UIState uiState, float deltaTime)
        {
            uiState.ShowMainMenu = false;

            gameState.TimeScale = 0f;
            gameState.IsPaused = true;
            gameState.MenuVisible = true;
        }

        [BurstCompile]
        private void ProcessCredits(ref GameState gameState, ref UIState uiState, float deltaTime)
        {
            uiState.ShowMainMenu = false;
            uiState.ShowCredits = true;

            gameState.TimeScale = 0f;
            gameState.IsPaused = true;
            gameState.MenuVisible = true;
        }

        [BurstCompile]
        private void ProcessScoreSummary(ref GameState gameState, ref UIState uiState,
            ref GameSessionState sessionState, float deltaTime)
        {
            uiState.ShowScoreSummary = true;
            uiState.ShowCrashOverlay = true;

            // Summary display is handled by ScreenFlowSystem crash flow
        }

        [BurstCompile]
        private void UpdateSessionState(ref GameState gameState, ref GameSessionState sessionState)
        {
            // Determine current phase from game state
            if (gameState.CurrentMenu == MenuState.MainMenu)
            {
                sessionState.CurrentPhase = GameFlowPhase.TitleScreen;
                sessionState.SessionActive = false;
            }
            else if (gameState.CurrentMenu == MenuState.ModeSelect)
            {
                sessionState.CurrentPhase = GameFlowPhase.ModeSelection;
            }
            else if (gameState.CrashPhase != CrashFlowPhase.None)
            {
                if (gameState.CrashPhase == CrashFlowPhase.Summary)
                {
                    sessionState.CurrentPhase = GameFlowPhase.Summary;
                }
                else
                {
                    sessionState.CurrentPhase = GameFlowPhase.Crashing;
                }
            }
            else if (gameState.IsPaused && gameState.CurrentMenu == MenuState.Pause)
            {
                sessionState.CurrentPhase = GameFlowPhase.Paused;
            }
            else if (gameState.CurrentMenu == MenuState.None && !gameState.IsPaused)
            {
                sessionState.CurrentPhase = GameFlowPhase.Playing;
                sessionState.SessionActive = true;
            }
        }

        // ============================================================================
        // Static navigation helpers
        // ============================================================================

        /// <summary>
        /// Navigate to main menu / title screen.
        /// </summary>
        public static void GoToMainMenu(ref GameState gameState, ref UIState uiState)
        {
            gameState.CurrentMenu = MenuState.MainMenu;
            gameState.MenuVisible = true;
            gameState.IsPaused = true;
            gameState.TimeScale = 0f;
            gameState.CrashPhase = CrashFlowPhase.None;

            // Clear gameplay overlays
            uiState.ShowPauseMenu = false;
            uiState.ShowCrashOverlay = false;
            uiState.ShowScoreSummary = false;
            uiState.ShowModeSelect = false;
            uiState.ShowMainMenu = true;
            uiState.OverlayAlpha = 0f;
        }

        /// <summary>
        /// Navigate to mode selection.
        /// </summary>
        public static void GoToModeSelect(ref GameState gameState, ref UIState uiState)
        {
            gameState.CurrentMenu = MenuState.ModeSelect;
            gameState.MenuVisible = true;

            uiState.ShowMainMenu = false;
            uiState.ShowModeSelect = true;
        }

        /// <summary>
        /// Navigate to leaderboard.
        /// </summary>
        public static void GoToLeaderboard(ref GameState gameState, ref UIState uiState)
        {
            gameState.CurrentMenu = MenuState.Leaderboard;
            gameState.MenuVisible = true;

            uiState.ShowMainMenu = false;
        }

        /// <summary>
        /// Navigate to settings.
        /// </summary>
        public static void GoToSettings(ref GameState gameState, ref UIState uiState)
        {
            gameState.CurrentMenu = MenuState.Settings;
            gameState.MenuVisible = true;

            uiState.ShowMainMenu = false;
        }

        /// <summary>
        /// Navigate to credits.
        /// </summary>
        public static void GoToCredits(ref GameState gameState, ref UIState uiState)
        {
            gameState.CurrentMenu = MenuState.Credits;
            gameState.MenuVisible = true;

            uiState.ShowMainMenu = false;
            uiState.ShowCredits = true;
        }

        /// <summary>
        /// Start gameplay with selected mode.
        /// </summary>
        public static void StartGame(ref GameState gameState, ref UIState uiState)
        {
            gameState.CurrentMenu = MenuState.None;
            gameState.MenuVisible = false;
            gameState.IsPaused = false;
            gameState.TimeScale = 1f;
            gameState.PlayerControlActive = true;
            gameState.AutopilotQueued = false;

            // Clear all menu overlays
            uiState.ShowMainMenu = false;
            uiState.ShowModeSelect = false;
            uiState.ShowPauseMenu = false;
            uiState.ShowCrashOverlay = false;
            uiState.ShowScoreSummary = false;
            uiState.ShowCredits = false;
            uiState.OverlayAlpha = 0f;
        }

        /// <summary>
        /// Navigate back from current menu.
        /// </summary>
        public static void GoBack(ref GameState gameState, ref UIState uiState)
        {
            switch (gameState.CurrentMenu)
            {
                case MenuState.ModeSelect:
                case MenuState.Leaderboard:
                case MenuState.Settings:
                case MenuState.Credits:
                    // Return to main menu
                    GoToMainMenu(ref gameState, ref uiState);
                    break;

                case MenuState.Pause:
                    // Resume gameplay
                    gameState.CurrentMenu = MenuState.None;
                    gameState.MenuVisible = false;
                    gameState.IsPaused = false;
                    gameState.TimeScale = 1f;
                    uiState.ShowPauseMenu = false;
                    break;

                case MenuState.ScoreSummary:
                    // Go to main menu after summary
                    GoToMainMenu(ref gameState, ref uiState);
                    break;
            }
        }

        /// <summary>
        /// Handle main menu item selection.
        /// </summary>
        public static void SelectMainMenuItem(ref GameState gameState, ref UIState uiState, int index)
        {
            switch (index)
            {
                case MenuItemPlay:
                    GoToModeSelect(ref gameState, ref uiState);
                    break;
                case MenuItemLeaderboard:
                    GoToLeaderboard(ref gameState, ref uiState);
                    break;
                case MenuItemSettings:
                    GoToSettings(ref gameState, ref uiState);
                    break;
                case MenuItemCredits:
                    GoToCredits(ref gameState, ref uiState);
                    break;
                case MenuItemQuit:
                    // Quit is handled in managed code
                    break;
            }
        }
    }

    /// <summary>
    /// Tag component for main menu entity.
    /// </summary>
    public struct MainMenuTag : IComponentData { }
}
