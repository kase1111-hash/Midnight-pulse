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
    /// Calculates display values for speed, score, damage, and warnings.
    /// Note: Actual UI rendering handled by MonoBehaviour bridge.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AudioSystem))]
    public partial struct UISystem : ISystem
    {
        // Display parameters
        private const float SpeedDisplaySmoothing = 10f;
        private const float ScoreDisplaySmoothing = 5f;
        private const float DamageFlashThreshold = 0.1f;
        private const float WarningFlashRate = 4f;        // Hz

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float time = (float)SystemAPI.Time.ElapsedTime;

            // =============================================================
            // Player HUD Data
            // =============================================================

            foreach (var (velocity, scoreSession, riskState, damage, speedTier, detection) in
                SystemAPI.Query<RefRO<Velocity>, RefRO<ScoreSession>, RefRO<RiskState>,
                               RefRO<DamageState>, RefRO<SpeedTier>, RefRO<EmergencyDetection>>()
                    .WithAll<PlayerVehicleTag>())
            {
                // =============================================================
                // Speedometer
                // =============================================================

                // Convert m/s to display units (km/h or mph)
                float speedKmh = velocity.ValueRO.Forward * 3.6f;

                // Calculate digital speedometer segments
                int speedDigits = (int)math.clamp(speedKmh, 0, 999);

                // Speed tier indicator (color/icon)
                int tier = speedTier.ValueRO.Tier;
                // 0 = Cruise (white), 1 = Fast (yellow), 2 = Boosted (red/orange)

                // =============================================================
                // Score Display
                // =============================================================

                float displayScore = scoreSession.ValueRO.Score;
                float displayMultiplier = scoreSession.ValueRO.Multiplier * (1f + scoreSession.ValueRO.RiskMultiplier);

                // Format score with commas for display
                // (actual formatting in UI layer)

                // Multiplier flash when increasing
                bool multiplierIncreasing = displayMultiplier > 1.5f;

                // =============================================================
                // Risk Meter
                // =============================================================

                float riskValue = riskState.ValueRO.Value;
                float riskCap = riskState.ValueRO.Cap;
                float riskPercent = riskCap > 0 ? riskValue / riskCap : 0f;

                // Visual indicator of cap reduction from damage
                float capReduction = 1f - riskCap; // How much cap has been reduced

                // =============================================================
                // Damage Indicator
                // =============================================================

                float totalDamage = damage.ValueRO.Total;

                // Per-zone damage for vehicle diagram
                float frontDamage = damage.ValueRO.Front;
                float rearDamage = damage.ValueRO.Rear;
                float leftDamage = damage.ValueRO.Left;
                float rightDamage = damage.ValueRO.Right;

                // Flash damage zones that just took damage
                // (would need previous frame comparison)

                // Critical damage warning
                bool criticalDamage = totalDamage > 0.8f;

                // =============================================================
                // Warning Indicators
                // =============================================================

                // Emergency vehicle warning
                bool emergencyWarning = detection.ValueRO.WarningActive;
                float emergencyDistance = detection.ValueRO.NearestDistance;
                float emergencyETA = detection.ValueRO.TimeToArrival;

                // Calculate warning flash state
                bool warningFlash = math.frac(time * WarningFlashRate) < 0.5f;

                // Combine warnings into priority system
                // 1. Emergency vehicle (highest)
                // 2. Critical damage
                // 3. High risk zone

                int warningPriority = 0;
                if (emergencyWarning) warningPriority = 3;
                else if (criticalDamage) warningPriority = 2;
                else if (riskPercent > 0.8f) warningPriority = 1;

                // =============================================================
                // Distance/Progress
                // =============================================================

                float distanceKm = scoreSession.ValueRO.Distance / 1000f;

                // =============================================================
                // Pack UI Data (would write to singleton)
                // =============================================================

                // TODO: Write to UIData singleton for MonoBehaviour bridge
                // UIData.SpeedKmh = speedKmh;
                // UIData.SpeedTier = tier;
                // UIData.Score = displayScore;
                // UIData.Multiplier = displayMultiplier;
                // UIData.RiskPercent = riskPercent;
                // UIData.DamageTotal = totalDamage;
                // UIData.DamageZones = new float4(frontDamage, rearDamage, leftDamage, rightDamage);
                // UIData.WarningPriority = warningPriority;
                // UIData.WarningFlash = warningFlash;
                // UIData.EmergencyETA = emergencyETA;
                // UIData.DistanceKm = distanceKm;
            }

            // =============================================================
            // Off-Screen Indicators
            // =============================================================

            // Collect active signals for UI rendering
            int signalCount = 0;

            foreach (var signal in SystemAPI.Query<RefRO<OffscreenSignal>>())
            {
                if (!signal.ValueRO.Active)
                    continue;

                // TODO: Pack into signal array for UI
                // UIData.Signals[signalCount].ScreenPos = signal.ValueRO.ScreenPosition;
                // UIData.Signals[signalCount].Color = signal.ValueRO.Color;
                // UIData.Signals[signalCount].Urgency = signal.ValueRO.Urgency;
                // UIData.Signals[signalCount].PulsePhase = signal.ValueRO.PulsePhase;

                signalCount++;
                if (signalCount >= 8) break; // Limit active indicators
            }

            // TODO: UIData.SignalCount = signalCount;

            // =============================================================
            // Crash/Game Over State
            // =============================================================

            foreach (var crashState in SystemAPI.Query<RefRO<CrashState>>().WithAll<PlayerVehicleTag>())
            {
                if (!crashState.ValueRO.IsCrashed)
                    continue;

                // TODO: Trigger crash UI overlay
                // UIData.ShowCrashOverlay = true;
                // UIData.CrashReason = crashState.ValueRO.Reason;
                // UIData.CrashTime = crashState.ValueRO.CrashTime;
            }
        }
    }
}
