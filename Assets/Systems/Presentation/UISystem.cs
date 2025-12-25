// ============================================================================
// Nightflow - UI System
// Execution Order: 5 (Presentation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Prepares UI data for HUD display.
    /// Writes to UIState singleton for MonoBehaviour bridge.
    ///
    /// From spec:
    /// - Speed, Multiplier, Score, Damage zone indicators
    /// - Transparent overlay (visible during autopilot)
    /// - Minimal HUD, no full-screen interruptions
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AudioSystem))]
    public partial struct UISystem : ISystem
    {
        // Display parameters
        private const float ScoreDisplaySmoothing = 8f;
        private const float WarningFlashRate = 4f;        // Hz

        // Previous frame values for flash detection
        private float _prevDamage;
        private float _prevMultiplier;

        public void OnCreate(ref SystemState state)
        {
            _prevDamage = 0f;
            _prevMultiplier = 1f;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float time = (float)SystemAPI.Time.ElapsedTime;

            // Get UIState singleton
            foreach (var uiState in SystemAPI.Query<RefRW<UIState>>())
            {
                // =============================================================
                // Player HUD Data
                // =============================================================

                foreach (var (velocity, scoreSession, riskState, damage, speedTier, detection, summary) in
                    SystemAPI.Query<RefRO<Velocity>, RefRO<ScoreSession>, RefRO<RiskState>,
                                   RefRO<DamageState>, RefRO<SpeedTier>, RefRO<EmergencyDetection>,
                                   RefRO<ScoreSummary>>()
                        .WithAll<PlayerVehicleTag>())
                {
                    // =============================================================
                    // Speedometer
                    // =============================================================

                    float speedMs = velocity.ValueRO.Forward;
                    uiState.ValueRW.SpeedKmh = speedMs * 3.6f;
                    uiState.ValueRW.SpeedMph = speedMs * 2.237f;
                    uiState.ValueRW.SpeedTier = speedTier.ValueRO.Tier;

                    // =============================================================
                    // Score Display
                    // =============================================================

                    float currentScore = scoreSession.ValueRO.Score;
                    float currentMultiplier = scoreSession.ValueRO.Multiplier *
                        (1f + scoreSession.ValueRO.RiskMultiplier);

                    // Smooth score display for animation
                    uiState.ValueRW.DisplayScore = math.lerp(
                        uiState.ValueRO.DisplayScore,
                        currentScore,
                        ScoreDisplaySmoothing * deltaTime
                    );

                    uiState.ValueRW.Score = currentScore;
                    uiState.ValueRW.Multiplier = currentMultiplier;
                    uiState.ValueRW.HighestMultiplier = scoreSession.ValueRO.HighestMultiplier;

                    // Flash multiplier when increasing
                    uiState.ValueRW.MultiplierFlash = currentMultiplier > _prevMultiplier + 0.1f;
                    _prevMultiplier = currentMultiplier;

                    // =============================================================
                    // Risk Meter
                    // =============================================================

                    uiState.ValueRW.RiskValue = riskState.ValueRO.Value;
                    uiState.ValueRW.RiskCap = riskState.ValueRO.Cap;
                    uiState.ValueRW.RiskPercent = riskState.ValueRO.Cap > 0
                        ? riskState.ValueRO.Value / riskState.ValueRO.Cap
                        : 0f;

                    // =============================================================
                    // Damage Indicator
                    // =============================================================

                    float totalDamage = damage.ValueRO.Total;
                    uiState.ValueRW.DamageTotal = math.saturate(totalDamage / 100f);
                    uiState.ValueRW.DamageFront = damage.ValueRO.Front;
                    uiState.ValueRW.DamageRear = damage.ValueRO.Rear;
                    uiState.ValueRW.DamageLeft = damage.ValueRO.Left;
                    uiState.ValueRW.DamageRight = damage.ValueRO.Right;

                    // Flash when taking new damage
                    uiState.ValueRW.DamageFlash = totalDamage > _prevDamage + 1f;
                    _prevDamage = totalDamage;

                    uiState.ValueRW.CriticalDamage = totalDamage > 80f;

                    // =============================================================
                    // Warning Indicators
                    // =============================================================

                    bool emergencyWarning = detection.ValueRO.WarningActive;
                    uiState.ValueRW.EmergencyDistance = detection.ValueRO.NearestDistance;
                    uiState.ValueRW.EmergencyETA = detection.ValueRO.TimeToArrival;

                    // Warning flash state
                    uiState.ValueRW.WarningFlash = math.frac(time * WarningFlashRate) < 0.5f;

                    // Priority: Emergency > Critical Damage > High Risk
                    int priority = 0;
                    if (emergencyWarning) priority = 3;
                    else if (uiState.ValueRO.CriticalDamage) priority = 2;
                    else if (uiState.ValueRO.RiskPercent > 0.8f) priority = 1;
                    uiState.ValueRW.WarningPriority = priority;

                    // =============================================================
                    // Progress
                    // =============================================================

                    uiState.ValueRW.DistanceKm = scoreSession.ValueRO.Distance / 1000f;
                    uiState.ValueRW.TimeSurvived = summary.ValueRO.TimeSurvived;

                    break;
                }

                // =============================================================
                // Off-Screen Signals
                // =============================================================

                int signalCount = 0;
                float4 signals0 = float4.zero;
                float4 signals1 = float4.zero;
                float4 signals2 = float4.zero;
                float4 signals3 = float4.zero;

                foreach (var signal in SystemAPI.Query<RefRO<OffscreenSignal>>())
                {
                    if (!signal.ValueRO.Active)
                        continue;

                    // Pack: xy=screenPos, z=urgency, w=type
                    float4 packed = new float4(
                        signal.ValueRO.ScreenPosition.x,
                        signal.ValueRO.ScreenPosition.y,
                        signal.ValueRO.Urgency,
                        (float)signal.ValueRO.Type
                    );

                    switch (signalCount)
                    {
                        case 0: signals0 = packed; break;
                        case 1: signals1 = packed; break;
                        case 2: signals2 = packed; break;
                        case 3: signals3 = packed; break;
                    }

                    signalCount++;
                    if (signalCount >= 4) break;
                }

                uiState.ValueRW.SignalCount = signalCount;
                uiState.ValueRW.Signal0 = signals0;
                uiState.ValueRW.Signal1 = signals1;
                uiState.ValueRW.Signal2 = signals2;
                uiState.ValueRW.Signal3 = signals3;

                // =============================================================
                // Crash/Menu State (from GameState)
                // =============================================================

                foreach (var gameState in SystemAPI.Query<RefRO<GameState>>())
                {
                    uiState.ValueRW.ShowPauseMenu = gameState.ValueRO.IsPaused;
                    uiState.ValueRW.ShowCrashOverlay =
                        gameState.ValueRO.CrashPhase != CrashFlowPhase.None;
                    uiState.ValueRW.ShowScoreSummary =
                        gameState.ValueRO.CrashPhase == CrashFlowPhase.Summary;
                    uiState.ValueRW.OverlayAlpha = gameState.ValueRO.FadeAlpha;

                    // Update ScoreSummaryDisplay singleton when entering summary phase
                    if (gameState.ValueRO.CrashPhase == CrashFlowPhase.Summary)
                    {
                        UpdateScoreSummaryDisplay(ref state);
                    }
                    break;
                }

                break;
            }
        }

        /// <summary>
        /// Copies player's ScoreSummary to the ScoreSummaryDisplay singleton.
        /// </summary>
        private void UpdateScoreSummaryDisplay(ref SystemState state)
        {
            // Get player score summary
            ScoreSummary playerSummary = default;
            float playerMaxSpeed = 0f;
            bool foundPlayer = false;

            foreach (var (summary, velocity, session) in
                SystemAPI.Query<RefRO<ScoreSummary>, RefRO<Velocity>, RefRO<ScoreSession>>()
                    .WithAll<PlayerVehicleTag>())
            {
                playerSummary = summary.ValueRO;
                playerMaxSpeed = summary.ValueRO.HighestSpeed * 3.6f; // Convert to km/h
                foundPlayer = true;
                break;
            }

            if (!foundPlayer) return;

            // Update the display singleton
            foreach (var display in SystemAPI.Query<RefRW<ScoreSummaryDisplay>>())
            {
                display.ValueRW.FinalScore = playerSummary.FinalScore;
                display.ValueRW.TotalDistance = playerSummary.TotalDistance;
                display.ValueRW.MaxSpeed = playerMaxSpeed;
                display.ValueRW.TimeSurvived = playerSummary.TimeSurvived;
                display.ValueRW.ClosePasses = playerSummary.ClosePasses;
                display.ValueRW.HazardsDodged = playerSummary.HazardsDodged;
                display.ValueRW.DriftRecoveries = playerSummary.DriftRecoveries;
                display.ValueRW.EndReason = playerSummary.EndReason;
                // IsNewHighScore and LeaderboardRank are updated by SaveManager
                break;
            }
        }
    }
}
