// ============================================================================
// Nightflow - UI Controller
// MonoBehaviour bridge connecting ECS UIState to UI Toolkit elements
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Save;

namespace Nightflow.UI
{
    /// <summary>
    /// Main UI controller that reads from ECS UIState and updates UI Toolkit elements.
    /// Attach to a GameObject with UIDocument component.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Animation Settings")]
        [SerializeField] private float scoreAnimationSpeed = 10f;
        [SerializeField] private float damageFlashDuration = 0.3f;
        [SerializeField] private float notificationDuration = 2f;
        [SerializeField] private float warningFlashRate = 2f;

        [Header("Speed Tiers")]
        [SerializeField] private float[] speedTiers = { 60f, 100f, 140f, 180f, 220f };

        // Root element
        private VisualElement root;

        // Top bar elements
        private Label scoreValue;
        private Label multiplierValue;
        private VisualElement multiplierFill;
        private Label distanceValue;

        // Damage indicator elements
        private VisualElement damageBarFill;
        private VisualElement damageIndicator;
        private VisualElement warningIndicator;
        private Label warningText;
        private List<VisualElement> damageZones = new List<VisualElement>();

        // Speedometer elements
        private VisualElement speedometer;
        private Label speedValue;
        private VisualElement speedTierIndicator;
        private List<VisualElement> laneDots = new List<VisualElement>();

        // Status indicators
        private VisualElement autopilotIndicator;
        private VisualElement boostIndicator;

        // Notifications
        private VisualElement notificationContainer;
        private Queue<NotificationData> pendingNotifications = new Queue<NotificationData>();
        private List<VisualElement> activeNotifications = new List<VisualElement>();

        // Emergency arrows
        private VisualElement leftArrow;
        private VisualElement rightArrow;
        private VisualElement behindArrow;

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

        // Performance stats panel elements
        private VisualElement perfPanel;
        private Label perfFps;
        private Label perfFrametime;
        private Label perfMinMax;
        private Label perfEntities;
        private Label perfTraffic;
        private Label perfHazards;
        private Label perfSegments;
        private Label perfSpeed;
        private Label perfPosition;

        // Challenge panel elements
        private VisualElement challengePanel;
        private Label streakValue;
        private Label timerValue;
        private VisualElement[] challengeRows = new VisualElement[3];
        private Label[] challengeDescs = new Label[3];
        private VisualElement[] challengeProgressFills = new VisualElement[3];
        private Label[] challengeProgressTexts = new Label[3];
        private Label[] challengeRewards = new Label[3];
        private VisualElement[] challengeChecks = new VisualElement[3];
        private VisualElement[] challengeIcons = new VisualElement[3];
        // Weekly challenge
        private VisualElement weeklyRow;
        private Label weeklyDesc;
        private VisualElement weeklyProgressFill;
        private Label weeklyProgressText;
        private Label weeklyReward;
        private VisualElement weeklyCheck;

        // Animation state
        private float displayedScore;
        private float damageFlashTimer;
        private float warningFlashTimer;
        private bool isWarningVisible;

        // Cached state
        private float lastDamage;
        private int lastMultiplier;

        // ECS access
        private EntityManager entityManager;
        private EntityQuery uiStateQuery;
        private EntityQuery scoreSummaryQuery;
        private EntityQuery perfStatsQuery;
        private EntityQuery challengeQuery;
        private bool ecsInitialized;

