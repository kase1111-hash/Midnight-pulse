// ============================================================================
// Nightflow - HUD Update System
// Updates UI state from gameplay entities for HUD display
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems.UI
{
    /// <summary>
    /// Updates UIState singleton from player and game state.
    /// Runs every frame to keep HUD current.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct HUDUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UIState>();
            state.RequireForUpdate<PlayerVehicleTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get UI state singleton
            RefRW<UIState> uiState = SystemAPI.GetSingletonRW<UIState>();

            // Find player vehicle and update UI
            foreach (var (velocity, damage, scoring, transform) in
                SystemAPI.Query<RefRO<Velocity>, RefRO<DamageState>,
                               RefRO<ScoreSession>, RefRO<WorldTransform>>()
                    .WithAll<PlayerVehicleTag>())
            {
                // Speed calculation
                float speedMs = velocity.ValueRO.Forward;
                float speedKmh = speedMs * 3.6f;
                float speedMph = speedMs * 2.237f;

                uiState.ValueRW.SpeedKmh = speedKmh;
                uiState.ValueRW.SpeedMph = speedMph;

                // Speed tier for visual effects
                uiState.ValueRW.SpeedTier = speedKmh switch
                {
                    < 80f => 0,   // Cruise
                    < 150f => 1,  // Fast
                    _ => 2        // Boosted
                };

                // Score with smooth animation
                float targetScore = scoring.ValueRO.Score;
                float displayScore = uiState.ValueRO.DisplayScore;
                float scoreDiff = targetScore - displayScore;

                // Animate score counter (faster when larger difference)
                float scoreSpeed = math.max(100f, math.abs(scoreDiff) * 5f);
                if (math.abs(scoreDiff) > 0.5f)
                {
                    displayScore += math.sign(scoreDiff) * scoreSpeed * deltaTime;
                    displayScore = math.clamp(displayScore, 0, targetScore + 1);
                }
                else
                {
                    displayScore = targetScore;
                }

                uiState.ValueRW.Score = targetScore;
                uiState.ValueRW.DisplayScore = displayScore;

                // Multiplier
                float prevMultiplier = uiState.ValueRO.Multiplier;
                uiState.ValueRW.Multiplier = scoring.ValueRO.Multiplier;
                uiState.ValueRW.HighestMultiplier = math.max(uiState.ValueRO.HighestMultiplier,
                                                              scoring.ValueRO.Multiplier);

                // Flash on multiplier change
                if (scoring.ValueRO.Multiplier > prevMultiplier)
                {
                    uiState.ValueRW.MultiplierFlash = true;
                }

                // Damage state
                uiState.ValueRW.DamageTotal = damage.ValueRO.Total;
                uiState.ValueRW.DamageFront = damage.ValueRO.Front;
                uiState.ValueRW.DamageRear = damage.ValueRO.Rear;
                uiState.ValueRW.DamageLeft = damage.ValueRO.Left;
                uiState.ValueRW.DamageRight = damage.ValueRO.Right;

                // Critical damage warning
                bool wasCritical = uiState.ValueRO.CriticalDamage;
                uiState.ValueRW.CriticalDamage = damage.ValueRO.Total > 0.75f;
                if (!wasCritical && uiState.ValueRO.CriticalDamage)
                {
                    uiState.ValueRW.DamageFlash = true;
                }

                // Distance and time
                uiState.ValueRW.DistanceKm = scoring.ValueRO.Distance / 1000f;
                uiState.ValueRW.TimeSurvived += deltaTime;

                break; // Only one player
            }

            // Update warning priority
            UpdateWarningPriority(ref uiState.ValueRW);

            // Decay flash states
            if (uiState.ValueRO.MultiplierFlash)
            {
                // Flash decays over frames (handled by UI)
            }
        }

        private void UpdateWarningPriority(ref UIState ui)
        {
            // Priority: Emergency (3) > Critical Damage (2) > Risk (1) > None (0)
            if (ui.EmergencyDistance > 0 && ui.EmergencyDistance < 100f)
            {
                ui.WarningPriority = 3;
                ui.WarningFlash = true;
            }
            else if (ui.CriticalDamage)
            {
                ui.WarningPriority = 2;
                ui.WarningFlash = true;
            }
            else if (ui.RiskPercent > 0.8f)
            {
                ui.WarningPriority = 1;
                ui.WarningFlash = false;
            }
            else
            {
                ui.WarningPriority = 0;
                ui.WarningFlash = false;
            }
        }
    }

    /// <summary>
    /// Manages HUD notifications (popups, bonuses, warnings).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(HUDUpdateSystem))]
    public partial struct HUDNotificationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UIControllerTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update notification timers
            foreach (var notifications in
                SystemAPI.Query<DynamicBuffer<HUDNotification>>()
                    .WithAll<UIControllerTag>())
            {
                // Decay notification timers
                for (int i = notifications.Length - 1; i >= 0; i--)
                {
                    var notif = notifications[i];
                    notif.TimeRemaining -= deltaTime;

                    if (notif.TimeRemaining <= 0)
                    {
                        notifications.RemoveAt(i);
                    }
                    else
                    {
                        notifications[i] = notif;
                    }
                }
            }
        }
    }
}
