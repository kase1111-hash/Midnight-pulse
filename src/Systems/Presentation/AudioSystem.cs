// ============================================================================
// Nightflow - Audio System
// Execution Order: 4 (Presentation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Manages audio parameters for engine, collision, and ambient sounds.
    /// Calculates pitch, volume, and spatial positioning.
    /// Note: Actual audio playback handled by MonoBehaviour bridge.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(LightingSystem))]
    public partial struct AudioSystem : ISystem
    {
        // Engine audio parameters
        private const float EngineMinPitch = 0.8f;
        private const float EngineMaxPitch = 2.0f;
        private const float EngineMinRPM = 1000f;
        private const float EngineMaxRPM = 8000f;
        private const float ThrottleVolumeBoost = 0.3f;

        // Collision audio
        private const float ImpactVolumeScale = 0.5f;
        private const float MinImpactVolume = 0.1f;
        private const float MaxImpactVolume = 1f;

        // Ambient/Music
        private const float MusicIntensitySmoothing = 0.5f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // =============================================================
            // Player Engine Audio
            // =============================================================

            foreach (var (velocity, input, driftState) in
                SystemAPI.Query<RefRO<Velocity>, RefRO<PlayerInput>, RefRO<DriftState>>()
                    .WithAll<PlayerVehicleTag>())
            {
                // Calculate RPM from speed (simplified)
                float speed = velocity.ValueRO.Forward;
                float rpmNorm = math.saturate(speed / 70f);
                float rpm = math.lerp(EngineMinRPM, EngineMaxRPM, rpmNorm);

                // Pitch from RPM
                float pitch = math.lerp(EngineMinPitch, EngineMaxPitch, rpmNorm);

                // Boost pitch during drift (revving effect)
                if (driftState.ValueRO.IsDrifting)
                {
                    pitch *= 1.1f;
                }

                // Volume from throttle
                float baseVolume = 0.5f + input.ValueRO.Throttle * ThrottleVolumeBoost;

                // TODO: Write to audio parameter singleton
                // AudioParams.EnginePitch = pitch;
                // AudioParams.EngineVolume = baseVolume;
                // AudioParams.EngineRPM = rpm;
            }

            // =============================================================
            // Collision Impact Audio
            // =============================================================

            foreach (var (impulse, transform) in
                SystemAPI.Query<RefRO<ImpulseData>, RefRO<WorldTransform>>()
                    .WithAll<PlayerVehicleTag>())
            {
                if (impulse.ValueRO.Magnitude < 0.1f)
                    continue;

                // Calculate impact volume from impulse magnitude
                float impactVolume = impulse.ValueRO.Magnitude * ImpactVolumeScale;
                impactVolume = math.clamp(impactVolume, MinImpactVolume, MaxImpactVolume);

                // Calculate pitch variation based on impact direction
                float pitch = 0.8f + impulse.ValueRO.Magnitude * 0.01f;

                // TODO: Trigger impact sound
                // AudioEvents.TriggerImpact(impactVolume, pitch, transform.ValueRO.Position);
            }

            // =============================================================
            // Emergency Siren Audio
            // =============================================================

            foreach (var (emergencyAI, transform) in
                SystemAPI.Query<RefRO<EmergencyAI>, RefRO<WorldTransform>>()
                    .WithAll<EmergencyVehicleTag>())
            {
                if (!emergencyAI.ValueRO.SirenActive)
                    continue;

                // Calculate doppler-like effect based on approach speed
                float approachSpeed = -emergencyAI.ValueRO.ApproachDistance; // Negative = approaching
                float dopplerPitch = 1f + math.clamp(approachSpeed * 0.001f, -0.2f, 0.2f);

                // Volume based on distance
                float distance = math.abs(emergencyAI.ValueRO.ApproachDistance);
                float volume = math.saturate(1f - distance / 150f);

                // TODO: Update siren audio
                // AudioParams.SirenPitch = dopplerPitch;
                // AudioParams.SirenVolume = volume;
                // AudioParams.SirenPosition = transform.ValueRO.Position;
            }

            // =============================================================
            // Drift/Tire Audio
            // =============================================================

            foreach (var (driftState, velocity) in
                SystemAPI.Query<RefRO<DriftState>, RefRO<Velocity>>()
                    .WithAll<PlayerVehicleTag>())
            {
                if (!driftState.ValueRO.IsDrifting)
                {
                    // TODO: Fade out tire squeal
                    continue;
                }

                // Squeal intensity from slip angle and speed
                float slipAmount = math.abs(driftState.ValueRO.SlipAngle);
                float speed = velocity.ValueRO.Forward;

                float squealVolume = math.saturate(slipAmount * speed * 0.01f);
                float squealPitch = 0.8f + squealVolume * 0.4f;

                // TODO: Update tire squeal
                // AudioParams.TireSquealVolume = squealVolume;
                // AudioParams.TireSquealPitch = squealPitch;
            }

            // =============================================================
            // Adaptive Music Intensity
            // =============================================================

            // Calculate intensity based on game state
            float musicIntensity = 0.5f; // Base intensity

            foreach (var (scoreSession, riskState, velocity) in
                SystemAPI.Query<RefRO<ScoreSession>, RefRO<RiskState>, RefRO<Velocity>>()
                    .WithAll<PlayerVehicleTag>())
            {
                // Higher intensity at high speed
                float speedFactor = math.saturate(velocity.ValueRO.Forward / 60f);

                // Higher intensity with risk multiplier
                float riskFactor = riskState.ValueRO.Value;

                // Combined intensity
                musicIntensity = 0.3f + speedFactor * 0.4f + riskFactor * 0.3f;
            }

            // Check for emergency vehicle proximity
            foreach (var detection in SystemAPI.Query<RefRO<EmergencyDetection>>().WithAll<PlayerVehicleTag>())
            {
                if (detection.ValueRO.WarningActive)
                {
                    musicIntensity = math.max(musicIntensity, 0.9f);
                }
            }

            // TODO: Update music system
            // AudioParams.MusicIntensity = math.lerp(AudioParams.MusicIntensity, musicIntensity, MusicIntensitySmoothing * deltaTime);
        }
    }
}
