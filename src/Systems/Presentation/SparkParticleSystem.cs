// ============================================================================
// Nightflow - Spark Particle System
// Handles collision spark effects with neon orange/yellow aesthetics
// ============================================================================

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Nightflow.Components;

namespace Nightflow.Systems.Presentation
{
    /// <summary>
    /// Processes collision events and spawns spark particles.
    /// Sparks are bright orange/yellow streaks that shoot from impact points.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct SparkParticleSystem : ISystem
    {
        private Random random;

        // Spark configuration
        private const int SparksPerImpact = 12;
        private const int SparksPerScrape = 4;
        private const float SparkSpeedMin = 5f;
        private const float SparkSpeedMax = 15f;
        private const float SparkLifetime = 0.4f;
        private const float SparkSizeMin = 0.02f;
        private const float SparkSizeMax = 0.08f;
        private const float SparkGravity = 15f;
        private const float SparkConeAngle = 0.8f; // Radians

        public void OnCreate(ref SystemState state)
        {
            random = Random.CreateFromIndex((uint)System.DateTime.Now.Ticks);
            state.RequireForUpdate<ParticleSystemConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Process collision events and spawn sparks
            foreach (var (collisionBuffer, emitter, particleBuffer) in
                SystemAPI.Query<DynamicBuffer<CollisionEvent>, RefRW<ParticleEmitter>, DynamicBuffer<Particle>>())
            {
                if (emitter.ValueRO.Type != ParticleType.Spark)
                    continue;

                for (int i = 0; i < collisionBuffer.Length; i++)
                {
                    var collision = collisionBuffer[i];
                    SpawnSparks(ref particleBuffer, collision, ref random);
                }
            }

            // Process spawn requests
            foreach (var (spawnBuffer, emitter, particleBuffer) in
                SystemAPI.Query<DynamicBuffer<ParticleSpawnRequest>, RefRW<ParticleEmitter>, DynamicBuffer<Particle>>())
            {
                if (emitter.ValueRO.Type != ParticleType.Spark)
                    continue;

                for (int i = 0; i < spawnBuffer.Length; i++)
                {
                    var request = spawnBuffer[i];
                    if (request.Type == ParticleType.Spark)
                    {
                        SpawnSparksFromRequest(ref particleBuffer, request, ref random);
                    }
                }
            }

            // Update existing spark particles
            foreach (var (emitter, particleBuffer) in
                SystemAPI.Query<RefRO<ParticleEmitter>, DynamicBuffer<Particle>>())
            {
                if (emitter.ValueRO.Type != ParticleType.Spark)
                    continue;

                UpdateSparkParticles(ref particleBuffer, deltaTime);
            }

            // Clear processed collision events
            foreach (var collisionBuffer in SystemAPI.Query<DynamicBuffer<CollisionEvent>>())
            {
                collisionBuffer.Clear();
            }

            // Clear processed spawn requests
            foreach (var spawnBuffer in SystemAPI.Query<DynamicBuffer<ParticleSpawnRequest>>())
            {
                // Only clear spark requests, leave others
                for (int i = spawnBuffer.Length - 1; i >= 0; i--)
                {
                    if (spawnBuffer[i].Type == ParticleType.Spark)
                    {
                        spawnBuffer.RemoveAt(i);
                    }
                }
            }
        }

        [BurstCompile]
        private void SpawnSparks(ref DynamicBuffer<Particle> particles, CollisionEvent collision, ref Random rng)
        {
            // Determine spark count based on collision type
            int sparkCount = collision.Type switch
            {
                CollisionType.VehicleVehicle => (int)(SparksPerImpact * 1.5f),
                CollisionType.VehicleBarrier => SparksPerImpact,
                CollisionType.VehicleHazard => SparksPerImpact / 2,
                CollisionType.Scrape => SparksPerScrape,
                _ => SparksPerImpact
            };

            // Scale by impulse
            float impulseScale = math.saturate(collision.Impulse / 50f);
            sparkCount = (int)(sparkCount * (0.5f + impulseScale * 0.5f));

            // Spawn sparks
            for (int i = 0; i < sparkCount; i++)
            {
                var spark = CreateSpark(collision.Position, collision.Normal, collision.RelativeVelocity, ref rng);
                particles.Add(spark);
            }
        }

        [BurstCompile]
        private void SpawnSparksFromRequest(ref DynamicBuffer<Particle> particles, ParticleSpawnRequest request, ref Random rng)
        {
            int sparkCount = math.max(1, request.Count);

            for (int i = 0; i < sparkCount; i++)
            {
                var spark = CreateSpark(request.Position, request.Normal, request.Direction * 10f, ref rng);
                spark.EmissionIntensity *= request.Intensity;
                particles.Add(spark);
            }
        }

        [BurstCompile]
        private Particle CreateSpark(float3 position, float3 normal, float3 relativeVelocity, ref Random rng)
        {
            // Calculate spark direction (reflect off surface with random spread)
            float3 reflectDir = math.reflect(math.normalize(relativeVelocity), normal);
            if (math.lengthsq(reflectDir) < 0.01f)
            {
                reflectDir = normal;
            }

            // Add random spread within cone
            float3 randomDir = rng.NextFloat3Direction();
            float3 spreadDir = math.normalize(reflectDir + randomDir * SparkConeAngle);

            // Random speed
            float speed = rng.NextFloat(SparkSpeedMin, SparkSpeedMax);

            // Spark colors: bright orange to yellow with glow
            float colorLerp = rng.NextFloat();
            float4 colorStart = new float4(1f, 0.6f + colorLerp * 0.3f, 0f, 1f); // Orange to yellow
            float4 colorEnd = new float4(1f, 0.3f, 0f, 0f); // Fade to transparent red

            return new Particle
            {
                Position = position + normal * 0.05f, // Offset slightly from surface
                Velocity = spreadDir * speed,
                Acceleration = new float3(0, -SparkGravity, 0),
                Color = colorStart,
                Size = rng.NextFloat(SparkSizeMin, SparkSizeMax),
                Rotation = rng.NextFloat(0f, math.PI * 2f),
                RotationSpeed = rng.NextFloat(-10f, 10f),
                Lifetime = SparkLifetime * rng.NextFloat(0.7f, 1.3f),
                MaxLifetime = SparkLifetime,
                EmissionIntensity = 3f + rng.NextFloat(0f, 2f) // Bright glow
            };
        }

        [BurstCompile]
        private void UpdateSparkParticles(ref DynamicBuffer<Particle> particles, float deltaTime)
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

                // Update physics
                p.Velocity += p.Acceleration * deltaTime;
                p.Position += p.Velocity * deltaTime;
                p.Rotation += p.RotationSpeed * deltaTime;

                // Fade out color and size
                float lifeRatio = p.Lifetime / p.MaxLifetime;
                p.Color.w = lifeRatio; // Alpha fade
                p.Size *= 0.98f; // Shrink slightly
                p.EmissionIntensity = 3f * lifeRatio; // Glow fade

                // Color shift from yellow to red as it dies
                p.Color.y = math.lerp(0.3f, 0.8f, lifeRatio);

                particles[i] = p;
            }
        }
    }

    /// <summary>
    /// Detects collisions and creates collision events for spark spawning.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PresentationSystemGroup))]
    public partial struct CollisionDetectionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // This system would integrate with Unity Physics or custom collision detection
        }

        public void OnUpdate(ref SystemState state)
        {
            // Collision detection would be implemented here
            // For now, this is a placeholder that would hook into physics events
        }
    }
}
