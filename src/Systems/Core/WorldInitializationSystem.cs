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
        }
    }
}
