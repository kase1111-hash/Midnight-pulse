// ============================================================================
// Nightflow - Collision Audio System
// Impact sounds, scrapes, and crash effects
// ============================================================================

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;

namespace Nightflow.Systems.Audio
{
    /// <summary>
    /// Processes collision events and triggers appropriate impact sounds.
    /// Handles one-shot impacts and continuous scraping sounds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CollisionAudioSystem : ISystem
    {
        // Impact thresholds
        private const float LightImpactThreshold = 5f;
        private const float MediumImpactThreshold = 20f;
        private const float HeavyImpactThreshold = 50f;
        private const float GlassShatterThreshold = 80f;

        // Scrape settings
        private const float ScrapeMinVelocity = 2f;
        private const float ScrapeFadeTime = 0.3f;

        // Cooldown to prevent sound spam
        private const float ImpactCooldown = 0.1f;
        private float lastImpactTime;

        public void OnCreate(ref SystemState state)
        {
            lastImpactTime = 0f;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Process collision audio events
            foreach (var (collisionEvents, oneShotBuffer) in
                SystemAPI.Query<DynamicBuffer<CollisionAudioEvent>, DynamicBuffer<OneShotAudioRequest>>())
            {
                for (int i = 0; i < collisionEvents.Length; i++)
                {
                    var collision = collisionEvents[i];

                    // Apply delay if specified
                    if (collision.Delay > 0f)
                    {
                        var delayed = collision;
                        delayed.Delay -= deltaTime;
                        if (delayed.Delay > 0f)
                        {
                            collisionEvents[i] = delayed;
                            continue;
                        }
                    }

                    // Check cooldown
                    if (currentTime - lastImpactTime < ImpactCooldown)
                        continue;

                    // Determine impact type if not specified
                    var audioType = collision.Type;
                    if (audioType == CollisionAudioType.None)
                    {
                        audioType = ClassifyImpact(collision.Impulse);
                    }

                    // Create one-shot audio request
                    var audioRequest = CreateImpactAudioRequest(
                        collision.Position,
                        audioType,
                        collision.Impulse
                    );

                    oneShotBuffer.Add(audioRequest);
                    lastImpactTime = currentTime;
                }

                // Clear processed events
                collisionEvents.Clear();
            }

            // Update continuous scrape sounds
            foreach (var scrapeAudio in SystemAPI.Query<RefRW<ScrapeAudio>>())
            {
                UpdateScrapeAudio(ref scrapeAudio.ValueRW, deltaTime);
            }

            // Process collision events from physics and update scrape state
            foreach (var (collisionBuffer, scrapeAudio) in
                SystemAPI.Query<DynamicBuffer<CollisionEvent>, RefRW<ScrapeAudio>>())
            {
                bool hasActiveScrape = false;
                float3 scrapePoint = float3.zero;
                float scrapeIntensity = 0f;

                for (int i = 0; i < collisionBuffer.Length; i++)
                {
                    var collision = collisionBuffer[i];

                    if (collision.Type == CollisionType.Scrape)
                    {
                        hasActiveScrape = true;
                        scrapePoint = collision.Position;
                        float velocity = math.length(collision.RelativeVelocity);
                        scrapeIntensity = math.max(scrapeIntensity,
                            math.saturate((velocity - ScrapeMinVelocity) / 20f));
                    }
                }

                if (hasActiveScrape)
                {
                    scrapeAudio.ValueRW.IsActive = true;
                    scrapeAudio.ValueRW.ContactPoint = scrapePoint;
                    scrapeAudio.ValueRW.Intensity = scrapeIntensity;
                    scrapeAudio.ValueRW.Duration += deltaTime;
                }
                else if (scrapeAudio.ValueRO.IsActive)
                {
                    // Fade out scrape
                    scrapeAudio.ValueRW.Intensity -= deltaTime / ScrapeFadeTime;
                    if (scrapeAudio.ValueRO.Intensity <= 0f)
                    {
                        scrapeAudio.ValueRW.IsActive = false;
                        scrapeAudio.ValueRW.Duration = 0f;
                    }
                }
            }
        }

        [BurstCompile]
        private CollisionAudioType ClassifyImpact(float impulse)
        {
            if (impulse >= GlassShatterThreshold)
                return CollisionAudioType.GlassShatter;
            if (impulse >= HeavyImpactThreshold)
                return CollisionAudioType.HeavyImpact;
            if (impulse >= MediumImpactThreshold)
                return CollisionAudioType.MediumImpact;
            if (impulse >= LightImpactThreshold)
                return CollisionAudioType.LightImpact;

            return CollisionAudioType.None;
        }

        [BurstCompile]
        private OneShotAudioRequest CreateImpactAudioRequest(float3 position, CollisionAudioType type, float impulse)
        {
            // Clip ID would map to actual audio clips
            int clipId = (int)type;

            // Volume scales with impact force
            float baseVolume = type switch
            {
                CollisionAudioType.LightImpact => 0.4f,
                CollisionAudioType.MediumImpact => 0.7f,
                CollisionAudioType.HeavyImpact => 1.0f,
                CollisionAudioType.GlassShatter => 0.9f,
                _ => 0.5f
            };

            float impulseMultiplier = math.saturate(impulse / HeavyImpactThreshold);
            float volume = baseVolume * (0.7f + impulseMultiplier * 0.3f);

            // Pitch variation for variety
            float pitch = 0.9f + (impulse % 10f) * 0.02f;

            return new OneShotAudioRequest
            {
                ClipID = clipId,
                Position = position,
                Volume = volume,
                Pitch = pitch,
                MinDistance = 2f,
                MaxDistance = 50f,
                Delay = 0f,
                Is3D = true
            };
        }

        [BurstCompile]
        private void UpdateScrapeAudio(ref ScrapeAudio scrape, float deltaTime)
        {
            if (!scrape.IsActive)
                return;

            // Volume based on intensity
            scrape.Volume = scrape.Intensity * 0.8f;

            // Pitch varies slightly with intensity
            scrape.Pitch = 0.9f + scrape.Intensity * 0.3f;

            // Add some variation based on duration
            float variation = math.sin(scrape.Duration * 15f) * 0.05f;
            scrape.Pitch += variation;
        }
    }

    /// <summary>
    /// Audio clip mapping for collision sounds.
    /// </summary>
    public struct CollisionAudioClipMapping : IComponentData
    {
        public int LightImpactClipStart;
        public int LightImpactClipCount;
        public int MediumImpactClipStart;
        public int MediumImpactClipCount;
        public int HeavyImpactClipStart;
        public int HeavyImpactClipCount;
        public int MetalScrapeClipID;
        public int GlassShatterClipStart;
        public int GlassShatterClipCount;
    }
}
