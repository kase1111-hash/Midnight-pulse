// ============================================================================
// Nightflow - Crash Handling System
// Execution Order: 8 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Config;

namespace Nightflow.Systems
{
    /// <summary>
    /// Evaluates crash conditions and triggers crash -> autopilot flow.
    /// Crash only on: lethal hazard, total damage, or compound failure.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageSystem))]
    public partial struct CrashSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Use ECB for structural changes - wrapped in try-finally for safe disposal
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            try
            {
                foreach (var (crashState, damage, crashable, velocity, driftState,
                             autopilot, scoreSession, summary, collision, entity) in
                    SystemAPI.Query<RefRW<CrashState>, RefRO<DamageState>, RefRO<Crashable>,
                                   RefRO<Velocity>, RefRO<DriftState>, RefRW<Autopilot>,
                                   RefRW<ScoreSession>, RefRW<ScoreSummary>, RefRO<CollisionEvent>>()
                        .WithAll<PlayerVehicleTag>()
                        .WithEntityAccess())
                {
                    if (crashState.ValueRO.IsCrashed)
                    {
                        // Already crashed, update timer
                        crashState.ValueRW.CrashTime += deltaTime;

                        // After fade time, enable autopilot
                        if (crashState.ValueRO.CrashTime > GameConstants.CrashFadeToAutopilotTime && !autopilot.ValueRO.Enabled)
                        {
                            autopilot.ValueRW.Enabled = true;
                            autopilot.ValueRW.TargetSpeed = GameConstants.AutopilotRecoverySpeed;

                            // Reset crash state for next run
                            crashState.ValueRW.IsCrashed = false;
                            crashState.ValueRW.CrashTime = 0f;

                            // Add autopilot tag
                            ecb.AddComponent<AutopilotActiveTag>(entity);
                        }
                        continue;
                    }

                    CrashReason reason = CrashReason.None;

                    // =============================================================
                    // Condition A: Lethal Hazard + Speed
                    // Severity > 0.8 AND v_impact > v_crash
                    // =============================================================

                    if (collision.ValueRO.Occurred)
                    {
                        Entity hazardEntity = collision.ValueRO.OtherEntity;

                        // Validate hazard entity before accessing components
                        if (hazardEntity == Entity.Null)
                        {
                            // Collision occurred but entity reference is invalid - skip hazard check
                            // This can happen if hazard was destroyed between collision detection and crash evaluation
                        }
                        else if (!SystemAPI.HasComponent<Hazard>(hazardEntity))
                        {
                            // Entity exists but missing Hazard component - data integrity issue
                            // Log for debugging but continue processing other crash conditions
                            UnityEngine.Debug.LogWarning($"[CrashSystem] Collision entity {hazardEntity.Index} missing Hazard component");
                        }
                        else
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

                    // =============================================================
                    // Condition B: Structural Damage Exceeded
                    // Damage.Total > D_max
                    // =============================================================

                    if (damage.ValueRO.Total > crashable.ValueRO.CrashThreshold)
                    {
                        reason = CrashReason.TotalDamage;
                    }

                    // =============================================================
                    // Condition C: Compound Failure
                    // |ψ| > ψ_fail AND v_f ≈ v_min AND Damage.Total > 0.6×D_max
                    // =============================================================

                    float yawThreshold = crashable.ValueRO.YawFailThreshold;
                    float damageThreshold = crashable.ValueRO.CrashThreshold * 0.6f;

                    bool yawFail = math.abs(driftState.ValueRO.YawOffset) > yawThreshold;
                    bool speedFail = velocity.ValueRO.Forward <= GameConstants.MinForwardSpeed + 1f;
                    bool damageFail = damage.ValueRO.Total > damageThreshold;

                    if (yawFail && speedFail && damageFail)
                    {
                        reason = CrashReason.CompoundFailure;
                    }

                    // =============================================================
                    // Trigger Crash
                    // =============================================================

                    if (reason != CrashReason.None)
                    {
                        crashState.ValueRW.IsCrashed = true;
                        crashState.ValueRW.CrashTime = 0f;
                        crashState.ValueRW.Reason = reason;

                        // End scoring
                        scoreSession.ValueRW.Active = false;

                        // Finalize score summary
                        summary.ValueRW.FinalScore = scoreSession.ValueRO.Score;
                        summary.ValueRW.EndReason = reason;

                        // Add crashed tag
                        ecb.AddComponent<CrashedTag>(entity);
                    }
                }

                ecb.Playback(state.EntityManager);
            }
            finally
            {
                ecb.Dispose();
            }
        }
    }
}
