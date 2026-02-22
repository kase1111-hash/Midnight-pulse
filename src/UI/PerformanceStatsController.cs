// Nightflow - Performance Stats Controller
// Handles performance stats panel: FPS, frame time, entity counts

using UnityEngine.UIElements;
using Unity.Entities;
using Nightflow.Components;
using Nightflow.Save;

namespace Nightflow.UI
{
    /// <summary>
    /// Manages the performance stats debug panel (toggled via F3 or settings).
    /// Reads PerformanceStats ECS component and updates UI Toolkit labels.
    /// </summary>
    public class PerformanceStatsController
    {
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

        private EntityManager entityManager;
        private EntityQuery perfStatsQuery;

        public void Initialize(VisualElement root, EntityManager em, EntityQuery query)
        {
            entityManager = em;
            perfStatsQuery = query;

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
        }

        public void Update()
        {
            if (perfStatsQuery.IsEmpty || perfPanel == null)
                return;

            SyncDisplaySetting();

            var stats = perfStatsQuery.GetSingleton<PerformanceStats>();

            if (stats.DisplayEnabled)
            {
                perfPanel.RemoveFromClassList("hidden");

                if (perfFps != null)
                {
                    perfFps.text = $"{stats.SmoothedFPS:F0}";

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

                if (perfFrametime != null)
                    perfFrametime.text = $"{stats.FrameTimeMs:F1}ms";

                if (perfMinMax != null)
                {
                    float min = stats.MinFrameTimeMs < 1000f ? stats.MinFrameTimeMs : 0f;
                    perfMinMax.text = $"{min:F1}/{stats.MaxFrameTimeMs:F1}";
                }

                if (perfEntities != null)
                    perfEntities.text = stats.EntityCount.ToString();

                if (perfTraffic != null)
                    perfTraffic.text = stats.TrafficCount.ToString();

                if (perfHazards != null)
                    perfHazards.text = stats.HazardCount.ToString();

                if (perfSegments != null)
                    perfSegments.text = stats.SegmentCount.ToString();

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

        public void Toggle()
        {
            if (perfStatsQuery.IsEmpty)
                return;

            var entity = perfStatsQuery.GetSingletonEntity();
            var stats = entityManager.GetComponentData<PerformanceStats>(entity);
            stats.DisplayEnabled = !stats.DisplayEnabled;
            entityManager.SetComponentData(entity, stats);

            var saveManager = SaveManager.Instance;
            if (saveManager != null)
            {
                var settings = saveManager.GetSettings();
                settings.Display.ShowFPS = stats.DisplayEnabled;
                saveManager.SaveSettings();
            }
        }

        private void SyncDisplaySetting()
        {
            var saveManager = SaveManager.Instance;
            if (saveManager == null || perfStatsQuery.IsEmpty)
                return;

            var settings = saveManager.GetSettings();
            bool shouldShow = settings.Display.ShowFPS;

            var entity = perfStatsQuery.GetSingletonEntity();
            var stats = entityManager.GetComponentData<PerformanceStats>(entity);

            if (stats.DisplayEnabled != shouldShow)
            {
                stats.DisplayEnabled = shouldShow;
                entityManager.SetComponentData(entity, stats);
            }
        }
    }
}
