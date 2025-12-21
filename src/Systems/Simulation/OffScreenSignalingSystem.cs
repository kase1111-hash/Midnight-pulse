// ============================================================================
// Nightflow - Off-Screen Signaling System
// Execution Order: 13 (Simulation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Calculates off-screen indicators for hazards and emergency vehicles.
    /// Projects world positions to screen edge with distance-based urgency.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ScoringSystem))]
    public partial struct OffScreenSignalingSystem : ISystem
    {
        // Signaling parameters
        private const float MaxSignalDistance = 100f;     // meters - beyond this, no signal
        private const float MinSignalDistance = 10f;      // meters - fully urgent
        private const float ScreenEdgeMargin = 0.05f;     // 5% from edge

        // Pulse parameters
        private const float BasePulseRate = 1f;           // Hz at max distance
        private const float MaxPulseRate = 4f;            // Hz at min distance

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;

            // Get camera state for frustum calculations
            float3 cameraPos = float3.zero;
            float3 cameraForward = new float3(0, 0, 1);
            float fov = 60f;

            foreach (var camera in SystemAPI.Query<RefRO<CameraState>>())
            {
                cameraPos = camera.ValueRO.Position;
                // TODO: Get actual camera rotation
                fov = 60f; // Placeholder
                break;
            }

            // Get player position for relative calculations
            float3 playerPos = float3.zero;
            foreach (var transform in SystemAPI.Query<RefRO<WorldTransform>>().WithAll<PlayerVehicleTag>())
            {
                playerPos = transform.ValueRO.Position;
                break;
            }

            // =============================================================
            // Update Hazard Signals
            // =============================================================

            foreach (var (hazard, signal, transform) in
                SystemAPI.Query<RefRO<Hazard>, RefRW<OffscreenSignal>, RefRO<WorldTransform>>()
                    .WithAll<HazardTag>())
            {
                float3 toHazard = transform.ValueRO.Position - playerPos;
                float distance = math.length(toHazard);

                if (distance > MaxSignalDistance || distance < 1f)
                {
                    signal.ValueRW.Active = false;
                    continue;
                }

                // Check if on-screen (simplified frustum check)
                float3 localPos = toHazard; // TODO: Transform to camera space
                bool onScreen = localPos.z > 0 &&
                               math.abs(localPos.x / localPos.z) < math.tan(math.radians(fov * 0.5f));

                if (onScreen)
                {
                    signal.ValueRW.Active = false;
                    continue;
                }

                // Calculate screen edge position
                signal.ValueRW.Active = true;
                signal.ValueRW.Distance = distance;

                // Normalize direction for edge placement
                float2 screenDir = math.normalize(new float2(localPos.x, localPos.z));
                signal.ValueRW.ScreenPosition = new float2(
                    math.clamp(screenDir.x * 0.5f + 0.5f, ScreenEdgeMargin, 1f - ScreenEdgeMargin),
                    localPos.z > 0 ? 1f - ScreenEdgeMargin : ScreenEdgeMargin
                );

                // Calculate urgency (0 = far, 1 = close)
                float urgency = 1f - math.saturate((distance - MinSignalDistance) / (MaxSignalDistance - MinSignalDistance));
                signal.ValueRW.Urgency = urgency;

                // Calculate pulse rate
                float pulseRate = math.lerp(BasePulseRate, MaxPulseRate, urgency);
                signal.ValueRW.PulsePhase = math.frac(time * pulseRate);

                // Color based on hazard severity
                signal.ValueRW.Color = new float4(1f, 1f - hazard.ValueRO.Severity, 0f, urgency);
            }

            // =============================================================
            // Update Emergency Vehicle Signals
            // =============================================================

            foreach (var (emergencyAI, signal, transform) in
                SystemAPI.Query<RefRO<EmergencyAI>, RefRW<OffscreenSignal>, RefRO<WorldTransform>>()
                    .WithAll<EmergencyVehicleTag>())
            {
                float3 toEmergency = transform.ValueRO.Position - playerPos;
                float distance = math.length(toEmergency);

                // Emergency vehicles always signal when approaching from behind
                bool approachingFromBehind = toEmergency.z < 0;

                if (!approachingFromBehind || distance > MaxSignalDistance * 1.5f)
                {
                    signal.ValueRW.Active = false;
                    continue;
                }

                signal.ValueRW.Active = true;
                signal.ValueRW.Distance = distance;

                // Emergency signals are always high urgency
                signal.ValueRW.Urgency = math.saturate(1f - distance / (MaxSignalDistance * 1.5f));

                // Position at bottom of screen (behind)
                signal.ValueRW.ScreenPosition = new float2(0.5f, ScreenEdgeMargin);

                // Alternating red/blue flash
                float flashPhase = math.frac(time * 4f);
                bool isRed = flashPhase < 0.5f;
                signal.ValueRW.Color = isRed ?
                    new float4(1f, 0f, 0f, 1f) :
                    new float4(0f, 0f, 1f, 1f);

                signal.ValueRW.PulsePhase = flashPhase;
            }
        }
    }
}
