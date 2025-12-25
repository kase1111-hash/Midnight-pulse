// ============================================================================
// Nightflow - Game State System
// Manages pause, crash flow, and autopilot activation
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages game flow state: pause, crash sequence, autopilot.
    ///
    /// From spec:
    /// - Pause with 5-second cooldown
    /// - Crash flow: Impact → Shake → Fade → Summary → Reset → Autopilot
    /// - No loading screens
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(InputSystem))]
    public partial struct GameStateSystem : ISystem
    {
        // Crash flow timing (seconds)
        private const float ImpactDuration = 0.3f;
        private const float ShakeDuration = 0.5f;
        private const float FadeOutDuration = 0.8f;
        private const float SummaryMinDuration = 2f;
        private const float ResetDuration = 0.3f;
        private const float FadeInDuration = 0.5f;

        // Slow motion
        private const float CrashSlowMoScale = 0.3f;

        // Idle timeout for autopilot
        private const float IdleTimeoutForAutopilot = 10f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var gameState in SystemAPI.Query<RefRW<GameState>>())
            {
                // =============================================================
                // Pause Cooldown
                // =============================================================

                if (gameState.ValueRO.PauseCooldown > 0f)
                {
                    gameState.ValueRW.PauseCooldown -= deltaTime;
                    if (gameState.ValueRO.PauseCooldown < 0f)
                    {
                        gameState.ValueRW.PauseCooldown = 0f;
                    }
                }

                // =============================================================
                // Crash Flow State Machine
                // =============================================================

                if (gameState.ValueRO.CrashPhase != CrashFlowPhase.None)
                {
                    gameState.ValueRW.CrashPhaseTimer += deltaTime;

                    switch (gameState.ValueRO.CrashPhase)
                    {
                        case CrashFlowPhase.Impact:
                            // Slow motion during impact
                            gameState.ValueRW.TimeScale = CrashSlowMoScale;
                            if (gameState.ValueRO.CrashPhaseTimer >= ImpactDuration)
                            {
                                gameState.ValueRW.CrashPhase = CrashFlowPhase.ScreenShake;
                                gameState.ValueRW.CrashPhaseTimer = 0f;
                            }
                            break;

                        case CrashFlowPhase.ScreenShake:
                            // Extended shake, gradual time return
                            gameState.ValueRW.TimeScale = math.lerp(
                                CrashSlowMoScale, 1f,
                                gameState.ValueRO.CrashPhaseTimer / ShakeDuration
                            );
                            if (gameState.ValueRO.CrashPhaseTimer >= ShakeDuration)
                            {
                                gameState.ValueRW.CrashPhase = CrashFlowPhase.FadeOut;
                                gameState.ValueRW.CrashPhaseTimer = 0f;
                            }
                            break;

                        case CrashFlowPhase.FadeOut:
                            // Fade to black
                            gameState.ValueRW.TimeScale = 1f;
                            gameState.ValueRW.FadeAlpha = math.saturate(
                                gameState.ValueRO.CrashPhaseTimer / FadeOutDuration
                            );
                            if (gameState.ValueRO.CrashPhaseTimer >= FadeOutDuration)
                            {
                                gameState.ValueRW.CrashPhase = CrashFlowPhase.Summary;
                                gameState.ValueRW.CrashPhaseTimer = 0f;
                                gameState.ValueRW.FadeAlpha = 1f;
                            }
                            break;

                        case CrashFlowPhase.Summary:
                            // Score summary display (wait for input after min time)
                            // For now, auto-advance after minimum duration
                            if (gameState.ValueRO.CrashPhaseTimer >= SummaryMinDuration)
                            {
                                // Check for input to advance
                                bool inputReceived = false;
                                foreach (var input in SystemAPI.Query<RefRO<PlayerInput>>()
                                    .WithAll<PlayerVehicleTag>())
                                {
                                    inputReceived = input.ValueRO.Throttle > 0.1f ||
                                                   input.ValueRO.Brake > 0.1f;
                                    break;
                                }

                                if (inputReceived || gameState.ValueRO.CrashPhaseTimer >= SummaryMinDuration + 5f)
                                {
                                    gameState.ValueRW.CrashPhase = CrashFlowPhase.Reset;
                                    gameState.ValueRW.CrashPhaseTimer = 0f;
                                    gameState.ValueRW.AutopilotQueued = true;
                                }
                            }
                            break;

                        case CrashFlowPhase.Reset:
                            // Vehicle reset (handled by CrashSystem)
                            if (gameState.ValueRO.CrashPhaseTimer >= ResetDuration)
                            {
                                gameState.ValueRW.CrashPhase = CrashFlowPhase.FadeIn;
                                gameState.ValueRW.CrashPhaseTimer = 0f;
                            }
                            break;

                        case CrashFlowPhase.FadeIn:
                            // Fade back in, autopilot starts
                            gameState.ValueRW.FadeAlpha = 1f - math.saturate(
                                gameState.ValueRO.CrashPhaseTimer / FadeInDuration
                            );
                            if (gameState.ValueRO.CrashPhaseTimer >= FadeInDuration)
                            {
                                gameState.ValueRW.CrashPhase = CrashFlowPhase.None;
                                gameState.ValueRW.CrashPhaseTimer = 0f;
                                gameState.ValueRW.FadeAlpha = 0f;

                                // Enable autopilot
                                if (gameState.ValueRO.AutopilotQueued)
                                {
                                    foreach (var autopilot in SystemAPI.Query<RefRW<Autopilot>>()
                                        .WithAll<PlayerVehicleTag>())
                                    {
                                        autopilot.ValueRW.Enabled = true;
                                        autopilot.ValueRW.TargetSpeed = 25f; // Medium speed
                                        break;
                                    }
                                    gameState.ValueRW.AutopilotQueued = false;
                                    gameState.ValueRW.PlayerControlActive = false;
                                }
                            }
                            break;
                    }
                }
                else
                {
                    // Normal gameplay - check for crash trigger
                    foreach (var crashState in SystemAPI.Query<RefRO<CrashState>>()
                        .WithAll<PlayerVehicleTag>())
                    {
                        if (crashState.ValueRO.IsCrashed && gameState.ValueRO.CrashPhase == CrashFlowPhase.None)
                        {
                            // Start crash sequence
                            gameState.ValueRW.CrashPhase = CrashFlowPhase.Impact;
                            gameState.ValueRW.CrashPhaseTimer = 0f;
                        }
                        break;
                    }
                }

                // =============================================================
                // Player Control Detection
                // =============================================================

                // Check for player input to disable autopilot
                foreach (var (input, autopilot) in
                    SystemAPI.Query<RefRO<PlayerInput>, RefRW<Autopilot>>()
                        .WithAll<PlayerVehicleTag>())
                {
                    bool hasInput = math.abs(input.ValueRO.Steer) > 0.1f ||
                                   input.ValueRO.Throttle > 0.1f ||
                                   input.ValueRO.Brake > 0.1f ||
                                   input.ValueRO.Handbrake;

                    if (hasInput && autopilot.ValueRO.Enabled)
                    {
                        // Player taking control - disable autopilot
                        autopilot.ValueRW.Enabled = false;
                        gameState.ValueRW.PlayerControlActive = true;
                        gameState.ValueRW.IdleTimer = 0f;
                    }
                    else if (!hasInput && gameState.ValueRO.PlayerControlActive)
                    {
                        // Track idle time
                        gameState.ValueRW.IdleTimer += deltaTime;

                        // Re-enable autopilot after idle timeout
                        if (gameState.ValueRO.IdleTimer >= IdleTimeoutForAutopilot)
                        {
                            autopilot.ValueRW.Enabled = true;
                            autopilot.ValueRW.TargetSpeed = 25f;
                            gameState.ValueRW.PlayerControlActive = false;
                        }
                    }
                    break;
                }

                break;
            }
        }
    }
}
