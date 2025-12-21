// ============================================================================
// Nightflow - World Initialization System
// Creates singleton entities for world state management
// ============================================================================

using Unity.Entities;
using Unity.Burst;
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
        }
    }
}
