// ============================================================================
// Nightflow - Risk Event System
// Detects near-misses and risky maneuvers for scoring bonuses
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Detects risk events that spike the risk multiplier:
    /// - Close passes (traffic within threshold) - tiered by proximity
    /// - Hazard dodges (near-miss with hazard) - bonus scales with closeness
    /// - Emergency clears (emergency vehicle passes)
    /// - Drift recoveries (exit drift state successfully)
    /// - Full spins (complete 360° rotation)
    /// - Lane weaving (rapid lane changes at speed)
    /// - Threading the needle (passing between two obstacles)
    /// - Combo chains (sequential risk events)
    ///
    /// Risk events temporarily boost the risk multiplier for higher scores.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VehicleMovementSystem))]
    [UpdateBefore(typeof(ScoringSystem))]
    public partial struct RiskEventSystem : ISystem
    {
        // =============================================================
        // Close Pass Detection - Tiered System
        // =============================================================
        private const float ClosePassZoneStart = 10f;     // Start tracking when this close (forward)
        private const float ClosePassZoneEnd = -5f;       // Stop tracking when this far behind
        private const float ClosePassLateralMax = 6f;     // Maximum lateral distance to track

        // Tiered bonuses based on minimum clearance during pass
        private const float ClosePassTier1Distance = 3.5f;  // Barely close
        private const float ClosePassTier2Distance = 2.5f;  // Very close
        private const float ClosePassTier3Distance = 1.8f;  // Extremely close (paint trading)
        private const float ClosePassTier1Bonus = 0.10f;
        private const float ClosePassTier2Bonus = 0.20f;
        private const float ClosePassTier3Bonus = 0.35f;

        // =============================================================
        // Hazard Dodge Detection - Proximity Scaling
        // =============================================================
        private const float HazardApproachZone = 8f;      // Start tracking when approaching
        private const float HazardPassedZone = -3f;       // Hazard is behind us
        private const float HazardDodgeMinSpeed = 25f;    // Minimum speed for dodge bonus
        private const float HazardDodgeBaseBonus = 0.15f;
        private const float HazardDodgeMaxBonus = 0.40f;  // Max bonus for extremely close dodge
        private const float HazardDodgeCloseThreshold = 1.5f;  // Closest "safe" distance

        // =============================================================
        // Lane Weaving Detection
        // =============================================================
        private const float LaneWeaveWindow = 3f;         // Time window to track lane changes
        private const int LaneWeaveMinChanges = 3;        // Minimum changes for bonus
        private const float LaneWeaveMinSpeed = 35f;      // Minimum speed for weave bonus
        private const float LaneWeaveBonus = 0.25f;

        // =============================================================
        // Threading the Needle Detection
        // =============================================================
        private const float ThreadingMaxGap = 5f;         // Maximum gap between obstacles
        private const float ThreadingMinSpeed = 30f;
        private const float ThreadingBonus = 0.45f;

        // =============================================================
        // Combo Chain System
        // =============================================================
        private const float ComboWindowTime = 2.5f;       // Seconds to chain events
        private const float ComboMultiplierPerEvent = 0.15f; // Extra bonus per chained event
        private const int ComboMaxStack = 5;              // Maximum combo multiplier

        // =============================================================
        // Drift Recovery Detection
        // =============================================================
        private const float DriftRecoveryYaw = 0.5f;      // radians - significant drift
        private const float DriftRecoveryRiskBonus = 0.25f;

        // Full spin detection
        private const float FullSpinThreshold = 2f * math.PI; // 360 degrees
        private const float FullSpinRiskBonus = 0.5f;

        // Emergency clear
        private const float EmergencyClearDistance = 15f;
        private const float EmergencyClearRiskBonus = 0.3f;

        // Perfect segment
        private const float PerfectSegmentRiskBonus = 0.4f;
        private const float PerfectSegmentScoreBonus = 1000f;

        // =============================================================
        // State Tracking
        // =============================================================

        // Drift/spin tracking
        private float _accumulatedYaw;
        private bool _wasInDrift;
        private float _lastDriftYaw;

        // Emergency tracking
        private bool _emergencyWasClose;

        // Perfect segment tracking
        private int _lastSegmentIndex;
        private float _segmentDamageAccum;

        // Lane weaving tracking (circular buffer for last 8 lane changes)
        private const int LaneChangeHistorySize = 8;
        private FixedList64Bytes<float> _laneChangeTimes;
        private int _lastLane;

        // Combo chain tracking
        private float _comboTimer;
        private int _comboCount;

        // Close pass tracking (track up to 8 vehicles being passed)
        private const int MaxTrackedPasses = 8;
        private FixedList128Bytes<int> _trackedPassEntityIds;
        private FixedList128Bytes<float> _trackedPassMinDistance;

        // Hazard tracking (track up to 8 hazards being approached)
        private FixedList128Bytes<int> _trackedHazardIds;
        private FixedList128Bytes<float> _trackedHazardMinDistance;

        // Per-frame event counters
        private int _closePassesThisFrame;
        private int _hazardDodgesThisFrame;
        private int _driftRecoveriesThisFrame;
        private int _perfectSegmentsThisFrame;
        private int _laneWeavesThisFrame;
        private int _threadingsThisFrame;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _accumulatedYaw = 0f;
            _wasInDrift = false;
            _lastDriftYaw = 0f;
            _emergencyWasClose = false;
            _lastSegmentIndex = -1;
            _segmentDamageAccum = 0f;

            _laneChangeTimes = new FixedList64Bytes<float>();
            _lastLane = -1;

            _comboTimer = 0f;
            _comboCount = 0;

            _trackedPassEntityIds = new FixedList128Bytes<int>();
            _trackedPassMinDistance = new FixedList128Bytes<float>();
            _trackedHazardIds = new FixedList128Bytes<int>();
            _trackedHazardMinDistance = new FixedList128Bytes<float>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float time = (float)SystemAPI.Time.ElapsedTime;

            // Reset per-frame counters
            _closePassesThisFrame = 0;
            _hazardDodgesThisFrame = 0;
            _driftRecoveriesThisFrame = 0;
            _perfectSegmentsThisFrame = 0;
            _laneWeavesThisFrame = 0;
            _threadingsThisFrame = 0;

            // Update combo timer
            if (_comboTimer > 0)
            {
                _comboTimer -= deltaTime;
                if (_comboTimer <= 0)
                {
                    _comboCount = 0;
                }
            }

            // Get player state
            float3 playerPos = float3.zero;
            float playerSpeed = 0f;
            int playerLane = 0;
            bool playerDrifting = false;
            float playerYawOffset = 0f;
            float playerYawRate = 0f;

            Entity playerEntity = Entity.Null;
            foreach (var (transform, velocity, laneFollower, driftState, entity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>,
                               RefRO<LaneFollower>, RefRO<DriftState>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithEntityAccess())
            {
                playerPos = transform.ValueRO.Position;
                playerSpeed = velocity.ValueRO.Forward;
                playerLane = laneFollower.ValueRO.CurrentLane;
                playerDrifting = driftState.ValueRO.IsDrifting;
                playerYawOffset = driftState.ValueRO.YawOffset;
                playerYawRate = driftState.ValueRO.YawRate;
                playerEntity = entity;
                break;
            }

            if (playerEntity == Entity.Null)
                return;

            // Get risk state
            RiskState riskState = default;
            foreach (var risk in SystemAPI.Query<RefRO<RiskState>>().WithAll<PlayerVehicleTag>())
            {
                riskState = risk.ValueRO;
                break;
            }

            float riskBonus = 0f;

            // =============================================================
            // Lane Weaving Detection
            // =============================================================

            if (_lastLane >= 0 && playerLane != _lastLane)
            {
                // Lane change detected - record timestamp
                if (_laneChangeTimes.Length >= LaneChangeHistorySize)
                {
                    _laneChangeTimes.RemoveAt(0);
                }
                _laneChangeTimes.Add(time);

                // Count changes within window
                int recentChanges = 0;
                for (int i = 0; i < _laneChangeTimes.Length; i++)
                {
                    if (time - _laneChangeTimes[i] < LaneWeaveWindow)
                    {
                        recentChanges++;
                    }
                }

                // Award bonus for rapid lane weaving at speed
                if (recentChanges >= LaneWeaveMinChanges && playerSpeed >= LaneWeaveMinSpeed)
                {
                    float weaveBonus = LaneWeaveBonus * (1f + (recentChanges - LaneWeaveMinChanges) * 0.1f);
                    riskBonus += weaveBonus;
                    _laneWeavesThisFrame++;
                    RegisterRiskEvent(ref riskBonus);
                }
            }
            _lastLane = playerLane;

            // =============================================================
            // Enhanced Close Pass Detection - Track minimum clearance
            // =============================================================

            // Build list of currently tracked vehicles that are still in range
            var stillTrackedIds = new FixedList128Bytes<int>();
            var stillTrackedDist = new FixedList128Bytes<float>();

            foreach (var (transform, laneFollower, entity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<LaneFollower>>()
                    .WithAll<TrafficVehicleTag>()
                    .WithEntityAccess())
            {
                float3 trafficPos = transform.ValueRO.Position;
                float3 toTraffic = trafficPos - playerPos;
                float forwardDist = toTraffic.z;  // Positive = ahead, negative = behind
                float lateralDist = math.abs(toTraffic.x);
                int entityId = entity.Index;

                // Check if this vehicle is in our tracking zone
                bool inZone = forwardDist < ClosePassZoneStart &&
                              forwardDist > ClosePassZoneEnd &&
                              lateralDist < ClosePassLateralMax &&
                              laneFollower.ValueRO.CurrentLane != playerLane;

                // Find if we're already tracking this vehicle
                int trackedIndex = -1;
                for (int i = 0; i < _trackedPassEntityIds.Length; i++)
                {
                    if (_trackedPassEntityIds[i] == entityId)
                    {
                        trackedIndex = i;
                        break;
                    }
                }

                if (inZone)
                {
                    float currentMinDist = lateralDist;

                    if (trackedIndex >= 0)
                    {
                        // Update minimum distance
                        currentMinDist = math.min(_trackedPassMinDistance[trackedIndex], lateralDist);
                    }

                    // Add to still-tracked list
                    if (stillTrackedIds.Length < MaxTrackedPasses)
                    {
                        stillTrackedIds.Add(entityId);
                        stillTrackedDist.Add(currentMinDist);
                    }
                }
                else if (trackedIndex >= 0 && forwardDist <= ClosePassZoneEnd)
                {
                    // Vehicle just exited zone behind us - award bonus based on minimum clearance
                    float minClearance = _trackedPassMinDistance[trackedIndex];
                    float passBonus = CalculateClosePassBonus(minClearance);

                    if (passBonus > 0)
                    {
                        riskBonus += passBonus;
                        _closePassesThisFrame++;
                        RegisterRiskEvent(ref riskBonus);
                    }
                }
            }

            // Update tracking lists
            _trackedPassEntityIds = stillTrackedIds;
            _trackedPassMinDistance = stillTrackedDist;

            // =============================================================
            // Enhanced Hazard Dodge Detection - Track minimum clearance
            // =============================================================

            var stillTrackedHazardIds = new FixedList128Bytes<int>();
            var stillTrackedHazardDist = new FixedList128Bytes<float>();

            // Also collect nearby hazard positions for threading detection
            var nearbyHazards = new FixedList512Bytes<float3>();

            foreach (var (transform, hazard, entity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Hazard>>()
                    .WithAll<HazardTag>()
                    .WithEntityAccess())
            {
                float3 hazardPos = transform.ValueRO.Position;
                float forwardDist = hazardPos.z - playerPos.z;
                float lateralDist = math.abs(hazardPos.x - playerPos.x);
                float totalDist = math.distance(playerPos, hazardPos);
                int entityId = entity.Index;

                // Collect for threading detection
                if (forwardDist > -2f && forwardDist < 5f && totalDist < ThreadingMaxGap * 2f)
                {
                    if (nearbyHazards.Length < 16)
                    {
                        nearbyHazards.Add(hazardPos);
                    }
                }

                // Check if in approach zone
                bool inZone = forwardDist < HazardApproachZone &&
                              forwardDist > HazardPassedZone &&
                              totalDist < HazardApproachZone;

                // Find if tracking
                int trackedIndex = -1;
                for (int i = 0; i < _trackedHazardIds.Length; i++)
                {
                    if (_trackedHazardIds[i] == entityId)
                    {
                        trackedIndex = i;
                        break;
                    }
                }

                if (inZone)
                {
                    float currentMinDist = totalDist;
                    if (trackedIndex >= 0)
                    {
                        currentMinDist = math.min(_trackedHazardMinDistance[trackedIndex], totalDist);
                    }

                    if (stillTrackedHazardIds.Length < MaxTrackedPasses)
                    {
                        stillTrackedHazardIds.Add(entityId);
                        stillTrackedHazardDist.Add(currentMinDist);
                    }
                }
                else if (trackedIndex >= 0 && forwardDist <= HazardPassedZone && playerSpeed >= HazardDodgeMinSpeed)
                {
                    // Hazard passed - calculate dodge bonus
                    float minClearance = _trackedHazardMinDistance[trackedIndex];
                    float dodgeBonus = CalculateHazardDodgeBonus(minClearance, hazard.ValueRO.Severity);

                    if (dodgeBonus > 0)
                    {
                        riskBonus += dodgeBonus;
                        _hazardDodgesThisFrame++;
                        RegisterRiskEvent(ref riskBonus);
                    }
                }
            }

            _trackedHazardIds = stillTrackedHazardIds;
            _trackedHazardMinDistance = stillTrackedHazardDist;

            // =============================================================
            // Threading the Needle Detection
            // =============================================================

            if (playerSpeed >= ThreadingMinSpeed && nearbyHazards.Length >= 2)
            {
                // Check for pairs of hazards we're passing between
                for (int i = 0; i < nearbyHazards.Length - 1; i++)
                {
                    for (int j = i + 1; j < nearbyHazards.Length; j++)
                    {
                        float3 h1 = nearbyHazards[i];
                        float3 h2 = nearbyHazards[j];

                        // Check if they're on opposite sides of player
                        float h1Lateral = h1.x - playerPos.x;
                        float h2Lateral = h2.x - playerPos.x;

                        if (h1Lateral * h2Lateral < 0) // Opposite sides
                        {
                            float gap = math.abs(h1Lateral) + math.abs(h2Lateral);
                            float forwardAvg = ((h1.z + h2.z) * 0.5f) - playerPos.z;

                            // Threading if gap is small and we're in the middle of passing
                            if (gap < ThreadingMaxGap && forwardAvg > -2f && forwardAvg < 2f)
                            {
                                // Bonus scales with how tight the gap is
                                float tightness = 1f - (gap / ThreadingMaxGap);
                                riskBonus += ThreadingBonus * (1f + tightness * 0.5f);
                                _threadingsThisFrame++;
                                RegisterRiskEvent(ref riskBonus);
                            }
                        }
                    }
                }
            }

            // =============================================================
            // Drift Recovery Detection
            // =============================================================

            if (_wasInDrift && !playerDrifting)
            {
                // Just exited drift state
                float driftAmount = math.abs(_lastDriftYaw);
                if (driftAmount > DriftRecoveryYaw)
                {
                    // Successful drift recovery!
                    float recoveryBonus = DriftRecoveryRiskBonus * math.saturate(driftAmount / math.PI);
                    riskBonus += recoveryBonus;
                    _driftRecoveriesThisFrame++;
                    RegisterRiskEvent(ref riskBonus);
                }
            }

            _wasInDrift = playerDrifting;
            if (playerDrifting)
            {
                _lastDriftYaw = playerYawOffset;
            }

            // =============================================================
            // Full Spin Detection
            // =============================================================

            // Track accumulated yaw during drift
            if (playerDrifting)
            {
                _accumulatedYaw += math.abs(playerYawRate * deltaTime);

                // Check for full spin
                if (_accumulatedYaw >= FullSpinThreshold)
                {
                    riskBonus += FullSpinRiskBonus;
                    _accumulatedYaw -= FullSpinThreshold; // Allow multiple spins
                    RegisterRiskEvent(ref riskBonus);
                }
            }
            else
            {
                // Decay accumulated yaw when not drifting
                _accumulatedYaw *= 0.9f;
            }

            // =============================================================
            // Emergency Clear Detection
            // =============================================================

            bool emergencyClose = false;
            foreach (var (transform, emergencyAI) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<EmergencyAI>>()
                    .WithAll<EmergencyVehicleTag>())
            {
                if (!emergencyAI.ValueRO.SirenActive)
                    continue;

                float dist = math.distance(playerPos, transform.ValueRO.Position);
                if (dist < EmergencyClearDistance)
                {
                    emergencyClose = true;
                }
                else if (_emergencyWasClose && dist > EmergencyClearDistance * 2f)
                {
                    // Emergency passed us successfully
                    riskBonus += EmergencyClearRiskBonus;
                    RegisterRiskEvent(ref riskBonus);
                }
            }
            _emergencyWasClose = emergencyClose;

            // =============================================================
            // Perfect Segment Detection
            // Complete a track segment without taking damage
            // =============================================================

            // Find current segment
            int currentSegmentIndex = -1;
            foreach (var segment in
                SystemAPI.Query<RefRO<TrackSegment>>()
                    .WithAll<TrackSegmentTag>())
            {
                if (playerPos.z >= segment.ValueRO.StartZ && playerPos.z <= segment.ValueRO.EndZ)
                {
                    currentSegmentIndex = segment.ValueRO.Index;
                    break;
                }
            }

            // Track damage accumulation for current segment
            float currentDamage = 0f;
            foreach (var damage in SystemAPI.Query<RefRO<DamageState>>().WithAll<PlayerVehicleTag>())
            {
                currentDamage = damage.ValueRO.Total;
                break;
            }

            if (currentSegmentIndex != _lastSegmentIndex && _lastSegmentIndex >= 0)
            {
                // Crossed into a new segment
                // Check if we completed the previous segment without damage
                if (_segmentDamageAccum < 0.01f && playerSpeed >= 25f)
                {
                    // Perfect segment! No damage taken and maintaining speed
                    riskBonus += PerfectSegmentRiskBonus;
                    _perfectSegmentsThisFrame++;
                    RegisterRiskEvent(ref riskBonus);

                    // One-time score bonus added directly
                    foreach (var session in SystemAPI.Query<RefRW<ScoreSession>>().WithAll<PlayerVehicleTag>())
                    {
                        session.ValueRW.Score += PerfectSegmentScoreBonus;
                    }
                }

                // Reset damage accumulator for new segment
                _segmentDamageAccum = 0f;
            }
            else if (currentSegmentIndex == _lastSegmentIndex)
            {
                // Same segment - accumulate damage delta
                // This is a simplified approach; ideally track damage per segment
                _segmentDamageAccum = currentDamage;
            }

            _lastSegmentIndex = currentSegmentIndex;

            // =============================================================
            // Apply Risk Bonus with Combo Multiplier
            // =============================================================

            if (riskBonus > 0)
            {
                // Apply combo multiplier to the total bonus
                float comboMultiplier = 1f + math.min(_comboCount, ComboMaxStack) * ComboMultiplierPerEvent;
                float finalBonus = riskBonus * comboMultiplier;

                foreach (var (risk, summary) in
                    SystemAPI.Query<RefRW<RiskState>, RefRW<ScoreSummary>>()
                        .WithAll<PlayerVehicleTag>())
                {
                    // Add risk bonus scaled by rebuild rate, capped by damage-reduced cap
                    // Damage reduces rebuild rate, making it harder to accumulate risk bonus
                    float scaledBonus = finalBonus * risk.ValueRO.RebuildRate;
                    risk.ValueRW.Value = math.min(
                        risk.ValueRO.Value + scaledBonus,
                        risk.ValueRO.Cap
                    );
                }
            }

            // Track event statistics for end-of-run summary
            foreach (var summary in SystemAPI.Query<RefRW<ScoreSummary>>().WithAll<PlayerVehicleTag>())
            {
                summary.ValueRW.ClosePasses += _closePassesThisFrame;
                summary.ValueRW.HazardsDodged += _hazardDodgesThisFrame;
                summary.ValueRW.DriftRecoveries += _driftRecoveriesThisFrame;
                summary.ValueRW.PerfectSegments += _perfectSegmentsThisFrame;
                summary.ValueRW.LaneWeaves += _laneWeavesThisFrame;
                summary.ValueRW.Threadings += _threadingsThisFrame;

                // Track highest combo reached
                if (_comboCount > summary.ValueRO.HighestCombo)
                {
                    summary.ValueRW.HighestCombo = _comboCount;
                }
            }
        }

        // =============================================================
        // Helper Methods
        // =============================================================

        /// <summary>
        /// Register a risk event for combo tracking.
        /// Extends combo timer and increments combo count.
        /// </summary>
        private void RegisterRiskEvent(ref float riskBonus)
        {
            _comboTimer = ComboWindowTime;
            _comboCount++;
        }

        /// <summary>
        /// Calculate close pass bonus based on minimum clearance during pass.
        /// Tiered system rewards closer passes more.
        /// </summary>
        private static float CalculateClosePassBonus(float minClearance)
        {
            if (minClearance <= ClosePassTier3Distance)
            {
                // Extremely close - maximum bonus
                return ClosePassTier3Bonus;
            }
            else if (minClearance <= ClosePassTier2Distance)
            {
                // Very close - interpolate between tier 2 and tier 3
                float t = (ClosePassTier2Distance - minClearance) /
                          (ClosePassTier2Distance - ClosePassTier3Distance);
                return math.lerp(ClosePassTier2Bonus, ClosePassTier3Bonus, t);
            }
            else if (minClearance <= ClosePassTier1Distance)
            {
                // Barely close - interpolate between tier 1 and tier 2
                float t = (ClosePassTier1Distance - minClearance) /
                          (ClosePassTier1Distance - ClosePassTier2Distance);
                return math.lerp(ClosePassTier1Bonus, ClosePassTier2Bonus, t);
            }

            // Not close enough for bonus
            return 0f;
        }

        /// <summary>
        /// Calculate hazard dodge bonus based on minimum clearance and hazard severity.
        /// Closer dodges and more severe hazards give bigger bonuses.
        /// </summary>
        private static float CalculateHazardDodgeBonus(float minClearance, float severity)
        {
            // Must be within close threshold to get any bonus
            if (minClearance > HazardApproachZone * 0.5f)
                return 0f;

            // Scale bonus inversely with clearance (closer = higher)
            // minClearance of 1.5m = max bonus, minClearance of 4m = base bonus
            float closenessRatio = 1f - math.saturate((minClearance - HazardDodgeCloseThreshold) /
                                                      (HazardApproachZone * 0.5f - HazardDodgeCloseThreshold));
            float proximityBonus = math.lerp(HazardDodgeBaseBonus, HazardDodgeMaxBonus, closenessRatio);

            // Scale by severity (more dangerous hazard = bigger reward)
            return proximityBonus * (0.5f + severity * 0.5f);
        }
    }
}
