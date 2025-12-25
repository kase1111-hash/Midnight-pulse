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
            float3 cameraRight = new float3(1, 0, 0);
            float fov = 60f;
            float yawOffset = 0f;

            foreach (var camera in SystemAPI.Query<RefRO<CameraState>>())
            {
                cameraPos = camera.ValueRO.Position;
                fov = camera.ValueRO.FOV > 0 ? camera.ValueRO.FOV : 60f;
                yawOffset = camera.ValueRO.YawOffset;
                break;
            }

            // Get player position and rotation for camera-space calculations
            float3 playerPos = float3.zero;
            quaternion playerRotation = quaternion.identity;

            foreach (var transform in SystemAPI.Query<RefRO<WorldTransform>>().WithAll<PlayerVehicleTag>())
            {
                playerPos = transform.ValueRO.Position;
                playerRotation = transform.ValueRO.Rotation;
                break;
            }

            // Calculate camera orientation from player rotation + yaw offset
            // Apply yaw offset for drift whip effect
            quaternion yawRotation = quaternion.RotateY(math.radians(yawOffset));
            quaternion cameraRotation = math.mul(playerRotation, yawRotation);

            // Extract camera basis vectors
            cameraForward = math.mul(cameraRotation, new float3(0, 0, 1));
            cameraRight = math.mul(cameraRotation, new float3(1, 0, 0));

            // Calculate half-angle tangent for frustum check
            float halfFovTan = math.tan(math.radians(fov * 0.5f));
            float aspectRatio = 16f / 9f; // Standard widescreen

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

                // Transform to camera-local space using camera basis vectors
                // localPos.x = right component, localPos.y = up, localPos.z = forward
                float3 localPos = new float3(
                    math.dot(toHazard, cameraRight),    // X: right/left
                    math.dot(toHazard, new float3(0, 1, 0)), // Y: up/down (world up)
                    math.dot(toHazard, cameraForward)   // Z: forward/back
                );

                // Check if on-screen using proper frustum check
                // Object is on-screen if within FOV cone in both X and Y
                bool inFrontOfCamera = localPos.z > 0.5f; // Small margin to avoid edge cases
                float horizontalAngle = math.abs(localPos.x) / localPos.z;
                float verticalAngle = math.abs(localPos.y) / localPos.z;
                bool withinHorizontalFov = horizontalAngle < halfFovTan * aspectRatio;
                bool withinVerticalFov = verticalAngle < halfFovTan;

                bool onScreen = inFrontOfCamera && withinHorizontalFov && withinVerticalFov;

                if (onScreen)
                {
                    signal.ValueRW.Active = false;
                    continue;
                }

                // Calculate screen edge position using atan2 for proper angle
                signal.ValueRW.Active = true;
                signal.ValueRW.Distance = distance;

                // Calculate angle from camera forward to hazard (in XZ plane of camera space)
                float angle = math.atan2(localPos.x, localPos.z);

                // Map angle to screen edge position
                // angle = 0 -> top center, angle = PI/2 -> right, angle = -PI/2 -> left
                // angle = PI or -PI -> bottom center
                float2 screenPos = CalculateEdgePosition(angle, localPos.z, ScreenEdgeMargin);
                signal.ValueRW.ScreenPosition = screenPos;

                // Calculate urgency (0 = far, 1 = close)
                float urgency = 1f - math.saturate((distance - MinSignalDistance) / (MaxSignalDistance - MinSignalDistance));
                signal.ValueRW.Urgency = urgency;

                // Calculate pulse rate
                float pulseRate = math.lerp(BasePulseRate, MaxPulseRate, urgency);
                signal.ValueRW.PulsePhase = math.frac(time * pulseRate);

                // Color based on hazard severity with distance-weighted alpha
                // Closer hazards are more opaque
                float alpha = math.lerp(0.4f, 1f, urgency);
                signal.ValueRW.Color = new float4(1f, 1f - hazard.ValueRO.Severity, 0f, alpha);
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

                // Transform to camera-local space
                float3 localPos = new float3(
                    math.dot(toEmergency, cameraRight),
                    math.dot(toEmergency, new float3(0, 1, 0)),
                    math.dot(toEmergency, cameraForward)
                );

                // Emergency vehicles signal when behind the camera (approaching from behind)
                bool approachingFromBehind = localPos.z < 0;

                if (!approachingFromBehind || distance > MaxSignalDistance * 1.5f)
                {
                    signal.ValueRW.Active = false;
                    continue;
                }

                signal.ValueRW.Active = true;
                signal.ValueRW.Distance = distance;

                // Emergency signals are always high urgency
                float urgency = math.saturate(1f - distance / (MaxSignalDistance * 1.5f));
                signal.ValueRW.Urgency = urgency;

                // Calculate angle for proper lateral positioning
                float angle = math.atan2(localPos.x, localPos.z);

                // Position at bottom of screen with lateral offset based on approach angle
                float2 screenPos = CalculateEdgePosition(angle, localPos.z, ScreenEdgeMargin);
                signal.ValueRW.ScreenPosition = screenPos;

                // Alternating red/blue flash with distance-based intensity
                float flashPhase = math.frac(time * 4f);
                bool isRed = flashPhase < 0.5f;
                float alpha = math.lerp(0.6f, 1f, urgency);
                signal.ValueRW.Color = isRed ?
                    new float4(1f, 0f, 0f, alpha) :
                    new float4(0f, 0.3f, 1f, alpha);

                signal.ValueRW.PulsePhase = flashPhase;
            }
        }

        /// <summary>
        /// Calculate screen edge position from angle and forward distance.
        /// Maps an angle to a position along the screen edge.
        /// </summary>
        private static float2 CalculateEdgePosition(float angle, float forwardDist, float margin)
        {
            // Normalize angle to [-PI, PI]
            angle = math.fmod(angle + math.PI, 2f * math.PI) - math.PI;

            // Calculate position based on angle
            // Front (angle near 0): top edge
            // Right (angle near PI/2): right edge
            // Left (angle near -PI/2): left edge
            // Back (angle near ±PI): bottom edge

            float absAngle = math.abs(angle);
            float signX = math.sign(angle);

            // Determine which edge the indicator should be on
            // Using a smooth transition between edges

            if (forwardDist > 0)
            {
                // Object is ahead - position on top edge with lateral offset
                // Map angle to X position: -PI/2 to PI/2 -> 0 to 1
                float normalizedAngle = angle / (math.PI * 0.5f);
                float x = math.clamp(normalizedAngle * 0.4f + 0.5f, margin, 1f - margin);
                return new float2(x, 1f - margin);
            }
            else
            {
                // Object is behind - position on bottom edge with lateral offset
                float normalizedAngle = angle / math.PI;
                float x = math.clamp(normalizedAngle * 0.4f + 0.5f, margin, 1f - margin);
                return new float2(x, margin);
            }
        }
    }
}
