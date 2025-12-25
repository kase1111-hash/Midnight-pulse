// ============================================================================
// Nightflow - Dynamic Music System
// Intensity-based layered music with synthwave aesthetic
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems.Audio
{
    /// <summary>
    /// Manages dynamic music with intensity-based layer crossfading.
    /// Responds to gameplay events to increase/decrease musical intensity.
    ///
    /// Music layers:
    /// - Base: Always playing, foundational beat
    /// - Low intensity: Ambient pads and subtle melodies (cruising)
    /// - High intensity: Driving synths and arpeggios (boosted speed)
    /// - Stingers: One-shot accents for events
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MusicSystem : ISystem
    {
        // Intensity thresholds
        private const float LowIntensityThreshold = 0.3f;
        private const float HighIntensityThreshold = 0.7f;

        // Timing
        private const float DefaultBPM = 120f;
        private const float BeatsPerMeasure = 4f;

        // Layer fade speeds
        private const float LayerFadeSpeed = 1.5f;
        private const float IntensitySmoothSpeed = 2.0f;

        // Intensity decay
        private const float IntensityDecayRate = 0.1f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MusicState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get player state for intensity calculation
            float playerSpeed = 0f;
            float damageLevel = 0f;
            float multiplier = 1f;

            foreach (var (velocity, _) in
                SystemAPI.Query<RefRO<VehicleVelocity>, RefRO<PlayerVehicleTag>>())
            {
                playerSpeed = math.length(velocity.ValueRO.Linear) * 3.6f; // km/h
                break;
            }

            // Get UI state for damage and multiplier
            if (SystemAPI.TryGetSingleton<UIState>(out var uiState))
            {
                damageLevel = uiState.DamageTotal;
                multiplier = uiState.Multiplier;
            }

            // Process music intensity events
            foreach (var (musicState, intensityEvents) in
                SystemAPI.Query<RefRW<MusicState>, DynamicBuffer<MusicIntensityEvent>>())
            {
                // Process pending events
                float intensityBoost = 0f;
                for (int i = intensityEvents.Length - 1; i >= 0; i--)
                {
                    var evt = intensityEvents[i];
                    intensityBoost = math.max(intensityBoost, evt.IntensityDelta);

                    evt.Duration -= deltaTime;
                    if (evt.Duration <= 0f)
                    {
                        intensityEvents.RemoveAt(i);
                    }
                    else
                    {
                        intensityEvents[i] = evt;
                    }
                }

                // Calculate base intensity from gameplay state
                float baseIntensity = CalculateBaseIntensity(playerSpeed, damageLevel, multiplier);

                // Combine with event boosts
                musicState.ValueRW.TargetIntensity = math.saturate(baseIntensity + intensityBoost);

                // Update music state
                UpdateMusicState(ref musicState.ValueRW, deltaTime);
            }
        }

        [BurstCompile]
        private float CalculateBaseIntensity(float speed, float damage, float multiplier)
        {
            // Speed contribution (0-1 based on 80-200 km/h range)
            float speedIntensity = math.saturate((speed - 80f) / 120f) * 0.5f;

            // Multiplier contribution
            float multiplierIntensity = math.saturate((multiplier - 1f) / 3f) * 0.3f;

            // Damage adds urgency
            float damageIntensity = damage * 0.2f;

            return speedIntensity + multiplierIntensity + damageIntensity;
        }

        [BurstCompile]
        private void UpdateMusicState(ref MusicState music, float deltaTime)
        {
            if (!music.IsPlaying)
                return;

            // Smooth intensity changes
            float smoothing = music.IntensitySmoothing > 0 ? music.IntensitySmoothing : IntensitySmoothSpeed;
            music.Intensity = math.lerp(music.Intensity, music.TargetIntensity, deltaTime * smoothing);

            // Natural intensity decay
            music.TargetIntensity = math.max(0f, music.TargetIntensity - IntensityDecayRate * deltaTime);

            // Update beat timing
            float bpm = music.BPM > 0 ? music.BPM : DefaultBPM;
            float beatsPerSecond = bpm / 60f;
            music.CurrentBeat += beatsPerSecond * deltaTime;
            music.MeasurePosition = math.fmod(music.CurrentBeat, BeatsPerMeasure) / BeatsPerMeasure;

            // Calculate layer volumes
            music.BaseLayerVolume = 1.0f; // Always full

            // Low intensity layer: full at low intensity, fades at high
            music.LowIntensityVolume = math.saturate(1f - (music.Intensity - LowIntensityThreshold) / 0.4f);
            music.LowIntensityVolume = math.max(0.2f, music.LowIntensityVolume); // Never fully silent

            // High intensity layer: fades in at high intensity
            music.HighIntensityVolume = math.saturate((music.Intensity - HighIntensityThreshold) / 0.3f);

            // Process transitions
            if (music.PendingTransition != MusicTransition.None)
            {
                ProcessTransition(ref music, deltaTime);
            }
        }

        [BurstCompile]
        private void ProcessTransition(ref MusicState music, float deltaTime)
        {
            switch (music.PendingTransition)
            {
                case MusicTransition.Crossfade:
                    music.TransitionProgress += deltaTime / 2f; // 2 second crossfade
                    if (music.TransitionProgress >= 1f)
                    {
                        music.PendingTransition = MusicTransition.None;
                        music.TransitionProgress = 0f;
                    }
                    break;

                case MusicTransition.BeatSync:
                    // Wait for next beat
                    if (music.MeasurePosition < 0.1f && music.TransitionProgress > 0.5f)
                    {
                        music.PendingTransition = MusicTransition.None;
                        music.TransitionProgress = 0f;
                    }
                    music.TransitionProgress += deltaTime;
                    break;

                case MusicTransition.FadeOut:
                    music.TransitionProgress += deltaTime / 3f; // 3 second fade
                    music.BaseLayerVolume = 1f - music.TransitionProgress;
                    music.LowIntensityVolume *= (1f - music.TransitionProgress);
                    music.HighIntensityVolume *= (1f - music.TransitionProgress);

                    if (music.TransitionProgress >= 1f)
                    {
                        music.IsPlaying = false;
                        music.PendingTransition = MusicTransition.None;
                    }
                    break;

                case MusicTransition.Immediate:
                    music.PendingTransition = MusicTransition.None;
                    music.TransitionProgress = 0f;
                    break;
            }
        }

        /// <summary>
        /// Triggers a music intensity boost from gameplay events.
        /// </summary>
        public static void TriggerIntensityEvent(
            ref DynamicBuffer<MusicIntensityEvent> events,
            MusicIntensityReason reason)
        {
            float intensity = reason switch
            {
                MusicIntensityReason.SpeedBoost => 0.3f,
                MusicIntensityReason.NearMiss => 0.2f,
                MusicIntensityReason.Collision => 0.4f,
                MusicIntensityReason.EmergencyClose => 0.35f,
                MusicIntensityReason.HighMultiplier => 0.25f,
                MusicIntensityReason.LowDamage => -0.1f,
                _ => 0f
            };

            float duration = reason switch
            {
                MusicIntensityReason.SpeedBoost => 5f,
                MusicIntensityReason.NearMiss => 2f,
                MusicIntensityReason.Collision => 3f,
                MusicIntensityReason.EmergencyClose => 8f,
                MusicIntensityReason.HighMultiplier => 4f,
                _ => 2f
            };

            events.Add(new MusicIntensityEvent
            {
                IntensityDelta = intensity,
                Duration = duration,
                Reason = reason
            });
        }

        /// <summary>
        /// Starts a track transition.
        /// </summary>
        public static void StartTransition(
            ref MusicState music,
            MusicTrack newTrack,
            MusicTransition transitionType)
        {
            music.CurrentTrack = newTrack;
            music.PendingTransition = transitionType;
            music.TransitionProgress = 0f;

            if (transitionType == MusicTransition.Immediate)
            {
                music.IsPlaying = newTrack != MusicTrack.None;
            }
        }
    }

    /// <summary>
    /// Triggers music events based on gameplay.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MusicSystem))]
    public partial struct MusicEventTriggerSystem : ISystem
    {
        private float lastSpeed;
        private float speedBoostCooldown;
        private float nearMissCooldown;

        private const float SpeedBoostThreshold = 180f; // km/h
        private const float SpeedBoostCooldownTime = 10f;
        private const float NearMissCooldownTime = 1f;

        public void OnCreate(ref SystemState state)
        {
            lastSpeed = 0f;
            speedBoostCooldown = 0f;
            nearMissCooldown = 0f;
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update cooldowns
            speedBoostCooldown = math.max(0f, speedBoostCooldown - deltaTime);
            nearMissCooldown = math.max(0f, nearMissCooldown - deltaTime);

            // Get player speed
            float currentSpeed = 0f;
            foreach (var (velocity, _) in
                SystemAPI.Query<RefRO<VehicleVelocity>, RefRO<PlayerVehicleTag>>())
            {
                currentSpeed = math.length(velocity.ValueRO.Linear) * 3.6f;
                break;
            }

            // Check for speed boost trigger
            if (currentSpeed > SpeedBoostThreshold && lastSpeed <= SpeedBoostThreshold && speedBoostCooldown <= 0f)
            {
                foreach (var intensityEvents in SystemAPI.Query<DynamicBuffer<MusicIntensityEvent>>())
                {
                    MusicSystem.TriggerIntensityEvent(ref intensityEvents, MusicIntensityReason.SpeedBoost);
                }
                speedBoostCooldown = SpeedBoostCooldownTime;
            }

            lastSpeed = currentSpeed;

            // Near miss events would be triggered from the scoring system
            // Emergency proximity would be triggered from the siren system
        }
    }
}
