// Nightflow - UI Controller
// MonoBehaviour orchestrator connecting ECS UIState to sub-controllers

using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Utilities;

namespace Nightflow.UI
{
    /// <summary>
    /// Main UI controller that reads from ECS UIState and delegates to specialized
    /// sub-controllers. Attach to a GameObject with UIDocument component.
    ///
    /// Sub-controllers:
    ///   HUDController              - score, speed, damage, lane, emergency warnings
    ///   MenuController             - overlays, button callbacks, menu navigation
    ///   NotificationController     - toast notification queue and lifecycle
    ///   PerformanceStatsController - FPS/frame time debug panel
    ///   ChallengeController        - daily/weekly challenge panel
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

        // Sub-controllers
        private HUDController hud;
        private MenuController menu;
        private NotificationController notifications;
        private PerformanceStatsController perfStats;
        private ChallengeController challenges;

        // ECS access
        private EntityManager entityManager;
        private EntityQuery uiStateQuery;
        private EntityQuery scoreSummaryQuery;
        private EntityQuery perfStatsQuery;
        private EntityQuery challengeQuery;
        private bool ecsInitialized;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }
        }

        private void Start()
        {
            InitializeSubControllers();
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

                // Wire ECS queries into sub-controllers that need them
                menu.Initialize(
                    uiDocument.rootVisualElement,
                    entityManager,
                    uiStateQuery,
                    scoreSummaryQuery
                );
                perfStats.Initialize(uiDocument.rootVisualElement, entityManager, perfStatsQuery);
                challenges.Initialize(uiDocument.rootVisualElement, entityManager, challengeQuery);
            }
        }

        private void InitializeSubControllers()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Log.SystemError("UIController", "UIDocument or root element is null");
                return;
            }

            var root = uiDocument.rootVisualElement;

            // Create sub-controllers
            hud = new HUDController();
            menu = new MenuController();
            notifications = new NotificationController();
            perfStats = new PerformanceStatsController();
            challenges = new ChallengeController();

            // Initialize UI-only sub-controllers (no ECS dependency)
            hud.Initialize(root, scoreAnimationSpeed, damageFlashDuration,
                warningFlashRate, speedTiers,
                (text, value, style) => notifications.ShowNotification(text, value, style));
            notifications.Initialize(root, notificationDuration);

            // Menu, PerfStats, and Challenges need ECS queries — deferred to TryInitializeECS
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

            menu.HandleMenuInput();
            hud.Update(uiState, Time.deltaTime);
            menu.UpdateOverlays(uiState);
            notifications.Update();
            perfStats.Update();
            challenges.Update();
        }

        // Public API (preserved for external callers and Unity event hooks)

        /// <summary>
        /// Queue a notification toast to display on screen.
        /// </summary>
        public void ShowNotification(string text, string value = null, string styleClass = null)
        {
            notifications?.ShowNotification(text, value, styleClass);
        }

        /// <summary>
        /// Toggle the performance stats debug panel on/off.
        /// Can be called from keyboard shortcut (e.g., F3) or debug menu.
        /// </summary>
        public void TogglePerformanceStats()
        {
            perfStats?.Toggle();
        }

        public void TriggerFadeIn()
        {
            menu?.TriggerFadeIn();
        }

        public void TriggerFadeOut()
        {
            menu?.TriggerFadeOut();
        }
    }
}
