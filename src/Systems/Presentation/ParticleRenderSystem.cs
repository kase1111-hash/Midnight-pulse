// ============================================================================
// Nightflow - Particle Render System
// GPU instanced rendering for all particle types
// ============================================================================

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Nightflow.Components;
using Nightflow.Utilities;

namespace Nightflow.Systems.Presentation
{
    /// <summary>
    /// Collects all particles and prepares them for GPU instanced rendering.
    /// Handles different particle types with appropriate materials.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SparkParticleSystem))]
    [UpdateAfter(typeof(TireSmokeParticleSystem))]
    [UpdateAfter(typeof(SpeedLinesSystem))]
    public partial class ParticleRenderSystem : SystemBase
    {
        // Render data arrays for GPU instancing
        private NativeList<Matrix4x4> sparkMatrices;
        private NativeList<Vector4> sparkColors;
        private NativeList<Matrix4x4> smokeMatrices;
        private NativeList<Vector4> smokeColors;
        private NativeList<Matrix4x4> speedLineMatrices;
        private NativeList<Vector4> speedLineColors;

        // Material property block for instancing
        private MaterialPropertyBlock propertyBlock;

        // Cached material references (set via ParticleRenderConfig)
        private Material sparkMaterial;
        private Material smokeMaterial;
        private Material speedLineMaterial;
        private Mesh particleQuadMesh;
        private Mesh particleLineMesh;

        private static readonly int ColorProperty = Shader.PropertyToID("_Colors");
        private static readonly int EmissionProperty = Shader.PropertyToID("_EmissionIntensity");

        // Reusable arrays for batched rendering to reduce GC allocations
        private Matrix4x4[] _batchMatricesBuffer;
        private Vector4[] _batchColorsBuffer;
        private const int BatchBufferSize = 1023; // Unity's maximum for DrawMeshInstanced

        // Warning tracking
        private bool _hasWarnedAboutMissingMaterials;
        private int _missingMaterialFrameCount;

        protected override void OnCreate()
        {
            sparkMatrices = new NativeList<Matrix4x4>(1000, Allocator.Persistent);
            sparkColors = new NativeList<Vector4>(1000, Allocator.Persistent);
            smokeMatrices = new NativeList<Matrix4x4>(500, Allocator.Persistent);
            smokeColors = new NativeList<Vector4>(500, Allocator.Persistent);
            speedLineMatrices = new NativeList<Matrix4x4>(200, Allocator.Persistent);
            speedLineColors = new NativeList<Vector4>(200, Allocator.Persistent);

            propertyBlock = new MaterialPropertyBlock();

            // Pre-allocate batch buffers to avoid per-frame allocations
            _batchMatricesBuffer = new Matrix4x4[BatchBufferSize];
            _batchColorsBuffer = new Vector4[BatchBufferSize];

            // Create default quad mesh for particles
            CreateParticleMeshes();
        }

        protected override void OnDestroy()
        {
            if (sparkMatrices.IsCreated) sparkMatrices.Dispose();
            if (sparkColors.IsCreated) sparkColors.Dispose();
            if (smokeMatrices.IsCreated) smokeMatrices.Dispose();
            if (smokeColors.IsCreated) smokeColors.Dispose();
            if (speedLineMatrices.IsCreated) speedLineMatrices.Dispose();
            if (speedLineColors.IsCreated) speedLineColors.Dispose();
        }

        private void CreateParticleMeshes()
        {
            // Billboard quad for sparks and smoke
            particleQuadMesh = new Mesh();
            particleQuadMesh.name = "ParticleQuad";

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0)
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            int[] triangles = new int[] { 0, 2, 1, 0, 3, 2 };

            particleQuadMesh.vertices = vertices;
            particleQuadMesh.uv = uvs;
            particleQuadMesh.triangles = triangles;
            particleQuadMesh.RecalculateNormals();
            particleQuadMesh.RecalculateBounds();

            // Elongated quad for speed lines
            particleLineMesh = new Mesh();
            particleLineMesh.name = "ParticleLine";

            Vector3[] lineVertices = new Vector3[]
            {
                new Vector3(-0.05f, 0, -0.5f),
                new Vector3(0.05f, 0, -0.5f),
                new Vector3(0.05f, 0, 0.5f),
                new Vector3(-0.05f, 0, 0.5f)
            };

            particleLineMesh.vertices = lineVertices;
            particleLineMesh.uv = uvs;
            particleLineMesh.triangles = triangles;
            particleLineMesh.RecalculateNormals();
            particleLineMesh.RecalculateBounds();
        }

        protected override void OnUpdate()
        {
            // Clear previous frame data
            sparkMatrices.Clear();
            sparkColors.Clear();
            smokeMatrices.Clear();
            smokeColors.Clear();
            speedLineMatrices.Clear();
            speedLineColors.Clear();

            // Get camera for billboarding
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;

            Quaternion cameraRotation = mainCamera.transform.rotation;

            // Collect particles from all emitters
            Entities
                .WithoutBurst()
                .ForEach((in ParticleEmitter emitter, in DynamicBuffer<Particle> particles) =>
                {
                    switch (emitter.Type)
                    {
                        case ParticleType.Spark:
                            CollectSparkParticles(particles, cameraRotation);
                            break;
                        case ParticleType.TireSmoke:
                            CollectSmokeParticles(particles, cameraRotation);
                            break;
                        case ParticleType.SpeedLine:
                            CollectSpeedLineParticles(particles);
                            break;
                    }
                }).Run();

            // Render particles if materials are available
            RenderParticles();
        }

