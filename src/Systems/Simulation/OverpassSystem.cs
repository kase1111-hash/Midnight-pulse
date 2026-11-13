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

        // Share of the arch height the chase camera rises by, and how fast (1/s)
        private const float CameraElevationShare = 0.25f;
        private const float CameraElevationResponse = 3f;

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

            float elevation = 0f;
            foreach (var envState in SystemAPI.Query<RefRW<EnvironmentState>>()
                .WithAll<PlayerVehicleTag>())
            {
                envState.ValueRW.OnOverpass = onOverpass;

                // Smoothly transition elevation (frame-rate independent)
                envState.ValueRW.ElevationOffset = math.lerp(
                    envState.ValueRO.ElevationOffset,
                    targetElevation,
                    VehicleDynamics.Damp(ElevationBlendSpeed, deltaTime)
                );
                elevation = envState.ValueRO.ElevationOffset;
            }

            // The road surface under an overpass arch stays flat (only the
            // barriers and pillars rise), so the car is never lifted here:
            // LaneMagnetismSystem keeps it on the road. Adding the offset to
            // the car's height every frame used to launch it skyward.

            // =============================================================
            // Update Camera Height for Overpass View
            // =============================================================

            // CameraState is a singleton on the camera entity (not the player).
            // The camera rises a little as the arch passes overhead and settles
            // back afterwards (it eases toward a target instead of adding the
            // elevation to itself every frame, which drifted upward forever).
            foreach (var cameraState in SystemAPI.Query<RefRW<CameraState>>())
            {
                float3 targetOffset = new float3(0f, elevation * CameraElevationShare, 0f);

                cameraState.ValueRW.TargetOffset = math.lerp(
                    cameraState.ValueRO.TargetOffset,
                    targetOffset,
                    VehicleDynamics.Damp(CameraElevationResponse, deltaTime)
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
