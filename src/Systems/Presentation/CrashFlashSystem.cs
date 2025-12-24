// ============================================================================
// Nightflow - Crash Flash Effect System
// Screen overlay flash effect for impact feedback
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems.Presentation
{
    /// <summary>
    /// Manages screen flash effects during crashes and heavy impacts.
    /// Creates dramatic visual feedback with quick white flash fading to red.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct CrashFlashSystem : ISystem
    {
        // Flash timing
        private const float FlashInDuration = 0.05f;    // Quick flash in
        private const float HoldDuration = 0.08f;       // Brief hold
        private const float FadeOutDuration = 0.4f;     // Slower fade

        // Flash colors
        private static readonly float4 FlashColorWhite = new float4(1f, 1f, 1f, 1f);
        private static readonly float4 FlashColorRed = new float4(1f, 0.2f, 0.1f, 0.8f);
        private static readonly float4 FlashColorOrange = new float4(1f, 0.5f, 0f, 0.6f);

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CrashFlashEffect>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var flashEffect in SystemAPI.Query<RefRW<CrashFlashEffect>>())
            {
                if (!flashEffect.ValueRO.IsActive)
                    continue;

                UpdateFlashEffect(ref flashEffect.ValueRW, deltaTime);
            }

            // Check for crash triggers from GameState
            foreach (var (gameState, flashEffect) in
                SystemAPI.Query<RefRO<GameState>, RefRW<CrashFlashEffect>>())
            {
                // Trigger flash when entering impact phase
                if (gameState.ValueRO.CrashPhase == CrashFlowPhase.Impact &&
                    flashEffect.ValueRO.Phase == CrashFlashPhase.None)
                {
                    TriggerFlash(ref flashEffect.ValueRW, FlashType.Crash);
                }
            }
        }

        [BurstCompile]
        private void UpdateFlashEffect(ref CrashFlashEffect effect, float deltaTime)
        {
            effect.Timer += deltaTime;

            switch (effect.Phase)
            {
                case CrashFlashPhase.FlashIn:
                    // Quick ramp up to full intensity
                    float flashInProgress = math.saturate(effect.Timer / FlashInDuration);
                    effect.Intensity = EaseOutQuad(flashInProgress);

                    if (effect.Timer >= FlashInDuration)
                    {
                        effect.Phase = CrashFlashPhase.Hold;
                        effect.Timer = 0f;
                    }
                    break;

                case CrashFlashPhase.Hold:
                    // Brief hold at peak intensity
                    effect.Intensity = 1f;

                    // Color transition from white to red during hold
                    float holdProgress = math.saturate(effect.Timer / HoldDuration);
                    effect.FlashColor = math.lerp(FlashColorWhite, FlashColorRed, holdProgress);

                    if (effect.Timer >= HoldDuration)
                    {
                        effect.Phase = CrashFlashPhase.FadeOut;
                        effect.Timer = 0f;
                    }
                    break;

                case CrashFlashPhase.FadeOut:
                    // Gradual fade out
                    float fadeProgress = math.saturate(effect.Timer / FadeOutDuration);
                    effect.Intensity = 1f - EaseInQuad(fadeProgress);

                    // Continue color shift to orange then transparent
                    effect.FlashColor = math.lerp(FlashColorRed, FlashColorOrange, fadeProgress * 0.5f);
                    effect.FlashColor.w = effect.Intensity;

                    if (effect.Timer >= FadeOutDuration)
                    {
                        // Flash complete
                        effect.Phase = CrashFlashPhase.None;
                        effect.IsActive = false;
                        effect.Intensity = 0f;
                        effect.Timer = 0f;
                    }
                    break;
            }
        }

        [BurstCompile]
        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        [BurstCompile]
        private float EaseInQuad(float t)
        {
            return t * t;
        }

        /// <summary>
        /// Triggers a flash effect of the specified type.
        /// </summary>
        public static void TriggerFlash(ref CrashFlashEffect effect, FlashType type)
        {
            effect.IsActive = true;
            effect.Phase = CrashFlashPhase.FlashIn;
            effect.Timer = 0f;
            effect.Intensity = 0f;

            switch (type)
            {
                case FlashType.Crash:
                    effect.FlashColor = FlashColorWhite;
                    effect.Duration = FlashInDuration + HoldDuration + FadeOutDuration;
                    break;

                case FlashType.LightImpact:
                    effect.FlashColor = new float4(1f, 1f, 1f, 0.3f);
                    effect.Duration = 0.15f;
                    break;

                case FlashType.MediumImpact:
                    effect.FlashColor = new float4(1f, 0.8f, 0.6f, 0.6f);
                    effect.Duration = 0.25f;
                    break;

                case FlashType.Damage:
                    effect.FlashColor = FlashColorRed;
                    effect.Duration = 0.2f;
                    break;
            }
        }
    }

    /// <summary>
    /// Types of flash effects.
    /// </summary>
    public enum FlashType : byte
    {
        Crash = 0,          // Full crash - white to red
        LightImpact = 1,    // Light hit - subtle white flash
        MediumImpact = 2,   // Medium hit - orange flash
        Damage = 3          // Damage taken - red flash
    }

    /// <summary>
    /// System to trigger flash on collision impacts.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionDetectionSystem))]
    public partial struct ImpactFlashTriggerSystem : ISystem
    {
        private const float LightImpactThreshold = 10f;
        private const float MediumImpactThreshold = 30f;
        private const float HeavyImpactThreshold = 60f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CrashFlashEffect>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Process collision events for player vehicle
            foreach (var (collisionBuffer, _, flashEffect) in
                SystemAPI.Query<DynamicBuffer<CollisionEvent>, RefRO<PlayerVehicleTag>, RefRW<CrashFlashEffect>>())
            {
                for (int i = 0; i < collisionBuffer.Length; i++)
                {
                    var collision = collisionBuffer[i];

                    // Skip if already flashing
                    if (flashEffect.ValueRO.IsActive)
                        continue;

                    // Determine flash type based on impact
                    if (collision.Impulse >= HeavyImpactThreshold)
                    {
                        CrashFlashSystem.TriggerFlash(ref flashEffect.ValueRW, FlashType.MediumImpact);
                    }
                    else if (collision.Impulse >= MediumImpactThreshold)
                    {
                        CrashFlashSystem.TriggerFlash(ref flashEffect.ValueRW, FlashType.MediumImpact);
                    }
                    else if (collision.Impulse >= LightImpactThreshold)
                    {
                        CrashFlashSystem.TriggerFlash(ref flashEffect.ValueRW, FlashType.LightImpact);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Render data for the crash flash overlay shader.
    /// </summary>
    public struct CrashFlashRenderData : IComponentData
    {
        public float4 Color;
        public float Intensity;
        public float VignetteStrength;  // Stronger vignette during flash
        public float ChromaticAberration; // Slight chromatic aberration
    }
}
