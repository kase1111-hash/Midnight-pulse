// ============================================================================
// Nightflow - Raytracing System
// Dynamic reflections for headlights, emergency lights, and tunnel bounce
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages raytraced and screen-space reflections.
    ///
    /// Features:
    /// - Dynamic headlight reflections on wet roads
    /// - Emergency vehicle light bouncing (red/blue on road)
    /// - Tunnel wall/ceiling light bounce
    /// - Automatic SSR fallback for non-RT hardware
    ///
    /// From spec:
    /// - Full RT for dynamic headlight reflections
    /// - Emergency vehicle light bouncing off wet roads
    /// - Tunnel light bounce and reflections
    /// - Screen-space fallback for non-RT hardware
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(LightingSystem))]
    [UpdateBefore(typeof(WireframeRenderSystem))]
    public partial struct RaytracingSystem : ISystem
    {
        // Reflection parameters
        private const float WetRoadReflectivity = 0.7f;
        private const float DryRoadReflectivity = 0.1f;
        private const float ReflectionFalloff = 0.02f;
        private const float EmergencyReflectionRange = 50f;

        // Tunnel bounce parameters
        private const float WallBounceIntensity = 0.4f;
        private const float CeilingBounceIntensity = 0.3f;
        private const float FloorBounceIntensity = 0.6f;
        private const float BounceDecay = 0.7f;

        // SSR parameters (fallback)
        private const float SSRDefaultIntensity = 0.8f;
        private const int SSRDefaultSteps = 32;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float time = (float)SystemAPI.Time.ElapsedTime;

            // =============================================================
            // Get RT State (determines RT vs SSR path)
            // =============================================================

            bool useRT = false;
            float globalWetness = 0.3f;
            float reflectionIntensity = 1f;

            foreach (var rtState in SystemAPI.Query<RefRO<RaytracingState>>())
            {
                useRT = rtState.ValueRO.RTEnabled && !rtState.ValueRO.UseSSRFallback;
                globalWetness = rtState.ValueRO.WetnessLevel;
                reflectionIntensity = rtState.ValueRO.ReflectionIntensity;
                break;
            }

            // =============================================================
            // Get Player Position and Headlight State
            // =============================================================

            float3 playerPos = float3.zero;
            float3 playerForward = new float3(0, 0, 1);
            float3 headlightColor = new float3(1f, 0.95f, 0.8f);
            float headlightIntensity = 1f;
            float headlightRange = 60f;

            foreach (var (transform, headlight, velocity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Headlight>, RefRO<Velocity>>()
                    .WithAll<PlayerVehicleTag>())
            {
                playerPos = transform.ValueRO.Position;
                playerForward = math.mul(transform.ValueRO.Rotation, new float3(0, 0, 1));
                headlightColor = headlight.ValueRO.Color;
                headlightIntensity = headlight.ValueRO.Intensity;
                headlightRange = headlight.ValueRO.Range;
                break;
            }

            // =============================================================
            // Calculate Headlight Road Reflections
            // =============================================================

            foreach (var roadReflection in SystemAPI.Query<RefRW<RoadReflection>>())
            {
                ref var refl = ref roadReflection.ValueRW;

                // Check if player headlights are illuminating this segment
                float segmentCenter = (refl.StartZ + refl.EndZ) * 0.5f;
                float distToSegment = segmentCenter - playerPos.z;

                // Only reflect ahead of player, within headlight range
                if (distToSegment > 0 && distToSegment < headlightRange)
                {
                    // Calculate reflection intensity based on distance
                    float distFactor = 1f - (distToSegment / headlightRange);
                    distFactor = distFactor * distFactor; // Quadratic falloff

                    // Wetness affects reflectivity
                    float wetness = refl.Wetness > 0 ? refl.Wetness : globalWetness;
                    float surfaceReflectivity = math.lerp(DryRoadReflectivity, WetRoadReflectivity, wetness);

                    // Calculate headlight reflection
                    float3 reflectedColor = headlightColor * headlightIntensity *
                                           distFactor * surfaceReflectivity * reflectionIntensity;

                    refl.HeadlightReflection = reflectedColor;
                    refl.HasReflections = true;
                }
                else
                {
                    // Decay existing reflections
                    refl.HeadlightReflection *= (1f - ReflectionFalloff);
                    if (math.lengthsq(refl.HeadlightReflection) < 0.001f)
                    {
                        refl.HeadlightReflection = float3.zero;
                    }
                }

                // Update total intensity for bloom
                refl.TotalReflectionIntensity = math.length(refl.HeadlightReflection) +
                                                math.length(refl.EmergencyReflection);
                refl.HasReflections = refl.TotalReflectionIntensity > 0.01f;
            }

            // =============================================================
            // Emergency Vehicle Light Reflections
            // =============================================================

            foreach (var (emergencyRefl, transform) in
                SystemAPI.Query<RefRW<EmergencyLightReflection>, RefRO<WorldTransform>>())
            {
                ref var refl = ref emergencyRefl.ValueRW;

                // Find nearby emergency vehicles
                float closestDist = float.MaxValue;
                float3 closestPos = float3.zero;
                bool foundEmergency = false;
                float emergencyIntensity = 0f;
                bool sirenActive = false;

                foreach (var (emergencyTransform, emergencyAI, lightEmitter) in
                    SystemAPI.Query<RefRO<WorldTransform>, RefRO<EmergencyAI>, RefRO<LightEmitter>>()
                        .WithAll<EmergencyVehicleTag>())
                {
                    float3 emergencyPos = emergencyTransform.ValueRO.Position;
                    float dist = math.distance(transform.ValueRO.Position, emergencyPos);

                    if (dist < EmergencyReflectionRange && dist < closestDist)
                    {
                        closestDist = dist;
                        closestPos = emergencyPos;
                        foundEmergency = true;
                        emergencyIntensity = lightEmitter.ValueRO.Intensity;
                        sirenActive = emergencyAI.ValueRO.SirenActive;
                    }
                }

                if (foundEmergency && sirenActive)
                {
                    refl.Distance = closestDist;
                    refl.Visible = true;

                    // Flash pattern (red-blue alternating)
                    float flashFreq = 4f;
                    refl.FlashPhase = math.frac(time * flashFreq);

                    bool redPhase = refl.FlashPhase < 0.25f || (refl.FlashPhase >= 0.5f && refl.FlashPhase < 0.75f);
                    bool bluePhase = (refl.FlashPhase >= 0.25f && refl.FlashPhase < 0.5f) || refl.FlashPhase >= 0.75f;

                    // Distance falloff
                    float distFactor = 1f - (closestDist / EmergencyReflectionRange);
                    distFactor = math.max(0, distFactor);

                    // Wetness affects spread
                    refl.ReflectionSpread = globalWetness * 2f;

                    // Calculate reflection colors
                    if (redPhase)
                    {
                        refl.RedIntensity = emergencyIntensity * distFactor * reflectionIntensity;
                        refl.RedReflection = new float3(1f, 0.1f, 0.1f) * refl.RedIntensity;
                    }
                    else
                    {
                        refl.RedIntensity *= 0.7f; // Decay
                        refl.RedReflection *= 0.7f;
                    }

                    if (bluePhase)
                    {
                        refl.BlueIntensity = emergencyIntensity * distFactor * reflectionIntensity;
                        refl.BlueReflection = new float3(0.1f, 0.3f, 1f) * refl.BlueIntensity;
                    }
                    else
                    {
                        refl.BlueIntensity *= 0.7f; // Decay
                        refl.BlueReflection *= 0.7f;
                    }
                }
                else
                {
                    // Decay reflections when no emergency nearby
                    refl.RedIntensity *= 0.9f;
                    refl.BlueIntensity *= 0.9f;
                    refl.RedReflection *= 0.9f;
                    refl.BlueReflection *= 0.9f;

                    if (refl.RedIntensity < 0.01f && refl.BlueIntensity < 0.01f)
                    {
                        refl.Visible = false;
                    }
                }
            }

            // =============================================================
            // Tunnel Light Bounce
            // =============================================================

            foreach (var (tunnelRefl, tunnelLighting) in
                SystemAPI.Query<RefRW<TunnelReflection>, RefRO<TunnelLighting>>())
            {
                ref var refl = ref tunnelRefl.ValueRW;

                if (!tunnelLighting.ValueRO.IsInTunnel)
                {
                    // Decay bounce when exiting tunnel
                    refl.BounceLight *= 0.9f;
                    refl.BounceIntensity *= 0.9f;
                    continue;
                }

                float tunnelBlend = tunnelLighting.ValueRO.TunnelBlend;

                // Accumulate light bounces
                float3 accumulatedBounce = float3.zero;
                int bounceCount = 0;

                // Wall bounce from headlights
                float wallBounce = headlightIntensity * WallBounceIntensity * tunnelBlend;
                accumulatedBounce += headlightColor * wallBounce * refl.WallReflectivity;
                bounceCount++;

                // Ceiling bounce (indirect from walls)
                float ceilingBounce = wallBounce * CeilingBounceIntensity * BounceDecay;
                accumulatedBounce += headlightColor * ceilingBounce * refl.CeilingReflectivity;
                bounceCount++;

                // Floor bounce (wet surface)
                float floorBounce = headlightIntensity * FloorBounceIntensity * tunnelBlend * globalWetness;
                accumulatedBounce += headlightColor * floorBounce * refl.FloorReflectivity;
                bounceCount++;

                // Add tunnel ambient lighting contribution
                float3 tunnelAmbient = tunnelLighting.ValueRO.AmbientColor *
                                       tunnelLighting.ValueRO.AmbientIntensity;
                accumulatedBounce += tunnelAmbient * 0.3f;

                // Smooth transition
                refl.BounceLight = math.lerp(refl.BounceLight, accumulatedBounce, 0.1f);
                refl.BounceIntensity = math.length(refl.BounceLight);
                refl.BounceCount = bounceCount;
            }

            // =============================================================
            // Update RT Light Sources
            // =============================================================

            foreach (var (rtLight, lightEmitter, transform) in
                SystemAPI.Query<RefRW<RTLight>, RefRO<LightEmitter>, RefRO<WorldTransform>>())
            {
                ref var rt = ref rtLight.ValueRW;

                rt.Position = transform.ValueRO.Position;
                rt.Color = lightEmitter.ValueRO.Color;
                rt.Intensity = lightEmitter.ValueRO.Intensity;
                rt.Radius = lightEmitter.ValueRO.Radius;
                rt.CastsReflections = rt.Intensity > 0.5f; // Only bright lights cast reflections
            }

            // =============================================================
            // Update Reflection Probes
            // =============================================================

            foreach (var probe in SystemAPI.Query<RefRW<ReflectionProbe>>())
            {
                ref var p = ref probe.ValueRW;

                // Check if player is within probe radius
                float distToPlayer = math.distance(p.Position, playerPos);

                if (distToPlayer < p.Radius)
                {
                    p.BlendWeight = 1f - (distToPlayer / p.Radius);
                    p.NeedsUpdate = true;

                    // Accumulate reflection color from nearby lights
                    float3 reflColor = float3.zero;
                    float luminance = 0f;

                    foreach (var (lightEmitter, lightTransform) in
                        SystemAPI.Query<RefRO<LightEmitter>, RefRO<WorldTransform>>())
                    {
                        float distToLight = math.distance(p.Position, lightTransform.ValueRO.Position);
                        if (distToLight < lightEmitter.ValueRO.Radius)
                        {
                            float lightContrib = lightEmitter.ValueRO.Intensity *
                                                (1f - distToLight / lightEmitter.ValueRO.Radius);
                            reflColor += lightEmitter.ValueRO.Color * lightContrib;
                            luminance += lightContrib;
                        }
                    }

                    p.ReflectionColor = reflColor;
                    p.AverageLuminance = luminance;
                }
                else
                {
                    p.BlendWeight *= 0.95f; // Fade out
                    p.NeedsUpdate = false;
                }
            }

            // =============================================================
            // Update SSR State (fallback mode)
            // =============================================================

            if (!useRT)
            {
                foreach (var ssrState in SystemAPI.Query<RefRW<SSRState>>())
                {
                    ref var ssr = ref ssrState.ValueRW;
                    ssr.Enabled = true;
                    ssr.Intensity = SSRDefaultIntensity * reflectionIntensity;

                    // Adjust SSR based on wetness (more reflection = more steps needed)
                    ssr.Steps = (int)(SSRDefaultSteps * (1f + globalWetness * 0.5f));
                }
            }
        }
    }

    /// <summary>
    /// Initializes raytracing singletons on world creation.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct RaytracingInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Create RT state singleton if not exists
            if (!SystemAPI.HasSingleton<RaytracingState>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, RaytracingState.Default);
                state.EntityManager.SetName(entity, "RaytracingState");
            }

            // Create SSR state singleton if not exists
            if (!SystemAPI.HasSingleton<SSRState>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, SSRState.Default);
                state.EntityManager.SetName(entity, "SSRState");
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            // One-time init, disable after first run
            state.Enabled = false;
        }
    }
}
