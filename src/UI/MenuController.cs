// Nightflow - Menu Controller
// Handles overlays, button callbacks, menu navigation, and game state transitions

using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using System.Collections.Generic;
using Nightflow.Components;
using Nightflow.Utilities;

namespace Nightflow.UI
{
    /// <summary>
    /// Manages all menu overlays (main menu, pause, game over, mode select,
    /// credits, leaderboard) and their button callbacks.
    /// </summary>
    public class MenuController
    {
        // Overlays
        private VisualElement pauseOverlay;
        private VisualElement gameOverOverlay;
        private VisualElement fadeOverlay;
        private VisualElement mainMenuOverlay;
        private VisualElement creditsOverlay;
        private VisualElement modeSelectOverlay;
        private VisualElement leaderboardOverlay;
        private Label crashTitle;
        private Label highscoreLabel;

        // Main menu elements
        private Label gameTitle;
        private Label pressStartText;
        private VisualElement mainMenuItems;
        private List<VisualElement> menuItemElements = new List<VisualElement>();

        // Score summary labels
        private Label summaryDistance;
        private Label summaryNearMisses;
        private Label summaryPerfectDodges;
        private Label summaryMaxMultiplier;
        private Label summaryMaxSpeed;
        private Label summaryFinalScore;

        // ECS references
        private EntityManager entityManager;
        private EntityQuery uiStateQuery;
        private EntityQuery scoreSummaryQuery;

        // Mode selection state
        private int selectedModeIndex;

        // Root element (for button queries)
        private VisualElement root;

        public void Initialize(VisualElement root, EntityManager em, EntityQuery uiQuery, EntityQuery summaryQuery)
        {
            this.root = root;
            entityManager = em;
            uiStateQuery = uiQuery;
            scoreSummaryQuery = summaryQuery;

            // Overlays
            pauseOverlay = root.Q<VisualElement>("pause-overlay");
            gameOverOverlay = root.Q<VisualElement>("gameover-overlay");
            fadeOverlay = root.Q<VisualElement>("fade-overlay");
            mainMenuOverlay = root.Q<VisualElement>("main-menu-overlay");
            creditsOverlay = root.Q<VisualElement>("credits-overlay");
            modeSelectOverlay = root.Q<VisualElement>("mode-select-overlay");
            leaderboardOverlay = root.Q<VisualElement>("leaderboard-overlay");
            crashTitle = root.Q<Label>("gameover-title");
            highscoreLabel = root.Q<Label>("highscore-text");

            // Main menu elements
            gameTitle = root.Q<Label>("game-title");
            pressStartText = root.Q<Label>("press-start-text");
            mainMenuItems = root.Q<VisualElement>("main-menu-items");
            if (mainMenuItems != null)
            {
                mainMenuItems.Query<VisualElement>(className: "main-menu-item").ForEach(item => menuItemElements.Add(item));
            }

            // Score summary
            summaryDistance = root.Q<Label>("summary-distance");
            summaryNearMisses = root.Q<Label>("summary-nearmisses");
            summaryPerfectDodges = root.Q<Label>("summary-perfectdodges");
            summaryMaxMultiplier = root.Q<Label>("summary-maxmultiplier");
            summaryMaxSpeed = root.Q<Label>("summary-maxspeed");
            summaryFinalScore = root.Q<Label>("summary-finalscore");

            SetupButtons();
            HideAllOverlays();
        }

