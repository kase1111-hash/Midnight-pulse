// ============================================================================
// Nightflow - Speed Lines Effect System
// Screen-space velocity streaks for high-speed driving
// ============================================================================

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems.Presentation
{
    /// <summary>
    /// Manages speed line particles that streak across the screen at high velocities.
    /// Creates the classic anime/manga speed effect with neon aesthetics.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct SpeedLinesSystem : ISystem
    {
        private Random random;

        // Speed line configuration: on the shared speed curve
        // (VehicleDynamics.SpeedFeel), so they start ~120 km/h and keep
        // thickening well past 250 km/h instead of maxing out there
        private const float StartFeel = 0.347f;         // SpeedFeel at ~120 km/h
        private const float FullFeel = 0.93f;           // SpeedFeel at ~145 m/s
        private const float LineLengthMin = 2f;
        private const float LineLengthMax = 8f;
        private const float LineWidthMin = 0.02f;
        private const float LineWidthMax = 0.08f;
        private const float LineLifetimeMin = 0.1f;
        private const float LineLifetimeMax = 0.3f;
        private const float EmissionRateBase = 100f;    // Lines per second at max speed
        private const float SpawnRadiusMin = 3f;
        private const float SpawnRadiusMax = 15f;

        public void OnCreate(ref SystemState state)
        {
            random = Random.CreateFromIndex((uint)(System.DateTime.Now.Ticks + 2));
            state.RequireForUpdate<SpeedLineEffect>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get player speed and camera info
            float playerSpeed = 0f;
            float3 playerPos = float3.zero;
            float3 playerForward = new float3(0, 0, 1);

            foreach (var (transform, velocity) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<VehicleVelocity>>()
                    .WithAll<PlayerVehicleTag>())
            {
                playerSpeed = math.length(velocity.ValueRO.Linear); // m/s
                playerPos = transform.ValueRO.Position;

                // Streak along the direction of travel, the way the camera
                // faces, not the body: a sliding or spinning car would
                // otherwise sweep the streaks across a camera looking down the road
                playerForward = math.normalizesafe(velocity.ValueRO.Linear,
                    math.forward(transform.ValueRO.Rotation));
                break;
            }

            // Update speed line effect state
            foreach (var (speedLineEffect, emitter, particleBuffer) in
                SystemAPI.Query<RefRW<SpeedLineEffect>, RefRW<ParticleEmitter>, DynamicBuffer<Particle>>())
            {
                if (emitter.ValueRO.Type != ParticleType.SpeedLine)
                    continue;

                // Calculate effect intensity
                float intensity = math.saturate(
                    (VehicleDynamics.SpeedFeel(playerSpeed) - StartFeel) / (FullFeel - StartFeel));
                speedLineEffect.ValueRW.Intensity = intensity;
                speedLineEffect.ValueRW.IsActive = intensity > 0.01f;

                // Emit speed lines
                if (speedLineEffect.ValueRO.IsActive)
                {
                    EmitSpeedLines(
                        particleBuffer,
                        ref emitter.ValueRW,
                        playerPos,
                        playerForward,
                        intensity,
                        deltaTime,
                        ref random
                    );
                }

                // Update existing speed lines
                UpdateSpeedLines(particleBuffer, playerForward, deltaTime);
            }
        }

        [BurstCompile]
        private void EmitSpeedLines(
            DynamicBuffer<Particle> particles,
            ref ParticleEmitter emitter,
            float3 playerPos,
            float3 playerForward,
            float intensity,
            float deltaTime,
            ref Random rng)
        {
            // Calculate emission rate based on intensity
            float emissionRate = EmissionRateBase * intensity * intensity;
            emitter.EmissionAccumulator += emissionRate * deltaTime;

            int linesToSpawn = (int)emitter.EmissionAccumulator;
            emitter.EmissionAccumulator -= linesToSpawn;

            // Limit total particles
            if (particles.Length + linesToSpawn > emitter.MaxParticles)
            {
                linesToSpawn = math.max(0, emitter.MaxParticles - particles.Length);
            }

            // Spawn speed lines in a cone ahead of the player
            for (int i = 0; i < linesToSpawn; i++)
            {
                var line = CreateSpeedLine(playerPos, playerForward, intensity, ref rng);
                particles.Add(line);
            }
        }

        [BurstCompile]
        private Particle CreateSpeedLine(float3 playerPos, float3 forward, float intensity, ref Random rng)
        {
            // Spawn lines in a cylindrical area around the forward direction
            float angle = rng.NextFloat(0f, math.PI * 2f);
            float radius = rng.NextFloat(SpawnRadiusMin, SpawnRadiusMax);

            // Create right and up vectors
            float3 up = math.up();
            float3 right = math.normalize(math.cross(up, forward));
            float3 localUp = math.cross(forward, right);

            // Random position in the spawn cylinder
            float3 offset = right * (math.cos(angle) * radius) + localUp * (math.sin(angle) * radius);
            float3 spawnPos = playerPos + forward * rng.NextFloat(10f, 30f) + offset;

            // Speed lines move toward the camera (opposite of forward)
            float speed = rng.NextFloat(50f, 100f) * (1f + intensity);
            float3 velocity = -forward * speed;

            // Line length based on intensity
            float lineLength = math.lerp(LineLengthMin, LineLengthMax, intensity);

            // Colors: white core with cyan/magenta edge glow
            float colorVariant = rng.NextFloat();
            float4 color;
            if (colorVariant < 0.7f)
            {
                // White/cyan
                color = new float4(0.8f, 1f, 1f, 0.8f * intensity);
            }
            else if (colorVariant < 0.9f)
            {
                // Cyan
                color = new float4(0f, 1f, 1f, 0.6f * intensity);
            }
            else
            {
                // Magenta
                color = new float4(1f, 0f, 1f, 0.6f * intensity);
            }

            float lifetime = rng.NextFloat(LineLifetimeMin, LineLifetimeMax);

            return new Particle
            {
                Position = spawnPos,
                Velocity = velocity,
                Acceleration = float3.zero,
                Color = color,
                Size = lineLength, // Use size for line length
                Rotation = math.atan2(velocity.x, velocity.z), // Align with velocity
                RotationSpeed = 0f,
                Lifetime = lifetime,
                MaxLifetime = lifetime,
                EmissionIntensity = 2f * intensity // Glow intensity
            };
        }

        [BurstCompile]
        private void UpdateSpeedLines(DynamicBuffer<Particle> particles, float3 forward, float deltaTime)
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

                // Update position
                p.Position += p.Velocity * deltaTime;

                // Stretch effect - lines get longer as they pass
                float lifeRatio = p.Lifetime / p.MaxLifetime;
                float stretchMultiplier = 1f + (1f - lifeRatio) * 0.5f;
                // Size stores original length, we modify emission for visual stretch

                // Fade out
                p.Color.w *= (0.95f + lifeRatio * 0.05f);
                p.EmissionIntensity *= (0.9f + lifeRatio * 0.1f);

                particles[i] = p;
            }
        }
    }

    /// <summary>
    /// Speed line rendering data for the GPU.
    /// </summary>
    public struct SpeedLineRenderData : IComponentData
    {
        public float Intensity;
        public float4 TintColor;
        public float RadialOffset;
        public int LineCount;
    }
}
