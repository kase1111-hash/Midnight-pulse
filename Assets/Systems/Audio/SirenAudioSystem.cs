// ============================================================================
// Nightflow - Siren Audio System
// Emergency vehicle sirens with doppler effect
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Nightflow.Components;

namespace Nightflow.Systems.Audio
{
    /// <summary>
    /// Manages emergency vehicle siren audio with realistic doppler effect.
    /// Calculates pitch shift based on relative velocity between siren and listener.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SirenAudioSystem : ISystem
    {
        // Siren pattern frequencies (Hz)
        private const float PoliceWailFreq = 1.5f;      // Slow wail
        private const float AmbulanceYelpFreq = 4.0f;   // Fast yelp
        private const float FireHornFreq = 0.8f;        // Air horn blasts

        // Siren pitch ranges
        private const float PolicePitchLow = 0.8f;
        private const float PolicePitchHigh = 1.2f;
        private const float AmbulancePitchLow = 0.9f;
        private const float AmbulancePitchHigh = 1.4f;

        // Distance attenuation
        private const float MinAudibleDistance = 5f;
        private const float MaxAudibleDistance = 300f;
        private const float FadeDistance = 50f;

        // Volume settings
        private const float MaxSirenVolume = 1.0f;
        private const float VolumeFadeSpeed = 2.0f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AudioListener>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float time = (float)SystemAPI.Time.ElapsedTime;

            // Get listener position
            AudioListener listener = SystemAPI.GetSingleton<AudioListener>();
            AudioConfig config = SystemAPI.TryGetSingleton<AudioConfig>(out var cfg) ? cfg : GetDefaultConfig();

            // Update all siren audio sources
            foreach (var (sirenAudio, transform, velocity) in
                SystemAPI.Query<RefRW<SirenAudio>, RefRO<LocalTransform>, RefRO<VehicleVelocity>>())
            {
                UpdateSirenAudio(
                    ref sirenAudio.ValueRW,
                    transform.ValueRO.Position,
                    velocity.ValueRO.Linear,
                    listener,
                    config,
                    deltaTime,
                    time
                );
            }

            // Also handle sirens without velocity component (use position delta)
            foreach (var (sirenAudio, transform) in
                SystemAPI.Query<RefRW<SirenAudio>, RefRO<LocalTransform>>()
                .WithNone<VehicleVelocity>())
            {
                // Estimate velocity from position change
                float3 estimatedVelocity = (transform.ValueRO.Position - sirenAudio.ValueRO.Position) / math.max(deltaTime, 0.001f);

                UpdateSirenAudio(
                    ref sirenAudio.ValueRW,
                    transform.ValueRO.Position,
                    estimatedVelocity,
                    listener,
                    config,
                    deltaTime,
                    time
                );
            }
        }

        [BurstCompile]
        private void UpdateSirenAudio(
            ref SirenAudio siren,
            float3 position,
            float3 velocity,
            AudioListener listener,
            AudioConfig config,
            float deltaTime,
            float time)
        {
            // Update position and velocity
            siren.Position = position;
            siren.Velocity = velocity;

            // Calculate distance to listener
            float3 toListener = listener.Position - position;
            siren.Distance = math.length(toListener);

            if (!siren.IsActive || siren.Distance > MaxAudibleDistance)
            {
                // Fade out if inactive or too far
                siren.TargetVolume = 0f;
                siren.Volume = math.lerp(siren.Volume, 0f, deltaTime * VolumeFadeSpeed);
                return;
            }

            // Calculate relative velocity (positive = approaching)
            float3 direction = math.normalizesafe(toListener);
            float sirenTowardListener = math.dot(velocity, direction);
            float listenerTowardSiren = math.dot(listener.Velocity, -direction);
            siren.RelativeVelocity = sirenTowardListener + listenerTowardSiren;

            // Calculate Doppler shift
            // Doppler formula: f' = f * (c + v_listener) / (c + v_source)
            // Simplified: pitch = 1 + (relativeVelocity / speedOfSound)
            float speedOfSound = config.SpeedOfSound > 0 ? config.SpeedOfSound : 343f;
            float dopplerScale = config.DopplerScale > 0 ? config.DopplerScale : 1f;

            float dopplerShift = 1f + (siren.RelativeVelocity / speedOfSound) * dopplerScale;
            dopplerShift = math.clamp(dopplerShift, 0.5f, 2.0f); // Limit extreme shifts
            siren.DopplerShift = dopplerShift;

            // Update siren pattern phase
            float frequency = GetSirenFrequency(siren.Type);
            siren.Phase += frequency * deltaTime;
            siren.Phase = math.fmod(siren.Phase, 1f);
            siren.Frequency = frequency;

            // Calculate target volume based on distance
            float distanceAttenuation = CalculateDistanceAttenuation(siren.Distance, config);
            siren.TargetVolume = MaxSirenVolume * distanceAttenuation;

            // Smooth volume changes
            siren.Volume = math.lerp(siren.Volume, siren.TargetVolume, deltaTime * VolumeFadeSpeed);
        }

