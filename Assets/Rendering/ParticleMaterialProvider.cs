// ============================================================================
// Nightflow - Particle Material Provider
// MonoBehaviour that provides materials to the ECS ParticleRenderSystem
// ============================================================================

using UnityEngine;
using Unity.Entities;
using Nightflow.Systems.Presentation;

namespace Nightflow.Rendering
{
    /// <summary>
    /// Provides particle materials to the ECS-based ParticleRenderSystem.
    /// Attach to a GameObject in the scene to enable particle rendering.
    /// </summary>
    [ExecuteAlways]
    public class ParticleMaterialProvider : MonoBehaviour
    {
        [Header("Particle Materials")]
        [Tooltip("Material for spark particles (additive/emissive recommended)")]
        [SerializeField] private Material sparkMaterial;

        [Tooltip("Material for smoke particles (soft additive recommended)")]
        [SerializeField] private Material smokeMaterial;

        [Tooltip("Material for speed line particles (additive recommended)")]
        [SerializeField] private Material speedLineMaterial;

        [Header("Settings")]
        [Tooltip("Create default materials if none assigned")]
        [SerializeField] private bool createDefaultMaterials = true;

        private ParticleRenderSystem _particleRenderSystem;
        private bool _materialsApplied;

        private void OnEnable()
        {
            if (createDefaultMaterials)
            {
                CreateDefaultMaterialsIfNeeded();
            }
            ApplyMaterials();
        }

        private void Update()
        {
            // Re-apply materials if system wasn't ready on enable
            if (!_materialsApplied)
            {
                ApplyMaterials();
            }
        }

        private void ApplyMaterials()
        {
            if (sparkMaterial == null && smokeMaterial == null && speedLineMaterial == null)
            {
                return;
            }

            // Get the ParticleRenderSystem from the default world
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                _particleRenderSystem = World.DefaultGameObjectInjectionWorld
                    .GetExistingSystemManaged<ParticleRenderSystem>();

                if (_particleRenderSystem != null)
                {
                    _particleRenderSystem.SetMaterials(sparkMaterial, smokeMaterial, speedLineMaterial);
                    _materialsApplied = true;
                }
            }
        }

        private void CreateDefaultMaterialsIfNeeded()
        {
            // Create spark material (bright additive)
            if (sparkMaterial == null)
            {
                sparkMaterial = CreateDefaultParticleMaterial("Nightflow_SparkParticle",
                    new Color(1f, 0.6f, 0.1f, 1f), 3f);
            }

            // Create smoke material (soft additive)
            if (smokeMaterial == null)
            {
                smokeMaterial = CreateDefaultParticleMaterial("Nightflow_SmokeParticle",
                    new Color(0.3f, 0.3f, 0.3f, 0.5f), 0f);
            }

            // Create speed line material (bright additive)
            if (speedLineMaterial == null)
            {
                speedLineMaterial = CreateDefaultParticleMaterial("Nightflow_SpeedLineParticle",
                    new Color(0.8f, 0.9f, 1f, 0.6f), 1f);
            }
        }

        private Material CreateDefaultParticleMaterial(string name, Color color, float emission)
        {
            // Try to use URP particle shader, fall back to standard
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                Debug.LogWarning($"[ParticleMaterialProvider] Could not find particle shader for {name}");
                return null;
            }

            var mat = new Material(shader);
            mat.name = name;
            mat.color = color;

            // Set common particle properties
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            if (mat.HasProperty("_EmissionColor") && emission > 0)
            {
                mat.SetColor("_EmissionColor", color * emission);
                mat.EnableKeyword("_EMISSION");
            }

            // Set blend mode to additive
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1); // Transparent
            }
            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 1); // Additive
            }

            // Set rendering properties
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000; // Transparent queue

            return mat;
        }

        private void OnDisable()
        {
            _materialsApplied = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Re-apply materials when changed in editor
            _materialsApplied = false;
        }
#endif
    }
}
