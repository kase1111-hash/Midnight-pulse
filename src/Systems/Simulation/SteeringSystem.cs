// ============================================================================
// Nightflow - Steering & Lane Transition System
// Execution Order: 3 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Processes steering input and manages lane transitions.
    /// Applies smoothing to steering angle.
    /// Handles lane change trigger, progress, and abort logic.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AutopilotSystem))]
    public partial struct SteeringSystem : ISystem
    {
        // Lane change parameters (from spec)
        private const float SteerTriggerThreshold = 0.35f;
        private const float AbortThreshold = 0.7f;
        private const float BaseDuration = 0.6f;
        private const float MinDuration = 0.45f;
        private const float MaxDuration = 1.0f;
        private const float ReferenceSpeed = 40f; // m/s

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (input, steeringState, laneTransition, velocity, laneFollower) in
                SystemAPI.Query<RefRO<PlayerInput>, RefRW<SteeringState>,
                               RefRW<LaneTransition>, RefRO<Velocity>, RefRO<LaneFollower>>())
            {
                float steerInput = input.ValueRO.Steer;

                // =============================================================
                // Steering Smoothing
                // =============================================================
                steeringState.ValueRW.TargetAngle = steerInput * math.PI * 0.25f; // Max 45 degrees

                float smoothness = steeringState.ValueRO.Smoothness;
                float currentAngle = steeringState.ValueRO.CurrentAngle;
                float targetAngle = steeringState.ValueRW.TargetAngle;

                // Exponential smoothing
                steeringState.ValueRW.CurrentAngle = math.lerp(currentAngle, targetAngle,
                    1f - math.exp(-deltaTime / math.max(smoothness, 0.01f)));

                // =============================================================
                // Lane Transition Logic
                // =============================================================
                ref var transition = ref laneTransition.ValueRW;

                if (transition.Active)
                {
                    // Progress the transition
                    transition.Progress += deltaTime;

                    // Check for abort (counter-steer)
                    bool counterSteer = (transition.Direction > 0 && steerInput < -AbortThreshold) ||
                                       (transition.Direction < 0 && steerInput > AbortThreshold);

                    if (counterSteer)
                    {
                        // Reverse transition
                        Entity temp = transition.FromLane;
                        transition.FromLane = transition.ToLane;
                        transition.ToLane = temp;
                        transition.Direction = -transition.Direction;

                        // Calculate remaining progress
                        float t = math.saturate(transition.Progress / transition.Duration);
                        transition.Progress = transition.Duration * (1f - t);
                    }

                    // Check for completion
                    if (transition.Progress >= transition.Duration)
                    {
                        transition.Active = false;
                        transition.Progress = 0f;
                        // LaneFollower will be updated by LaneMagnetismSystem
                    }
                }
                else
                {
                    // Check for lane change trigger
                    if (math.abs(steerInput) > SteerTriggerThreshold)
                    {
                        int direction = steerInput > 0 ? 1 : -1;

                        // TODO: Check if target lane exists and is not blocked
                        // Entity targetLane = GetAdjacentLane(laneFollower.ValueRO.LaneEntity, direction);
                        // if (targetLane != Entity.Null && !IsLaneBlocked(targetLane))

                        // Calculate speed-aware duration
                        float speed = velocity.ValueRO.Forward;
                        float duration = BaseDuration * (speed / ReferenceSpeed);
                        duration = math.clamp(duration, MinDuration, MaxDuration);

                        // Start transition
                        transition.Active = true;
                        transition.FromLane = laneFollower.ValueRO.LaneEntity;
                        // transition.ToLane = targetLane;
                        transition.Progress = 0f;
                        transition.Duration = duration;
                        transition.Direction = direction;
                    }
                }
            }
        }
    }
}
