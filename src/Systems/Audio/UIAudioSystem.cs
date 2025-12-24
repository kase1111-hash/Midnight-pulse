// ============================================================================
// Nightflow - UI Audio System
// Menu sounds, score feedback, and gameplay UI audio
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;

namespace Nightflow.Systems.Audio
{
    /// <summary>
    /// Processes UI audio events and manages feedback sounds.
    /// Handles score ticks, multiplier chimes, warnings, and menu sounds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct UIAudioSystem : ISystem
    {
        // Sound timing
        private const float ScoreTickMinInterval = 0.05f;
        private const float MultiplierChimeCooldown = 0.5f;
        private const float WarningBeepInterval = 0.8f;

        // Pitch variations for variety
        private const float PitchVariationMin = 0.95f;
        private const float PitchVariationMax = 1.05f;

        private float lastScoreTickTime;
        private float lastMultiplierTime;
        private float lastWarningTime;
        private float lastScore;
        private int lastMultiplier;
        private bool wasWarning;

        public void OnCreate(ref SystemState state)
        {
            lastScoreTickTime = 0f;
            lastMultiplierTime = 0f;
            lastWarningTime = 0f;
            lastScore = 0f;
            lastMultiplier = 1;
            wasWarning = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get UI state for automatic sound triggers
            if (SystemAPI.TryGetSingleton<UIState>(out var uiState))
            {
                foreach (var uiAudioEvents in SystemAPI.Query<DynamicBuffer<UIAudioEvent>>())
                {
                    // Score tick sounds
                    if (uiState.Score > lastScore + 100f &&
                        currentTime - lastScoreTickTime > ScoreTickMinInterval)
                    {
                        // Pitch increases slightly with score rate
                        float scoreRate = (uiState.Score - lastScore) / math.max(deltaTime, 0.001f);
                        float pitch = 0.9f + math.saturate(scoreRate / 5000f) * 0.3f;

                        uiAudioEvents.Add(new UIAudioEvent
                        {
                            Type = UISoundType.ScoreTick,
                            Volume = 0.3f,
                            Pitch = pitch,
                            Delay = 0f
                        });

                        lastScoreTickTime = currentTime;
                        lastScore = uiState.Score;
                    }

                    // Multiplier change sounds
                    int currentMultiplier = (int)uiState.Multiplier;
                    if (currentMultiplier > lastMultiplier &&
                        currentTime - lastMultiplierTime > MultiplierChimeCooldown)
                    {
                        // Higher multiplier = higher pitch
                        float pitch = 0.8f + currentMultiplier * 0.1f;

                        uiAudioEvents.Add(new UIAudioEvent
                        {
                            Type = UISoundType.MultiplierUp,
                            Volume = 0.6f,
                            Pitch = math.min(pitch, 1.5f),
                            Delay = 0f
                        });

                        lastMultiplierTime = currentTime;
                    }
                    else if (currentMultiplier < lastMultiplier && lastMultiplier > 1)
                    {
                        uiAudioEvents.Add(new UIAudioEvent
                        {
                            Type = UISoundType.MultiplierLost,
                            Volume = 0.5f,
                            Pitch = 1.0f,
                            Delay = 0f
                        });
                    }
                    lastMultiplier = currentMultiplier;

                    // Damage warning beeps
                    bool isWarning = uiState.CriticalDamage || uiState.WarningPriority >= 2;
                    if (isWarning && currentTime - lastWarningTime > WarningBeepInterval)
                    {
                        float urgency = uiState.CriticalDamage ? 1.2f : 1.0f;

                        uiAudioEvents.Add(new UIAudioEvent
                        {
                            Type = UISoundType.DamageWarning,
                            Volume = 0.7f * urgency,
                            Pitch = urgency,
                            Delay = 0f
                        });

                        lastWarningTime = currentTime;
                    }
                    wasWarning = isWarning;
                }
            }

            // Process pending UI audio events
            foreach (var (uiAudioEvents, oneShotBuffer) in
                SystemAPI.Query<DynamicBuffer<UIAudioEvent>, DynamicBuffer<OneShotAudioRequest>>())
            {
                for (int i = uiAudioEvents.Length - 1; i >= 0; i--)
                {
                    var evt = uiAudioEvents[i];

                    // Handle delay
                    if (evt.Delay > 0f)
                    {
                        evt.Delay -= deltaTime;
                        uiAudioEvents[i] = evt;
                        continue;
                    }

                    // Convert to one-shot request
                    var audioRequest = CreateUIAudioRequest(evt);
                    oneShotBuffer.Add(audioRequest);

                    uiAudioEvents.RemoveAt(i);
                }
            }
        }

        [BurstCompile]
        private OneShotAudioRequest CreateUIAudioRequest(UIAudioEvent evt)
        {
            // Clip ID maps to UI sound type
            int clipId = 100 + (int)evt.Type; // Offset to avoid collision with other sounds

            return new OneShotAudioRequest
            {
                ClipID = clipId,
                Position = float3.zero, // UI sounds are 2D
                Volume = evt.Volume,
                Pitch = evt.Pitch,
                MinDistance = 0f,
                MaxDistance = 0f,
                Delay = 0f,
                Is3D = false
            };
        }
    }

