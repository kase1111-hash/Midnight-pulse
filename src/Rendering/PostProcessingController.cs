// ============================================================================
// Nightflow - Post-Processing Controller
// MonoBehaviour that manages Unity's Volume-based post-processing
// Dynamically adjusts effects based on ECS game state (speed, damage, crash)
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Config;
using Nightflow.Tags;
using Nightflow.Systems.Presentation;

namespace Nightflow.Rendering
{
    /// <summary>
    /// Controls post-processing effects based on game state.
    /// Creates and manages a Volume component with dynamic effect overrides.
    /// Reads base settings from VisualConfig and adjusts based on ECS state.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Volume))]
    public class PostProcessingController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private VisualConfig visualConfig;

        [Header("Effect Intensity Modifiers")]
        [Tooltip("Multiplier for speed-based motion blur")]
        [Range(0f, 2f)]
        [SerializeField] private float motionBlurSpeedScale = 1f;

        [Tooltip("Multiplier for damage vignette pulse")]
        [Range(0f, 2f)]
        [SerializeField] private float damageVignetteScale = 1f;

        [Tooltip("Multiplier for crash chromatic aberration")]
        [Range(0f, 2f)]
        [SerializeField] private float crashAberrationScale = 1f;

        [Header("Speed Thresholds (km/h)")]
        [SerializeField] private float motionBlurStartSpeed = 80f;
        [SerializeField] private float bloomBoostSpeed = 150f;
        [SerializeField] private float chromaticBoostSpeed = 200f;

        [Header("Damage Thresholds")]
        [SerializeField] private float damageVignetteStart = 0.3f;
        [SerializeField] private float criticalDamageThreshold = 0.7f;

        // Volume and effects
        private Volume _volume;
        private VolumeProfile _profile;

        // Post-processing overrides
        private Bloom _bloom;
        private Vignette _vignette;
        private ChromaticAberration _chromaticAberration;
        private MotionBlur _motionBlur;
        private FilmGrain _filmGrain;
        private ColorAdjustments _colorAdjustments;
        private LensDistortion _lensDistortion;

        [Header("ECS Integration")]
        [Tooltip("Integration mode for reading game state. ECS-driven reads from PostProcessState singleton (recommended). " +
            "Component-driven reads directly from player entity components.")]
        [SerializeField] private PostProcessIntegrationMode integrationMode = PostProcessIntegrationMode.ECSDriven;

        // ECS access
        private EntityManager _entityManager;
        private EntityQuery _renderStateQuery;
        private EntityQuery _playerStateQuery;
        private EntityQuery _crashFlashQuery;
        private EntityQuery _postProcessStateQuery;
        private bool _ecsInitialized;

        // Cached state
        private float _currentSpeed;
        private float _currentDamage;
        private float _crashIntensity;
        private float _flashIntensity;
        private float4 _flashColor;
        private bool _isCrashing;

        // Animation state
        private float _damagePulsePhase;
        private float _vignetteTarget;
        private float _vignetteCurrent;
        private float _chromaticTarget;
        private float _chromaticCurrent;

        private void Awake()
        {
            SetupVolume();
        }

        private void Start()
        {
            TryInitializeECS();
            ApplyBaseSettings();
        }

        private void OnEnable()
        {
            if (_volume != null)
            {
                _volume.enabled = true;
            }
        }

        private void OnDisable()
        {
            if (_volume != null)
            {
                _volume.enabled = false;
            }
        }

        /// <summary>
        /// Creates Volume component and sets up all post-processing overrides.
        /// </summary>
        private void SetupVolume()
        {
            _volume = GetComponent<Volume>();
            if (_volume == null)
            {
                _volume = gameObject.AddComponent<Volume>();
            }

            _volume.isGlobal = true;
            _volume.priority = 100f; // High priority for gameplay effects

            // Create runtime profile
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "NightflowPostProcess";
            _volume.profile = _profile;

            // Add all effect overrides
            _bloom = _profile.Add<Bloom>(true);
            _vignette = _profile.Add<Vignette>(true);
            _chromaticAberration = _profile.Add<ChromaticAberration>(true);
            _motionBlur = _profile.Add<MotionBlur>(true);
            _filmGrain = _profile.Add<FilmGrain>(true);
            _colorAdjustments = _profile.Add<ColorAdjustments>(true);
            _lensDistortion = _profile.Add<LensDistortion>(true);

            // Enable all overrides by default
            EnableAllOverrides();
        }

