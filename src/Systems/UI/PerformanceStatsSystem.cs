// ============================================================================
// Nightflow - Performance Stats System
// Tracks FPS, entity counts, and gameplay metrics for debug display
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems.UI
{
    /// <summary>
    /// Updates PerformanceStats singleton with current frame metrics.
    /// Runs at the end of the frame for accurate timing.
    ///
    /// Metrics tracked:
    /// - FPS (instantaneous and smoothed)
    /// - Frame time (current, min, max)
    /// - Entity counts (total, traffic, hazards, segments)
    /// - Player state (speed, position)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    public partial struct PerformanceStatsSystem : ISystem
    {
        // Smoothing factor for exponential moving average
        private const float SmoothingFactor = 0.1f;

        // Update interval for entity counting (every N frames to reduce overhead)
        private const int CountUpdateInterval = 10;

        private int _frameCounter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _frameCounter = 0;

            // Create the singleton entity if it doesn't exist
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, PerformanceStats.CreateDefault());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

            _frameCounter++;

            // Get performance stats singleton
            foreach (var stats in SystemAPI.Query<RefRW<PerformanceStats>>())
            {
                ref var s = ref stats.ValueRW;

                // =============================================================
                // Frame Timing
                // =============================================================

                float frameTimeMs = deltaTime * 1000f;
                float instantFPS = deltaTime > 0.0001f ? 1f / deltaTime : 0f;

                s.FrameTimeMs = frameTimeMs;
                s.FPS = instantFPS;

                // Smoothed FPS using exponential moving average
                s.SmoothedFPS = math.lerp(s.SmoothedFPS, instantFPS, SmoothingFactor);

                // Track min/max frame times
                if (frameTimeMs < s.MinFrameTimeMs && frameTimeMs > 0.1f)
                {
                    s.MinFrameTimeMs = frameTimeMs;
                }
                if (frameTimeMs > s.MaxFrameTimeMs)
                {
                    s.MaxFrameTimeMs = frameTimeMs;
                }

                // Update averaging accumulators
                s.AccumulatedTime += deltaTime;
                s.FrameCount++;

                // Game time
                s.GameTime = elapsedTime;

                // =============================================================
                // Entity Counts (updated periodically to reduce overhead)
                // =============================================================

                if (_frameCounter % CountUpdateInterval == 0)
                {
                    // Total entity count
                    s.EntityCount = state.EntityManager.UniversalQuery.CalculateEntityCount();

                    // Traffic count
                    int trafficCount = 0;
                    foreach (var _ in SystemAPI.Query<RefRO<TrafficAI>>().WithAll<TrafficVehicleTag>())
                    {
                        trafficCount++;
                    }
                    s.TrafficCount = trafficCount;

                    // Hazard count
                    int hazardCount = 0;
                    foreach (var _ in SystemAPI.Query<RefRO<Hazard>>().WithAll<HazardTag>())
                    {
                        hazardCount++;
                    }
                    s.HazardCount = hazardCount;

                    // Track segment count
                    int segmentCount = 0;
                    foreach (var _ in SystemAPI.Query<RefRO<TrackSegment>>().WithAll<TrackSegmentTag>())
                    {
                        segmentCount++;
                    }
                    s.SegmentCount = segmentCount;
                }

                // =============================================================
                // Player State
                // =============================================================

                foreach (var (velocity, transform) in
                    SystemAPI.Query<RefRO<Velocity>, RefRO<WorldTransform>>()
                        .WithAll<PlayerVehicleTag>())
                {
                    s.PlayerSpeed = velocity.ValueRO.Forward;
                    s.PlayerZ = transform.ValueRO.Position.z;
                    break;
                }
            }
        }

        /// <summary>
        /// Toggle the performance stats display.
        /// Call from settings or debug input handler.
        /// </summary>
        public static void ToggleDisplay(ref SystemState state)
        {
            foreach (var stats in SystemAPI.Query<RefRW<PerformanceStats>>())
            {
                stats.ValueRW.DisplayEnabled = !stats.ValueRO.DisplayEnabled;
                break;
            }
        }

        /// <summary>
        /// Toggle extended stats display (memory, system timings).
        /// </summary>
        public static void ToggleExtended(ref SystemState state)
        {
            foreach (var stats in SystemAPI.Query<RefRW<PerformanceStats>>())
            {
                stats.ValueRW.ShowExtended = !stats.ValueRO.ShowExtended;
                break;
            }
        }

        /// <summary>
        /// Reset min/max frame time tracking.
        /// </summary>
        public static void ResetMinMax(ref SystemState state)
        {
            foreach (var stats in SystemAPI.Query<RefRW<PerformanceStats>>())
            {
                stats.ValueRW.MinFrameTimeMs = float.MaxValue;
                stats.ValueRW.MaxFrameTimeMs = 0f;
                stats.ValueRW.FrameCount = 0;
                stats.ValueRW.AccumulatedTime = 0f;
                break;
            }
        }
    }
}
