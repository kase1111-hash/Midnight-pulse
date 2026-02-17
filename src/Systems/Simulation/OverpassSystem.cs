// ============================================================================
// Nightflow - Overpass System
// Handles elevation for overpass segments
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages overpass elevation and transitions.
    ///
    /// From spec:
    /// - Height offset: h(t) = A × sin(πt)
    /// - Stacked overpasses: Duplicate segment at higher y
    /// - Audio: Ringing, metallic reverb (handled by AudioSystem)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TunnelSystem))]
    public partial struct OverpassSystem : ISystem
    {
        // Transition parameters
        private const float ElevationBlendSpeed = 4f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get player position and transform
            float3 playerPos = float3.zero;
            float playerZ = 0f;

            foreach (var transform in SystemAPI.Query<RefRO<WorldTransform>>()
                .WithAll<PlayerVehicleTag>())
            {
                playerPos = transform.ValueRO.Position;
                playerZ = playerPos.z;
                break;
            }

            // =============================================================
            // Calculate Elevation for Player on Overpass
            // =============================================================

            float targetElevation = 0f;
            bool onOverpass = false;

            foreach (var (segment, overpassData, spline) in
                SystemAPI.Query<RefRO<TrackSegment>, RefRO<OverpassData>, RefRO<HermiteSpline>>()
                    .WithAll<OverpassTag>())
            {
                if (playerZ >= segment.ValueRO.StartZ && playerZ <= segment.ValueRO.EndZ)
                {
                    onOverpass = true;

                    // Calculate t parameter (progress through segment)
                    float segmentLength = segment.ValueRO.EndZ - segment.ValueRO.StartZ;
                    float t = (playerZ - segment.ValueRO.StartZ) / math.max(segmentLength, 0.001f);

                    // Apply sinusoidal elevation: h(t) = A × sin(πt)
                    // This creates a smooth arc that rises and falls
                    float amplitude = overpassData.ValueRO.ElevationAmplitude;
                    targetElevation = amplitude * math.sin(math.PI * t);

                    break;
                }
            }

            // =============================================================
            // Update Player Environment State
            // =============================================================

            foreach (var envState in SystemAPI.Query<RefRW<EnvironmentState>>()
                .WithAll<PlayerVehicleTag>())
            {
                envState.ValueRW.OnOverpass = onOverpass;

                // Smoothly transition elevation
                envState.ValueRW.ElevationOffset = math.lerp(
                    envState.ValueRO.ElevationOffset,
                    targetElevation,
                    ElevationBlendSpeed * deltaTime
                );
            }

            // =============================================================
            // Apply Elevation to Player Transform
            // =============================================================

            foreach (var (transform, envState) in
                SystemAPI.Query<RefRW<WorldTransform>, RefRO<EnvironmentState>>()
                    .WithAll<PlayerVehicleTag>())
            {
                // Adjust Y position based on elevation offset
                // Note: This modifies the visual position, physics stays on track
                float3 pos = transform.ValueRO.Position;

                // Only apply if there's actual elevation change
                if (math.abs(envState.ValueRO.ElevationOffset) > 0.01f)
                {
                    pos.y += envState.ValueRO.ElevationOffset;
                    transform.ValueRW.Position = pos;
                }
            }

            // =============================================================
            // Update Camera Height for Overpass View
            // =============================================================

            foreach (var cameraState in SystemAPI.Query<RefRW<CameraState>>()
                .WithAll<PlayerVehicleTag>())
            {
                float elevation = 0f;
                foreach (var envState in SystemAPI.Query<RefRO<EnvironmentState>>()
                    .WithAll<PlayerVehicleTag>())
                {
                    elevation = envState.ValueRO.ElevationOffset;
                    break;
                }

                // Adjust camera target height
                // Camera follows player elevation smoothly
                float3 baseOffset = cameraState.ValueRO.TargetOffset;
                float3 targetOffset = new float3(baseOffset.x, baseOffset.y + elevation, baseOffset.z);

                cameraState.ValueRW.TargetOffset = math.lerp(
                    baseOffset,
                    targetOffset,
                    3f * deltaTime
                );
            }

            // =============================================================
            // Visual Effect: Slight Exposure Increase on Overpass
            // =============================================================

            foreach (var renderState in SystemAPI.Query<RefRW<RenderState>>())
            {
                float targetExposure = onOverpass ? 1.1f : 1f;

                renderState.ValueRW.Exposure = math.lerp(
                    renderState.ValueRO.Exposure,
                    targetExposure,
                    2f * deltaTime
                );

                break;
            }
        }
    }
}