        private void EnableAllOverrides()
        {
            // Bloom
            _bloom.active = true;
            _bloom.threshold.overrideState = true;
            _bloom.intensity.overrideState = true;
            _bloom.scatter.overrideState = true;
            _bloom.tint.overrideState = true;

            // Vignette
            _vignette.active = true;
            _vignette.intensity.overrideState = true;
            _vignette.smoothness.overrideState = true;
            _vignette.color.overrideState = true;

            // Chromatic Aberration
            _chromaticAberration.active = true;
            _chromaticAberration.intensity.overrideState = true;

            // Motion Blur
            _motionBlur.active = true;
            _motionBlur.intensity.overrideState = true;
            _motionBlur.quality.overrideState = true;

            // Film Grain
            _filmGrain.active = true;
            _filmGrain.intensity.overrideState = true;
            _filmGrain.type.overrideState = true;

            // Color Adjustments
            _colorAdjustments.active = true;
            _colorAdjustments.saturation.overrideState = true;
            _colorAdjustments.contrast.overrideState = true;
            _colorAdjustments.colorFilter.overrideState = true;

            // Lens Distortion (for crash effect)
            _lensDistortion.active = true;
            _lensDistortion.intensity.overrideState = true;
        }

        /// <summary>
        /// Initialize ECS queries for game state.
        /// </summary>
        private void TryInitializeECS()
        {
            if (World.DefaultGameObjectInjectionWorld == null)
                return;

            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Query for render state singleton
            _renderStateQuery = _entityManager.CreateEntityQuery(typeof(RenderState));

            // Query for player vehicle state (speed, damage)
            _playerStateQuery = _entityManager.CreateEntityQuery(
                typeof(PlayerVehicleTag),
                typeof(Velocity),
                typeof(DamageState)
            );

            // Query for crash flash effect
            _crashFlashQuery = _entityManager.CreateEntityQuery(typeof(CrashFlashEffect));

            // Query for PostProcessState singleton
            _postProcessStateQuery = _entityManager.CreateEntityQuery(typeof(PostProcessState));

            _ecsInitialized = true;
        }

        /// <summary>
        /// Apply base settings from VisualConfig.
        /// </summary>
        private void ApplyBaseSettings()
        {
            if (visualConfig == null)
                return;

            var config = visualConfig.postProcess;

            // Bloom
            _bloom.threshold.value = config.bloomThreshold;
            _bloom.intensity.value = config.bloomIntensity;
            _bloom.scatter.value = config.bloomScatter;
            _bloom.active = config.bloomEnabled;

            // Vignette
            _vignette.intensity.value = config.vignetteIntensity;
            _vignette.smoothness.value = config.vignetteSmoothness;
            _vignette.active = config.vignetteEnabled;

            // Chromatic Aberration
            _chromaticAberration.intensity.value = config.chromaticAberrationIntensity;
            _chromaticAberration.active = config.chromaticAberrationEnabled;

            // Motion Blur
            _motionBlur.intensity.value = config.motionBlurIntensity;
            _motionBlur.quality.value = GetMotionBlurQuality(config.motionBlurSamples);
            _motionBlur.active = config.motionBlurEnabled;

            // Film Grain
            _filmGrain.intensity.value = config.grainIntensity;
            _filmGrain.active = config.filmGrainEnabled;

            // Color Adjustments
            _colorAdjustments.saturation.value = (config.saturation - 1f) * 100f; // Convert to -100 to 100 range
            _colorAdjustments.contrast.value = (config.contrast - 1f) * 100f;
        }

        private MotionBlurQuality GetMotionBlurQuality(int samples)
        {
            if (samples <= 4) return MotionBlurQuality.Low;
            if (samples <= 8) return MotionBlurQuality.Medium;
            return MotionBlurQuality.High;
        }

        private void Update()
        {
            if (!_ecsInitialized)
            {
                TryInitializeECS();
                return;
            }

            // Read game state from ECS based on configured integration mode
            // Note: Only one mode is active at a time to prevent conflicting effect values
            switch (integrationMode)
            {
                case PostProcessIntegrationMode.ECSDriven:
                    // ECS-driven mode: PostProcessState singleton computed by ECS systems
                    // This is the recommended mode as it keeps logic in ECS
                    ReadFromPostProcessState();
                    break;

                case PostProcessIntegrationMode.ComponentDriven:
                    // Component-driven mode: Read directly from player entity components
                    // Useful for debugging or when PostProcessState system isn't running
                    ReadGameState();
                    UpdateSpeedEffects();
                    UpdateDamageEffects();
                    UpdateCrashEffects();
                    break;

                case PostProcessIntegrationMode.Disabled:
                    // Effects are static, only base config applied
                    break;
            }

            // Smooth transitions
            SmoothTransitions();
        }

