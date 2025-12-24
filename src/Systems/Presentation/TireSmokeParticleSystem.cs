// ============================================================================
// Nightflow - Tire Smoke Particle System
// Handles drift/skid smoke effects with volumetric appearance
// ============================================================================

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems.Presentation
{
    /// <summary>
    /// Spawns and updates tire smoke particles based on drift state.
    /// Smoke appears as billowing cyan-tinted clouds behind drifting wheels.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct TireSmokeParticleSystem : ISystem
    {
        private Random random;

        // Smoke configuration
        private const float SmokeSizeStart = 0.3f;
        private const float SmokeSizeEnd = 1.5f;
        private const float SmokeSpeedMin = 1f;
        private const float SmokeSpeedMax = 3f;
        private const float SmokeLifetime = 1.2f;
        private const float SmokeDrag = 2f;
        private const float SmokeRiseSpeed = 1.5f;
        private const float EmissionRateBase = 30f; // Particles per second at full drift

        // Wheel positions relative to vehicle center
        private static readonly float3[] WheelOffsets = new float3[]
        {
            new float3(-0.8f, 0f, 1.5f),   // Front-left
            new float3(0.8f, 0f, 1.5f),    // Front-right
            new float3(-0.8f, 0f, -1.5f),  // Rear-left
            new float3(0.8f, 0f, -1.5f)    // Rear-right
        };

        public void OnCreate(ref SystemState state)
        {
            random = Random.CreateFromIndex((uint)(System.DateTime.Now.Ticks + 1));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update smoke emitters based on drift state
            foreach (var (transform, driftState, emitter, particleBuffer) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<DriftVisualState>, RefRW<ParticleEmitter>, DynamicBuffer<Particle>>())
            {
                if (emitter.ValueRO.Type != ParticleType.TireSmoke)
                    continue;

                // Emit smoke based on drift intensity
                if (driftState.ValueRO.IsDrifting && driftState.ValueRO.DriftIntensity > 0.1f)
                {
                    EmitTireSmoke(
                        ref particleBuffer,
                        ref emitter.ValueRW,
                        transform.ValueRO,
                        driftState.ValueRO,
                        deltaTime,
                        ref random
                    );
                }
            }

            // Update existing smoke particles
            foreach (var (emitter, particleBuffer) in
                SystemAPI.Query<RefRO<ParticleEmitter>, DynamicBuffer<Particle>>())
            {
                if (emitter.ValueRO.Type != ParticleType.TireSmoke)
                    continue;

                UpdateSmokeParticles(ref particleBuffer, deltaTime);
            }

            // Process spawn requests for tire smoke
            foreach (var (spawnBuffer, emitter, particleBuffer) in
                SystemAPI.Query<DynamicBuffer<ParticleSpawnRequest>, RefRW<ParticleEmitter>, DynamicBuffer<Particle>>())
            {
                if (emitter.ValueRO.Type != ParticleType.TireSmoke)
                    continue;

                for (int i = spawnBuffer.Length - 1; i >= 0; i--)
                {
                    var request = spawnBuffer[i];
                    if (request.Type == ParticleType.TireSmoke)
                    {
                        SpawnSmokeFromRequest(ref particleBuffer, request, ref random);
                        spawnBuffer.RemoveAt(i);
                    }
                }
            }
        }

        [BurstCompile]
        private void EmitTireSmoke(
            ref DynamicBuffer<Particle> particles,
            ref ParticleEmitter emitter,
            LocalTransform transform,
            DriftVisualState driftState,
            float deltaTime,
            ref Random rng)
        {
            // Calculate emission rate based on drift intensity
            float emissionRate = EmissionRateBase * driftState.DriftIntensity;
            emitter.EmissionAccumulator += emissionRate * deltaTime;

            int particlesToSpawn = (int)emitter.EmissionAccumulator;
            emitter.EmissionAccumulator -= particlesToSpawn;

            // Limit total particles
            if (particles.Length + particlesToSpawn > emitter.MaxParticles)
            {
                particlesToSpawn = math.max(0, emitter.MaxParticles - particles.Length);
            }

            // Emit from rear wheels (where drift smoke comes from)
            for (int i = 0; i < particlesToSpawn; i++)
            {
                // Randomly pick rear-left or rear-right wheel
                int wheelIndex = rng.NextBool() ? 2 : 3;

                // Check if this wheel is slipping
                float wheelSlip = driftState.WheelSlip[wheelIndex];
                if (wheelSlip < 0.2f)
                {
                    wheelIndex = wheelSlip > driftState.WheelSlip[wheelIndex == 2 ? 3 : 2] ? wheelIndex : (wheelIndex == 2 ? 3 : 2);
                }

                // Calculate world position of wheel
                float3 localPos = WheelOffsets[wheelIndex];
                float3 worldPos = transform.Position + math.rotate(transform.Rotation, localPos);

                var smoke = CreateSmokeParticle(worldPos, transform.Rotation, driftState.DriftIntensity, ref rng);
                particles.Add(smoke);
            }
        }

        [BurstCompile]
        private void SpawnSmokeFromRequest(ref DynamicBuffer<Particle> particles, ParticleSpawnRequest request, ref Random rng)
        {
            int count = math.max(1, request.Count);

            for (int i = 0; i < count; i++)
            {
                var smoke = CreateSmokeParticle(
                    request.Position,
                    quaternion.LookRotation(request.Direction, math.up()),
                    request.Intensity,
                    ref rng
                );
                particles.Add(smoke);
            }
        }

        [BurstCompile]
        private Particle CreateSmokeParticle(float3 position, quaternion rotation, float intensity, ref Random rng)
        {
            // Smoke rises and spreads outward
            float3 backward = -math.forward(rotation);
            float3 randomSpread = rng.NextFloat3Direction() * 0.5f;

            float speed = rng.NextFloat(SmokeSpeedMin, SmokeSpeedMax);
            float3 velocity = (backward + randomSpread) * speed;
            velocity.y += SmokeRiseSpeed * rng.NextFloat(0.7f, 1.3f);

            // Smoke colors: dark gray with subtle cyan tint from neon lights
            float grayValue = rng.NextFloat(0.2f, 0.4f);
            float cyanTint = 0.1f * intensity;
            float4 colorStart = new float4(
                grayValue,
                grayValue + cyanTint,
                grayValue + cyanTint * 1.5f,
                0.6f * intensity
            );
            float4 colorEnd = new float4(grayValue * 0.5f, grayValue * 0.5f, grayValue * 0.5f, 0f);

            return new Particle
            {
                Position = position + new float3(rng.NextFloat(-0.2f, 0.2f), 0.05f, rng.NextFloat(-0.2f, 0.2f)),
                Velocity = velocity,
                Acceleration = new float3(0, 0.5f, 0), // Slight upward acceleration
                Color = colorStart,
                Size = SmokeSizeStart * rng.NextFloat(0.8f, 1.2f),
                Rotation = rng.NextFloat(0f, math.PI * 2f),
                RotationSpeed = rng.NextFloat(-1f, 1f),
                Lifetime = SmokeLifetime * rng.NextFloat(0.8f, 1.2f),
                MaxLifetime = SmokeLifetime,
                EmissionIntensity = 0.3f * intensity // Subtle glow from ambient neon
            };
        }

        [BurstCompile]
        private void UpdateSmokeParticles(ref DynamicBuffer<Particle> particles, float deltaTime)
        {
            for (int i = particles.Length - 1; i >= 0; i--)
            {
                var p = particles[i];

                // Update lifetime
                p.Lifetime -= deltaTime;
                if (p.Lifetime <= 0f)
                {
                    particles.RemoveAtSwapBack(i);
                    continue;
                }

                // Apply drag
                p.Velocity *= (1f - SmokeDrag * deltaTime);

                // Update physics
                p.Velocity += p.Acceleration * deltaTime;
                p.Position += p.Velocity * deltaTime;
                p.Rotation += p.RotationSpeed * deltaTime;

                // Expand size over lifetime
                float lifeRatio = 1f - (p.Lifetime / p.MaxLifetime);
                p.Size = math.lerp(SmokeSizeStart, SmokeSizeEnd, lifeRatio);

                // Fade out alpha
                p.Color.w = math.lerp(0.6f, 0f, lifeRatio * lifeRatio);

                // Reduce emission intensity
                p.EmissionIntensity = math.lerp(0.3f, 0f, lifeRatio);

                particles[i] = p;
            }
        }
    }

    /// <summary>
    /// Updates drift state based on vehicle physics.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct DriftDetectionSystem : ISystem
    {
        private const float DriftAngleThreshold = 5f; // Degrees
        private const float SlipThreshold = 0.3f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerVehicleTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update drift state for vehicles with drift detection
            foreach (var (transform, velocity, driftState) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<VehicleVelocity>, RefRW<DriftVisualState>>())
            {
                float3 forward = math.forward(transform.ValueRO.Rotation);
                float3 velocityDir = math.normalizesafe(velocity.ValueRO.Linear);

                // Calculate drift angle
                float dotProduct = math.dot(forward, velocityDir);
                float angle = math.degrees(math.acos(math.clamp(dotProduct, -1f, 1f)));

                // Determine if drifting
                bool isDrifting = angle > DriftAngleThreshold && math.length(velocity.ValueRO.Linear) > 10f;

                driftState.ValueRW.IsDrifting = isDrifting;
                driftState.ValueRW.DriftAngle = angle;
                driftState.ValueRW.DriftIntensity = math.saturate(angle / 45f); // Max at 45 degrees

                if (isDrifting)
                {
                    driftState.ValueRW.DriftTime += deltaTime;
                }
                else
                {
                    driftState.ValueRW.DriftTime = 0f;
                }

                // Wheel slip would come from vehicle physics
                // For now, estimate based on drift angle
                float slip = driftState.ValueRO.DriftIntensity;
                driftState.ValueRW.WheelSlip = new float4(
                    slip * 0.3f,  // FL
                    slip * 0.3f,  // FR
                    slip,         // RL
                    slip          // RR
                );
            }
        }
    }
}