        private void SetupButtons()
        {
            // Pause menu buttons
            var resumeButton = root.Q<Button>("resume-button");
            var restartButtonPause = root.Q<Button>("restart-button");
            var settingsButton = root.Q<Button>("settings-button");
            var quitButtonPause = root.Q<Button>("quit-button");

            if (resumeButton != null) resumeButton.clicked += OnResumeClicked;
            if (restartButtonPause != null) restartButtonPause.clicked += OnRestartClicked;
            if (settingsButton != null) settingsButton.clicked += OnSettingsClicked;
            if (quitButtonPause != null) quitButtonPause.clicked += OnQuitClicked;

            // Game over buttons
            var retryButton = root.Q<Button>("retry-button");
            var menuButton = root.Q<Button>("menu-button");

            if (retryButton != null) retryButton.clicked += OnRestartClicked;
            if (menuButton != null) menuButton.clicked += OnMainMenuClicked;

            // Main menu buttons
            var playButton = root.Q<Button>("menu-item-0");
            var leaderboardButton = root.Q<Button>("menu-item-1");
            var settingsMenuButton = root.Q<Button>("menu-item-2");
            var creditsButton = root.Q<Button>("menu-item-3");
            var quitMenuButton = root.Q<Button>("menu-item-4");

            if (playButton != null) playButton.clicked += OnPlayClicked;
            if (leaderboardButton != null) leaderboardButton.clicked += OnLeaderboardClicked;
            if (settingsMenuButton != null) settingsMenuButton.clicked += OnSettingsClicked;
            if (creditsButton != null) creditsButton.clicked += OnCreditsClicked;
            if (quitMenuButton != null) quitMenuButton.clicked += OnQuitClicked;

            // Credits back button
            var creditsBackButton = root.Q<Button>("credits-back");
            if (creditsBackButton != null) creditsBackButton.clicked += OnCreditsBackClicked;

            // Settings back button
            var settingsBackButton = root.Q<Button>("settings-back");
            if (settingsBackButton != null) settingsBackButton.clicked += OnSettingsBackClicked;

            // Mode select buttons
            var modeStartButton = root.Q<Button>("mode-start");
            var modeBackButton = root.Q<Button>("mode-back");
            if (modeStartButton != null) modeStartButton.clicked += OnModeStartClicked;
            if (modeBackButton != null) modeBackButton.clicked += OnModeBackClicked;

            // Mode cards
            var modeNightflow = root.Q<VisualElement>("mode-nightflow");
            var modeRedline = root.Q<VisualElement>("mode-redline");
            var modeGhost = root.Q<VisualElement>("mode-ghost");
            var modeFreeflow = root.Q<VisualElement>("mode-freeflow");
            if (modeNightflow != null) modeNightflow.RegisterCallback<ClickEvent>(e => SelectMode(0));
            if (modeRedline != null) modeRedline.RegisterCallback<ClickEvent>(e => SelectMode(1));
            if (modeGhost != null) modeGhost.RegisterCallback<ClickEvent>(e => SelectMode(2));
            if (modeFreeflow != null) modeFreeflow.RegisterCallback<ClickEvent>(e => SelectMode(3));

            // Leaderboard back button
            var leaderboardBackButton = root.Q<Button>("leaderboard-back");
            if (leaderboardBackButton != null) leaderboardBackButton.clicked += OnLeaderboardBackClicked;
        }

        public void HandleMenuInput()
        {
            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (gameStateQuery.IsEmpty)
            {
                gameStateQuery.Dispose();
                return;
            }

            var entity = gameStateQuery.GetSingletonEntity();
            var gameState = entityManager.GetComponentData<Components.GameState>(entity);

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscapeKey(ref gameState, entity);
            }

            if (gameState.CurrentMenu == MenuState.MainMenu)
            {
                HandleMainMenuInput(ref gameState, entity);
            }

            gameStateQuery.Dispose();
        }

