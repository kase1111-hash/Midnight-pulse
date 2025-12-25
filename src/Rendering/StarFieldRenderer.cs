// ============================================================================
// Nightflow - Star Field Renderer
// MonoBehaviour that renders a subtle star field for visual rotation tracking
// Stars are deliberately dim to provide reference without distraction
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using Nightflow.Components;

namespace Nightflow.Rendering
{
    /// <summary>
    /// Renders a subtle star field on the sky dome.
    /// Stars provide visual reference points for tracking vehicle rotation.
    /// Attach to a GameObject in the scene to enable star rendering.
    /// </summary>
    [ExecuteAlways]
    public class StarFieldRenderer : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private Material starMaterial;
        [SerializeField] private Camera targetCamera;

        [Header("Star Field Settings")]
        [Tooltip("Total number of stars")]
        [SerializeField] private int starCount = 200;

        [Tooltip("Distance to star dome")]
        [SerializeField] private float domeRadius = 2000f;

        [Tooltip("Minimum angle above horizon (degrees)")]
        [SerializeField] private float minElevation = 10f;

        [Tooltip("Maximum angle above horizon (degrees)")]
        [SerializeField] private float maxElevation = 80f;

        [Header("Star Appearance")]
        [Tooltip("Base opacity - keep low for subtle effect")]
        [Range(0.02f, 0.3f)]
        [SerializeField] private float baseOpacity = 0.08f;

        [Tooltip("How much stars twinkle")]
        [Range(0f, 0.5f)]
        [SerializeField] private float twinkleIntensity = 0.15f;

        [Tooltip("Base star size in world units")]
        [SerializeField] private float baseStarSize = 8f;

        [Header("Star Colors")]
        [SerializeField] private Color warmStarColor = new Color(1f, 0.9f, 0.8f, 1f);
        [SerializeField] private Color coolStarColor = new Color(0.85f, 0.9f, 1f, 1f);
        [SerializeField] private Color neutralStarColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        // Mesh data
        private Mesh _starMesh;
        private List<Vector3> _vertices = new List<Vector3>();
        private List<Color> _colors = new List<Color>();
        private List<int> _triangles = new List<int>();

        // Star data (generated once, positions fixed)
        private StarInstance[] _stars;
        private float _twinkleTime;
        private bool _needsRebuild = true;

        // ECS access
        private EntityManager _entityManager;
        private EntityQuery _starFieldQuery;
        private bool _ecsInitialized;

        private struct StarInstance
        {
            public float Azimuth;       // Horizontal angle (radians)
            public float Elevation;     // Vertical angle (radians)
            public float Brightness;    // Base brightness multiplier
            public float Size;          // Size multiplier
            public float TwinklePhase;  // Phase offset for twinkle
            public float TwinkleSpeed;  // How fast this star twinkles
            public float ColorTemp;     // 0=warm, 0.5=neutral, 1=cool
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            CreateDefaultMaterial();
        }

        private void Start()
        {
            TryInitializeECS();
            GenerateStarField();
        }