        [BurstCompile]
        private float GetSirenFrequency(SirenType type)
        {
            return type switch
            {
                SirenType.Police => PoliceWailFreq,
                SirenType.Ambulance => AmbulanceYelpFreq,
                SirenType.Fire => FireHornFreq,
                _ => PoliceWailFreq
            };
        }

        [BurstCompile]
        private float CalculateDistanceAttenuation(float distance, AudioConfig config)
        {
            if (distance < MinAudibleDistance)
                return 1f;

            float minDist = config.MinDistance > 0 ? config.MinDistance : MinAudibleDistance;
            float maxDist = config.MaxDistance > 0 ? config.MaxDistance : MaxAudibleDistance;
            float rolloff = config.RolloffFactor > 0 ? config.RolloffFactor : 1f;

            // Logarithmic rolloff
            float attenuation = minDist / (minDist + rolloff * (distance - minDist));

            // Additional fade at max distance
            if (distance > maxDist - FadeDistance)
            {
                float fadeFactor = 1f - (distance - (maxDist - FadeDistance)) / FadeDistance;
                attenuation *= math.saturate(fadeFactor);
            }

            return math.saturate(attenuation);
        }

        private AudioConfig GetDefaultConfig()
        {
            return new AudioConfig
            {
                MasterVolume = 1f,
                SFXVolume = 1f,
                DopplerScale = 1f,
                SpeedOfSound = 343f,
                MinDistance = 5f,
                MaxDistance = 300f,
                RolloffFactor = 1f
            };
        }
    }

    /// <summary>
    /// Calculates siren pitch pattern for the audio output.
    /// </summary>
    [BurstCompile]
    public static class SirenPatterns
    {
        /// <summary>
        /// Gets the current pitch multiplier for a siren based on its phase.
        /// </summary>
        public static float GetSirenPitch(SirenType type, float phase, float dopplerShift)
        {
            float basePitch = type switch
            {
                SirenType.Police => GetPoliceWailPitch(phase),
                SirenType.Ambulance => GetAmbulanceYelpPitch(phase),
                SirenType.Fire => GetFireHornPitch(phase),
                _ => 1f
            };

            return basePitch * dopplerShift;
        }

        /// <summary>
        /// Police wail: smooth sine wave between low and high pitch.
        /// </summary>
        private static float GetPoliceWailPitch(float phase)
        {
            // Smooth sine wave oscillation
            float sine = math.sin(phase * math.PI * 2f);
            return math.lerp(0.8f, 1.2f, (sine + 1f) * 0.5f);
        }

        /// <summary>
        /// Ambulance yelp: rapid alternation between two pitches.
        /// </summary>
        private static float GetAmbulanceYelpPitch(float phase)
        {
            // Sharp alternation
            float saw = math.fmod(phase * 2f, 1f);
            float pitch = saw < 0.5f ? 0.9f : 1.4f;

            // Slight smoothing at transitions
            float transition = math.abs(saw - 0.5f) * 4f;
            transition = math.saturate(transition);

            return pitch;
        }

        /// <summary>
        /// Fire horn: periodic blasts with decay.
        /// </summary>
        private static float GetFireHornPitch(float phase)
        {
            // Short blast with slight pitch drop
            float blastPhase = math.fmod(phase * 3f, 1f);

            if (blastPhase < 0.4f)
            {
                // During blast
                float decay = 1f - blastPhase * 0.2f;
                return 1.0f * decay;
            }

            // Between blasts (silence handled by volume)
            return 1.0f;
        }

        /// <summary>
        /// Gets volume envelope for fire horn blasts.
        /// </summary>
        public static float GetFireHornVolume(float phase)
        {
            float blastPhase = math.fmod(phase * 3f, 1f);

            if (blastPhase < 0.4f)
            {
                // Blast with attack and decay
                float attack = math.saturate(blastPhase * 20f);
                float decay = 1f - math.saturate((blastPhase - 0.3f) * 10f);
                return attack * decay;
            }

            return 0f;
        }
    }
}