        private void HandleEscapeKey(ref Components.GameState gameState, Entity gameStateEntity)
        {
            if (gameState.CurrentMenu == MenuState.Credits)
            {
                OnCreditsBackClicked();
                return;
            }
            if (gameState.CurrentMenu == MenuState.Settings)
            {
                OnSettingsBackClicked();
                return;
            }
            if (gameState.CurrentMenu == MenuState.Leaderboard)
            {
                OnLeaderboardBackClicked();
                return;
            }
            if (gameState.CurrentMenu == MenuState.ModeSelect)
            {
                OnModeBackClicked();
                return;
            }

            if (gameState.CurrentMenu == MenuState.MainMenu)
            {
                return;
            }

            if (gameState.CrashPhase == CrashFlowPhase.Summary)
            {
                gameState.CrashPhase = CrashFlowPhase.Reset;
                gameState.CrashPhaseTimer = 0f;
                entityManager.SetComponentData(gameStateEntity, gameState);
                return;
            }

            if (gameState.CurrentMenu == MenuState.None || gameState.CurrentMenu == MenuState.Pause)
            {
                if (gameState.CrashPhase != CrashFlowPhase.None)
                    return;

                if (gameState.PauseCooldown > 0f && !gameState.IsPaused)
                    return;

                gameState.IsPaused = !gameState.IsPaused;
                gameState.TimeScale = gameState.IsPaused ? 0f : 1f;
                gameState.CurrentMenu = gameState.IsPaused ? MenuState.Pause : MenuState.None;
                gameState.MenuVisible = gameState.IsPaused;

                if (gameState.IsPaused)
                {
                    gameState.PauseCooldown = 5f;
                }

                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);
                uiState.ShowPauseMenu = gameState.IsPaused;
                entityManager.SetComponentData(uiStateEntity, uiState);
                entityManager.SetComponentData(gameStateEntity, gameState);
            }
        }

        private void HandleMainMenuInput(ref Components.GameState gameState, Entity gameStateEntity)
        {
            var mainMenuQuery = entityManager.CreateEntityQuery(typeof(MainMenuState));
            if (mainMenuQuery.IsEmpty)
            {
                mainMenuQuery.Dispose();
                return;
            }

            var mainMenuEntity = mainMenuQuery.GetSingletonEntity();
            var mainMenuState = entityManager.GetComponentData<MainMenuState>(mainMenuEntity);

            if (!mainMenuState.InputReceived)
            {
                bool anyInput = UnityEngine.Input.anyKeyDown ||
                                UnityEngine.Input.GetMouseButtonDown(0) ||
                                UnityEngine.Input.GetMouseButtonDown(1);

                if (anyInput)
                {
                    mainMenuState.InputReceived = true;
                    entityManager.SetComponentData(mainMenuEntity, mainMenuState);

                    if (mainMenuItems != null)
                    {
                        mainMenuItems.RemoveFromClassList("hidden");
                    }
                    if (pressStartText != null)
                    {
                        pressStartText.AddToClassList("hidden");
                    }

                    var uiStateEntity = uiStateQuery.GetSingletonEntity();
                    var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);
                    uiState.ShowPressStart = false;
                    entityManager.SetComponentData(uiStateEntity, uiState);
                }
            }

            mainMenuQuery.Dispose();
        }

        public void UpdateOverlays(UIState state)
        {
            // Main menu
            if (state.ShowMainMenu)
            {
                ShowMainMenu(state);
            }
            else if (mainMenuOverlay != null && !mainMenuOverlay.ClassListContains("hidden"))
            {
                mainMenuOverlay.AddToClassList("hidden");
            }

            // Credits
            if (state.ShowCredits)
            {
                if (creditsOverlay != null)
                    creditsOverlay.RemoveFromClassList("hidden");
            }
            else if (creditsOverlay != null && !creditsOverlay.ClassListContains("hidden"))
            {
                creditsOverlay.AddToClassList("hidden");
            }

            // Mode select
            if (state.ShowModeSelect)
            {
                if (modeSelectOverlay != null)
                    modeSelectOverlay.RemoveFromClassList("hidden");
            }
            else if (modeSelectOverlay != null && !modeSelectOverlay.ClassListContains("hidden"))
            {
                modeSelectOverlay.AddToClassList("hidden");
            }

            // Leaderboard
            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            bool showLeaderboard = false;
            if (!gameStateQuery.IsEmpty)
            {
                var gameState = gameStateQuery.GetSingleton<Components.GameState>();
                showLeaderboard = gameState.CurrentMenu == MenuState.Leaderboard;
            }
            gameStateQuery.Dispose();

            if (showLeaderboard)
            {
                if (leaderboardOverlay != null)
                    leaderboardOverlay.RemoveFromClassList("hidden");
            }
            else if (leaderboardOverlay != null && !leaderboardOverlay.ClassListContains("hidden"))
            {
                leaderboardOverlay.AddToClassList("hidden");
            }

            // Pause menu
            if (state.ShowPauseMenu)
            {
                ShowPauseMenu();
            }
            else if (pauseOverlay != null && !pauseOverlay.ClassListContains("hidden"))
            {
                pauseOverlay.AddToClassList("hidden");
            }

            // Crash/game over
            if (state.ShowCrashOverlay || state.ShowScoreSummary)
            {
                ShowGameOver(state);
            }
            else if (gameOverOverlay != null && !gameOverOverlay.ClassListContains("hidden"))
            {
                gameOverOverlay.AddToClassList("hidden");
            }

            // Fade overlay
            if (fadeOverlay != null)
            {
                if (state.OverlayAlpha > 0.01f)
                {
                    fadeOverlay.style.opacity = state.OverlayAlpha;
                    fadeOverlay.AddToClassList("active");
                }
                else
                {
                    fadeOverlay.RemoveFromClassList("active");
                }
            }
        }

        private void HideAllOverlays()
        {
            if (pauseOverlay != null) pauseOverlay.AddToClassList("hidden");
            if (gameOverOverlay != null) gameOverOverlay.AddToClassList("hidden");
            if (mainMenuOverlay != null) mainMenuOverlay.AddToClassList("hidden");
            if (creditsOverlay != null) creditsOverlay.AddToClassList("hidden");
            if (modeSelectOverlay != null) modeSelectOverlay.AddToClassList("hidden");
            if (leaderboardOverlay != null) leaderboardOverlay.AddToClassList("hidden");
        }

        private void ShowMainMenu(UIState state)
        {
            if (mainMenuOverlay != null)
                mainMenuOverlay.RemoveFromClassList("hidden");

            if (gameTitle != null)
                gameTitle.AddToClassList("animated");

            if (pressStartText != null)
            {
                if (state.ShowPressStart)
                {
                    pressStartText.style.display = DisplayStyle.Flex;
                    pressStartText.AddToClassList("blink");
                }
                else
                {
                    pressStartText.RemoveFromClassList("blink");
                }
            }

            for (int i = 0; i < menuItemElements.Count; i++)
            {
                var item = menuItemElements[i];
                if (i == state.MainMenuSelection)
                    item.AddToClassList("selected");
                else
                    item.RemoveFromClassList("selected");
            }
        }

        private void ShowPauseMenu()
        {
            if (pauseOverlay != null)
                pauseOverlay.RemoveFromClassList("hidden");
        }

        private void ShowGameOver(UIState uiState)
        {
            if (gameOverOverlay != null)
                gameOverOverlay.RemoveFromClassList("hidden");

            if (!scoreSummaryQuery.IsEmpty)
            {
                var summary = scoreSummaryQuery.GetSingleton<ScoreSummaryDisplay>();

                if (crashTitle != null)
                    crashTitle.text = GetCrashReasonText(summary.EndReason);

                if (highscoreLabel != null)
                    highscoreLabel.style.display = summary.IsNewHighScore ? DisplayStyle.Flex : DisplayStyle.None;

                UpdateScoreSummary(summary, uiState);
            }
            else
            {
                if (crashTitle != null)
                    crashTitle.text = "CRASHED";
                if (highscoreLabel != null)
                    highscoreLabel.style.display = DisplayStyle.None;
            }
        }

        private string GetCrashReasonText(CrashReason reason)
        {
            return reason switch
            {
                CrashReason.TotalDamage => "TOTALED",
                CrashReason.BarrierImpact => "BARRIER CRASH",
                CrashReason.HeadOnCollision => "HEAD-ON COLLISION",
                CrashReason.Rollover => "ROLLOVER",
                _ => "CRASHED"
            };
        }

        private void UpdateScoreSummary(ScoreSummaryDisplay summary, UIState uiState)
        {
            if (summaryDistance != null)
            {
                float km = summary.TotalDistance / 1000f;
                summaryDistance.text = $"{km:F2} km";
            }

            if (summaryNearMisses != null)
                summaryNearMisses.text = summary.ClosePasses.ToString();

            if (summaryPerfectDodges != null)
                summaryPerfectDodges.text = summary.HazardsDodged.ToString();

            if (summaryMaxMultiplier != null)
                summaryMaxMultiplier.text = $"x{uiState.HighestMultiplier:F1}";

            if (summaryMaxSpeed != null)
                summaryMaxSpeed.text = $"{Mathf.RoundToInt(summary.MaxSpeed)} km/h";

            if (summaryFinalScore != null)
                summaryFinalScore.text = summary.FinalScore.ToString("N0");
        }

        public void TriggerFadeIn()
        {
            if (fadeOverlay != null)
                fadeOverlay.AddToClassList("active");
        }

        public void TriggerFadeOut()
        {
            if (fadeOverlay != null)
                fadeOverlay.RemoveFromClassList("active");
        }

        #region Button Callbacks

        private void OnSettingsClicked()
        {
            var settingsController = Object.FindAnyObjectByType<SettingsUIController>();
            if (settingsController != null)
            {
                settingsController.Show();
            }
        }

        private void OnResumeClicked()
        {
            RequestUnpause();
        }

        private void OnRestartClicked()
        {
            RequestRestart();
        }

        private void OnQuitClicked()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        private void OnPlayClicked()
        {
            DismissPressStart();
            NavigateToMenu(MenuState.ModeSelect, showModeSelect: true);
        }

        private void OnLeaderboardClicked()
        {
            DismissPressStart();
            NavigateToMenu(MenuState.Leaderboard);
        }

        private void OnCreditsClicked()
        {
            DismissPressStart();
            NavigateToMenu(MenuState.Credits, showCredits: true);
        }

        private void OnCreditsBackClicked()
        {
            NavigateToMenu(MenuState.MainMenu, showMainMenu: true, clearCredits: true);
        }

        private void OnSettingsBackClicked()
        {
            NavigateToMenu(MenuState.MainMenu, showMainMenu: true);
        }

        private void OnMainMenuClicked()
        {
            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);

                gameState.CurrentMenu = MenuState.MainMenu;
                gameState.MenuVisible = true;
                gameState.IsPaused = true;
                gameState.TimeScale = 0f;
                gameState.CrashPhase = CrashFlowPhase.None;

                uiState.ShowPauseMenu = false;
                uiState.ShowCrashOverlay = false;
                uiState.ShowScoreSummary = false;
                uiState.ShowModeSelect = false;
                uiState.ShowMainMenu = true;
                uiState.OverlayAlpha = 0f;

                entityManager.SetComponentData(entity, gameState);
                entityManager.SetComponentData(uiStateEntity, uiState);
            }
            gameStateQuery.Dispose();
        }

        private void OnModeStartClicked()
        {
            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);

                gameState.CurrentMenu = MenuState.None;
                gameState.MenuVisible = false;
                gameState.IsPaused = false;
                gameState.TimeScale = 1f;
                gameState.PlayerControlActive = true;

                uiState.ShowMainMenu = false;
                uiState.ShowModeSelect = false;
                uiState.OverlayAlpha = 0f;

                entityManager.SetComponentData(entity, gameState);
                entityManager.SetComponentData(uiStateEntity, uiState);
            }
            gameStateQuery.Dispose();
        }

        private void OnModeBackClicked()
        {
            NavigateToMenu(MenuState.MainMenu, showMainMenu: true, clearModeSelect: true);
        }

        private void OnLeaderboardBackClicked()
        {
            NavigateToMenu(MenuState.MainMenu, showMainMenu: true);
        }

        private void SelectMode(int modeIndex)
        {
            selectedModeIndex = modeIndex;

            var modeCards = new[] { "mode-nightflow", "mode-redline", "mode-ghost", "mode-freeflow" };
            for (int i = 0; i < modeCards.Length; i++)
            {
                var card = root.Q<VisualElement>(modeCards[i]);
                if (card != null)
                {
                    if (i == modeIndex)
                        card.AddToClassList("selected");
                    else
                        card.RemoveFromClassList("selected");
                }
            }
        }

        #endregion

        #region Helpers

        private void NavigateToMenu(MenuState targetMenu, bool showMainMenu = false,
            bool showCredits = false, bool showModeSelect = false,
            bool clearCredits = false, bool clearModeSelect = false)
        {
            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);

                gameState.CurrentMenu = targetMenu;
                gameState.MenuVisible = true;

                // Clear previous screen flags
                uiState.ShowMainMenu = showMainMenu;
                if (showCredits) uiState.ShowCredits = true;
                if (showModeSelect) uiState.ShowModeSelect = true;
                if (clearCredits) uiState.ShowCredits = false;
                if (clearModeSelect) uiState.ShowModeSelect = false;

                entityManager.SetComponentData(entity, gameState);
                entityManager.SetComponentData(uiStateEntity, uiState);
            }
            gameStateQuery.Dispose();
        }

        private void DismissPressStart()
        {
            var mainMenuQuery = entityManager.CreateEntityQuery(typeof(MainMenuState));
            if (!mainMenuQuery.IsEmpty)
            {
                var entity = mainMenuQuery.GetSingletonEntity();
                var mainMenuState = entityManager.GetComponentData<MainMenuState>(entity);
                mainMenuState.InputReceived = true;
                entityManager.SetComponentData(entity, mainMenuState);
            }
            mainMenuQuery.Dispose();

            if (mainMenuItems != null)
                mainMenuItems.RemoveFromClassList("hidden");
            if (pressStartText != null)
                pressStartText.AddToClassList("hidden");
        }

        private void RequestUnpause()
        {
            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                gameState.IsPaused = false;
                gameState.TimeScale = 1f;
                gameState.CurrentMenu = MenuState.None;
                gameState.MenuVisible = false;
                entityManager.SetComponentData(entity, gameState);
            }
            gameStateQuery.Dispose();
        }

        private void RequestRestart()
        {
            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);

                if (gameState.CrashPhase == CrashFlowPhase.Summary)
                {
                    gameState.CrashPhase = CrashFlowPhase.Reset;
                    gameState.CrashPhaseTimer = 0f;
                }
                else
                {
                    gameState.CrashPhase = CrashFlowPhase.None;
                    gameState.IsPaused = false;
                    gameState.TimeScale = 1f;
                    gameState.FadeAlpha = 0f;
                    gameState.AutopilotQueued = true;
                }

                entityManager.SetComponentData(entity, gameState);
            }
            gameStateQuery.Dispose();
        }

        #endregion
    }
}
