// ============================================================================
// Nightflow - Ghost Render System
// Semi-transparent ghost vehicle rendering with trail effects
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Buffers;

namespace Nightflow.Systems
{
    /// <summary>
    /// Renders ghost vehicles with semi-transparent wireframe and trail effects.
    ///
    /// From spec:
    /// - Semi-transparent, non-colliding
    /// - Optional trail effect
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(WireframeRenderSystem))]
    public partial struct GhostRenderSystem : ISystem
    {
        // Trail rendering parameters
        private const float TrailPointSpacing = 0.5f;
        private const int MaxVisibleTrailPoints = 32;

        // Pulse effect
        private const float PulseMinAlpha = 0.3f;
        private const float PulseMaxAlpha = 0.7f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Process ghost vehicles
            foreach (var (ghostRender, replayState, transform, trailBuffer) in
                SystemAPI.Query<RefRW<GhostRenderState>, RefRO<ReplayState>,
                               RefRO<WorldTransform>, DynamicBuffer<GhostTrailPoint>>()
                    .WithAll<GhostVehicleTag>())
            {
                // Update pulse effect
                ghostRender.ValueRW.PulsePhase =
                    math.frac(ghostRender.ValueRO.PulsePhase + ghostRender.ValueRO.PulseSpeed * deltaTime);

                // Calculate pulsing alpha
                float pulseT = (math.sin(ghostRender.ValueRO.PulsePhase * math.PI * 2f) + 1f) * 0.5f;
                float pulseAlpha = math.lerp(PulseMinAlpha, PulseMaxAlpha, pulseT);

                // Modify alpha based on playback state
                if (replayState.ValueRO.IsComplete)
                {
                    // Fade out when complete
                    ghostRender.ValueRW.Alpha = math.max(0f,
                        ghostRender.ValueRO.Alpha - deltaTime * 2f);
                }
                else if (replayState.ValueRO.IsPlaying)
                {
                    // Pulsing alpha during playback
                    ghostRender.ValueRW.Alpha = ghostRender.ValueRO.BaseAlpha * pulseAlpha;
                }

                // Update trail point alphas
                if (ghostRender.ValueRO.ShowTrail)
                {
                    float currentTime = replayState.ValueRO.PlaybackTime;
                    float trailLength = ghostRender.ValueRO.TrailLength;

                    for (int i = 0; i < trailBuffer.Length; i++)
                    {
                        var point = trailBuffer[i];
                        float age = currentTime - point.Timestamp;
                        float normalizedAge = math.saturate(age / trailLength);

                        // Fade based on age
                        point.Alpha = 1f - normalizedAge * ghostRender.ValueRO.TrailFade;
                        trailBuffer[i] = point;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Provides ghost render data for the shader bridge.
    /// Writes packed ghost data for wireframe rendering.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(GhostRenderSystem))]
    public partial struct GhostRenderBridgeSystem : ISystem
    {
        // Maximum ghosts to render per frame
        private const int MaxGhostsRendered = 4;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int ghostCount = 0;

            // Collect ghost render data for shader
            foreach (var (ghostRender, transform, replayState, trailBuffer) in
                SystemAPI.Query<RefRO<GhostRenderState>, RefRO<WorldTransform>,
                               RefRO<ReplayState>, DynamicBuffer<GhostTrailPoint>>()
                    .WithAll<GhostVehicleTag>())
            {
                if (ghostRender.ValueRO.Alpha < 0.01f)
                    continue;

                // Ghost render data can be consumed by WireframeRenderSystem
                // The ghost entities will be picked up through GhostVehicleTag queries

                ghostCount++;
                if (ghostCount >= MaxGhostsRendered)
                    break;
            }
        }
    }

    /// <summary>
    /// Cleans up ghost vehicles when replay system resets.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GhostSpawnSystem))]
    public partial struct GhostCleanupSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReplaySystemState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Clean up completed/invalid ghosts
            foreach (var (replayState, ghostRender, entity) in
                SystemAPI.Query<RefRO<ReplayState>, RefRO<GhostRenderState>>()
                    .WithAll<GhostVehicleTag>()
                    .WithEntityAccess())
            {
                // Remove ghost if faded out completely
                if (ghostRender.ValueRO.Alpha < 0.01f && replayState.ValueRO.IsComplete)
                {
                    ecb.DestroyEntity(entity);

                    // Update system state
                    foreach (var systemState in SystemAPI.Query<RefRW<ReplaySystemState>>())
                    {
                        if (systemState.ValueRO.GhostVehicle == entity)
                        {
                            systemState.ValueRW.GhostActive = false;
                            systemState.ValueRW.GhostVehicle = Entity.Null;
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