        /// <summary>
        /// Read effect values directly from PostProcessState (ECS-driven approach).
        /// </summary>
        private void ReadFromPostProcessState()
        {
            if (_postProcessStateQuery.CalculateEntityCount() == 0) return;

            using var entities = _postProcessStateQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) return;

            var ppState = _entityManager.GetComponentData<PostProcessState>(entities[0]);

            // Apply ECS-computed values
            _currentSpeed = ppState.SpeedLevel * 250f; // Convert back to km/h
            _currentDamage = ppState.DamageLevel;
            _flashIntensity = ppState.FlashIntensity;
            _flashColor = ppState.FlashColor;
            _isCrashing = ppState.IsCrashing;

            // Apply effect values from ECS
            if (visualConfig == null) return;
            var config = visualConfig.postProcess;

            // Motion blur (scaled by ECS value)
            if (config.motionBlurEnabled)
            {
                _motionBlur.intensity.value = ppState.MotionBlurIntensity * config.motionBlurIntensity;
            }

            // Vignette from ECS
            if (config.vignetteEnabled)
            {
                _vignetteTarget = ppState.VignetteIntensity;

                // Red tint based on damage
                if (ppState.DamageLevel > 0.3f)
                {
                    float redTint = ppState.DamageLevel * 0.15f;
                    _vignette.color.value = Color.Lerp(Color.black, new Color(1f, 0.1f, 0.1f), redTint);
                }
                else
                {
                    _vignette.color.value = Color.black;
                }
            }

            // Chromatic aberration from ECS
            if (config.chromaticAberrationEnabled)
            {
                _chromaticTarget = ppState.ChromaticAberration;
            }

            // Lens distortion during crash
            _lensDistortion.intensity.value = ppState.LensDistortion;

