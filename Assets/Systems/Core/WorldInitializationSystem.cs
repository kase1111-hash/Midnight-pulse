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
                CurrentZone = ReverbZoneType.OpenRoad,
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
            // Create Tunnel Lighting Singleton
            // Manages tunnel environment lighting state
            // =============================================================

            var tunnelEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(tunnelEntity, new TunnelLighting
            {
                AmbientColor = new float3(0.2f, 0.15f, 0.1f),
                AmbientIntensity = 1f,
                LightSpacing = 20f,
                LightIntensity = 0.8f,
                IsInTunnel = false,
                TunnelBlend = 0f
            });

#if UNITY_EDITOR
            state.EntityManager.SetName(tunnelEntity, "TunnelLighting");
#endif

            // NOTE: UIState and GameState singletons are created by GameBootstrapSystem
            // with proper menu-based initialization (IsPaused=true, TimeScale=0f, CurrentMenu=MainMenu)

            // =============================================================
            // Create Replay System State Singleton
            // Manages input recording and ghost playback
            // =============================================================

            var replayEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(replayEntity, new ReplaySystemState
            {
                IsRecording = false,
                RecordingTime = 0f,
                RecordingInterval = 1f / 60f,        // 60 Hz fixed timestep
                TimeSinceLastRecord = 0f,
                InputsRecorded = 0,
                MaxInputs = 1024,
                GhostVehicle = Entity.Null,
                GhostActive = false,
                CurrentSeed = (uint)System.DateTime.Now.Ticks
            });

#if UNITY_EDITOR
            state.EntityManager.SetName(replayEntity, "ReplaySystemState");
#endif
        }
    }
}
