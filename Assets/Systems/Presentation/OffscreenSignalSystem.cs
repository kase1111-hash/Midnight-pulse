// ============================================================================
// Nightflow - Off-Screen Signal System
// Execution Order: 1 (Presentation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Calculates off-screen threat indicators for HUD display.
    /// Shows direction and urgency of approaching threats.
    ///
    /// Signal types:
    /// - Emergency vehicle approaching from behind
    /// - Crashed vehicle/hazard ahead (out of view)
    ///
    /// Output is consumed by the UI rendering system.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct OffscreenSignalSystem : ISystem
    {
        // Signal thresholds
        private const float EmergencySignalDistance = 100f;   // meters
        private const float HazardSignalDistance = 80f;       // meters ahead
        private const float SignalFadeDistance = 20f;         // fade in/out range

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // =============================================================
            // Get Camera/View State (approximated from player)
            // =============================================================

            float3 playerPos = float3.zero;
            float3 playerForward = new float3(0, 0, 1);
            float playerSpeed = 0f;

            foreach (var (transform, velocity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                playerPos = transform.ValueRO.Position;
                playerForward = math.forward(transform.ValueRO.Rotation);
                playerSpeed = velocity.ValueRO.Forward;
                break;
            }

            // =============================================================
            // Update Player Off-Screen Signals
            // =============================================================

            foreach (var (signal, detection) in
                SystemAPI.Query<RefRW<OffscreenSignal>, RefRO<EmergencyDetection>>()
                    .WithAll<PlayerVehicleTag>())
            {
                // Reset signal
                signal.ValueRW.Active = false;
                signal.ValueRW.Urgency = 0f;

                // =============================================================
                // Emergency Behind Signal
                // =============================================================

                if (detection.ValueRO.ApproachingFromBehind &&
                    detection.ValueRO.NearestDistance < EmergencySignalDistance)
                {
                    signal.ValueRW.Active = true;
                    signal.ValueRW.Type = OffscreenSignalType.EmergencyBehind;

                    // Calculate screen-space direction (behind = bottom of screen)
                    signal.ValueRW.Direction = new float2(0, -1);

                    // Urgency increases as emergency gets closer
                    float distRatio = detection.ValueRO.NearestDistance / EmergencySignalDistance;
                    signal.ValueRW.Urgency = 1f - distRatio;

                    // Add lateral offset hint
                    int emergencyLane = detection.ValueRO.EmergencyLane;
                    float lateralHint = emergencyLane <= 1 ? -0.3f : 0.3f;
                    signal.ValueRW.Direction = math.normalize(new float2(lateralHint, -1));
                }
            }

            // =============================================================
            // Check for Crashed Vehicles Ahead
            // =============================================================

            // Find any crashed car hazards ahead
            float nearestCrashedZ = float.MaxValue;
            float3 nearestCrashedPos = float3.zero;

            foreach (var (hazard, transform) in
                SystemAPI.Query<RefRO<Hazard>, RefRO<WorldTransform>>()
                    .WithAll<HazardTag>())
            {
                if (hazard.ValueRO.Type == HazardType.CrashedCar)
                {
                    float dz = transform.ValueRO.Position.z - playerPos.z;

                    // Only signal if ahead and within range
                    if (dz > 0 && dz < HazardSignalDistance && dz < nearestCrashedZ)
                    {
                        nearestCrashedZ = dz;
                        nearestCrashedPos = transform.ValueRO.Position;
                    }
                }
            }

            // Update signal if crashed vehicle found (and no emergency signal active)
            foreach (var signal in
                SystemAPI.Query<RefRW<OffscreenSignal>>()
                    .WithAll<PlayerVehicleTag>())
            {
                if (nearestCrashedZ < float.MaxValue && !signal.ValueRO.Active)
                {
                    signal.ValueRW.Active = true;
                    signal.ValueRW.Type = OffscreenSignalType.CrashedVehicleAhead;

                    // Calculate direction to crashed vehicle
                    float3 toCrashed = nearestCrashedPos - playerPos;
                    float lateralOffset = toCrashed.x;

                    // Screen direction (ahead with lateral offset)
                    float xDir = math.clamp(lateralOffset / 10f, -1f, 1f);
                    signal.ValueRW.Direction = math.normalize(new float2(xDir, 1));

                    // Urgency based on distance and speed
                    float distRatio = nearestCrashedZ / HazardSignalDistance;
                    float timeToImpact = nearestCrashedZ / math.max(playerSpeed, 1f);
                    signal.ValueRW.Urgency = math.saturate(1f - distRatio) *
                                             math.saturate(3f / timeToImpact);
                }
            }
        }
    }
}
