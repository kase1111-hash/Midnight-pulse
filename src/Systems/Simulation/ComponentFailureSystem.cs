// ============================================================================
// Nightflow - Component Failure System (Phase 2 Damage)
// Execution Order: 7.5 (After DamageSystem, Before CrashSystem)
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
    /// Phase 2 Damage: Evaluates component health and triggers failures.
    ///
    /// Component damage mapping:
    /// - Front damage → Steering health
    /// - Rear damage → Transmission health
    /// - Side damage → Suspension health
    /// - Total damage → Engine health
    /// - All impacts → Tire health
    ///
    /// Failure thresholds:
    /// - Health &lt; 0.1 → Component failure
    /// - Steering + Suspension failed → Critical failure → Crash
    /// - 3+ components failed → Cascade failure → Crash
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageSystem))]
    [UpdateBefore(typeof(CrashSystem))]
    public partial struct ComponentFailureSystem : ISystem
    {
        // Health degradation rates
        private const float HealthDegradeBase = 0.15f;   // Base health loss per damage unit
        private const float ImpactDegradeRate = 0.08f;   // Additional tire damage per impact

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (damage, health, failureState, config, collision, crashState) in
                SystemAPI.Query<RefRO<DamageState>, RefRW<ComponentHealth>,
                               RefRW<ComponentFailureState>, RefRO<ComponentDamageConfig>,
                               RefRO<CollisionEvent>, RefRW<CrashState>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                // Update time since last failure
                if (failureState.ValueRO.FailedComponents != ComponentFailures.None)
                {
                    failureState.ValueRW.TimeSinceLastFailure += deltaTime;
                }

                // Skip if no recent collision
                if (!collision.ValueRO.Occurred)
                    continue;

                // =============================================================
                // Calculate Component Health Degradation
                // =============================================================

                var cfg = config.ValueRO;
                var dmg = damage.ValueRO;

                // Steering: primarily affected by front damage
                float steeringDamage = dmg.Front * cfg.FrontToSteeringRatio * HealthDegradeBase;
                health.ValueRW.Steering -= steeringDamage;

                // Transmission: primarily affected by rear damage
                float transmissionDamage = dmg.Rear * cfg.RearToTransmissionRatio * HealthDegradeBase;
                health.ValueRW.Transmission -= transmissionDamage;

                // Suspension: affected by side damage
                float sideDamage = (dmg.Left + dmg.Right) * 0.5f;
                float suspensionDamage = sideDamage * cfg.SideToSuspensionRatio * HealthDegradeBase;
                health.ValueRW.Suspension -= suspensionDamage;

                // Engine: affected by overall damage
                float totalDamageRatio = dmg.Total / GameConstants.MaxDamage;
                float engineDamage = totalDamageRatio * cfg.TotalToEngineRatio * HealthDegradeBase;
                health.ValueRW.Engine -= engineDamage;

                // Tires: affected by every impact
                float tireDamage = collision.ValueRO.ImpactSpeed * cfg.ImpactToTiresRatio * ImpactDegradeRate;
                health.ValueRW.Tires -= tireDamage;

                // Clamp all health values to [0, 1]
                health.ValueRW.Suspension = math.saturate(health.ValueRO.Suspension);
                health.ValueRW.Steering = math.saturate(health.ValueRO.Steering);
                health.ValueRW.Tires = math.saturate(health.ValueRO.Tires);
                health.ValueRW.Engine = math.saturate(health.ValueRO.Engine);
                health.ValueRW.Transmission = math.saturate(health.ValueRO.Transmission);

                // =============================================================
                // Check for New Component Failures
                // =============================================================

                ComponentFailures previousFailures = failureState.ValueRO.FailedComponents;
                ComponentFailures newFailures = previousFailures;

                // Check each component for failure
                if (health.ValueRO.Suspension <= cfg.FailureThreshold &&
                    !failureState.ValueRO.HasFailed(ComponentFailures.Suspension))
                {
                    newFailures |= ComponentFailures.Suspension;
                }

                if (health.ValueRO.Steering <= cfg.FailureThreshold &&
                    !failureState.ValueRO.HasFailed(ComponentFailures.Steering))
                {
                    newFailures |= ComponentFailures.Steering;
                }

                if (health.ValueRO.Tires <= cfg.FailureThreshold &&
                    !failureState.ValueRO.HasFailed(ComponentFailures.Tires))
                {
                    newFailures |= ComponentFailures.Tires;
                }

                if (health.ValueRO.Engine <= cfg.FailureThreshold &&
                    !failureState.ValueRO.HasFailed(ComponentFailures.Engine))
                {
                    newFailures |= ComponentFailures.Engine;
                }

                if (health.ValueRO.Transmission <= cfg.FailureThreshold &&
                    !failureState.ValueRO.HasFailed(ComponentFailures.Transmission))
                {
                    newFailures |= ComponentFailures.Transmission;
                }

                // Update failure state if new failures occurred
                if (newFailures != previousFailures)
                {
                    failureState.ValueRW.FailedComponents = newFailures;
                    failureState.ValueRW.TimeSinceLastFailure = 0f;

                    // =============================================================
                    // Check for Critical/Cascade Failure → Crash
                    // =============================================================

                    // Critical failure: steering OR suspension failed
                    bool hasCriticalFailure =
                        ((newFailures & ComponentFailures.Steering) != 0 &&
                         (previousFailures & ComponentFailures.Steering) == 0) ||
                        ((newFailures & ComponentFailures.Suspension) != 0 &&
                         (previousFailures & ComponentFailures.Suspension) == 0);

                    // Cascade failure: 3+ components failed
                    int failureCount = math.countbits((int)newFailures);
                    bool hasCascadeFailure = failureCount >= 3;

                    // Trigger crash on critical or cascade failure
                    if (hasCriticalFailure || hasCascadeFailure)
                    {
                        crashState.ValueRW.IsCrashed = true;
                        crashState.ValueRW.CrashTime = 0f;
                        crashState.ValueRW.Reason = CrashReason.ComponentFailure;
                    }
                }
            }
        }

        /// <summary>
        /// Initialize component health and failure tracking for new vehicles.
        /// </summary>
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerVehicleTag>();
        }
    }

    /// <summary>
    /// System to initialize Phase 2 damage components on player vehicle.
    /// Runs once when player vehicle is created.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ComponentHealthInitSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<PlayerVehicleTag>>()
                    .WithNone<ComponentHealth>()
                    .WithEntityAccess())
            {
                // Add component health at full
                ecb.AddComponent(entity, ComponentHealth.FullHealth);

                // Add failure state (no failures)
                ecb.AddComponent(entity, new ComponentFailureState
                {
                    FailedComponents = ComponentFailures.None,
                    TimeSinceLastFailure = 0f
                });

                // Add damage config with defaults
                ecb.AddComponent(entity, ComponentDamageConfig.Default);

                // Add soft-body state for enhanced visuals
                ecb.AddComponent(entity, new SoftBodyState
                {
                    CurrentDeformation = float4.zero,
                    TargetDeformation = float4.zero,
                    DeformationVelocity = float4.zero,
                    SpringConstant = 8f,
                    Damping = 0.7f
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
