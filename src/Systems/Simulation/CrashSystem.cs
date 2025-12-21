// ============================================================================
// Nightflow - Crash Handling System
// Execution Order: 8 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

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

            // Use ECB for structural changes
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (crashState, damage, crashable, velocity, driftState,
                         autopilot, scoreSession, entity) in
                SystemAPI.Query<RefRW<CrashState>, RefRO<DamageState>, RefRO<Crashable>,
                               RefRO<Velocity>, RefRO<DriftState>, RefRW<Autopilot>,
                               RefRW<ScoreSession>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithEntityAccess())
            {
                if (crashState.ValueRO.IsCrashed)
                {
                    // Already crashed, update timer
                    crashState.ValueRW.CrashTime += deltaTime;

                    // After fade time, enable autopilot
                    if (crashState.ValueRO.CrashTime > 1.5f && !autopilot.ValueRO.Enabled)
                    {
                        autopilot.ValueRW.Enabled = true;
                        autopilot.ValueRW.TargetSpeed = 20f; // Slow autopilot

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

                // TODO: Check from most recent collision event
                // if (severity > 0.8f && vImpact > crashable.ValueRO.CrashSpeed)
                // {
                //     reason = CrashReason.LethalHazard;
                // }

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
                float minSpeed = 8f;
                float damageThreshold = crashable.ValueRO.CrashThreshold * 0.6f;

                bool yawFail = math.abs(driftState.ValueRO.YawOffset) > yawThreshold;
                bool speedFail = velocity.ValueRO.Forward <= minSpeed + 1f;
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

                    // Add crashed tag
                    ecb.AddComponent<CrashedTag>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
