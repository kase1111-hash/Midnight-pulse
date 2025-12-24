// ============================================================================
// Nightflow - Ambient Audio System
// Environment sounds, reverb zones, and atmospheric audio
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Nightflow.Components;

namespace Nightflow.Systems.Audio
{
    /// <summary>
    /// Manages ambient audio layers and reverb zones.
    /// Handles transitions between environments (open road, tunnel, overpass).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AmbientAudioSystem : ISystem
    {
        // Fade speeds
        private const float AmbientFadeSpeed = 1.5f;
        private const float ReverbBlendSpeed = 2.0f;

        // Default ambient volumes
        private const float OpenRoadVolume = 0.3f;
        private const float DistantTrafficVolume = 0.2f;
        private const float TunnelDroneVolume = 0.4f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AudioListener>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get listener position
            AudioListener listener = SystemAPI.GetSingleton<AudioListener>();

            // Determine current environment from reverb zones
            ReverbType currentEnvironment = ReverbType.OpenRoad;
            float maxBlend = 0f;

            // Find the dominant reverb zone
            foreach (var reverbZone in SystemAPI.Query<RefRW<ReverbZone>>())
            {
                float blend = CalculateZoneBlend(
                    listener.Position,
                    reverbZone.ValueRO.Center,
                    reverbZone.ValueRO.Size,
                    reverbZone.ValueRO.BlendDistance
                );

                reverbZone.ValueRW.CurrentBlend = math.lerp(
                    reverbZone.ValueRO.CurrentBlend,
                    blend,
                    deltaTime * ReverbBlendSpeed
                );

                if (blend > maxBlend)
                {
                    maxBlend = blend;
                    currentEnvironment = reverbZone.ValueRO.Type;
                }
            }

            // Update ambient layers based on environment
            foreach (var ambientAudio in SystemAPI.Query<RefRW<AmbientAudio>>())
            {
                UpdateAmbientLayer(
                    ref ambientAudio.ValueRW,
                    currentEnvironment,
                    maxBlend,
                    deltaTime
                );
            }
        }

        [BurstCompile]
        private float CalculateZoneBlend(float3 listenerPos, float3 zoneCenter, float3 zoneSize, float blendDistance)
        {
            // Calculate distance to zone boundary
            float3 localPos = listenerPos - zoneCenter;
            float3 halfSize = zoneSize * 0.5f;

            // Distance to each face
            float3 distToEdge = halfSize - math.abs(localPos);

            // Check if inside zone
            if (distToEdge.x < 0 || distToEdge.y < 0 || distToEdge.z < 0)
            {
                // Outside zone - calculate blend based on distance
                float3 closestPoint = math.clamp(localPos, -halfSize, halfSize);
                float distOutside = math.length(localPos - closestPoint);

                if (distOutside > blendDistance)
                    return 0f;

                return 1f - (distOutside / blendDistance);
            }

            // Inside zone
            float minDistToEdge = math.min(math.min(distToEdge.x, distToEdge.y), distToEdge.z);

            if (minDistToEdge > blendDistance)
                return 1f;

            // Blend at edges
            return minDistToEdge / blendDistance;
        }

        [BurstCompile]
        private void UpdateAmbientLayer(ref AmbientAudio ambient, ReverbType environment, float environmentBlend, float deltaTime)
        {
            // Determine target volume based on layer type and environment
            float targetVolume = GetTargetVolume(ambient.Type, environment, environmentBlend);
            ambient.TargetVolume = targetVolume;

            // Smooth volume transition
            float fadeSpeed = ambient.FadeSpeed > 0 ? ambient.FadeSpeed : AmbientFadeSpeed;
            ambient.Volume = math.lerp(ambient.Volume, targetVolume, deltaTime * fadeSpeed);

            // Activate/deactivate based on volume
            ambient.IsActive = ambient.Volume > 0.01f;
        }

        [BurstCompile]
        private float GetTargetVolume(AmbientType layerType, ReverbType environment, float blend)
        {
            return layerType switch
            {
                AmbientType.OpenRoad => environment == ReverbType.OpenRoad ?
                    OpenRoadVolume : OpenRoadVolume * (1f - blend),

                AmbientType.DistantTraffic => environment != ReverbType.Tunnel ?
                    DistantTrafficVolume : DistantTrafficVolume * (1f - blend * 0.8f),

                AmbientType.TunnelDrone => environment == ReverbType.Tunnel ?
                    TunnelDroneVolume * blend : 0f,

                AmbientType.CityAmbience => environment == ReverbType.Urban ?
                    0.25f * blend : 0.1f,

                _ => 0f
            };
        }
    }

    /// <summary>
    /// Reverb parameters for different environments.
    /// </summary>
    [BurstCompile]
    public static class ReverbPresets
    {
        /// <summary>
        /// Open road: minimal reverb, dry sound.
        /// </summary>
        public static ReverbZone OpenRoad => new ReverbZone
        {
            Type = ReverbType.OpenRoad,
            DecayTime = 0.5f,
            EarlyReflections = 0.1f,
            LateReverb = 0.05f,
            Diffusion = 0.2f,
            BlendDistance = 20f
        };

        /// <summary>
        /// Tunnel: long decay, metallic reflections.
        /// </summary>
        public static ReverbZone Tunnel => new ReverbZone
        {
            Type = ReverbType.Tunnel,
            DecayTime = 3.5f,
            EarlyReflections = 0.6f,
            LateReverb = 0.7f,
            Diffusion = 0.5f,
            BlendDistance = 15f
        };

        /// <summary>
        /// Overpass: short decay, concrete reflections.
        /// </summary>
        public static ReverbZone Overpass => new ReverbZone
        {
            Type = ReverbType.Overpass,
            DecayTime = 1.5f,
            EarlyReflections = 0.4f,
            LateReverb = 0.3f,
            Diffusion = 0.6f,
            BlendDistance = 10f
        };

        /// <summary>
        /// Urban: medium reverb, diffuse.
        /// </summary>
        public static ReverbZone Urban => new ReverbZone
        {
            Type = ReverbType.Urban,
            DecayTime = 2.0f,
            EarlyReflections = 0.3f,
            LateReverb = 0.4f,
            Diffusion = 0.8f,
            BlendDistance = 30f
        };

        /// <summary>
        /// Applies reverb preset to a zone.
        /// </summary>
        public static void ApplyPreset(ref ReverbZone zone, ReverbType type)
        {
            var preset = type switch
            {
                ReverbType.OpenRoad => OpenRoad,
                ReverbType.Tunnel => Tunnel,
                ReverbType.Overpass => Overpass,
                ReverbType.Urban => Urban,
                _ => OpenRoad
            };

            zone.Type = preset.Type;
            zone.DecayTime = preset.DecayTime;
            zone.EarlyReflections = preset.EarlyReflections;
            zone.LateReverb = preset.LateReverb;
            zone.Diffusion = preset.Diffusion;
            zone.BlendDistance = preset.BlendDistance;
        }
    }

    /// <summary>
    /// Creates reverb zones from road segment data.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AmbientAudioSystem))]
    public partial struct ReverbZoneSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // This system would create reverb zones based on road segment types
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check for new tunnel/overpass segments and create corresponding reverb zones
            // This would integrate with the road spawning system
        }
    }
}
