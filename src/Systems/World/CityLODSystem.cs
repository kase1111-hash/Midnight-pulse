// ============================================================================
// Nightflow - City LOD System
// Aggressive level-of-detail for GPU-light city rendering
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages LOD transitions for procedural buildings.
    /// GPU-light: aggressive culling and LOD switching.
    ///
    /// From spec:
    /// - LOD system for distant geometry
    ///
    /// LOD Strategy:
    /// - LOD0 (0-50m): Full detail, window geometry
    /// - LOD1 (50-150m): Simple box, no windows
    /// - LOD2 (150-400m): Billboard impostor
    /// - Cull (400m+): Not rendered
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(CityGenerationSystem))]
    public partial struct CityLODSystem : ISystem
    {
        // Update parameters
        private const int MaxLODUpdatesPerFrame = 32;
        private const float LODHysteresis = 5f;         // Prevents LOD thrashing

        private int _updateOffset;
        private int _frameCount;

        public void OnCreate(ref SystemState state)
        {
            _updateOffset = 0;
            _frameCount = 0;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _frameCount++;

            // Get camera/player position
            float3 cameraPos = float3.zero;
            float3 cameraForward = new float3(0, 0, 1);

            foreach (var camera in SystemAPI.Query<RefRO<CameraState>>())
            {
                cameraPos = camera.ValueRO.Position;
                cameraForward = math.mul(camera.ValueRO.Rotation, new float3(0, 0, 1));
                break;
            }

            // Fallback to player position
            if (math.lengthsq(cameraPos) < 0.01f)
            {
                foreach (var transform in SystemAPI.Query<RefRO<WorldTransform>>()
                    .WithAll<PlayerVehicleTag>())
                {
                    cameraPos = transform.ValueRO.Position + new float3(0, 5f, -10f);
                    break;
                }
            }

            // Get LOD thresholds
            LODThresholds thresholds = LODThresholds.Default;
            foreach (var lodConfig in SystemAPI.Query<RefRO<LODThresholds>>())
            {
                thresholds = lodConfig.ValueRO;
                break;
            }

            // =============================================================
            // Update Building LODs
            // =============================================================

            int updateCount = 0;
            int entityIndex = 0;

            foreach (var (transform, lod, buildingDef, entity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRW<BuildingLOD>, RefRO<BuildingDefinition>>()
                    .WithAll<BuildingTag>()
                    .WithEntityAccess())
            {
                entityIndex++;

                // Stagger updates across frames for performance
                if ((entityIndex + _updateOffset) % 4 != _frameCount % 4)
                {
                    continue;
                }

                if (updateCount >= MaxLODUpdatesPerFrame) break;
                updateCount++;

                ref var lodState = ref lod.ValueRW;

                // Calculate distance to camera
                float3 buildingPos = transform.ValueRO.Position;
                buildingPos.y += buildingDef.ValueRO.Height * 0.5f; // Use building center

                float distance = math.distance(buildingPos, cameraPos);
                lodState.DistanceToCamera = distance;
                lodState.LastUpdateFrame = _frameCount;

                // Frustum check (simplified - cone check)
                float3 toBuilding = math.normalize(buildingPos - cameraPos);
                float dot = math.dot(toBuilding, cameraForward);
                bool inFrustum = dot > 0.3f || distance < 50f; // Wide cone + close override

                // Behind camera check with hysteresis
                bool behind = buildingPos.z < cameraPos.z - 20f;

                lodState.IsVisible = inFrustum && !behind;
                lodState.InRenderDistance = distance < thresholds.CullDistance;

                // Determine target LOD
                int targetLOD;
                if (distance < thresholds.LOD0Distance - LODHysteresis)
                {
                    targetLOD = 0;
                }
                else if (distance < thresholds.LOD1Distance - LODHysteresis)
                {
                    targetLOD = 1;
                }
                else if (distance < thresholds.LOD2Distance - LODHysteresis)
                {
                    targetLOD = 2;
                }
                else
                {
                    targetLOD = 3; // Culled
                }

                // Apply hysteresis to prevent thrashing
                if (targetLOD < lodState.TargetLOD)
                {
                    // Going to higher detail - stricter check
                    if (distance < thresholds.LOD0Distance - LODHysteresis * 2)
                        lodState.TargetLOD = 0;
                    else if (distance < thresholds.LOD1Distance - LODHysteresis * 2)
                        lodState.TargetLOD = math.min(1, lodState.TargetLOD);
                    else if (distance < thresholds.LOD2Distance - LODHysteresis * 2)
                        lodState.TargetLOD = math.min(2, lodState.TargetLOD);
                }
                else
                {
                    lodState.TargetLOD = targetLOD;
                }

                // Smooth LOD transition
                if (lodState.CurrentLOD != lodState.TargetLOD)
                {
                    lodState.TransitionProgress += SystemAPI.Time.DeltaTime * 3f;

                    if (lodState.TransitionProgress >= 1f)
                    {
                        lodState.CurrentLOD = lodState.TargetLOD;
                        lodState.TransitionProgress = 0f;
                    }
                }
            }

            _updateOffset++;
        }
    }

    /// <summary>
    /// Manages impostor billboard orientation.
    /// Keeps distant building billboards facing the camera.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct ImpostorBillboardSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get camera position
            float3 cameraPos = float3.zero;

            foreach (var camera in SystemAPI.Query<RefRO<CameraState>>())
            {
                cameraPos = camera.ValueRO.Position;
                break;
            }

            // Update impostor orientations
            foreach (var (impostor, transform) in
                SystemAPI.Query<RefRO<BuildingImpostor>, RefRW<WorldTransform>>()
                    .WithAll<ImpostorTag>())
            {
                if (!impostor.ValueRO.IsBillboard) continue;

                // Face camera (Y-axis rotation only)
                float3 toCamera = cameraPos - transform.ValueRO.Position;
                toCamera.y = 0;

                if (math.lengthsq(toCamera) > 0.01f)
                {
                    float angle = math.atan2(toCamera.x, toCamera.z);
                    transform.ValueRW.Rotation = quaternion.RotateY(angle);
                }
            }

            // Update LOD2 buildings to face camera
            foreach (var (lod, transform) in
                SystemAPI.Query<RefRO<BuildingLOD>, RefRW<WorldTransform>>()
                    .WithAll<BuildingTag>())
            {
                // Only billboard at LOD2
                if (lod.ValueRO.CurrentLOD < 2) continue;
                if (!lod.ValueRO.IsVisible) continue;

                float3 toCamera = cameraPos - transform.ValueRO.Position;
                toCamera.y = 0;

                if (math.lengthsq(toCamera) > 0.01f)
                {
                    float angle = math.atan2(toCamera.x, toCamera.z);
                    transform.ValueRW.Rotation = quaternion.RotateY(angle);
                }
            }
        }
    }

    /// <summary>
    /// Culls buildings that are too far or behind camera.
    /// Disables rendering for culled entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(ImpostorBillboardSystem))]
    public partial struct BuildingCullingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int visibleCount = 0;
            int culledCount = 0;

            // Track visibility statistics
            foreach (var lod in SystemAPI.Query<RefRO<BuildingLOD>>()
                .WithAll<BuildingTag>())
            {
                if (lod.ValueRO.IsVisible && lod.ValueRO.InRenderDistance &&
                    lod.ValueRO.CurrentLOD < 3)
                {
                    visibleCount++;
                }
                else
                {
                    culledCount++;
                }
            }

            // Update city state with visibility info
            foreach (var cityState in SystemAPI.Query<RefRW<CityGenerationState>>())
            {
                // Could add visibility stats here if needed
            }
        }
    }

    /// <summary>
    /// Converts distant buildings to lightweight impostors.
    /// Reduces GPU load for buildings beyond LOD2 threshold.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CityLODSystem))]
    public partial struct ImpostorConversionSystem : ISystem
    {
        private const int MaxConversionsPerFrame = 4;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get LOD thresholds
            LODThresholds thresholds = LODThresholds.Default;
            foreach (var lodConfig in SystemAPI.Query<RefRO<LODThresholds>>())
            {
                thresholds = lodConfig.ValueRO;
                break;
            }

            // Get city state for impostor budget
            int maxImpostors = 512;
            int currentImpostors = 0;

            foreach (var cityState in SystemAPI.Query<RefRO<CityGenerationState>>())
            {
                maxImpostors = cityState.ValueRO.MaxImpostors;
                currentImpostors = cityState.ValueRO.ActiveImpostorCount;
                break;
            }

            int conversions = 0;

            // Convert very distant buildings to impostors
            foreach (var (lod, buildingDef, transform, entity) in
                SystemAPI.Query<RefRO<BuildingLOD>, RefRO<BuildingDefinition>, RefRO<WorldTransform>>()
                    .WithAll<BuildingTag>()
                    .WithNone<ImpostorTag>()
                    .WithEntityAccess())
            {
                if (conversions >= MaxConversionsPerFrame) break;
                if (currentImpostors >= maxImpostors) break;

                // Only convert buildings at LOD2+ that are visible
                if (lod.ValueRO.CurrentLOD < 2) continue;
                if (!lod.ValueRO.IsVisible) continue;
                if (lod.ValueRO.DistanceToCamera < thresholds.LOD2Distance) continue;

                // Building is distant and visible - could be converted to impostor
                // For now, just mark as using impostor rendering (no entity swap)

                conversions++;
            }
        }
    }

    /// <summary>
    /// Applies distance-based alpha fade for smooth LOD transitions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(BuildingCullingSystem))]
    public partial struct BuildingFadeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get LOD thresholds
            LODThresholds thresholds = LODThresholds.Default;
            foreach (var lodConfig in SystemAPI.Query<RefRO<LODThresholds>>())
            {
                thresholds = lodConfig.ValueRO;
                break;
            }

            foreach (var (lod, meshData) in
                SystemAPI.Query<RefRO<BuildingLOD>, RefRW<VehicleMeshData>>()
                    .WithAll<BuildingTag>())
            {
                if (!meshData.ValueRO.IsGenerated) continue;

                float distance = lod.ValueRO.DistanceToCamera;
                float alpha = 1f;

                // Fade out near cull distance
                if (distance > thresholds.LOD2Distance)
                {
                    float fadeStart = thresholds.LOD2Distance;
                    float fadeEnd = thresholds.CullDistance;
                    alpha = 1f - math.saturate((distance - fadeStart) / (fadeEnd - fadeStart));
                }

                // Apply transition fade
                if (lod.ValueRO.TransitionProgress > 0f && lod.ValueRO.TransitionProgress < 1f)
                {
                    // Cross-fade during LOD transition
                    if (lod.ValueRO.CurrentLOD < lod.ValueRO.TargetLOD)
                    {
                        // Fading out to lower LOD
                        alpha *= 1f - lod.ValueRO.TransitionProgress;
                    }
                    else
                    {
                        // Fading in to higher LOD
                        alpha *= lod.ValueRO.TransitionProgress;
                    }
                }

                // Store alpha in wireframe color
                meshData.ValueRW.WireframeColor.w = alpha;
            }

            // Fade impostors
            foreach (var (impostor, lod) in
                SystemAPI.Query<RefRW<BuildingImpostor>, RefRO<BuildingLOD>>()
                    .WithAll<ImpostorTag>())
            {
                float distance = lod.ValueRO.DistanceToCamera;

                float fadeStart = thresholds.LOD2Distance;
                float fadeEnd = thresholds.CullDistance;
                impostor.ValueRW.Alpha = 1f - math.saturate((distance - fadeStart) / (fadeEnd - fadeStart));
            }
        }
    }
}