            // Apply flash effects
            if (ppState.IsCrashing)
            {
                // Bloom spike
                _bloom.intensity.value = config.bloomIntensity + ppState.FlashIntensity * 2f;

                // Color filter from flash
                Color flashFilter = new Color(
                    ppState.FlashColor.x,
                    ppState.FlashColor.y,
                    ppState.FlashColor.z,
                    1f
                );
                Color currentFilter = Color.Lerp(Color.white, flashFilter, ppState.FlashIntensity * 0.3f);
                _colorAdjustments.colorFilter.value = currentFilter;
            }
            else
            {
                // Reset bloom
                float bloomSpeedFactor = Mathf.InverseLerp(bloomBoostSpeed, 250f, _currentSpeed);
                _bloom.intensity.value = config.bloomIntensity * (1f + bloomSpeedFactor * 0.3f);
            }
        }

        /// <summary>
        /// Read current game state from ECS components.
        /// </summary>
        private void ReadGameState()
        {
            // Read player speed and damage using EntityManager
            if (_playerStateQuery.CalculateEntityCount() > 0)
            {
                using var entities = _playerStateQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                if (entities.Length > 0)
                {
                    var entity = entities[0];

                    if (_entityManager.HasComponent<Velocity>(entity))
                    {
                        var velocity = _entityManager.GetComponentData<Velocity>(entity);
                        // Convert m/s to km/h
                        _currentSpeed = velocity.Forward * 3.6f;
                    }

                    if (_entityManager.HasComponent<DamageState>(entity))
                    {
                        var damage = _entityManager.GetComponentData<DamageState>(entity);
                        _currentDamage = damage.Total / 100f; // Normalize to 0-1
                    }
                }
            }

            // Read crash flash state using EntityManager
            if (_crashFlashQuery.CalculateEntityCount() > 0)
            {
                using var flashEntities = _crashFlashQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                if (flashEntities.Length > 0)
                {
                    var flashEffect = _entityManager.GetComponentData<CrashFlashEffect>(flashEntities[0]);
                    _flashIntensity = flashEffect.Intensity;
                    _flashColor = flashEffect.FlashColor;
                    _isCrashing = flashEffect.IsActive;
                }
            }
        }

        /// <summary>
        /// Update effects based on vehicle speed.
        /// </summary>
        private void UpdateSpeedEffects()
        {
            if (visualConfig == null) return;
            var config = visualConfig.postProcess;

            // Motion blur increases with speed
            if (config.motionBlurEnabled)
            {
                float speedFactor = Mathf.InverseLerp(
                    motionBlurStartSpeed,
                    config.motionBlurMaxSpeed,
                    _currentSpeed
                );
                float blurIntensity = Mathf.Lerp(0f, config.motionBlurIntensity, speedFactor);
                _motionBlur.intensity.value = blurIntensity * motionBlurSpeedScale;
            }

            // Bloom intensity boost at high speed
            if (config.bloomEnabled)
            {
                float bloomSpeedFactor = Mathf.InverseLerp(
                    bloomBoostSpeed,
                    config.motionBlurMaxSpeed,
                    _currentSpeed
                );
                float bloomBoost = 1f + bloomSpeedFactor * 0.3f; // Up to 30% boost
                _bloom.intensity.value = config.bloomIntensity * bloomBoost;
            }

            // Chromatic aberration at very high speed
            if (config.chromaticAberrationEnabled && _currentSpeed > chromaticBoostSpeed)
            {
                float aberrationSpeedFactor = Mathf.InverseLerp(
                    chromaticBoostSpeed,
                    config.motionBlurMaxSpeed,
                    _currentSpeed
                );
                _chromaticTarget = config.chromaticAberrationIntensity +
                    aberrationSpeedFactor * 0.02f;
            }
            else
            {
                _chromaticTarget = config.chromaticAberrationIntensity;
            }
        }

        /// <summary>
        /// Update effects based on damage state.
        /// </summary>
        private void UpdateDamageEffects()
        {
            if (visualConfig == null) return;
            var config = visualConfig.postProcess;

            if (!config.vignetteEnabled) return;

            // Vignette pulses when damaged
            if (_currentDamage > damageVignetteStart)
            {
                float damageIntensity = Mathf.InverseLerp(
                    damageVignetteStart,
                    1f,
                    _currentDamage
                );

                // Pulse faster at critical damage
                float pulseSpeed = _currentDamage > criticalDamageThreshold ? 8f : 3f;
                _damagePulsePhase += Time.deltaTime * pulseSpeed;
                float pulse = (Mathf.Sin(_damagePulsePhase) + 1f) * 0.5f;

                // Base vignette + damage pulse
                float damageVignette = damageIntensity * pulse * 0.3f * damageVignetteScale;
                _vignetteTarget = config.vignetteIntensity + damageVignette;

                // Red tint when damaged
                float redTint = damageIntensity * 0.1f;
                Color vignetteColor = Color.Lerp(Color.black,
                    new Color(1f, 0.1f, 0.1f), redTint);
                _vignette.color.value = vignetteColor;
            }
            else
            {
                _vignetteTarget = config.vignetteIntensity;
                _vignette.color.value = Color.black;
                _damagePulsePhase = 0f;
            }
        }

        /// <summary>
        /// Update effects during crash sequence.
        /// </summary>
        private void UpdateCrashEffects()
        {
            if (!_isCrashing)
            {
                // Reset crash effects
                _lensDistortion.intensity.value = 0f;
                return;
            }

            if (visualConfig == null) return;
            var particleConfig = visualConfig.particles;

            // Heavy chromatic aberration during crash
            float crashAberration = _flashIntensity *
                particleConfig.flashChromaticAberration * crashAberrationScale;
            _chromaticTarget = Mathf.Max(_chromaticTarget, crashAberration);

            // Lens distortion pulse
            float distortionPulse = _flashIntensity * -0.15f; // Slight barrel distortion
            _lensDistortion.intensity.value = distortionPulse;

            // Override vignette during flash
            float flashVignette = _flashIntensity * 0.5f;
            _vignetteTarget = Mathf.Max(_vignetteTarget, flashVignette);

            // White/red flash affects color filter
            Color flashFilter = new Color(
                _flashColor.x,
                _flashColor.y,
                _flashColor.z,
                1f
            );
            Color currentFilter = Color.Lerp(Color.white, flashFilter, _flashIntensity * 0.3f);
            _colorAdjustments.colorFilter.value = currentFilter;

            // Bloom spike during crash
            if (visualConfig.postProcess.bloomEnabled)
            {
                float crashBloom = visualConfig.postProcess.bloomIntensity +
                    _flashIntensity * 2f;
                _bloom.intensity.value = crashBloom;
            }
        }

        /// <summary>
        /// Smooth effect transitions for visual polish.
        /// </summary>
        private void SmoothTransitions()
        {
            float smoothSpeed = 8f * Time.deltaTime;

            // Smooth vignette
            _vignetteCurrent = Mathf.Lerp(_vignetteCurrent, _vignetteTarget, smoothSpeed);
            _vignette.intensity.value = _vignetteCurrent;

            // Smooth chromatic aberration
            _chromaticCurrent = Mathf.Lerp(_chromaticCurrent, _chromaticTarget, smoothSpeed);
            _chromaticAberration.intensity.value = _chromaticCurrent;

            // Reset color filter when not crashing
            if (!_isCrashing)
            {
                Color currentFilter = _colorAdjustments.colorFilter.value;
                _colorAdjustments.colorFilter.value = Color.Lerp(currentFilter, Color.white, smoothSpeed);
            }
        }

        /// <summary>
        /// Trigger a temporary boost effect (for pickups, events, etc.)
        /// </summary>
        public void TriggerBoostFlash(float intensity = 1f, float duration = 0.2f)
        {
            StartCoroutine(BoostFlashCoroutine(intensity, duration));
        }

        private System.Collections.IEnumerator BoostFlashCoroutine(float intensity, float duration)
        {
            if (visualConfig == null) yield break;

            Color boostColor = visualConfig.colorPalette.uiBoostMagenta;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float flashT = 1f - t; // Fade out

                // Boost bloom and tint
                _bloom.tint.value = Color.Lerp(Color.white, boostColor, flashT * intensity * 0.5f);
                _bloom.intensity.value = visualConfig.postProcess.bloomIntensity + flashT * intensity;

                yield return null;
            }

            // Reset
            _bloom.tint.value = Color.white;
        }

        /// <summary>
        /// Set post-processing quality level.
        /// </summary>
        public void SetQuality(PostProcessQuality quality)
        {
            switch (quality)
            {
                case PostProcessQuality.Low:
                    _motionBlur.quality.value = MotionBlurQuality.Low;
                    _filmGrain.active = false;
                    _chromaticAberration.active = false;
                    break;

                case PostProcessQuality.Medium:
                    _motionBlur.quality.value = MotionBlurQuality.Medium;
                    _filmGrain.active = true;
                    _chromaticAberration.active = true;
                    break;

                case PostProcessQuality.High:
                    _motionBlur.quality.value = MotionBlurQuality.High;
                    _filmGrain.active = true;
                    _chromaticAberration.active = true;
                    break;
            }
        }

        private void OnDestroy()
        {
            // Clean up runtime profile
            if (_profile != null)
            {
                if (Application.isPlaying)
                    Destroy(_profile);
                else
                    DestroyImmediate(_profile);
            }
        }

        // Editor helpers
        [ContextMenu("Reload Config")]
        private void ReloadConfig()
        {
            ApplyBaseSettings();
        }

        [ContextMenu("Reset Effects")]
        private void ResetEffects()
        {
            _currentSpeed = 0f;
            _currentDamage = 0f;
            _flashIntensity = 0f;
            _isCrashing = false;
            _vignetteTarget = visualConfig?.postProcess.vignetteIntensity ?? 0.3f;
            _chromaticTarget = visualConfig?.postProcess.chromaticAberrationIntensity ?? 0.01f;

            if (_lensDistortion != null)
                _lensDistortion.intensity.value = 0f;
            if (_colorAdjustments != null)
                _colorAdjustments.colorFilter.value = Color.white;
        }
    }

    /// <summary>
    /// Post-processing quality presets.
    /// </summary>
    public enum PostProcessQuality
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Integration mode for PostProcessingController.
    /// Determines how the controller reads game state to drive effects.
    /// </summary>
    public enum PostProcessIntegrationMode
    {
        /// <summary>
        /// Read effect values from PostProcessState ECS singleton.
        /// Recommended mode - keeps rendering logic in ECS systems.
        /// </summary>
        ECSDriven,

        /// <summary>
        /// Read directly from player entity components (Velocity, DamageState, etc).
        /// Use for debugging or when PostProcessState system isn't available.
        /// </summary>
        ComponentDriven,

        /// <summary>
        /// Disable dynamic effects. Only base config settings are applied.
        /// Use for performance testing or static scenes.
        /// </summary>
        Disabled
    }
}