        private void CollectSparkParticles(DynamicBuffer<Particle> particles, Quaternion cameraRotation)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                var p = particles[i];

                // Create billboard matrix
                Matrix4x4 matrix = Matrix4x4.TRS(
                    p.Position,
                    cameraRotation * Quaternion.Euler(0, 0, math.degrees(p.Rotation)),
                    new Vector3(p.Size, p.Size, p.Size)
                );

                sparkMatrices.Add(matrix);

                // Pack color with emission in alpha channel for shader
                Vector4 color = new Vector4(p.Color.x, p.Color.y, p.Color.z, p.Color.w);
                sparkColors.Add(color);
            }
        }

        private void CollectSmokeParticles(DynamicBuffer<Particle> particles, Quaternion cameraRotation)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                var p = particles[i];

                // Create billboard matrix with larger size for smoke
                Matrix4x4 matrix = Matrix4x4.TRS(
                    p.Position,
                    cameraRotation * Quaternion.Euler(0, 0, math.degrees(p.Rotation)),
                    new Vector3(p.Size, p.Size, p.Size)
                );

                smokeMatrices.Add(matrix);
                smokeColors.Add(new Vector4(p.Color.x, p.Color.y, p.Color.z, p.Color.w));
            }
        }

        private void CollectSpeedLineParticles(DynamicBuffer<Particle> particles)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                var p = particles[i];

                // Speed lines are aligned with velocity, not camera
                Quaternion rotation = Quaternion.Euler(0, math.degrees(p.Rotation), 0);

                Matrix4x4 matrix = Matrix4x4.TRS(
                    p.Position,
                    rotation,
                    new Vector3(0.1f, 0.1f, p.Size) // Size is line length
                );

                speedLineMatrices.Add(matrix);
                speedLineColors.Add(new Vector4(p.Color.x, p.Color.y, p.Color.z, p.Color.w));
            }
        }

        private void RenderParticles()
        {
            // Get materials from config if not set
            if (sparkMaterial == null || smokeMaterial == null || speedLineMaterial == null)
            {
                _missingMaterialFrameCount++;

                // Try to find ParticleRenderConfig
                if (SystemAPI.TryGetSingleton<ParticleRenderConfig>(out var config))
                {
                    // Materials would be set from managed component
                }

                // Warn about missing materials (only once, after allowing initialization time)
                if (!_hasWarnedAboutMissingMaterials && _missingMaterialFrameCount > 30)
                {
                    int missingCount = 0;
                    string missing = "";
                    if (sparkMaterial == null) { missingCount++; missing += "spark, "; }
                    if (smokeMaterial == null) { missingCount++; missing += "smoke, "; }
                    if (speedLineMaterial == null) { missingCount++; missing += "speedLine, "; }
                    missing = missing.TrimEnd(',', ' ');

                    Log.SystemWarn("ParticleRenderSystem", $"Missing {missingCount} particle material(s): [{missing}]. " +
                        "Ensure ParticleMaterialProvider component is in the scene and has valid materials assigned or " +
                        "'Create Default Materials' is enabled. Particles will not render until materials are provided.");
                    _hasWarnedAboutMissingMaterials = true;
                }

                return;
            }

            // Reset warning state when materials are available
            _missingMaterialFrameCount = 0;
            if (_hasWarnedAboutMissingMaterials)
            {
                Log.System("ParticleRenderSystem", "Particle materials are now available. Rendering enabled.");
                _hasWarnedAboutMissingMaterials = false;
            }

            // Render sparks
            if (sparkMatrices.Length > 0)
            {
                RenderBatch(sparkMaterial, particleQuadMesh, sparkMatrices, sparkColors);
            }

            // Render smoke
            if (smokeMatrices.Length > 0)
            {
                RenderBatch(smokeMaterial, particleQuadMesh, smokeMatrices, smokeColors);
            }

            // Render speed lines
            if (speedLineMatrices.Length > 0)
            {
                RenderBatch(speedLineMaterial, particleLineMesh, speedLineMatrices, speedLineColors);
            }
        }

        private void RenderBatch(Material material, Mesh mesh, NativeList<Matrix4x4> matrices, NativeList<Vector4> colors)
        {
            for (int i = 0; i < matrices.Length; i += BatchBufferSize)
            {
                int count = math.min(BatchBufferSize, matrices.Length - i);

                // Copy to pre-allocated managed arrays for rendering (avoids per-frame GC allocations)
                for (int j = 0; j < count; j++)
                {
                    _batchMatricesBuffer[j] = matrices[i + j];
                    _batchColorsBuffer[j] = colors[i + j];
                }

                propertyBlock.SetVectorArray(ColorProperty, _batchColorsBuffer);
                Graphics.DrawMeshInstanced(mesh, 0, material, _batchMatricesBuffer, count, propertyBlock);
            }
        }

        /// <summary>
        /// Sets particle materials from managed code.
        /// </summary>
        public void SetMaterials(Material spark, Material smoke, Material speedLine)
        {
            sparkMaterial = spark;
            smokeMaterial = smoke;
            speedLineMaterial = speedLine;
        }
    }

    /// <summary>
    /// Configuration for particle renderer (singleton).
    /// </summary>
    public struct ParticleRenderConfig : IComponentData
    {
        public int MaxSparkParticles;
        public int MaxSmokeParticles;
        public int MaxSpeedLineParticles;
        public float GlobalBrightness;
    }
}
