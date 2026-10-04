// ============================================================================
// Nightflow - Crash Handling System
// Execution Order: 8 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Config;
using Nightflow.Systems.UI;

namespace Nightflow.Systems
{
    /// <summary>
    /// Evaluates crash conditions, starts the crash flow, and performs the
    /// vehicle reset that hands the car to the autopilot.
    ///
    /// Crash only on: lethal hazard, total damage, or compound failure
    /// (ComponentFailureSystem raises component-failure crashes the same way).
    /// The autopilot never crashes: while it drives, damage is not accumulated
    /// (DamageSystem) and crash conditions are not evaluated here, so the
    /// self-playing loop can run unattended for hours.
    ///
    /// Reset (requested through GameState.VehicleResetPending by the crash flow,
    /// a restart, or the safety net) happens in place: no scene reload, the car
    /// keeps its position and heading, damage/health/drift/crash state are
    /// cleared, the run is closed, and the autopilot takes the wheel.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageSystem))]
    public partial struct CrashSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            RefRW<GameState> gameState = SystemAPI.GetSingletonRW<GameState>();

            // Use ECB for structural changes - wrapped in try-finally for safe disposal
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // A player-driven run that ended this frame, for adaptive difficulty
            bool runCompleted = false;
            float runAverageMultiplier = 1f;
            float runTime = 0f;
            float runDistance = 0f;
            int runHazardsDodged = 0;
            int runHazardsHit = 0;

