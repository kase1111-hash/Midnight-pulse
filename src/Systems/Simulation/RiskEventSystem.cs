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
    /// - Close passes (traffic within threshold)
    /// - Hazard dodges (near-miss with hazard)
    /// - Emergency clears (emergency vehicle passes)
    /// - Drift recoveries (exit drift state successfully)
    /// - Full spins (complete 360° rotation)
    ///
    /// Risk events temporarily boost the risk multiplier for higher scores.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VehicleMovementSystem))]
    [UpdateBefore(typeof(ScoringSystem))]
    public partial struct RiskEventSystem : ISystem
    {
        // Close pass detection
        private const float ClosePassDistance = 4f;       // meters lateral
        private const float ClosePassForward = 8f;        // meters forward range
        private const float ClosePassCooldown = 0.5f;     // seconds between passes
        private const float ClosePassRiskBonus = 0.15f;   // risk multiplier spike

        // Hazard dodge detection
        private const float HazardDodgeDistance = 3f;     // meters
        private const float HazardDodgeSpeed = 30f;       // min speed for bonus
        private const float HazardDodgeRiskBonus = 0.2f;

        // Drift recovery detection
        private const float DriftRecoveryYaw = 0.5f;      // radians - significant drift
        private const float DriftRecoveryRiskBonus = 0.25f;

        // Full spin detection
        private const float FullSpinThreshold = 2f * math.PI; // 360 degrees
        private const float FullSpinRiskBonus = 0.5f;

        // Emergency clear
        private const float EmergencyClearDistance = 15f;
        private const float EmergencyClearRiskBonus = 0.3f;

        // State tracking
        private float _accumulatedYaw;
        private float _closePassCooldown;
        private bool _wasInDrift;
        private float _lastDriftYaw;
        private bool _emergencyWasClose;

        // Per-frame event counters
        private int _closePassesThisFrame;
        private int _hazardDodgesThisFrame;
        private int _driftRecoveriesThisFrame;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _accumulatedYaw = 0f;
            _closePassCooldown = 0f;
            _wasInDrift = false;
            _lastDriftYaw = 0f;
            _emergencyWasClose = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Reset per-frame counters
            _closePassesThisFrame = 0;
            _hazardDodgesThisFrame = 0;
            _driftRecoveriesThisFrame = 0;

            // Update cooldowns
            if (_closePassCooldown > 0)
                _closePassCooldown -= deltaTime;

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
            // Close Pass Detection
            // =============================================================

            if (_closePassCooldown <= 0)
            {
                foreach (var (transform, laneFollower) in
                    SystemAPI.Query<RefRO<WorldTransform>, RefRO<LaneFollower>>()
                        .WithAll<TrafficVehicleTag>())
                {
                    float3 trafficPos = transform.ValueRO.Position;
                    float3 toTraffic = trafficPos - playerPos;

                    // Check if in close pass range
                    float forwardDist = math.abs(toTraffic.z);
                    float lateralDist = math.abs(toTraffic.x);

                    if (forwardDist < ClosePassForward && lateralDist < ClosePassDistance)
                    {
                        // Different lanes = close pass!
                        if (laneFollower.ValueRO.CurrentLane != playerLane)
                        {
                            riskBonus += ClosePassRiskBonus;
                            _closePassCooldown = ClosePassCooldown;
                            _closePassesThisFrame++;
                            break; // Only one bonus per cooldown
                        }
                    }
                }
            }

            // =============================================================
            // Hazard Dodge Detection
            // =============================================================

            if (playerSpeed >= HazardDodgeSpeed)
            {
                foreach (var (transform, hazard) in
                    SystemAPI.Query<RefRO<WorldTransform>, RefRO<Hazard>>()
                        .WithAll<HazardTag>())
                {
                    float3 hazardPos = transform.ValueRO.Position;
                    float dist = math.distance(playerPos, hazardPos);

                    // Near miss - close but not colliding
                    if (dist < HazardDodgeDistance && dist > 1.5f)
                    {
                        // Check if we're moving past it (not toward it)
                        float dz = hazardPos.z - playerPos.z;
                        if (dz < 2f && dz > -5f) // Just passed
                        {
                            riskBonus += HazardDodgeRiskBonus * hazard.ValueRO.Severity;
                            _hazardDodgesThisFrame++;
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
                }
            }
            _emergencyWasClose = emergencyClose;

            // =============================================================
            // Apply Risk Bonus and Track Statistics
            // =============================================================

            if (riskBonus > 0)
            {
                foreach (var (risk, summary) in
                    SystemAPI.Query<RefRW<RiskState>, RefRW<ScoreSummary>>()
                        .WithAll<PlayerVehicleTag>())
                {
                    // Add risk bonus, capped by damage-reduced cap
                    risk.ValueRW.Value = math.min(
                        risk.ValueRO.Value + riskBonus,
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
            }
        }
    }
}
