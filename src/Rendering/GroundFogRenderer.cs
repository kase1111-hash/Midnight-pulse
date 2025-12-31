// ============================================================================
// Nightflow - Ground Fog Renderer
// Creates dense fog layer at ground level that the track dips through
// Layered planes with animated drift for volumetric effect
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using Nightflow.Components;
using Nightflow.Utilities;

namespace Nightflow.Rendering
{
    /// <summary>
    /// Renders ground-level fog as layered planes with animated drift.
    /// Track dips into and climbs out of the fog for atmospheric depth.
    /// </summary>
    [ExecuteAlways]
    public class GroundFogRenderer : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private Material fogMaterial;
        [SerializeField] private Camera targetCamera;

        [Header("Fog Layer Settings")]
        [Tooltip("Base height of fog layer (meters)")]
        [SerializeField] private float baseHeight = -5f;

        [Tooltip("Total thickness of fog layer (meters)")]
        [SerializeField] private float thickness = 15f;

        [Tooltip("Number of fog planes for volumetric effect")]
        [Range(4, 16)]
        [SerializeField] private int layerCount = 8;

        [Tooltip("Size of each fog plane (meters)")]
        [SerializeField] private float planeSize = 600f;

        [Header("Fog Density")]
        [Tooltip("Maximum fog opacity at core")]
        [Range(0.1f, 1f)]
        [SerializeField] private float maxDensity = 0.7f;

        [Tooltip("How sharply fog fades at edges (vertical falloff)")]
        [Range(0.5f, 5f)]
        [SerializeField] private float falloffSharpness = 2f;

        [Tooltip("Distance where fog starts to fade")]
        [SerializeField] private float fadeStartDistance = 50f;

        [Tooltip("Distance where fog is fully faded")]
        [SerializeField] private float fadeEndDistance = 400f;

        [Header("Fog Colors")]
        [SerializeField] private Color fogColor = new Color(0.15f, 0.18f, 0.25f, 1f);
        [SerializeField] private Color fogColorAlt = new Color(0.1f, 0.15f, 0.22f, 1f);

        [Tooltip("How much color shifts over time")]
        [Range(0f, 1f)]
        [SerializeField] private float colorShiftIntensity = 0.3f;

        [Header("Animation")]
        [Tooltip("Speed of horizontal fog drift")]
        [SerializeField] private float driftSpeed = 2f;

        [Tooltip("Turbulence intensity")]
        [Range(0f, 2f)]
        [SerializeField] private float turbulence = 0.5f;

        [Tooltip("Vertical bob amplitude")]
        [SerializeField] private float verticalBob = 0.5f;

        [Header("Surface Glow")]
        [Tooltip("Add subtle glow at fog surface")]
        [SerializeField] private bool enableSurfaceGlow = true;

        [SerializeField] private Color glowColor = new Color(0.2f, 0.4f, 0.5f, 1f);

        [Range(0f, 0.5f)]
        [SerializeField] private float glowIntensity = 0.15f;

        // Mesh data
        private Mesh _fogMesh;
        private List<Vector3> _vertices = new List<Vector3>();
        private List<Color> _colors = new List<Color>();
        private List<Vector2> _uvs = new List<Vector2>();
        private List<int> _triangles = new List<int>();

        // Animation
        private float _animationTime;
        private Vector2 _driftOffset;

