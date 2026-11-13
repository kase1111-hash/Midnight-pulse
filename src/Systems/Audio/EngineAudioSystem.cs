// ============================================================================
// Nightflow - Engine Audio System
// Layered engine sounds with RPM-based pitch and volume modulation
// ============================================================================

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems.Audio
{
    /// <summary>
    /// Updates engine audio parameters based on vehicle state.
    /// Manages layered engine sounds that crossfade based on RPM.
    ///
    /// Engine layers:
    /// - Idle: 600-1500 RPM
    /// - Low: 1000-3500 RPM
    /// - Mid: 3000-5500 RPM
    /// - High: 5000-8000 RPM
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EngineAudioSystem : ISystem
    {
        // RPM ranges for each layer
        private const float IdleRPMMin = 600f;
        private const float IdleRPMMax = 1500f;
        private const float LowRPMMin = 1000f;
        private const float LowRPMMax = 3500f;
        private const float MidRPMMin = 3000f;
        private const float MidRPMMax = 5500f;
        private const float HighRPMMin = 5000f;
        private const float HighRPMMax = 8000f;

        // Pitch ranges
        private const float MinPitch = 0.8f;
        private const float MaxPitch = 2.0f;

        // RPM smoothing
        private const float RPMSmoothSpeed = 5f;

        // Gearbox: there is no top speed, so the engine climbs through the
        // gears as the momentum builds instead of sitting on the limiter.
        // Each gear sweeps ShiftDownRPM -> ShiftUpRPM over its speed band; the
        // top gear never tops out and creeps toward the redline forever.
        private const float Gear1Top = 14f;               // m/s
        private const float Gear2Top = 26f;
        private const float Gear3Top = 40f;
        private const float Gear4Top = 56f;
        private const float Gear5Top = 76f;
        private const float ShiftDownRPM = 3600f;
        private const float ShiftUpRPM = 7400f;
        private const float TopGearRPMSpan = 80f;         // m/s for the top gear to cover ~63% of its range
        private const float OffThrottleRPMShare = 0.9f;   // momentum keeps the engine loaded off the pedal

        // Tyre squeal from traction: starts as the tyres near their limit
        private const float SquealStartUsage = 0.75f;
        private const float SquealFullUsage = 1.15f;
        private const float DriftSquealPerRadian = 1.5f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AudioConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get audio config
            AudioConfig config = SystemAPI.GetSingleton<AudioConfig>();

            // Player pedal and tyre state (engine/tyre audio live on the player car)
            float throttle = 0f;
            foreach (var input in SystemAPI.Query<RefRO<PlayerInput>>().WithAll<PlayerVehicleTag>())
            {
                throttle = input.ValueRO.Throttle;
                break;
            }

            float slipRatio = 0f;
            foreach (var (traction, drift) in
                SystemAPI.Query<RefRO<TractionState>, RefRO<DriftState>>().WithAll<PlayerVehicleTag>())
            {
                float gripSqueal = math.saturate((traction.ValueRO.Usage - SquealStartUsage) /
                                                 (SquealFullUsage - SquealStartUsage));
                float driftSqueal = drift.ValueRO.IsDrifting
                    ? math.saturate(math.abs(drift.ValueRO.SlipAngle) * DriftSquealPerRadian)
                    : 0f;
                slipRatio = math.max(gripSqueal, driftSqueal);
                break;
            }

            // Update engine audio for all vehicles with engine audio
            foreach (var (engineAudio, velocity) in
                SystemAPI.Query<RefRW<EngineAudio>, RefRO<VehicleVelocity>>())
            {
                if (!engineAudio.ValueRO.IsActive)
                    continue;

                engineAudio.ValueRW.ThrottleInput = throttle;
                UpdateEngineAudio(ref engineAudio.ValueRW, velocity.ValueRO, deltaTime, config);
            }

            // Also update tire and wind audio
            foreach (var (tireAudio, velocity) in
                SystemAPI.Query<RefRW<TireAudio>, RefRO<VehicleVelocity>>())
            {
                if (!tireAudio.ValueRO.IsActive)
                    continue;

                tireAudio.ValueRW.SlipRatio = slipRatio;
                UpdateTireAudio(ref tireAudio.ValueRW, velocity.ValueRO, deltaTime);
            }

            foreach (var (windAudio, velocity) in
                SystemAPI.Query<RefRW<WindAudio>, RefRO<VehicleVelocity>>())
            {
                if (!windAudio.ValueRO.IsActive)
                    continue;

                UpdateWindAudio(ref windAudio.ValueRW, velocity.ValueRO, deltaTime);
            }
        }

        [BurstCompile]
        private void UpdateEngineAudio(ref EngineAudio engine, VehicleVelocity velocity, float deltaTime, AudioConfig config)
        {
            float speed = math.length(velocity.Linear);

            // Target RPM from the gear the speed puts the car in; throttle adds
            // the last bit of revs (off the pedal the pace still loads the engine)
            float speedRPM = GearRPM(speed);
            engine.TargetRPM = math.lerp(speedRPM * OffThrottleRPMShare, speedRPM, math.saturate(engine.ThrottleInput));

            // Smooth RPM changes (frame-rate independent); a shift drops the
            // target and the smoothing makes it sound like one
            float rpmSmoothing = config.EngineRPMSmoothing > 0 ? config.EngineRPMSmoothing : RPMSmoothSpeed;
            engine.RPM = math.lerp(engine.RPM, engine.TargetRPM, 1f - math.exp(-rpmSmoothing * deltaTime));
            engine.RPM = math.clamp(engine.RPM, IdleRPMMin, HighRPMMax);

            // Determine engine state
            engine.State = DetermineEngineState(engine.RPM, engine.ThrottleInput, speed);

            // Calculate layer volumes based on RPM crossfades
            engine.IdleVolume = CalculateLayerVolume(engine.RPM, IdleRPMMin, IdleRPMMax, true);
            engine.LowRPMVolume = CalculateLayerVolume(engine.RPM, LowRPMMin, LowRPMMax, false);
            engine.MidRPMVolume = CalculateLayerVolume(engine.RPM, MidRPMMin, MidRPMMax, false);
            engine.HighRPMVolume = CalculateLayerVolume(engine.RPM, HighRPMMin, HighRPMMax, false);

            // Apply load modulation (higher load = fuller sound)
            float loadMultiplier = 0.7f + engine.Load * 0.3f;
            engine.LowRPMVolume *= loadMultiplier;
            engine.MidRPMVolume *= loadMultiplier;
            engine.HighRPMVolume *= loadMultiplier;

            // Calculate pitch based on RPM
            float normalizedRPM = (engine.RPM - IdleRPMMin) / (HighRPMMax - IdleRPMMin);
            float pitchRange = config.EnginePitchRange > 0 ? config.EnginePitchRange : (MaxPitch - MinPitch);
            engine.CurrentPitch = engine.BasePitch * (MinPitch + normalizedRPM * pitchRange);

            // Clamp pitch
            engine.CurrentPitch = math.clamp(engine.CurrentPitch, 0.5f, 3.0f);
        }

        [BurstCompile]
        private EngineState DetermineEngineState(float rpm, float throttle, float speed)
        {
            if (rpm < IdleRPMMin + 200f && speed < 2f)
                return EngineState.Idle;

            if (rpm > HighRPMMax - 500f)
                return EngineState.Redline;

            if (throttle > 0.7f)
                return EngineState.Accelerating;

            if (throttle < 0.1f && speed > 10f)
                return EngineState.Decelerating;

            return EngineState.Cruising;
        }

        /// <summary>
        /// Engine RPM for a road speed through a five-ratio box plus an endless
        /// top gear: each gear sweeps ShiftDownRPM to ShiftUpRPM, first gear
        /// starts from idle, and the top gear keeps climbing toward the redline
        /// however fast the car goes.
        /// </summary>
        private static float GearRPM(float speed)
        {
            float v = math.max(speed, 0f);

            if (v < Gear1Top)
                return math.lerp(IdleRPMMax, ShiftUpRPM, v / Gear1Top);
            if (v < Gear2Top)
                return math.lerp(ShiftDownRPM, ShiftUpRPM, (v - Gear1Top) / (Gear2Top - Gear1Top));
            if (v < Gear3Top)
                return math.lerp(ShiftDownRPM, ShiftUpRPM, (v - Gear2Top) / (Gear3Top - Gear2Top));
            if (v < Gear4Top)
                return math.lerp(ShiftDownRPM, ShiftUpRPM, (v - Gear3Top) / (Gear4Top - Gear3Top));
            if (v < Gear5Top)
                return math.lerp(ShiftDownRPM, ShiftUpRPM, (v - Gear4Top) / (Gear5Top - Gear4Top));

            float topGear = 1f - math.exp(-(v - Gear5Top) / TopGearRPMSpan);
            return math.lerp(ShiftDownRPM, HighRPMMax, topGear);
        }

        [BurstCompile]
        private float CalculateLayerVolume(float rpm, float minRPM, float maxRPM, bool isIdle)
        {
            float midPoint = (minRPM + maxRPM) * 0.5f;
            float halfRange = (maxRPM - minRPM) * 0.5f;

            // Calculate distance from center of range
            float distance = math.abs(rpm - midPoint);
            float normalizedDist = distance / halfRange;

            // Bell curve falloff
            float volume = 1f - math.saturate(normalizedDist);
            volume = volume * volume; // Square for smoother falloff

            // Idle layer stays audible at low RPM
            if (isIdle && rpm < minRPM + 300f)
            {
                volume = math.max(volume, 0.8f);
            }

            return volume;
        }

        [BurstCompile]
        private void UpdateTireAudio(ref TireAudio tire, VehicleVelocity velocity, float deltaTime)
        {
            tire.Speed = math.length(velocity.Linear);

            // Rolling volume scales with speed
            float speedFactor = math.saturate(tire.Speed / 40f); // Max at ~144 km/h
            tire.RollVolume = speedFactor * 0.8f;

            // Skid volume based on slip
            tire.SkidVolume = math.saturate(tire.SlipRatio * 2f);

            // Per-wheel slip would come from physics
            // For now, estimate from total slip
            float slip = tire.SlipRatio;
            tire.WheelSlip = new float4(slip * 0.5f, slip * 0.5f, slip, slip);
        }

        [BurstCompile]
        private void UpdateWindAudio(ref WindAudio wind, VehicleVelocity velocity, float deltaTime)
        {
            wind.Speed = math.length(velocity.Linear);

            // Wind roars up with speed squared (aerodynamic) and keeps building
            // past ~250 km/h instead of flattening out: there is no top speed
            float speedNormalized = wind.Speed / 70f; // ~250 km/h reference
            wind.Volume = (1f - math.exp(-speedNormalized * speedNormalized)) * 0.85f;

            // Pitch increases with speed
            wind.Pitch = math.min(0.8f + speedNormalized * 0.4f, 2f);

            // Turbulence increases at high speed
            wind.TurbulenceAmount = math.min(speedNormalized * 0.2f, 0.6f);
        }
    }

    /// <summary>
    /// Applies engine audio to actual audio sources.
    /// This is a managed system that interfaces with Unity's audio.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]   // Presentation already follows Simulation
    public partial class EngineAudioOutputSystem : SystemBase
    {
        // Audio source references would be managed here
        // In a real implementation, this would update AudioSource components

        protected override void OnUpdate()
        {
            // This system would apply the calculated values to actual AudioSources
            // For now, the ECS data is prepared for a managed audio controller

            Entities
                .WithoutBurst()
                .ForEach((in EngineAudio engine, in WorldTransform transform) =>
                {
                    if (!engine.IsActive) return;

                    // In a full implementation:
                    // - Find or create AudioSource for this entity
                    // - Set idle layer volume/pitch
                    // - Set low RPM layer volume/pitch
                    // - Set mid RPM layer volume/pitch
                    // - Set high RPM layer volume/pitch
                    // - Position audio source at transform.Position

                }).Run();
        }
    }
}
