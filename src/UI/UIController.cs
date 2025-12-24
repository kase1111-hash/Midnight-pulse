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
        private Label crashTitle;
        private Label highscoreLabel;

        // Score summary labels
        private Label summaryDistance;
        private Label summaryNearMisses;
        private Label summaryPerfectDodges;
        private Label summaryMaxMultiplier;
        private Label summaryMaxSpeed;
        private Label summaryFinalScore;

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
            crashTitle = root.Q<Label>("crash-title");
            highscoreLabel = root.Q<Label>("highscore-label");

            // Score summary
            summaryDistance = root.Q<Label>("summary-distance");
            summaryNearMisses = root.Q<Label>("summary-nearmisses");
            summaryPerfectDodges = root.Q<Label>("summary-perfectdodges");
            summaryMaxMultiplier = root.Q<Label>("summary-maxmultiplier");
            summaryMaxSpeed = root.Q<Label>("summary-maxspeed");
            summaryFinalScore = root.Q<Label>("summary-finalscore");

            // Setup button callbacks
            SetupButtons();

            // Hide overlays initially
            HideAllOverlays();
        }

        private void SetupButtons()
        {
            // Pause menu buttons
            var resumeButton = root.Q<Button>("resume-button");
            var restartButtonPause = root.Q<Button>("restart-button-pause");
            var quitButtonPause = root.Q<Button>("quit-button-pause");

            if (resumeButton != null) resumeButton.clicked += OnResumeClicked;
            if (restartButtonPause != null) restartButtonPause.clicked += OnRestartClicked;
            if (quitButtonPause != null) quitButtonPause.clicked += OnQuitClicked;

            // Game over buttons
            var restartButtonGameover = root.Q<Button>("restart-button-gameover");
            var quitButtonGameover = root.Q<Button>("quit-button-gameover");

            if (restartButtonGameover != null) restartButtonGameover.clicked += OnRestartClicked;
            if (quitButtonGameover != null) quitButtonGameover.clicked += OnQuitClicked;
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

        private void UpdateOverlays(UIState state)
        {
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
