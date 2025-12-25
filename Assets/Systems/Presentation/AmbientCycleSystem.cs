// ============================================================================
// Nightflow - Ambient Cycle System
// Handles atmospheric transitions and time-of-day progression
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages ambient atmospheric state over extended play sessions.
    /// Applies gradual visual transitions based on accumulated play time.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct AmbientCycleSystem : ISystem
    {
        // Time constants (in seconds)
        // The night is long. But not forever.
        private const double Kappa = 16200.0;      // 4.5 hours - first light
        private const double Omega = 18000.0;      // 5.0 hours - threshold
        private const double TransitionWindow = 1800.0; // 30 minutes

        // Visual parameters
        private const float MaxHorizonBlend = 0.85f;
        private const float MaxSaturationShift = 0.4f;
        private const float FogReduction = 0.6f;

        // Phase timing
        private const float TerminalFadeDuration = 8.0f;
        private const float TextRevealRate = 0.05f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AtmosphericState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Only accumulate when player is active (not crashed, not in menu)
            bool playerActive = false;
            foreach (var scoreSession in
                SystemAPI.Query<RefRO<ScoreSession>>()
                    .WithAll<PlayerVehicleTag>()
                    .WithNone<CrashedTag>())
            {
                if (scoreSession.ValueRO.Active)
                {
                    playerActive = true;
                    break;
                }
            }

            foreach (var (atmosphere, terminal) in
                SystemAPI.Query<RefRW<AtmosphericState>, RefRW<TerminalSequence>>()
                    .WithAll<AtmosphereControllerTag>())
            {
                // =============================================================
                // Accumulate Cycle Time
                // =============================================================

                if (playerActive && !terminal.ValueRO.Active)
                {
                    atmosphere.ValueRW.CycleAccumulator += deltaTime;
                }

                double t = atmosphere.ValueRO.CycleAccumulator;

                // =============================================================
                // Pre-Threshold: False Dawn
                // Something stirs on the horizon...
                // =============================================================

                if (t >= Kappa && t < Omega)
                {
                    // Gradual transition: night yields reluctantly
                    double progress = (t - Kappa) / TransitionWindow;
                    float p = (float)math.saturate(progress);

                    // Smoothstep for organic feel
                    float lambda = p * p * (3f - 2f * p);

                    atmosphere.ValueRW.HorizonBlend = lambda * MaxHorizonBlend;
                    atmosphere.ValueRW.SaturationShift = lambda * MaxSaturationShift;
                    atmosphere.ValueRW.FogDensity = 1f - (lambda * FogReduction);
                    atmosphere.ValueRW.PhaseProgress = p;

                    // The sky remembers what the road forgets
                }

                // =============================================================
                // Threshold: The Long Night Ends
                // =============================================================

                if (t >= Omega && !atmosphere.ValueRO.ThresholdReached)
                {
                    atmosphere.ValueRW.ThresholdReached = true;
                    terminal.ValueRW.Active = true;
                    terminal.ValueRW.Phase = 0;
                    terminal.ValueRW.Progress = 0f;

                    // You made it. You drove through the entire night.
                    // From midnight until dawn.
                    // The freeway remembers.
                }

                // =============================================================
                // Terminal Sequence
                // =============================================================

                if (terminal.ValueRO.Active)
                {
                    terminal.ValueRW.Progress += deltaTime;

                    // Phase 0: Initial hold (road fades to white)
                    // Phase 1: Fade in credits
                    // Phase 2: Scroll
                    // Phase 3: Final message

                    float sequenceTime = terminal.ValueRO.Progress;

                    if (terminal.ValueRO.Phase == 0)
                    {
                        // Fade to dawn light
                        float fadeProgress = sequenceTime / TerminalFadeDuration;
                        terminal.ValueRW.FadeAlpha = math.saturate(fadeProgress);

                        if (fadeProgress >= 1f)
                        {
                            terminal.ValueRW.Phase = 1;
                            terminal.ValueRW.Progress = 0f;
                        }
                    }
                    else if (terminal.ValueRO.Phase == 1)
                    {
                        // Hold on white, then begin text
                        if (sequenceTime > 2f)
                        {
                            terminal.ValueRW.TextReveal += deltaTime * TextRevealRate;
                        }

                        if (sequenceTime > 30f)
                        {
                            terminal.ValueRW.Phase = 2;
                            terminal.ValueRW.Progress = 0f;
                        }
                    }
                    else if (terminal.ValueRO.Phase == 2)
                    {
                        // Main credit roll
                        terminal.ValueRW.TextReveal += deltaTime * TextRevealRate;

                        if (sequenceTime > 60f)
                        {
                            terminal.ValueRW.Phase = 3;
                            terminal.ValueRW.Progress = 0f;
                        }
                    }
                    else if (terminal.ValueRO.Phase == 3)
                    {
                        // Final message: "Thank you for driving through the night."
                        // Then fade to title screen

                        if (sequenceTime > 10f)
                        {
                            // Reset for new game
                            terminal.ValueRW.Active = false;
                            terminal.ValueRW.FadeAlpha = 0f;
                            atmosphere.ValueRW.CycleAccumulator = 0;
                            atmosphere.ValueRW.ThresholdReached = false;
                            atmosphere.ValueRW.HorizonBlend = 0f;
                            atmosphere.ValueRW.SaturationShift = 0f;
                            atmosphere.ValueRW.FogDensity = 1f;

                            // Back to midnight. The cycle begins again.
                        }
                    }
                }
            }
        }
    }
}