        // Layer data
        private struct FogLayer
        {
            public float Height;
            public float Opacity;
            public Vector2 DriftOffset;
            public float Phase;
        }
        private FogLayer[] _layers;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            CreateDefaultMaterial();
            InitializeLayers();
        }

        private void Start()
        {
            BuildMesh();
        }

        private void CreateDefaultMaterial()
        {
            if (fogMaterial == null)
            {
                // Use transparent shader for fog
                var shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Unlit/Color");

                // Final fallback - should never happen in a properly configured project
                if (shader == null)
                {
                    Log.SystemError("GroundFogRenderer", "No suitable shader found for fog material. Check that shaders are included in build.");
                    shader = Shader.Find("Hidden/InternalErrorShader");
                }

                fogMaterial = new Material(shader);

                // Configure for alpha blending
                fogMaterial.SetFloat("_Mode", 2); // Fade
                fogMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                fogMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                fogMaterial.SetInt("_ZWrite", 0);
                fogMaterial.DisableKeyword("_ALPHATEST_ON");
                fogMaterial.EnableKeyword("_ALPHABLEND_ON");
                fogMaterial.renderQueue = (int)RenderQueue.Transparent - 50;
            }
        }

        private void InitializeLayers()
        {
            _layers = new FogLayer[layerCount];

            var random = new System.Random(77777);

            for (int i = 0; i < layerCount; i++)
            {
                float t = i / (float)(layerCount - 1);

                // Distribute layers through fog thickness
                // Concentrate more layers near the middle for denser core
                float heightOffset = Mathf.Lerp(0, thickness, t);

                // Opacity based on vertical position (densest in middle)
                float verticalT = Mathf.Abs(t - 0.5f) * 2f; // 0 at middle, 1 at edges
                float opacity = Mathf.Pow(1f - verticalT, falloffSharpness);

                _layers[i] = new FogLayer
                {
                    Height = baseHeight + heightOffset,
                    Opacity = opacity * maxDensity,
                    DriftOffset = new Vector2(
                        (float)random.NextDouble() * 100f,
                        (float)random.NextDouble() * 100f
                    ),
                    Phase = (float)random.NextDouble() * Mathf.PI * 2f
                };
            }
        }

        private void Update()
        {
            if (_layers == null || _layers.Length != layerCount)
            {
                InitializeLayers();
            }

            // Update animation
            _animationTime += Time.deltaTime;
            _driftOffset += new Vector2(driftSpeed * 0.3f, driftSpeed) * Time.deltaTime;

            // Rebuild mesh with animation
            BuildMesh();

            // Render
            RenderFog();
        }

        private void BuildMesh()
        {
            Vector3 camPos = targetCamera != null ? targetCamera.transform.position : Vector3.zero;
            Vector3 camForward = targetCamera != null ? targetCamera.transform.forward : Vector3.forward;

            _vertices.Clear();
            _colors.Clear();
            _uvs.Clear();
            _triangles.Clear();

            float halfSize = planeSize * 0.5f;

            // Build each fog layer
            for (int layer = 0; layer < _layers.Length; layer++)
            {
                ref var fogLayer = ref _layers[layer];

                // Animated height with turbulence
                float turbulenceOffset = Mathf.Sin(_animationTime * 0.5f + fogLayer.Phase) * turbulence;
                float bobOffset = Mathf.Sin(_animationTime * 0.3f + fogLayer.Phase * 0.7f) * verticalBob;
                float layerHeight = fogLayer.Height + turbulenceOffset + bobOffset;

                // Animated drift
                Vector2 layerDrift = _driftOffset + fogLayer.DriftOffset;
                layerDrift.x = (layerDrift.x % planeSize) - halfSize;
                layerDrift.y = (layerDrift.y % planeSize) - halfSize;

                // Center fog plane on camera XZ, offset by drift
                float centerX = camPos.x + layerDrift.x * 0.1f;
                float centerZ = camPos.z + layerDrift.y * 0.1f;

                // Calculate color with shift
                float colorT = (Mathf.Sin(_animationTime * 0.2f + fogLayer.Phase) + 1f) * 0.5f;
                colorT *= colorShiftIntensity;
                Color layerColor = Color.Lerp(fogColor, fogColorAlt, colorT);

                // Build quad for this layer
                int baseVert = _vertices.Count;

                // Four corners of the fog plane
                Vector3[] corners = new Vector3[]
                {
                    new Vector3(centerX - halfSize, layerHeight, centerZ - halfSize),
                    new Vector3(centerX + halfSize, layerHeight, centerZ - halfSize),
                    new Vector3(centerX + halfSize, layerHeight, centerZ + halfSize),
                    new Vector3(centerX - halfSize, layerHeight, centerZ + halfSize)
                };

                // Add vertices with distance-based opacity
                for (int c = 0; c < 4; c++)
                {
                    Vector3 corner = corners[c];

                    // Calculate distance from camera
                    float dist = Vector3.Distance(new Vector3(corner.x, camPos.y, corner.z), camPos);

                    // Distance fade
                    float distanceFade = 1f;
                    if (dist > fadeStartDistance)
                    {
                        distanceFade = 1f - Mathf.Clamp01((dist - fadeStartDistance) / (fadeEndDistance - fadeStartDistance));
                    }

                    // Near camera fade (prevent clipping)
                    float nearFade = Mathf.Clamp01(dist / 20f);

                    // Combine opacity
                    float finalOpacity = fogLayer.Opacity * distanceFade * nearFade;

                    // Add surface glow for top layers
                    if (enableSurfaceGlow && layer >= _layers.Length - 2)
                    {
                        Color glowAdd = glowColor * glowIntensity * distanceFade;
                        layerColor = Color.Lerp(layerColor, layerColor + glowAdd, 0.5f);
                    }

                    Color vertColor = layerColor;
                    vertColor.a = finalOpacity;

                    _vertices.Add(corner);
                    _colors.Add(vertColor);
                    _uvs.Add(new Vector2(c % 2, c / 2));
                }

                // Triangles (double-sided)
                // Front face
                _triangles.Add(baseVert);
                _triangles.Add(baseVert + 2);
                _triangles.Add(baseVert + 1);
                _triangles.Add(baseVert);
                _triangles.Add(baseVert + 3);
                _triangles.Add(baseVert + 2);

                // Back face
                _triangles.Add(baseVert);
                _triangles.Add(baseVert + 1);
                _triangles.Add(baseVert + 2);
                _triangles.Add(baseVert);
                _triangles.Add(baseVert + 2);
                _triangles.Add(baseVert + 3);
            }

            // Create or update mesh
            if (_fogMesh == null)
            {
                _fogMesh = new Mesh();
                _fogMesh.name = "GroundFog";
                _fogMesh.MarkDynamic();
            }

            _fogMesh.Clear();
            _fogMesh.SetVertices(_vertices);
            _fogMesh.SetColors(_colors);
            _fogMesh.SetUVs(0, _uvs);
            _fogMesh.SetTriangles(_triangles, 0);
            _fogMesh.RecalculateBounds();
        }

        private void RenderFog()
        {
            if (_fogMesh == null || fogMaterial == null) return;

            Graphics.DrawMesh(_fogMesh, Matrix4x4.identity, fogMaterial, 0);
        }

        private void OnDestroy()
        {
            if (_fogMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_fogMesh);
                else
                    DestroyImmediate(_fogMesh);
            }
        }

        /// <summary>
        /// Get fog density at a specific world position.
        /// Useful for gameplay effects (headlight scattering, etc).
        /// </summary>
        public float GetFogDensityAt(Vector3 worldPosition)
        {
            float height = worldPosition.y;

            // Check if within fog layer
            if (height < baseHeight || height > baseHeight + thickness)
            {
                return 0f;
            }

            // Calculate vertical position in fog (0 = bottom, 1 = top)
            float t = (height - baseHeight) / thickness;

            // Density curve (densest in middle)
            float verticalT = Mathf.Abs(t - 0.5f) * 2f;
            float density = Mathf.Pow(1f - verticalT, falloffSharpness) * maxDensity;

            return density;
        }

        /// <summary>
        /// Check if a position is inside the fog layer.
        /// </summary>
        public bool IsInFog(Vector3 worldPosition)
        {
            return worldPosition.y >= baseHeight && worldPosition.y <= baseHeight + thickness;
        }

        /// <summary>
        /// Get the top of the fog layer.
        /// </summary>
        public float GetFogTopHeight()
        {
            return baseHeight + thickness;
        }

        /// <summary>
        /// Get the bottom of the fog layer.
        /// </summary>
        public float GetFogBottomHeight()
        {
            return baseHeight;
        }

        // Editor visualization
        private void OnDrawGizmosSelected()
        {
            // Draw fog volume bounds
            Vector3 center = transform.position;
            center.y = baseHeight + thickness * 0.5f;

            Gizmos.color = new Color(0.3f, 0.5f, 0.6f, 0.3f);
            Gizmos.DrawCube(center, new Vector3(planeSize, thickness, planeSize));

            Gizmos.color = new Color(0.3f, 0.5f, 0.6f, 0.8f);
            Gizmos.DrawWireCube(center, new Vector3(planeSize, thickness, planeSize));

            // Draw base height line
            Gizmos.color = Color.cyan;
            Vector3 baseCenter = transform.position;
            baseCenter.y = baseHeight;
            Gizmos.DrawLine(
                baseCenter + Vector3.left * 50f,
                baseCenter + Vector3.right * 50f
            );

            // Draw top height line
            baseCenter.y = baseHeight + thickness;
            Gizmos.DrawLine(
                baseCenter + Vector3.left * 50f,
                baseCenter + Vector3.right * 50f
            );

            // Draw layer positions
            if (_layers != null)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                foreach (var layer in _layers)
                {
                    Vector3 layerPos = transform.position;
                    layerPos.y = layer.Height;
                    Gizmos.DrawLine(
                        layerPos + Vector3.left * 20f,
                        layerPos + Vector3.right * 20f
                    );
                }
            }
        }
    }
}
