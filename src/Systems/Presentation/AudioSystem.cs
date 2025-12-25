// ============================================================================
// Nightflow - Audio System
// Execution Order: 4 (Presentation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages audio parameters for engine, collision, and ambient sounds.
    /// Calculates pitch, volume, and spatial positioning.
    ///
    /// From spec:
    /// - Engine: Pitch/load blend based on speed
    /// - Tires: Slip/skid layer
    /// - Wind: Rush intensity ∝ speed²
    /// - Environment: Reverb zones (tunnel boomy, overpass ringing, open dry)
    /// - Doppler: Exaggerated for sirens and passing traffic
    ///
    /// Note: Actual audio playback handled by MonoBehaviour bridge.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(LightingSystem))]
    public partial struct AudioSystem : ISystem
    {
        // Engine audio parameters
        private const float EngineMinPitch = 0.8f;
        private const float EngineMaxPitch = 2.0f;
        private const float EngineMinRPM = 1000f;
        private const float EngineMaxRPM = 8000f;
        private const float ThrottleVolumeBoost = 0.3f;
        private const float DamageDetuneMax = 0.15f;

        // Collision audio
        private const float ImpactVolumeScale = 0.5f;
        private const float MinImpactVolume = 0.1f;
        private const float MaxImpactVolume = 1f;

        // Wind audio (intensity ∝ speed²)
        private const float WindVolumeScale = 0.0002f;    // Volume = speed² × scale
        private const float WindPitchBase = 0.6f;
        private const float WindPitchScale = 0.01f;
        private const float WindMaxVolume = 0.8f;

        // Doppler effect
        private const float DopplerExaggeration = 2.0f;   // Exaggerated for sirens

        // Reverb zones
        private const float TunnelReverbMix = 0.7f;
        private const float TunnelReverbDecay = 2.5f;
        private const float OverpassReverbMix = 0.4f;
        private const float OverpassReverbDecay = 1.2f;
        private const float OpenRoadReverbMix = 0.1f;
        private const float OpenRoadReverbDecay = 0.3f;

        // Ambient/Music
        private const float MusicIntensitySmoothing = 2f;
        private const float AudioParamSmoothing = 8f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get player position for spatial audio
            float3 playerPos = float3.zero;
            float playerSpeed = 0f;
            float totalDamage = 0f;

            foreach (var (transform, velocity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>>()
                    .WithAll<PlayerVehicleTag>())
            {
                playerPos = transform.ValueRO.Position;
                playerSpeed = velocity.ValueRO.Forward;
                break;
            }

            foreach (var damage in SystemAPI.Query<RefRO<DamageState>>().WithAll<PlayerVehicleTag>())
            {
                totalDamage = damage.ValueRO.Total;
                break;
            }

            // =============================================================
            // Update AudioState Singleton
            // =============================================================

            foreach (var audioState in SystemAPI.Query<RefRW<AudioState>>())
            {
                // =============================================================
                // Player Engine Audio
                // =============================================================

                foreach (var (velocity, input, driftState) in
                    SystemAPI.Query<RefRO<Velocity>, RefRO<PlayerInput>, RefRO<DriftState>>()
                        .WithAll<PlayerVehicleTag>())
                {
                    // Calculate RPM from speed (simplified)
                    float speed = velocity.ValueRO.Forward;
                    float rpmNorm = math.saturate(speed / 70f);
                    float rpm = math.lerp(EngineMinRPM, EngineMaxRPM, rpmNorm);

                    // Pitch from RPM
                    float pitch = math.lerp(EngineMinPitch, EngineMaxPitch, rpmNorm);

                    // Boost pitch during drift (revving effect)
                    if (driftState.ValueRO.IsDrifting)
                    {
                        pitch *= 1.1f;
                    }

                    // Damage detune (engine sounds sick when damaged)
                    float damageNorm = math.saturate(totalDamage / 100f);
                    pitch *= (1f - damageNorm * DamageDetuneMax);

                    // Volume from throttle
                    float volume = 0.5f + input.ValueRO.Throttle * ThrottleVolumeBoost;

                    // Engine load (affects sound character)
                    float load = input.ValueRO.Throttle * (1f + damageNorm * 0.3f);

                    // Smooth update
                    audioState.ValueRW.EnginePitch = math.lerp(audioState.ValueRO.EnginePitch, pitch, AudioParamSmoothing * deltaTime);
                    audioState.ValueRW.EngineVolume = math.lerp(audioState.ValueRO.EngineVolume, volume, AudioParamSmoothing * deltaTime);
                    audioState.ValueRW.EngineRPM = rpm;
                    audioState.ValueRW.EngineLoad = load;
                    break;
                }

                // =============================================================
                // Wind Audio (intensity ∝ speed²)
                // =============================================================

                float windVolume = math.min(playerSpeed * playerSpeed * WindVolumeScale, WindMaxVolume);
                float windPitch = WindPitchBase + playerSpeed * WindPitchScale;

                audioState.ValueRW.WindVolume = math.lerp(audioState.ValueRO.WindVolume, windVolume, AudioParamSmoothing * deltaTime);
                audioState.ValueRW.WindPitch = math.lerp(audioState.ValueRO.WindPitch, windPitch, AudioParamSmoothing * deltaTime);

                // =============================================================
                // Collision Impact Audio
                // =============================================================

                audioState.ValueRW.ImpactTriggered = false;

                foreach (var (impulse, transform) in
                    SystemAPI.Query<RefRO<ImpulseData>, RefRO<WorldTransform>>()
                        .WithAll<PlayerVehicleTag>())
                {
                    if (impulse.ValueRO.Magnitude > 0.1f)
                    {
                        // Calculate impact volume from impulse magnitude
                        float impactVolume = impulse.ValueRO.Magnitude * ImpactVolumeScale;
                        impactVolume = math.clamp(impactVolume, MinImpactVolume, MaxImpactVolume);

                        // Calculate pitch variation based on impact
                        float impactPitch = 0.8f + math.min(impulse.ValueRO.Magnitude * 0.01f, 0.4f);

                        audioState.ValueRW.ImpactTriggered = true;
                        audioState.ValueRW.ImpactVolume = impactVolume;
                        audioState.ValueRW.ImpactPitch = impactPitch;
                        audioState.ValueRW.ImpactPosition = transform.ValueRO.Position;
                    }
                    break;
                }

                // =============================================================
                // Emergency Siren Audio (with exaggerated Doppler)
                // =============================================================

                audioState.ValueRW.SirenActive = false;

                foreach (var (emergencyAI, transform) in
                    SystemAPI.Query<RefRO<EmergencyAI>, RefRO<WorldTransform>>()
                        .WithAll<EmergencyVehicleTag>())
                {
                    if (!emergencyAI.ValueRO.SirenActive)
                        continue;

                    // Calculate doppler effect based on approach velocity
                    // Exaggerated per spec
                    float approachSpeed = -emergencyAI.ValueRO.ApproachDistance; // Negative = approaching
                    float dopplerShift = approachSpeed * 0.001f * DopplerExaggeration;
                    float dopplerPitch = 1f + math.clamp(dopplerShift, -0.3f, 0.3f);

                    // Volume based on distance (realistic falloff)
                    float distance = math.abs(emergencyAI.ValueRO.ApproachDistance);
                    float volume = math.saturate(1f - distance / 150f);

                    // Intensify when off-screen but approaching
                    if (distance > 50f && approachSpeed > 0)
                    {
                        volume *= 1.2f; // Slight boost for warning
                    }

                    audioState.ValueRW.SirenActive = true;
                    audioState.ValueRW.SirenPitch = dopplerPitch;
                    audioState.ValueRW.SirenVolume = volume;
                    audioState.ValueRW.SirenPosition = transform.ValueRO.Position;
                    break;
                }

                // =============================================================
                // Drift/Tire Audio
                // =============================================================

                float tireSquealVolume = 0f;
                float tireSquealPitch = 1f;

                foreach (var (driftState, velocity) in
                    SystemAPI.Query<RefRO<DriftState>, RefRO<Velocity>>()
                        .WithAll<PlayerVehicleTag>())
                {
                    if (driftState.ValueRO.IsDrifting)
                    {
                        // Squeal intensity from slip angle and speed
                        float slipAmount = math.abs(driftState.ValueRO.SlipAngle);
                        float speed = velocity.ValueRO.Forward;

                        tireSquealVolume = math.saturate(slipAmount * speed * 0.01f);
                        tireSquealPitch = 0.8f + tireSquealVolume * 0.4f;
                    }
                    break;
                }

                audioState.ValueRW.TireSquealVolume = math.lerp(audioState.ValueRO.TireSquealVolume, tireSquealVolume, AudioParamSmoothing * deltaTime);
                audioState.ValueRW.TireSquealPitch = math.lerp(audioState.ValueRO.TireSquealPitch, tireSquealPitch, AudioParamSmoothing * deltaTime);

                // =============================================================
                // Environment Reverb Zones
                // =============================================================

                // Detect current zone based on track segment type
                ReverbZoneType currentZone = ReverbZoneType.OpenRoad;
                float reverbMix = OpenRoadReverbMix;
                float reverbDecay = OpenRoadReverbDecay;

                foreach (var segment in
                    SystemAPI.Query<RefRO<TrackSegment>>()
                        .WithAll<TrackSegmentTag>())
                {
                    if (playerPos.z >= segment.ValueRO.StartZ && playerPos.z <= segment.ValueRO.EndZ)
                    {
                        // Check segment type for reverb zone
                        // Type 0 = straight, 1 = curve, 2 = tunnel, 3 = overpass
                        int segType = segment.ValueRO.Type;

                        if (segType == 2) // Tunnel
                        {
                            currentZone = ReverbZoneType.Tunnel;
                            reverbMix = TunnelReverbMix;
                            reverbDecay = TunnelReverbDecay;
                        }
                        else if (segType == 3) // Overpass
                        {
                            currentZone = ReverbZoneType.Overpass;
                            reverbMix = OverpassReverbMix;
                            reverbDecay = OverpassReverbDecay;
                        }
                        break;
                    }
                }

                audioState.ValueRW.CurrentZone = currentZone;
                audioState.ValueRW.ReverbMix = math.lerp(audioState.ValueRO.ReverbMix, reverbMix, 2f * deltaTime);
                audioState.ValueRW.ReverbDecay = math.lerp(audioState.ValueRO.ReverbDecay, reverbDecay, 2f * deltaTime);

                // =============================================================
                // Adaptive Music Intensity
                // =============================================================

                float musicIntensity = 0.5f; // Base intensity

                foreach (var (scoreSession, riskState, velocity) in
                    SystemAPI.Query<RefRO<ScoreSession>, RefRO<RiskState>, RefRO<Velocity>>()
                        .WithAll<PlayerVehicleTag>())
                {
                    // Higher intensity at high speed
                    float speedFactor = math.saturate(velocity.ValueRO.Forward / 60f);

                    // Higher intensity with risk multiplier
                    float riskFactor = riskState.ValueRO.Value;

                    // Combined intensity
                    musicIntensity = 0.3f + speedFactor * 0.4f + riskFactor * 0.3f;
                    break;
                }

                // Check for emergency vehicle proximity
                foreach (var detection in SystemAPI.Query<RefRO<EmergencyDetection>>().WithAll<PlayerVehicleTag>())
                {
                    if (detection.ValueRO.WarningActive)
                    {
                        musicIntensity = math.max(musicIntensity, 0.9f);
                    }
                    break;
                }

                audioState.ValueRW.MusicIntensity = math.lerp(audioState.ValueRO.MusicIntensity, musicIntensity, MusicIntensitySmoothing * deltaTime);
            }

            // =============================================================
            // Traffic Vehicle Spatial Audio
            // =============================================================

            foreach (var (trafficAudio, velocity, transform) in
                SystemAPI.Query<RefRW<TrafficAudioState>, RefRO<Velocity>, RefRO<WorldTransform>>()
                    .WithAll<TrafficVehicleTag>())
            {
                float3 toPlayer = playerPos - transform.ValueRO.Position;
                float distance = math.length(toPlayer);

                // Volume falloff
                float volume = math.saturate(1f - distance / 100f) * 0.3f;

                // Doppler effect for passing traffic
                float relativeVelocity = velocity.ValueRO.Forward - playerSpeed;
                float dopplerShift = relativeVelocity * 0.005f;

                trafficAudio.ValueRW.Distance = distance;
                trafficAudio.ValueRW.EngineVolume = volume;
                trafficAudio.ValueRW.EnginePitch = 1f + math.clamp(dopplerShift, -0.2f, 0.2f);
                trafficAudio.ValueRW.DopplerShift = dopplerShift;
            }
        }
    }
}
