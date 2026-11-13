// ============================================================================
// Nightflow - Post-Processing Sync System
// ECS system that updates RenderState based on gameplay for post-processing
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems.Presentation
{
    /// <summary>
    /// Syncs gameplay state to RenderState for post-processing effects.
    /// Updates motion blur, chromatic aberration, and other dynamic values.
    /// The PostProcessingController MonoBehaviour reads these values.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(CrashFlashSystem))]
    public partial struct PostProcessingSyncSystem : ISystem
    {
        // Speed effects follow VehicleDynamics.SpeedFeel, which keeps building
        // with no top speed: blur starts ~80 km/h and keeps deepening to
        // ~580 km/h; chromatic fringing creeps in from ~200 km/h
        private const float MotionBlurStartFeel = 0.185f;  // SpeedFeel at ~80 km/h
        private const float ChromaticStartFeel = 0.58f;    // SpeedFeel at ~200 km/h
        private const float FullEffectFeel = 0.95f;        // SpeedFeel at ~160 m/s
        private const float BaseChromatic = 0.01f;
        private const float SpeedChromatic = 0.02f;

        // Tyres near their grip limit: the image starts to fringe
        private const float TractionChromatic = 0.015f;
        private const float TractionEffectStart = 0.85f;   // usage
        private const float TractionEffectFull = 1.3f;     // usage

        // Damage thresholds
        private const float DamageVignetteStart = 30f;      // 30% damage
        private const float CriticalDamageThreshold = 70f;  // 70% damage

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderState>();
            state.RequireForUpdate<PlayerVehicleTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get player state
            float playerSpeed = 0f;
            float playerDamage = 0f;
            float tractionUsage = 0f;

            foreach (var (velocity, damage) in
                SystemAPI.Query<RefRO<Velocity>, RefRO<DamageState>>()
                .WithAll<PlayerVehicleTag>())
            {
                playerSpeed = velocity.ValueRO.Forward;
                playerDamage = damage.ValueRO.Total;
                break;
            }

            foreach (var traction in SystemAPI.Query<RefRO<TractionState>>().WithAll<PlayerVehicleTag>())
            {
                tractionUsage = traction.ValueRO.Usage;
                break;
            }

            // Update render state
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
            foreach (var renderState in SystemAPI.Query<RefRW<RenderState>>())
            {
                UpdateSpeedEffects(ref renderState.ValueRW, playerSpeed, tractionUsage);
                UpdateDamageEffects(ref renderState.ValueRW, playerDamage, elapsedTime);
            }
        }

        [BurstCompile]
        private void UpdateSpeedEffects(ref RenderState state, float speed, float tractionUsage)
        {
            float feel = VehicleDynamics.SpeedFeel(speed);

            // Motion blur builds with speed² (on the shared speed curve)
            float blur = math.saturate((feel - MotionBlurStartFeel) / (FullEffectFeel - MotionBlurStartFeel));
            state.MotionBlurIntensity = blur * blur;

            // Chromatic aberration at high speed, plus a fringe when the tyres
            // are near their limit (the car is about to let go)
            float chroma = math.saturate((feel - ChromaticStartFeel) / (FullEffectFeel - ChromaticStartFeel));
            float edge = math.saturate((tractionUsage - TractionEffectStart) / (TractionEffectFull - TractionEffectStart));
            state.ChromaticAberration = BaseChromatic + chroma * SpeedChromatic + edge * TractionChromatic;
        }

        [BurstCompile]
        private void UpdateDamageEffects(ref RenderState state, float damage, float elapsedTime)
        {
            // Vignette pulse based on damage
            if (damage > DamageVignetteStart)
            {
                float damageRatio = math.saturate((damage - DamageVignetteStart) /
                    (100f - DamageVignetteStart));

                // Pulse faster at critical damage
                float pulseSpeed = damage > CriticalDamageThreshold ? 8f : 3f;

                // Calculate pulse using passed-in elapsed time
                float pulse = math.sin(elapsedTime * pulseSpeed) * 0.5f + 0.5f;

                state.Vignette = 0.3f + damageRatio * pulse * 0.25f;
            }
            else
            {
                state.Vignette = 0.3f; // Base vignette
            }
        }
    }

    /// <summary>
    /// Singleton component for post-processing controller to read.
    /// Contains processed effect values ready for application.
    /// </summary>
    public struct PostProcessState : IComponentData
    {
        // Bloom
        public float BloomIntensity;
        public float BloomThreshold;

        // Motion blur
        public float MotionBlurIntensity;

        // Vignette
        public float VignetteIntensity;
        public float3 VignetteColor;

        // Chromatic aberration
        public float ChromaticAberration;

        // Film grain
        public float FilmGrainIntensity;

        // Crash flash overlay
        public float4 FlashColor;
        public float FlashIntensity;

        // Lens distortion
        public float LensDistortion;

        // Color adjustments
        public float Saturation;
        public float Contrast;
        public float3 ColorFilter;

        // State flags
        public bool IsCrashing;
        public bool IsBoosting;
        public float DamageLevel;
        public float SpeedLevel;

        /// <summary>
        /// Create default post-process state.
        /// </summary>
        public static PostProcessState Default => new PostProcessState
        {
            BloomIntensity = 1.5f,
            BloomThreshold = 0.8f,
            MotionBlurIntensity = 0f,
            VignetteIntensity = 0.3f,
            VignetteColor = new float3(0f, 0f, 0f),
            ChromaticAberration = 0.01f,
            FilmGrainIntensity = 0.1f,
            FlashColor = new float4(1f, 1f, 1f, 0f),
            FlashIntensity = 0f,
            LensDistortion = 0f,
            Saturation = 1.1f,
            Contrast = 1.05f,
            ColorFilter = new float3(1f, 1f, 1f),
            IsCrashing = false,
            IsBoosting = false,
            DamageLevel = 0f,
            SpeedLevel = 0f
        };
    }

    /// <summary>
    /// System to initialize PostProcessState singleton.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PostProcessStateInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Create singleton entity with PostProcessState
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, PostProcessState.Default);
            state.EntityManager.SetName(entity, "PostProcessState");
        }

        public void OnUpdate(ref SystemState state)
        {
            // Initialization only - disable after first frame
            state.Enabled = false;
        }
    }

    /// <summary>
    /// Comprehensive post-processing state updater.
    /// Combines all game state into PostProcessState for MonoBehaviour consumption.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PostProcessingSyncSystem))]
    [UpdateAfter(typeof(CrashFlashSystem))]
    public partial struct PostProcessStateUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PostProcessState>();
            state.RequireForUpdate<PlayerVehicleTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get player state
            float speed = 0f;
            float damage = 0f;

            foreach (var (velocity, damageState) in
                SystemAPI.Query<RefRO<Velocity>, RefRO<DamageState>>()
                .WithAll<PlayerVehicleTag>())
            {
                speed = velocity.ValueRO.Forward;
                damage = damageState.ValueRO.Total;
                break;
            }

            // Get crash flash state
            float flashIntensity = 0f;
            float4 flashColor = float4.zero;
            bool isCrashing = false;

            foreach (var crashFlash in SystemAPI.Query<RefRO<CrashFlashEffect>>())
            {
                flashIntensity = crashFlash.ValueRO.Intensity;
                flashColor = crashFlash.ValueRO.FlashColor;
                isCrashing = crashFlash.ValueRO.IsActive;
                break;
            }

            // Get render state values
            float motionBlur = 0f;
            float vignette = 0.3f;
            float chromatic = 0.01f;

            foreach (var renderState in SystemAPI.Query<RefRO<RenderState>>())
            {
                motionBlur = renderState.ValueRO.MotionBlurIntensity;
                vignette = renderState.ValueRO.Vignette;
                chromatic = renderState.ValueRO.ChromaticAberration;
                break;
            }

            // Update PostProcessState singleton
            foreach (var ppState in SystemAPI.Query<RefRW<PostProcessState>>())
            {
                // Speed relative to 250 km/h, linear and not clamped (there is no
                // top speed): PostProcessingController converts it back to km/h
                ppState.ValueRW.SpeedLevel = speed / 69.4f;
                ppState.ValueRW.DamageLevel = damage / 100f;
                ppState.ValueRW.MotionBlurIntensity = motionBlur;
                ppState.ValueRW.VignetteIntensity = vignette;
                ppState.ValueRW.ChromaticAberration = chromatic;
                ppState.ValueRW.FlashIntensity = flashIntensity;
                ppState.ValueRW.FlashColor = flashColor;
                ppState.ValueRW.IsCrashing = isCrashing;

                // Lens distortion during crash
                if (isCrashing)
                {
                    ppState.ValueRW.LensDistortion = -flashIntensity * 0.15f;
                }
                else
                {
                    ppState.ValueRW.LensDistortion = 0f;
                }
            }
        }
    }
}
