// ============================================================================
// Nightflow - World Initialization System
// Creates singleton entities for world state management
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;

namespace Nightflow.Systems
{
    /// <summary>
    /// Initializes world singleton entities on startup.
    /// Creates atmosphere controller, track manager, and other global state.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct WorldInitializationSystem : ISystem
    {
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            _initialized = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
                return;

            _initialized = true;

            // =============================================================
            // Create Atmosphere Controller
            // Manages sky state and... other long-term transitions
            // =============================================================

            var atmosphereEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<AtmosphereControllerTag>(atmosphereEntity);
            state.EntityManager.AddComponentData(atmosphereEntity, new AtmosphericState
            {
                CycleAccumulator = 0,
                HorizonBlend = 0f,
                SaturationShift = 0f,
                FogDensity = 1f,
                ThresholdReached = false,
                PhaseProgress = 0f
            });
            state.EntityManager.AddComponentData(atmosphereEntity, new TerminalSequence
            {
                Active = false,
                Progress = 0f,
                Phase = 0,
                FadeAlpha = 0f,
                TextReveal = 0f
            });

            // The night begins at midnight.
            // It ends... when it ends.

#if UNITY_EDITOR
            state.EntityManager.SetName(atmosphereEntity, "AtmosphereController");
#endif

            // =============================================================
            // Create Audio State Singleton
            // Bridges ECS audio parameters to MonoBehaviour audio system
            // =============================================================

            var audioEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(audioEntity, new AudioState
            {
                EnginePitch = 1f,
                EngineVolume = 0.5f,
                EngineRPM = 1000f,
                EngineLoad = 0f,
                TireSquealVolume = 0f,
                TireSquealPitch = 1f,
                TireSkidVolume = 0f,
                WindVolume = 0f,
                WindPitch = 0.6f,
                ImpactTriggered = false,
                ImpactVolume = 0f,
                ImpactPitch = 1f,
                SirenVolume = 0f,
                SirenPitch = 1f,
                SirenActive = false,
                CurrentZone = ReverbZone.OpenRoad,
                ReverbMix = 0.1f,
                ReverbDecay = 0.3f,
                MusicIntensity = 0.5f,
                AmbienceVolume = 0.3f
            });

#if UNITY_EDITOR
            state.EntityManager.SetName(audioEntity, "AudioState");
#endif

            // =============================================================
            // Create Render State Singleton
            // Global rendering parameters for shader bridge
            // =============================================================

            var renderEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(renderEntity, new RenderState
            {
                // Wireframe
                WireframeThickness = 1.5f,
                WireframeGlow = 1.2f,
                EdgeIntensity = 1f,

                // Bloom
                BloomThreshold = 0.8f,
                BloomIntensity = 1.5f,
                BloomRadius = 0.02f,
                BloomSoftKnee = 0.5f,

                // Motion blur (updated by speed)
                MotionBlurIntensity = 0f,
                MotionBlurSamples = 8f,

                // Ambient
                AmbientIntensity = 0.05f,
                AmbientColor = new float3(0.1f, 0.1f, 0.2f),
                GridGlowIntensity = 0.1f,

                // City glow
                CityGlowIntensity = 0.3f,
                CityGlowColor = new float3(1f, 0.6f, 0.3f),
                HorizonGlow = 0.2f,

                // Exposure
                Exposure = 1f,
                Contrast = 1.1f,
                Saturation = 1.2f,

                // Fog
                FogDensity = 0.02f,
                FogStart = 100f,
                FogEnd = 500f,
                FogColor = new float3(0.05f, 0.05f, 0.1f),

                // Screen effects
                ChromaticAberration = 0f,
                Vignette = 0.3f,
                FilmGrain = 0.05f
            });

            state.EntityManager.AddComponentData(renderEntity, new SkyboxState
            {
                HorizonColor = new float3(0.1f, 0.05f, 0.15f),
                ZenithColor = new float3(0.02f, 0.02f, 0.05f),
                StarIntensity = 0.8f,
                MoonPhase = 0.5f,
                CloudCover = 0.2f,
                AtmosphereScatter = 0.1f
            });

#if UNITY_EDITOR
            state.EntityManager.SetName(renderEntity, "RenderState");
#endif

            // =============================================================
            // Create UI State Singleton
            // Bridges ECS data to MonoBehaviour HUD
            // =============================================================

            var uiEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(uiEntity, new UIState
            {
                SpeedKmh = 0f,
                SpeedMph = 0f,
                SpeedTier = 0,
                Score = 0f,
                DisplayScore = 0f,
                Multiplier = 1f,
                HighestMultiplier = 1f,
                MultiplierFlash = false,
                RiskValue = 0f,
                RiskCap = 1f,
                RiskPercent = 0f,
                DamageTotal = 0f,
                DamageFront = 0f,
                DamageRear = 0f,
                DamageLeft = 0f,
                DamageRight = 0f,
                DamageFlash = false,
                CriticalDamage = false,
                WarningPriority = 0,
                WarningFlash = false,
                EmergencyDistance = 0f,
                EmergencyETA = 0f,
                DistanceKm = 0f,
                TimeSurvived = 0f,
                SignalCount = 0,
                ShowPauseMenu = false,
                ShowCrashOverlay = false,
                ShowScoreSummary = false,
                OverlayAlpha = 0f
            });

#if UNITY_EDITOR
            state.EntityManager.SetName(uiEntity, "UIState");
#endif

            // =============================================================
            // Create Game State Singleton
            // Manages pause, crash flow, and autopilot activation
            // =============================================================

            var gameStateEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(gameStateEntity, new GameState
            {
                IsPaused = false,
                PauseCooldown = 0f,
                PauseCooldownMax = 5f,          // 5 seconds per spec
                CrashPhase = CrashFlowPhase.None,
                CrashPhaseTimer = 0f,
                FadeAlpha = 0f,
                AutopilotQueued = true,         // Start with autopilot
                PlayerControlActive = false,
                IdleTimer = 0f,
                CurrentMenu = MenuState.None,
                MenuVisible = false,
                TimeScale = 1f
            });

#if UNITY_EDITOR
            state.EntityManager.SetName(gameStateEntity, "GameState");
#endif
        }
    }
}