        private struct NotificationData
        {
            public string Text;
            public string Value;
            public string StyleClass;
            public float Duration;
        }

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }
        }

        private void Start()
        {
            InitializeUI();
            TryInitializeECS();
        }

        private void TryInitializeECS()
        {
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                uiStateQuery = entityManager.CreateEntityQuery(typeof(UIState));
                scoreSummaryQuery = entityManager.CreateEntityQuery(typeof(ScoreSummaryDisplay));
                perfStatsQuery = entityManager.CreateEntityQuery(typeof(PerformanceStats));
                challengeQuery = entityManager.CreateEntityQuery(
                    typeof(DailyChallengeState),
                    typeof(ChallengeManagerTag)
                );
                ecsInitialized = true;
            }
        }

        private void InitializeUI()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogError("UIController: UIDocument or root element is null");
                return;
            }

            root = uiDocument.rootVisualElement;

            // Top bar
            scoreValue = root.Q<Label>("score-value");
            multiplierValue = root.Q<Label>("multiplier-value");
            multiplierFill = root.Q<VisualElement>("multiplier-fill");
            distanceValue = root.Q<Label>("distance-value");

            // Damage indicator
            damageIndicator = root.Q<VisualElement>("damage-indicator");
            damageBarFill = root.Q<VisualElement>("damage-bar-fill");
            warningIndicator = root.Q<VisualElement>("warning-indicator");
            warningText = root.Q<Label>("warning-text");

            // Get damage zones
            var zones = root.Q<VisualElement>("damage-zones");
            if (zones != null)
            {
                zones.Query<VisualElement>(className: "damage-zone").ForEach(zone => damageZones.Add(zone));
            }

            // Speedometer
            speedometer = root.Q<VisualElement>("speedometer");
            speedValue = root.Q<Label>("speed-value");
            speedTierIndicator = root.Q<VisualElement>("speed-tier-indicator");

            // Lane indicator
            var laneIndicator = root.Q<VisualElement>("lane-indicator");
            if (laneIndicator != null)
            {
                laneIndicator.Query<VisualElement>(className: "lane-dot").ForEach(dot => laneDots.Add(dot));
            }

            // Status indicators
            autopilotIndicator = root.Q<VisualElement>("autopilot-indicator");
            boostIndicator = root.Q<VisualElement>("boost-indicator");

            // Notifications
            notificationContainer = root.Q<VisualElement>("notification-container");

            // Emergency arrows
            leftArrow = root.Q<VisualElement>("emergency-left");
            rightArrow = root.Q<VisualElement>("emergency-right");
            behindArrow = root.Q<VisualElement>("emergency-behind");

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

            // Performance stats panel
            perfPanel = root.Q<VisualElement>("perf-panel");
            perfFps = root.Q<Label>("perf-fps");
            perfFrametime = root.Q<Label>("perf-frametime");
            perfMinMax = root.Q<Label>("perf-minmax");
            perfEntities = root.Q<Label>("perf-entities");
            perfTraffic = root.Q<Label>("perf-traffic");
            perfHazards = root.Q<Label>("perf-hazards");
            perfSegments = root.Q<Label>("perf-segments");
            perfSpeed = root.Q<Label>("perf-speed");
            perfPosition = root.Q<Label>("perf-position");

            // Challenge panel
            challengePanel = root.Q<VisualElement>("challenge-panel");
            streakValue = root.Q<Label>("streak-value");
            timerValue = root.Q<Label>("timer-value");

            // Daily challenges (0-2)
            for (int i = 0; i < 3; i++)
            {
                challengeRows[i] = root.Q<VisualElement>($"challenge-{i}");
                challengeDescs[i] = root.Q<Label>($"challenge-{i}-desc");
                challengeProgressFills[i] = root.Q<VisualElement>($"challenge-{i}-progress-fill");
                challengeProgressTexts[i] = root.Q<Label>($"challenge-{i}-progress");
                challengeRewards[i] = root.Q<Label>($"challenge-{i}-reward");
                challengeChecks[i] = root.Q<VisualElement>($"challenge-{i}-check");
                challengeIcons[i] = root.Q<VisualElement>($"challenge-{i}-icon");
            }

            // Weekly challenge
            weeklyRow = root.Q<VisualElement>("weekly-challenge");
            weeklyDesc = root.Q<Label>("weekly-desc");
            weeklyProgressFill = root.Q<VisualElement>("weekly-progress-fill");
            weeklyProgressText = root.Q<Label>("weekly-progress");
            weeklyReward = root.Q<Label>("weekly-reward");
            weeklyCheck = root.Q<VisualElement>("weekly-check");

            // Setup button callbacks
            SetupButtons();

            // Hide overlays initially
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

            // Mode cards (make them clickable)
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

        private void OnSettingsClicked()
        {
            // Find SettingsUIController and show it
            var settingsController = FindAnyObjectByType<SettingsUIController>();
            if (settingsController != null)
            {
                settingsController.Show();
            }
        }

        private void Update()
        {
            if (!ecsInitialized)
            {
                TryInitializeECS();
                return;
            }

            if (uiStateQuery.IsEmpty)
                return;

            var uiState = uiStateQuery.GetSingleton<UIState>();

            UpdateHUD(uiState);
            UpdateOverlays(uiState);
            UpdateAnimations(Time.deltaTime);
            ProcessNotifications();
            UpdatePerformanceStats();
            UpdateChallenges();
        }

        private void UpdateHUD(UIState state)
        {
            // Score (animated)
            displayedScore = Mathf.Lerp(displayedScore, state.Score, Time.deltaTime * scoreAnimationSpeed);
            if (scoreValue != null)
            {
                scoreValue.text = Mathf.RoundToInt(displayedScore).ToString("N0");
            }

            // Multiplier
            if (multiplierValue != null)
            {
                int currentMult = Mathf.RoundToInt(state.Multiplier);
                multiplierValue.text = $"x{state.Multiplier:F1}";

                // Flash on multiplier increase
                if (currentMult > lastMultiplier && lastMultiplier > 0)
                {
                    multiplierValue.AddToClassList("flash");
                    if (state.MultiplierFlash)
                    {
                        ShowNotification("MULTIPLIER UP!", $"x{state.Multiplier:F1}", "bonus");
                    }
                }
                else
                {
                    multiplierValue.RemoveFromClassList("flash");
                }
                lastMultiplier = currentMult;
            }

            // Multiplier bar fill (based on risk meter)
            if (multiplierFill != null)
            {
                float fillPercent = state.RiskPercent * 100f;
                multiplierFill.style.width = new StyleLength(new Length(fillPercent, LengthUnit.Percent));
            }

            // Distance
            if (distanceValue != null)
            {
                distanceValue.text = $"{state.DistanceKm:F2} km";
            }

            // Speed
            if (speedValue != null)
            {
                speedValue.text = Mathf.RoundToInt(state.SpeedKmh).ToString();
            }

            // Speed boost state
            if (speedometer != null)
            {
                if (state.SpeedTier >= 2)
                {
                    speedometer.AddToClassList("boosted");
                }
                else
                {
                    speedometer.RemoveFromClassList("boosted");
                }
            }

            // Update speed tier indicator
            UpdateSpeedTierIndicator(state.SpeedKmh);

            // Damage (pack zones into bitmask)
            int damageZoneMask = GetDamageZoneMask(state);
            UpdateDamageDisplay(state.DamageTotal, damageZoneMask);

            // Lane indicator - derive from signals or other state
            UpdateLaneIndicator(0); // Default to center lane

            // Status indicators
            UpdateStatusIndicators(false, state.SpeedTier >= 2);

            // Emergency warnings
            float2 emergencyDir = new float2(0, 1); // Behind by default
            UpdateEmergencyWarnings(emergencyDir, state.EmergencyDistance);

            // Warning indicator
            UpdateWarningIndicator(state);
        }

        private int GetDamageZoneMask(UIState state)
        {
            int mask = 0;
            float threshold = 0.3f;
            float criticalThreshold = 0.7f;

            // Front-left, Front-right, Rear-left, Rear-right
            if (state.DamageFront > threshold || state.DamageLeft > threshold) mask |= 1;
            if (state.DamageFront > threshold || state.DamageRight > threshold) mask |= 2;
            if (state.DamageRear > threshold || state.DamageLeft > threshold) mask |= 4;
            if (state.DamageRear > threshold || state.DamageRight > threshold) mask |= 8;

            // Critical flags in upper bits
            if (state.DamageFront > criticalThreshold || state.DamageLeft > criticalThreshold) mask |= 256;
            if (state.DamageFront > criticalThreshold || state.DamageRight > criticalThreshold) mask |= 512;
            if (state.DamageRear > criticalThreshold || state.DamageLeft > criticalThreshold) mask |= 1024;
            if (state.DamageRear > criticalThreshold || state.DamageRight > criticalThreshold) mask |= 2048;

            return mask;
        }

        private void UpdateSpeedTierIndicator(float speed)
        {
            if (speedTierIndicator == null) return;

            speedTierIndicator.Clear();

            for (int i = 0; i < speedTiers.Length; i++)
            {
                var tierDot = new VisualElement();
                tierDot.AddToClassList("speed-tier-dot");

                if (speed >= speedTiers[i])
                {
                    tierDot.AddToClassList("active");
                }

                speedTierIndicator.Add(tierDot);
            }
        }

        private void UpdateDamageDisplay(float damagePercent, int damageZoneMask)
        {
            // Damage bar
            if (damageBarFill != null)
            {
                float inversePercent = (1f - damagePercent) * 100f;
                damageBarFill.style.width = new StyleLength(new Length(inversePercent, LengthUnit.Percent));

                // Update damage state classes
                damageBarFill.RemoveFromClassList("warning");
                damageBarFill.RemoveFromClassList("critical");

                if (damagePercent > 0.75f)
                {
                    damageBarFill.AddToClassList("critical");
                }
                else if (damagePercent > 0.5f)
                {
                    damageBarFill.AddToClassList("warning");
                }

                // Flash on damage
                if (damagePercent > lastDamage + 0.01f)
                {
                    damageFlashTimer = damageFlashDuration;
                    if (damageIndicator != null)
                    {
                        damageIndicator.AddToClassList("flash");
                    }
                }
                lastDamage = damagePercent;
            }

            // Damage zones (bitfield: FL=1, FR=2, RL=4, RR=8, etc.)
            for (int i = 0; i < damageZones.Count && i < 8; i++)
            {
                var zone = damageZones[i];
                bool isDamaged = (damageZoneMask & (1 << i)) != 0;
                bool isCritical = (damageZoneMask & (1 << (i + 8))) != 0;

                zone.RemoveFromClassList("damaged");
                zone.RemoveFromClassList("critical");

                if (isCritical)
                {
                    zone.AddToClassList("critical");
                }
                else if (isDamaged)
                {
                    zone.AddToClassList("damaged");
                }
            }
        }

        private void UpdateLaneIndicator(int currentLane)
        {
            for (int i = 0; i < laneDots.Count; i++)
            {
                if (i == currentLane)
                {
                    laneDots[i].AddToClassList("active");
                }
                else
                {
                    laneDots[i].RemoveFromClassList("active");
                }
            }
        }

        private void UpdateStatusIndicators(bool autopilot, bool boosting)
        {
            if (autopilotIndicator != null)
            {
                autopilotIndicator.style.display = autopilot ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (boostIndicator != null)
            {
                boostIndicator.style.display = boosting ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void UpdateEmergencyWarnings(float2 direction, float distance)
        {
            bool hasEmergency = distance < 200f && distance > 0f;

            if (!hasEmergency)
            {
                HideEmergencyArrows();
                return;
            }

            // Determine which arrow to show based on direction
            float angle = math.atan2(direction.y, direction.x);
            float degrees = math.degrees(angle);

            HideEmergencyArrows();

            // Flash based on distance
            bool shouldFlash = (Time.time * warningFlashRate) % 1f < 0.5f;

            if (degrees > -45f && degrees < 45f)
            {
                // Behind
                if (behindArrow != null)
                {
                    behindArrow.style.display = DisplayStyle.Flex;
                    SetArrowFlash(behindArrow, shouldFlash && distance < 100f);
                }
            }
            else if (degrees >= 45f && degrees < 135f)
            {
                // Left
                if (leftArrow != null)
                {
                    leftArrow.style.display = DisplayStyle.Flex;
                    SetArrowFlash(leftArrow, shouldFlash && distance < 100f);
                }
            }
            else if (degrees <= -45f && degrees > -135f)
            {
                // Right
                if (rightArrow != null)
                {
                    rightArrow.style.display = DisplayStyle.Flex;
                    SetArrowFlash(rightArrow, shouldFlash && distance < 100f);
                }
            }
        }

        private void HideEmergencyArrows()
        {
            if (leftArrow != null) leftArrow.style.display = DisplayStyle.None;
            if (rightArrow != null) rightArrow.style.display = DisplayStyle.None;
            if (behindArrow != null) behindArrow.style.display = DisplayStyle.None;
        }

        private void SetArrowFlash(VisualElement arrow, bool flash)
        {
            if (flash)
            {
                arrow.AddToClassList("flash");
            }
            else
            {
                arrow.RemoveFromClassList("flash");
            }
        }

        private void UpdateWarningIndicator(UIState state)
        {
            bool showWarning = state.WarningPriority > 0;

            if (warningIndicator != null)
            {
                warningIndicator.style.display = showWarning ? DisplayStyle.Flex : DisplayStyle.None;

                if (showWarning && warningText != null)
                {
                    // Set warning text based on priority
                    if (state.WarningPriority >= 3)
                    {
                        warningText.text = "EMERGENCY VEHICLE";
                    }
                    else if (state.WarningPriority == 2 || state.CriticalDamage)
                    {
                        warningText.text = "CRITICAL DAMAGE";
                    }
                    else if (state.DamageTotal > 0.5f)
                    {
                        warningText.text = "HEAVY DAMAGE";
                    }
                    else
                    {
                        warningText.text = "HIGH RISK";
                    }

                    // Flash the warning
                    if (state.WarningFlash)
                    {
                        warningIndicator.AddToClassList("flash");
                    }
                    else
                    {
                        warningIndicator.RemoveFromClassList("flash");
                    }
                }
            }
        }

        private void UpdatePerformanceStats()
        {
            // Check if performance stats query has data
            if (perfStatsQuery.IsEmpty || perfPanel == null)
                return;

            // Sync display setting from SaveManager (if available)
            SyncPerformanceDisplaySetting();

            var stats = perfStatsQuery.GetSingleton<PerformanceStats>();

            // Show/hide panel based on DisplayEnabled flag
            if (stats.DisplayEnabled)
            {
                perfPanel.RemoveFromClassList("hidden");

                // Update FPS with color coding
                if (perfFps != null)
                {
                    perfFps.text = $"{stats.SmoothedFPS:F0}";

                    // Color code based on performance
                    perfFps.RemoveFromClassList("perf-fps-good");
                    perfFps.RemoveFromClassList("perf-fps-warn");
                    perfFps.RemoveFromClassList("perf-fps-bad");

                    if (stats.SmoothedFPS >= 55f)
                        perfFps.AddToClassList("perf-fps-good");
                    else if (stats.SmoothedFPS >= 30f)
                        perfFps.AddToClassList("perf-fps-warn");
                    else
                        perfFps.AddToClassList("perf-fps-bad");
                }

                // Frame time
                if (perfFrametime != null)
                {
                    perfFrametime.text = $"{stats.FrameTimeMs:F1}ms";
                }

                // Min/Max frame times
                if (perfMinMax != null)
                {
                    float min = stats.MinFrameTimeMs < 1000f ? stats.MinFrameTimeMs : 0f;
                    perfMinMax.text = $"{min:F1}/{stats.MaxFrameTimeMs:F1}";
                }

                // Entity counts
                if (perfEntities != null)
                    perfEntities.text = stats.EntityCount.ToString();

                if (perfTraffic != null)
                    perfTraffic.text = stats.TrafficCount.ToString();

                if (perfHazards != null)
                    perfHazards.text = stats.HazardCount.ToString();

                if (perfSegments != null)
                    perfSegments.text = stats.SegmentCount.ToString();

                // Player stats
                if (perfSpeed != null)
                    perfSpeed.text = $"{stats.PlayerSpeed:F1} m/s";

                if (perfPosition != null)
                    perfPosition.text = $"{stats.PlayerZ:F0}m";
            }
            else
            {
                perfPanel.AddToClassList("hidden");
            }
        }

        /// <summary>
        /// Sync the performance stats display setting from SaveManager.
        /// </summary>
        private void SyncPerformanceDisplaySetting()
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null || perfStatsQuery.IsEmpty)
                return;

            var settings = saveManager.GetSettings();
            bool shouldShow = settings.Display.ShowFPS;

            // Get entity and update DisplayEnabled if it differs
            var entity = perfStatsQuery.GetSingletonEntity();
            var stats = entityManager.GetComponentData<PerformanceStats>(entity);

            if (stats.DisplayEnabled != shouldShow)
            {
                stats.DisplayEnabled = shouldShow;
                entityManager.SetComponentData(entity, stats);
            }
        }

        /// <summary>
        /// Toggle the performance stats display on/off.
        /// Can be called from keyboard shortcut (e.g., F3) or debug menu.
        /// </summary>
        public void TogglePerformanceStats()
        {
            if (perfStatsQuery.IsEmpty)
                return;

            var entity = perfStatsQuery.GetSingletonEntity();
            var stats = entityManager.GetComponentData<PerformanceStats>(entity);
            stats.DisplayEnabled = !stats.DisplayEnabled;
            entityManager.SetComponentData(entity, stats);

            // Also update the save settings
            var saveManager = SaveManager.Instance;
            if (saveManager != null)
            {
                var settings = saveManager.GetSettings();
                settings.Display.ShowFPS = stats.DisplayEnabled;
                saveManager.SaveSettings();
            }
        }

        private void UpdateChallenges()
        {
            if (challengePanel == null || challengeQuery.IsEmpty)
                return;

            var entity = challengeQuery.GetSingletonEntity();
            var state = entityManager.GetComponentData<DailyChallengeState>(entity);
            var buffer = entityManager.GetBuffer<ChallengeBuffer>(entity);

            // Update streak
            if (streakValue != null)
            {
                streakValue.text = state.CurrentStreak.ToString();
            }

            // Update expiration timer
            if (timerValue != null)
            {
                long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                int currentDay = DailyChallengeState.GetCurrentDay(now);
                long dayEnd = (currentDay + 1) * 86400L;
                long secondsRemaining = dayEnd - now;

                if (secondsRemaining > 0)
                {
                    int hours = (int)(secondsRemaining / 3600);
                    int minutes = (int)((secondsRemaining % 3600) / 60);
                    int seconds = (int)(secondsRemaining % 60);
                    timerValue.text = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
                }
                else
                {
                    timerValue.text = "00:00:00";
                }
            }

            // Update daily challenges
            int dailyIndex = 0;
            Challenge weeklyChallenge = default;
            bool hasWeekly = false;

            for (int i = 0; i < buffer.Length; i++)
            {
                var challenge = buffer[i].Value;

                if (challenge.IsWeekly)
                {
                    weeklyChallenge = challenge;
                    hasWeekly = true;
                }
                else if (dailyIndex < 3)
                {
                    UpdateChallengeRow(dailyIndex, challenge);
                    dailyIndex++;
                }
            }

            // Hide unused daily rows
            for (int i = dailyIndex; i < 3; i++)
            {
                if (challengeRows[i] != null)
                {
                    challengeRows[i].style.display = DisplayStyle.None;
                }
            }

            // Update weekly challenge
            if (hasWeekly)
            {
                UpdateWeeklyChallenge(weeklyChallenge);
            }
            else if (weeklyRow != null)
            {
                weeklyRow.style.display = DisplayStyle.None;
            }
        }

        private void UpdateChallengeRow(int index, Challenge challenge)
        {
            if (challengeRows[index] == null) return;

            challengeRows[index].style.display = DisplayStyle.Flex;

            // Update description
            if (challengeDescs[index] != null)
            {
                challengeDescs[index].text = GetChallengeDescription(challenge);
            }

            // Update progress bar
            float progressPercent = Mathf.Clamp01(challenge.ProgressRatio) * 100f;
            if (challengeProgressFills[index] != null)
            {
                challengeProgressFills[index].style.width = new StyleLength(new Length(progressPercent, LengthUnit.Percent));
            }

            // Update progress text
            if (challengeProgressTexts[index] != null)
            {
                challengeProgressTexts[index].text = GetProgressText(challenge);
            }

            // Update reward
            if (challengeRewards[index] != null)
            {
                challengeRewards[index].text = $"+{challenge.ScoreReward}";
            }

            // Update difficulty icon
            if (challengeIcons[index] != null)
            {
                challengeIcons[index].RemoveFromClassList("bronze");
                challengeIcons[index].RemoveFromClassList("silver");
                challengeIcons[index].RemoveFromClassList("gold");

                string diffClass = challenge.Difficulty switch
                {
                    ChallengeDifficulty.Bronze => "bronze",
                    ChallengeDifficulty.Silver => "silver",
                    ChallengeDifficulty.Gold => "gold",
                    _ => "bronze"
                };
                challengeIcons[index].AddToClassList(diffClass);
            }

            // Update completion state
            if (challenge.Completed)
            {
                challengeRows[index].AddToClassList("completed");
                if (challengeChecks[index] != null)
                {
                    challengeChecks[index].RemoveFromClassList("hidden");
                }
            }
            else
            {
                challengeRows[index].RemoveFromClassList("completed");
                if (challengeChecks[index] != null)
                {
                    challengeChecks[index].AddToClassList("hidden");
                }
            }
        }

        private void UpdateWeeklyChallenge(Challenge challenge)
        {
            if (weeklyRow == null) return;

            weeklyRow.style.display = DisplayStyle.Flex;

            if (weeklyDesc != null)
            {
                weeklyDesc.text = GetChallengeDescription(challenge);
            }

            float progressPercent = Mathf.Clamp01(challenge.ProgressRatio) * 100f;
            if (weeklyProgressFill != null)
            {
                weeklyProgressFill.style.width = new StyleLength(new Length(progressPercent, LengthUnit.Percent));
            }

            if (weeklyProgressText != null)
            {
                weeklyProgressText.text = GetProgressText(challenge);
            }

            if (weeklyReward != null)
            {
                weeklyReward.text = $"+{challenge.ScoreReward}";
            }

            if (challenge.Completed)
            {
                weeklyRow.AddToClassList("completed");
                if (weeklyCheck != null) weeklyCheck.RemoveFromClassList("hidden");
            }
            else
            {
                weeklyRow.RemoveFromClassList("completed");
                if (weeklyCheck != null) weeklyCheck.AddToClassList("hidden");
            }
        }

        private string GetChallengeDescription(Challenge challenge)
        {
            return challenge.Type switch
            {
                ChallengeType.ReachScore => $"Score {challenge.TargetValue:N0} points",
                ChallengeType.SurviveTime => $"Survive {challenge.TargetValue / 60f:F0} minutes",
                ChallengeType.TravelDistance => $"Travel {challenge.TargetValue / 1000f:F1} km",
                ChallengeType.ClosePasses => $"Perform {challenge.TargetValue:F0} close passes",
                ChallengeType.DodgeHazards => $"Dodge {challenge.TargetValue:F0} hazards",
                ChallengeType.ReachMultiplier => $"Reach {challenge.TargetValue:F1}x multiplier",
                ChallengeType.PerfectSegments => $"Complete {challenge.TargetValue:F0} perfect segments",
                ChallengeType.LaneWeaves => $"Perform {challenge.TargetValue:F0} lane weaves",
                ChallengeType.ThreadNeedle => $"Thread the needle {challenge.TargetValue:F0} times",
                ChallengeType.ComboChain => $"Build a {challenge.TargetValue:F0}x combo chain",
                ChallengeType.ReachSpeed => $"Reach {challenge.TargetValue * 3.6f:F0} km/h",
                ChallengeType.NoBrakeRun => $"Complete {challenge.TargetValue:F0} no-brake runs",
                _ => "Complete challenge"
            };
        }

        private string GetProgressText(Challenge challenge)
        {
            return challenge.Type switch
            {
                ChallengeType.ReachScore => $"{challenge.CurrentProgress:N0} / {challenge.TargetValue:N0}",
                ChallengeType.SurviveTime =>
                    $"{challenge.CurrentProgress / 60f:F1} / {challenge.TargetValue / 60f:F0} min",
                ChallengeType.TravelDistance =>
                    $"{challenge.CurrentProgress / 1000f:F2} / {challenge.TargetValue / 1000f:F1} km",
                ChallengeType.ReachMultiplier =>
                    $"{challenge.CurrentProgress:F1}x / {challenge.TargetValue:F1}x",
                ChallengeType.ReachSpeed =>
                    $"{challenge.CurrentProgress * 3.6f:F0} / {challenge.TargetValue * 3.6f:F0} km/h",
                _ => $"{challenge.CurrentProgress:F0} / {challenge.TargetValue:F0}"
            };
        }

        private void UpdateOverlays(UIState state)
        {
            // Check for main menu
            if (state.ShowMainMenu)
            {
                ShowMainMenu(state);
            }
            else if (mainMenuOverlay != null && !mainMenuOverlay.ClassListContains("hidden"))
            {
                mainMenuOverlay.AddToClassList("hidden");
            }

            // Check for credits
            if (state.ShowCredits)
            {
                if (creditsOverlay != null)
                {
                    creditsOverlay.RemoveFromClassList("hidden");
                }
            }
            else if (creditsOverlay != null && !creditsOverlay.ClassListContains("hidden"))
            {
                creditsOverlay.AddToClassList("hidden");
            }

            // Check for mode select
            if (state.ShowModeSelect)
            {
                if (modeSelectOverlay != null)
                {
                    modeSelectOverlay.RemoveFromClassList("hidden");
                }
            }
            else if (modeSelectOverlay != null && !modeSelectOverlay.ClassListContains("hidden"))
            {
                modeSelectOverlay.AddToClassList("hidden");
            }

            // Check for leaderboard (based on menu state)
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
                {
                    leaderboardOverlay.RemoveFromClassList("hidden");
                }
            }
            else if (leaderboardOverlay != null && !leaderboardOverlay.ClassListContains("hidden"))
            {
                leaderboardOverlay.AddToClassList("hidden");
            }

            // Check for pause menu
            if (state.ShowPauseMenu)
            {
                ShowPauseMenu();
            }
            else if (pauseOverlay != null && !pauseOverlay.ClassListContains("hidden"))
            {
                pauseOverlay.AddToClassList("hidden");
            }

            // Check for crash/game over overlay
            if (state.ShowCrashOverlay || state.ShowScoreSummary)
            {
                ShowGameOver(state);
            }
            else if (gameOverOverlay != null && !gameOverOverlay.ClassListContains("hidden"))
            {
                gameOverOverlay.AddToClassList("hidden");
            }

            // Update fade overlay
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
            {
                mainMenuOverlay.RemoveFromClassList("hidden");
            }

            // Update title animation
            if (gameTitle != null)
            {
                gameTitle.AddToClassList("animated");
            }

            // Update "Press Start" blink
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

            // Update menu item selection
            for (int i = 0; i < menuItemElements.Count; i++)
            {
                var item = menuItemElements[i];
                if (i == state.MainMenuSelection)
                {
                    item.AddToClassList("selected");
                }
                else
                {
                    item.RemoveFromClassList("selected");
                }
            }
        }

        private void ShowPauseMenu()
        {
            if (pauseOverlay != null)
            {
                pauseOverlay.RemoveFromClassList("hidden");
            }
        }

        private void ShowGameOver(UIState uiState)
        {
            if (gameOverOverlay != null)
            {
                gameOverOverlay.RemoveFromClassList("hidden");
            }

            // Get score summary if available
            if (!scoreSummaryQuery.IsEmpty)
            {
                var summary = scoreSummaryQuery.GetSingleton<ScoreSummaryDisplay>();

                // Update crash title based on reason
                if (crashTitle != null)
                {
                    crashTitle.text = GetCrashReasonText(summary.EndReason);
                }

                // Show high score label if applicable
                if (highscoreLabel != null)
                {
                    highscoreLabel.style.display = summary.IsNewHighScore ? DisplayStyle.Flex : DisplayStyle.None;
                }

                // Update score summary
                UpdateScoreSummary(summary, uiState);
            }
            else
            {
                // Fallback with UIState only
                if (crashTitle != null)
                {
                    crashTitle.text = "CRASHED";
                }
                if (highscoreLabel != null)
                {
                    highscoreLabel.style.display = DisplayStyle.None;
                }
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
            {
                summaryNearMisses.text = summary.ClosePasses.ToString();
            }

            if (summaryPerfectDodges != null)
            {
                summaryPerfectDodges.text = summary.HazardsDodged.ToString();
            }

            if (summaryMaxMultiplier != null)
            {
                summaryMaxMultiplier.text = $"x{uiState.HighestMultiplier:F1}";
            }

            if (summaryMaxSpeed != null)
            {
                summaryMaxSpeed.text = $"{Mathf.RoundToInt(summary.MaxSpeed)} km/h";
            }

            if (summaryFinalScore != null)
            {
                summaryFinalScore.text = summary.FinalScore.ToString("N0");
            }
        }

        private void UpdateAnimations(float deltaTime)
        {
            // Damage flash timer
            if (damageFlashTimer > 0f)
            {
                damageFlashTimer -= deltaTime;
                if (damageFlashTimer <= 0f && damageIndicator != null)
                {
                    damageIndicator.RemoveFromClassList("flash");
                }
            }
        }

        private void ProcessNotifications()
        {
            // Remove expired notifications
            for (int i = activeNotifications.Count - 1; i >= 0; i--)
            {
                var notification = activeNotifications[i];
                if (notification.resolvedStyle.opacity < 0.1f)
                {
                    notification.RemoveFromHierarchy();
                    activeNotifications.RemoveAt(i);
                }
            }

            // Show pending notifications
            while (pendingNotifications.Count > 0 && activeNotifications.Count < 3)
            {
                var data = pendingNotifications.Dequeue();
                CreateNotificationElement(data);
            }
        }

        public void ShowNotification(string text, string value = null, string styleClass = null)
        {
            pendingNotifications.Enqueue(new NotificationData
            {
                Text = text,
                Value = value,
                StyleClass = styleClass,
                Duration = notificationDuration
            });
        }

        private void CreateNotificationElement(NotificationData data)
        {
            if (notificationContainer == null) return;

            var notification = new VisualElement();
            notification.AddToClassList("notification");

            if (!string.IsNullOrEmpty(data.StyleClass))
            {
                notification.AddToClassList(data.StyleClass);
            }

            var textLabel = new Label(data.Text);
            textLabel.AddToClassList("notification-text");
            notification.Add(textLabel);

            if (!string.IsNullOrEmpty(data.Value))
            {
                var valueLabel = new Label(data.Value);
                valueLabel.AddToClassList("notification-value");
                notification.Add(valueLabel);
            }

            notificationContainer.Add(notification);
            activeNotifications.Add(notification);

            // Schedule fade out
            notification.schedule.Execute(() =>
            {
                notification.style.opacity = 0f;
            }).StartingIn((long)(data.Duration * 1000));
        }

        // Button callbacks
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
            if (!ecsInitialized) return;

            // First, ensure we've dismissed the "Press Start" state
            DismissPressStart();

            // Navigate to mode select
            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);

                // Use the navigation helper
                gameState.CurrentMenu = MenuState.ModeSelect;
                gameState.MenuVisible = true;
                uiState.ShowMainMenu = false;
                uiState.ShowModeSelect = true;

                entityManager.SetComponentData(entity, gameState);
                entityManager.SetComponentData(uiStateEntity, uiState);
            }
            gameStateQuery.Dispose();
        }

        private void OnLeaderboardClicked()
        {
            if (!ecsInitialized) return;

            DismissPressStart();

            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);

                gameState.CurrentMenu = MenuState.Leaderboard;
                gameState.MenuVisible = true;
                uiState.ShowMainMenu = false;

                entityManager.SetComponentData(entity, gameState);
                entityManager.SetComponentData(uiStateEntity, uiState);
            }
            gameStateQuery.Dispose();
        }

        private void OnCreditsClicked()
        {
            if (!ecsInitialized) return;

            DismissPressStart();

            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);

                gameState.CurrentMenu = MenuState.Credits;
                gameState.MenuVisible = true;
                uiState.ShowMainMenu = false;
                uiState.ShowCredits = true;

                entityManager.SetComponentData(entity, gameState);
                entityManager.SetComponentData(uiStateEntity, uiState);
            }
            gameStateQuery.Dispose();
        }

        private void OnCreditsBackClicked()
        {
            if (!ecsInitialized) return;

            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);

                // Return to main menu
                gameState.CurrentMenu = MenuState.MainMenu;
                gameState.MenuVisible = true;
                uiState.ShowCredits = false;
                uiState.ShowMainMenu = true;

                entityManager.SetComponentData(entity, gameState);
                entityManager.SetComponentData(uiStateEntity, uiState);
            }
            gameStateQuery.Dispose();
        }

        private void OnSettingsBackClicked()
        {
            if (!ecsInitialized) return;

            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);

                // Return to main menu
                gameState.CurrentMenu = MenuState.MainMenu;
                gameState.MenuVisible = true;
                uiState.ShowMainMenu = true;

                entityManager.SetComponentData(entity, gameState);
                entityManager.SetComponentData(uiStateEntity, uiState);
            }
            gameStateQuery.Dispose();
        }

        private void OnMainMenuClicked()
        {
            if (!ecsInitialized) return;

            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);

                // Go to main menu
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

        private void DismissPressStart()
        {
            // Show the menu items by dismissing "Press Start" state
            var mainMenuQuery = entityManager.CreateEntityQuery(typeof(MainMenuState));
            if (!mainMenuQuery.IsEmpty)
            {
                var entity = mainMenuQuery.GetSingletonEntity();
                var mainMenuState = entityManager.GetComponentData<MainMenuState>(entity);
                mainMenuState.InputReceived = true;
                entityManager.SetComponentData(entity, mainMenuState);
            }
            mainMenuQuery.Dispose();

            // Show menu items visually
            if (mainMenuItems != null)
            {
                mainMenuItems.RemoveFromClassList("hidden");
            }
            if (pressStartText != null)
            {
                pressStartText.AddToClassList("hidden");
            }
        }

        private int selectedModeIndex = 0;

        private void SelectMode(int modeIndex)
        {
            selectedModeIndex = modeIndex;

            // Update visual selection
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

        private void OnModeStartClicked()
        {
            if (!ecsInitialized) return;

            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);

                // Start the game
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
            if (!ecsInitialized) return;

            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);

                // Return to main menu
                gameState.CurrentMenu = MenuState.MainMenu;
                uiState.ShowModeSelect = false;
                uiState.ShowMainMenu = true;

                entityManager.SetComponentData(entity, gameState);
                entityManager.SetComponentData(uiStateEntity, uiState);
            }
            gameStateQuery.Dispose();
        }

        private void OnLeaderboardBackClicked()
        {
            if (!ecsInitialized) return;

            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);
                var uiStateEntity = uiStateQuery.GetSingletonEntity();
                var uiState = entityManager.GetComponentData<UIState>(uiStateEntity);

                // Return to main menu
                gameState.CurrentMenu = MenuState.MainMenu;
                uiState.ShowMainMenu = true;

                entityManager.SetComponentData(entity, gameState);
                entityManager.SetComponentData(uiStateEntity, uiState);
            }
            gameStateQuery.Dispose();
        }

        private void RequestUnpause()
        {
            if (!ecsInitialized) return;

            // Find GameState entity and unpause
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
            if (!ecsInitialized) return;

            // Find GameState entity and trigger restart
            var gameStateQuery = entityManager.CreateEntityQuery(typeof(Components.GameState));
            if (!gameStateQuery.IsEmpty)
            {
                var entity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<Components.GameState>(entity);

                // If in summary phase, dismiss it
                if (gameState.CrashPhase == CrashFlowPhase.Summary)
                {
                    gameState.CrashPhase = CrashFlowPhase.Reset;
                    gameState.CrashPhaseTimer = 0f;
                }
                else
                {
                    // Direct restart - reset crash state
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

        public void TriggerFadeIn()
        {
            if (fadeOverlay != null)
            {
                fadeOverlay.AddToClassList("active");
            }
        }

        public void TriggerFadeOut()
        {
            if (fadeOverlay != null)
            {
                fadeOverlay.RemoveFromClassList("active");
            }
        }
    }
}