        private void TryInitializeECS()
        {
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                _starFieldQuery = _entityManager.CreateEntityQuery(typeof(StarFieldTag));
                _ecsInitialized = true;
            }
        }

        private void CreateDefaultMaterial()
        {
            if (starMaterial == null)
            {
                // Use additive shader for stars
                var shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Unlit/Color");

                starMaterial = new Material(shader);

                // Configure for additive blending
                starMaterial.SetFloat("_Mode", 4); // Additive
                starMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                starMaterial.SetInt("_DstBlend", (int)BlendMode.One);
                starMaterial.SetInt("_ZWrite", 0);
                starMaterial.DisableKeyword("_ALPHATEST_ON");
                starMaterial.EnableKeyword("_ALPHABLEND_ON");
                starMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                starMaterial.renderQueue = (int)RenderQueue.Transparent + 10;
            }
        }

        private void GenerateStarField()
        {
            _stars = new StarInstance[starCount];

            // Use fixed seed for consistent star positions
            var random = new System.Random(98765);

            float minElev = minElevation * Mathf.Deg2Rad;
            float maxElev = maxElevation * Mathf.Deg2Rad;

            for (int i = 0; i < starCount; i++)
            {
                // Random position on sky dome
                float azimuth = (float)random.NextDouble() * Mathf.PI * 2f;

                // Distribute elevation with slight bias toward horizon (more stars lower)
                float elevRoll = (float)random.NextDouble();
                float elevation = Mathf.Lerp(minElev, maxElev, Mathf.Sqrt(elevRoll));

                // Brightness variation - most stars dim, few brighter
                float brightnessRoll = (float)random.NextDouble();
                float brightness;
                if (brightnessRoll < 0.7f)
                    brightness = 0.3f + brightnessRoll * 0.5f;
                else if (brightnessRoll < 0.9f)
                    brightness = 0.6f + (brightnessRoll - 0.7f) * 1.5f;
                else
                    brightness = 0.9f + (brightnessRoll - 0.9f);

                // Size variation - correlate with brightness
                float size = 0.5f + brightness * 0.8f + (float)random.NextDouble() * 0.3f;

                // Twinkle parameters
                float twinklePhase = (float)random.NextDouble() * Mathf.PI * 2f;
                float twinkleSpeed = 0.5f + (float)random.NextDouble() * 2f;

                // Color temperature - slight variation
                float colorTemp = (float)random.NextDouble();

                _stars[i] = new StarInstance
                {
                    Azimuth = azimuth,
                    Elevation = elevation,
                    Brightness = brightness,
                    Size = size,
                    TwinklePhase = twinklePhase,
                    TwinkleSpeed = twinkleSpeed,
                    ColorTemp = colorTemp
                };
            }

            _needsRebuild = true;
        }

        private void Update()
        {
            if (_stars == null || _stars.Length == 0)
            {
                GenerateStarField();
                return;
            }

            // Update twinkle time
            _twinkleTime += Time.deltaTime;

            // Rebuild mesh with current twinkle state
            BuildMesh();

            // Render
            RenderStars();
        }

        private void BuildMesh()
        {
            Vector3 playerPos = targetCamera != null ? targetCamera.transform.position : Vector3.zero;

            // Clear lists
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();

            // Camera vectors for billboarding
            Vector3 camRight = targetCamera != null ? targetCamera.transform.right : Vector3.right;
            Vector3 camUp = targetCamera != null ? targetCamera.transform.up : Vector3.up;

            for (int i = 0; i < _stars.Length; i++)
            {
                ref var star = ref _stars[i];

                // Calculate world position on sky dome
                float cosElev = Mathf.Cos(star.Elevation);
                float sinElev = Mathf.Sin(star.Elevation);
                float cosAzi = Mathf.Cos(star.Azimuth);
                float sinAzi = Mathf.Sin(star.Azimuth);

                Vector3 starPos = new Vector3(
                    playerPos.x + cosElev * sinAzi * domeRadius,
                    playerPos.y + sinElev * domeRadius,
                    playerPos.z + cosElev * cosAzi * domeRadius
                );

                // Calculate twinkle
                float twinkle = Mathf.Sin(_twinkleTime * star.TwinkleSpeed + star.TwinklePhase);
                float twinkleMultiplier = 1f + twinkle * twinkleIntensity;

                // Final brightness (kept subtle)
                float finalBrightness = star.Brightness * twinkleMultiplier * baseOpacity;

                // Get star color based on temperature
                Color starColor = GetStarColor(star.ColorTemp);
                starColor.a = finalBrightness;

                // Star size
                float size = baseStarSize * star.Size;

                // Billboard quad
                Vector3 right = camRight * size * 0.5f;
                Vector3 up = camUp * size * 0.5f;

                int baseVert = _vertices.Count;

                _vertices.Add(starPos - right - up);
                _vertices.Add(starPos + right - up);
                _vertices.Add(starPos + right + up);
                _vertices.Add(starPos - right + up);

                _colors.Add(starColor);
                _colors.Add(starColor);
                _colors.Add(starColor);
                _colors.Add(starColor);

                _triangles.Add(baseVert);
                _triangles.Add(baseVert + 2);
                _triangles.Add(baseVert + 1);
                _triangles.Add(baseVert);
                _triangles.Add(baseVert + 3);
                _triangles.Add(baseVert + 2);
            }

            // Build mesh
            if (_starMesh == null)
            {
                _starMesh = new Mesh();
                _starMesh.name = "StarField";
                _starMesh.MarkDynamic();
            }

            _starMesh.Clear();
            _starMesh.SetVertices(_vertices);
            _starMesh.SetColors(_colors);
            _starMesh.SetTriangles(_triangles, 0);
        }

        private Color GetStarColor(float temperature)
        {
            // Blend between warm, neutral, and cool
            if (temperature < 0.4f)
            {
                // Warm to neutral
                float t = temperature / 0.4f;
                return Color.Lerp(warmStarColor, neutralStarColor, t);
            }
            else if (temperature > 0.6f)
            {
                // Neutral to cool
                float t = (temperature - 0.6f) / 0.4f;
                return Color.Lerp(neutralStarColor, coolStarColor, t);
            }
            else
            {
                // Neutral zone
                return neutralStarColor;
            }
        }

        private void RenderStars()
        {
            if (_starMesh == null || starMaterial == null) return;

            // Draw stars (transparent additive layer, behind skyline)
            Graphics.DrawMesh(_starMesh, Matrix4x4.identity, starMaterial, 0);
        }

        private void OnDestroy()
        {
            if (_starMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_starMesh);
                else
                    DestroyImmediate(_starMesh);
            }

            // Clean up material if we created it
            if (starMaterial != null && starMaterial.name.Contains("Instance"))
            {
                if (Application.isPlaying)
                    Destroy(starMaterial);
                else
                    DestroyImmediate(starMaterial);
            }
        }

        // Editor visualization
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
            Vector3 center = transform.position;

            // Draw dome outline
            int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a1 = (i / (float)segments) * Mathf.PI * 2f;
                float a2 = ((i + 1) / (float)segments) * Mathf.PI * 2f;

                // Horizon ring
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * domeRadius;
                Vector3 p2 = center + new Vector3(Mathf.Cos(a2), 0, Mathf.Sin(a2)) * domeRadius;
                Gizmos.DrawLine(p1, p2);

                // Elevation arc at 45 degrees
                float elev = 45f * Mathf.Deg2Rad;
                float r = domeRadius * Mathf.Cos(elev);
                float h = domeRadius * Mathf.Sin(elev);
                p1 = center + new Vector3(Mathf.Cos(a1) * r, h, Mathf.Sin(a1) * r);
                p2 = center + new Vector3(Mathf.Cos(a2) * r, h, Mathf.Sin(a2) * r);
                Gizmos.DrawLine(p1, p2);
            }
        }
    }
}