    /// <summary>
    /// Triggers UI sounds from gameplay events.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct UIAudioEventSystem : ISystem
    {
        private float nearMissCooldown;

        private const float NearMissCooldownTime = 0.3f;

        public void OnCreate(ref SystemState state)
        {
            nearMissCooldown = 0f;
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            nearMissCooldown = math.max(0f, nearMissCooldown - deltaTime);

            // Near miss detection would come from scoring system
            // For now, check HUD notifications
            foreach (var (notifications, uiAudioEvents) in
                SystemAPI.Query<DynamicBuffer<HUDNotification>, DynamicBuffer<UIAudioEvent>>())
            {
                for (int i = 0; i < notifications.Length; i++)
                {
                    var notification = notifications[i];

                    if (notification.Type == HUDNotificationType.NearMiss && nearMissCooldown <= 0f)
                    {
                        uiAudioEvents.Add(new UIAudioEvent
                        {
                            Type = UISoundType.NearMiss,
                            Volume = 0.5f,
                            Pitch = 1.0f + notification.Value * 0.001f, // Slight pitch based on points
                            Delay = 0f
                        });
                        nearMissCooldown = NearMissCooldownTime;
                    }
                    else if (notification.Type == HUDNotificationType.PerfectDodge)
                    {
                        uiAudioEvents.Add(new UIAudioEvent
                        {
                            Type = UISoundType.NearMiss,
                            Volume = 0.7f,
                            Pitch = 1.3f,
                            Delay = 0f
                        });
                    }
                    else if (notification.Type == HUDNotificationType.NewHighScore)
                    {
                        uiAudioEvents.Add(new UIAudioEvent
                        {
                            Type = UISoundType.HighScore,
                            Volume = 0.8f,
                            Pitch = 1.0f,
                            Delay = 0f
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Triggers a UI sound event.
        /// </summary>
        public static void TriggerSound(
            ref DynamicBuffer<UIAudioEvent> events,
            UISoundType type,
            float volume = 1f,
            float pitch = 1f)
        {
            events.Add(new UIAudioEvent
            {
                Type = type,
                Volume = volume,
                Pitch = pitch,
                Delay = 0f
            });
        }

        /// <summary>
        /// Triggers menu sounds.
        /// </summary>
        public static void TriggerMenuSound(
            ref DynamicBuffer<UIAudioEvent> events,
            bool isSelect)
        {
            events.Add(new UIAudioEvent
            {
                Type = isSelect ? UISoundType.MenuSelect : UISoundType.MenuBack,
                Volume = 0.5f,
                Pitch = 1.0f,
                Delay = 0f
            });
        }
    }

    /// <summary>
    /// UI audio clip definitions.
    /// </summary>
    public struct UIAudioClipMapping : IComponentData
    {
        public int ScoreTickClipID;
        public int MultiplierUpClipID;
        public int MultiplierLostClipID;
        public int DamageWarningClipID;
        public int NearMissClipID;
        public int LaneChangeClipID;
        public int MenuSelectClipID;
        public int MenuBackClipID;
        public int PauseClipID;
        public int UnpauseClipID;
        public int HighScoreClipID;
        public int GameOverClipID;
    }
}