            try
            {
                foreach (var (crashState, crashable, autopilot, scoreSession, summary, collision, entity) in
                    SystemAPI.Query<RefRW<CrashState>, RefRO<Crashable>, RefRW<Autopilot>,
                                   RefRW<ScoreSession>, RefRW<ScoreSummary>, RefRW<CollisionEvent>>()
                        .WithAll<PlayerVehicleTag>()
                        .WithEntityAccess())
                {
                    // =============================================================
                    // Vehicle Reset (crash flow Reset phase, restart, safety net)
                    // =============================================================

                    if (gameState.ValueRO.VehicleResetPending)
                    {
                        ResetVehicle(ref state, entity,
                            ref crashState.ValueRW, ref autopilot.ValueRW,
                            ref scoreSession.ValueRW, ref collision.ValueRW);

                        gameState.ValueRW.VehicleResetPending = false;
                        gameState.ValueRW.PlayerControlActive = false;

                        if (SystemAPI.HasComponent<CrashedTag>(entity))
                        {
                            ecb.RemoveComponent<CrashedTag>(entity);
                        }
                        continue;
                    }

                    if (crashState.ValueRO.IsCrashed)
                    {
                        // Wreck coasts through the crash sequence; timer feeds camera/effects
                        crashState.ValueRW.CrashTime += deltaTime;
                        continue;
                    }

                    // The autopilot is immune: attract mode must never enter the crash flow
                    if (autopilot.ValueRO.Enabled)
                        continue;

                    var damage = SystemAPI.GetComponent<DamageState>(entity);
                    var velocity = SystemAPI.GetComponent<Velocity>(entity);
                    var driftState = SystemAPI.GetComponent<DriftState>(entity);

                    CrashReason reason = CrashReason.None;

                    // =============================================================
                    // Condition A: Lethal Hazard + Speed
                    // Severity > 0.8 AND v_impact > v_crash
                    // =============================================================

                    if (collision.ValueRO.Occurred)
                    {
                        Entity hazardEntity = collision.ValueRO.OtherEntity;

                        // Validate hazard entity before accessing components
                        if (hazardEntity != Entity.Null &&
                            state.EntityManager.Exists(hazardEntity) &&
                            SystemAPI.HasComponent<Hazard>(hazardEntity))
                        {
                            var hazard = SystemAPI.GetComponent<Hazard>(hazardEntity);
                            float vImpact = collision.ValueRO.ImpactSpeed;

                            // Lethal hazards (Barrier, CrashedCar) have severity > 0.8
                            if (hazard.Severity > 0.8f && vImpact > crashable.ValueRO.CrashSpeed)
                            {
                                reason = CrashReason.LethalHazard;
                            }
                        }
                    }

                    // FIXME: Condition A uses CrashSpeed threshold from config, but there's no
                    // visual/audio feedback as the player approaches lethal impact speed. Add pre-crash warning.
                    // =============================================================
                    // Condition B: Structural Damage Exceeded
                    // Damage.Total > D_max
                    // Only set if no higher-priority reason already assigned
                    // =============================================================

                    if (reason == CrashReason.None &&
                        damage.Total > crashable.ValueRO.CrashThreshold)
                    {
                        reason = CrashReason.TotalDamage;
                    }

                    // =============================================================
                    // Condition C: Compound Failure
                    // |ψ| > ψ_fail AND v_f ≈ v_min AND Damage.Total > 0.6×D_max
                    // Only set if no higher-priority reason already assigned
                    // =============================================================

                    float yawThreshold = crashable.ValueRO.YawFailThreshold;
                    float damageThreshold = crashable.ValueRO.CrashThreshold * 0.6f;

                    bool yawFail = math.abs(driftState.YawOffset) > yawThreshold;
                    bool speedFail = velocity.Forward <= GameConstants.MinForwardSpeed + 1f;
                    bool damageFail = damage.Total > damageThreshold;

                    if (reason == CrashReason.None && yawFail && speedFail && damageFail)
                    {
                        reason = CrashReason.CompoundFailure;
                    }

                    // Condition D: ComponentFailureSystem flagged a critical/cascade failure
                    if (reason == CrashReason.None && crashState.ValueRO.Reason == CrashReason.ComponentFailure)
                    {
                        reason = CrashReason.ComponentFailure;
                    }

                    // =============================================================
                    // Trigger Crash
                    // =============================================================

                    if (reason != CrashReason.None)
                    {
                        bool runWasActive = scoreSession.ValueRO.Active;

                        TriggerCrash(ref crashState.ValueRW, ref scoreSession.ValueRW,
                            ref summary.ValueRW, ref gameState.ValueRW, reason);

                        if (AdaptiveDifficultyLogic.ShouldCountRun(runWasActive, summary.ValueRO.TimeSurvived))
                        {
                            runCompleted = true;
                            runAverageMultiplier = AdaptiveDifficultyLogic.RunAverageMultiplier(
                                scoreSession.ValueRO.Score, scoreSession.ValueRO.Distance);
                            runTime = summary.ValueRO.TimeSurvived;
                            runDistance = scoreSession.ValueRO.Distance;
                            runHazardsDodged = summary.ValueRO.HazardsDodged;
                            runHazardsHit = summary.ValueRO.HazardsHit;
                        }

                        // Add crashed tag
                        ecb.AddComponent<CrashedTag>(entity);
                    }
                }

                // Feed the finished run to adaptive difficulty
                if (runCompleted)
                {
                    foreach (var profile in SystemAPI.Query<RefRW<DifficultyProfile>>())
                    {
                        AdaptiveDifficultyLogic.CompleteRun(ref profile.ValueRW, runAverageMultiplier,
                            runTime, runDistance, runHazardsDodged, runHazardsHit);
                        break;
                    }
                }

                ecb.Playback(state.EntityManager);
            }
            finally
            {
                ecb.Dispose();
            }
        }

        /// <summary>
        /// Marks the vehicle crashed, closes the scoring run, finalizes the
        /// summary, and kicks off the crash flow on the GameState singleton.
        /// </summary>
        private void TriggerCrash(ref CrashState crashState, ref ScoreSession scoreSession,
            ref ScoreSummary summary, ref GameState gameState, CrashReason reason)
        {
            crashState.IsCrashed = true;
            crashState.CrashTime = 0f;
            crashState.Reason = reason;

            // End scoring
            scoreSession.Active = false;

            // Finalize score summary
            summary.FinalScore = scoreSession.Score;
            summary.TotalDistance = scoreSession.Distance;
            summary.EndReason = reason;

            ScreenFlowSystem.TriggerCrash(ref gameState, reason, queueAutopilot: true);
        }

        /// <summary>
        /// In-place vehicle reset: clears damage, component health, soft-body
        /// deformation, drift, impulse and crash state; keeps position and
        /// heading; closes the run and hands the wheel to the autopilot.
        /// </summary>
        private void ResetVehicle(ref SystemState state, Entity entity,
            ref CrashState crashState, ref Autopilot autopilot,
            ref ScoreSession scoreSession, ref CollisionEvent collision)
        {
            // Crash state
            crashState.IsCrashed = false;
            crashState.CrashTime = 0f;
            crashState.Reason = CrashReason.None;

            // Damage zones
            SystemAPI.SetComponent(entity, new DamageState());

            // Phase 2 damage (optional components added by ComponentHealthInitSystem)
            if (SystemAPI.HasComponent<ComponentHealth>(entity))
            {
                SystemAPI.SetComponent(entity, ComponentHealth.FullHealth);
            }
            if (SystemAPI.HasComponent<ComponentFailureState>(entity))
            {
                SystemAPI.SetComponent(entity, new ComponentFailureState
                {
                    FailedComponents = ComponentFailures.None,
                    TimeSinceLastFailure = 0f
                });
            }
            if (SystemAPI.HasComponent<SoftBodyState>(entity))
            {
                var softBody = SystemAPI.GetComponent<SoftBodyState>(entity);
                softBody.CurrentDeformation = float4.zero;
                softBody.TargetDeformation = float4.zero;
                softBody.DeformationVelocity = float4.zero;
                SystemAPI.SetComponent(entity, softBody);
            }

            // Motion: straighten out, keep rolling at recovery speed
            SystemAPI.SetComponent(entity, new DriftState());
            var velocity = SystemAPI.GetComponent<Velocity>(entity);
            velocity.Forward = math.max(GameConstants.AutopilotRecoverySpeed, GameConstants.MinForwardSpeed);
            velocity.Lateral = 0f;
            velocity.Angular = 0f;
            SystemAPI.SetComponent(entity, velocity);

            // Lane following: settle into the current lane, restore magnetism
            var laneFollower = SystemAPI.GetComponent<LaneFollower>(entity);
            int lane = math.clamp(laneFollower.CurrentLane, 0, GameConstants.DefaultNumLanes - 1);
            laneFollower.CurrentLane = lane;
            laneFollower.TargetLane = lane;
            laneFollower.MagnetStrength = GameConstants.DefaultMagnetStrength;
            SystemAPI.SetComponent(entity, laneFollower);

            var steering = SystemAPI.GetComponent<SteeringState>(entity);
            steering.CurrentAngle = 0f;
            steering.TargetAngle = 0f;
            steering.ChangingLanes = false;
            steering.LaneChangeTimer = 0f;
            steering.LaneChangeRequested = false;
            steering.LaneChangeDirection = 0;
            SystemAPI.SetComponent(entity, steering);

            // Collision bookkeeping
            collision = new CollisionEvent { OtherEntity = Entity.Null };
            SystemAPI.SetComponent(entity, new ImpulseData());

            // The run (if any) is over; a new one starts when the player takes the wheel
            scoreSession.Active = false;

            // Autopilot takes over
            autopilot.Enabled = true;
            autopilot.Reason = AutopilotReason.Crash;
            autopilot.TargetSpeed = GameConstants.AutopilotRecoverySpeed;
            autopilot.HumanInputDetected = false;
        }
    }
}
