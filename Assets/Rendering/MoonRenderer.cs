// ============================================================================
// Nightflow - Moon Renderer
// Renders the moon with accurate phase based on real system date/time
// Low-res stylized appearance with glow, full moon gets subtle halo ring
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using System;
using System.Collections.Generic;
using Nightflow.Components;

namespace Nightflow.Rendering
{
    /// <summary>
    /// Renders the moon with accurate lunar phase calculation.
    /// Uses system date/time to determine current moon phase.
    /// Features low-res stylized look with glow effect.
    /// </summary>
    [ExecuteAlways]
    public class MoonRenderer : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private Material moonMaterial;
        [SerializeField] private Material glowMaterial;
        [SerializeField] private Camera targetCamera;

        [Header("Moon Position")]
        [Tooltip("Fixed azimuth angle (degrees from north)")]
        [SerializeField] private float moonAzimuth = 135f;

        [Tooltip("Fixed elevation angle (degrees above horizon)")]
        [SerializeField] private float moonElevation = 45f;

        [Tooltip("Distance to moon billboard")]
        [SerializeField] private float renderDistance = 1800f;

        [Header("Moon Appearance")]
        [Tooltip("Angular size in degrees (real moon is ~0.5)")]
        [SerializeField] private float angularSize = 3f;

        [Tooltip("Resolution of moon disc (vertex count on edge)")]
        [Range(8, 32)]
        [SerializeField] private int moonResolution = 12;

        [SerializeField] private Color moonColor = new Color(0.9f, 0.9f, 0.85f, 1f);

        [Header("Glow Effect")]
        [Tooltip("Glow radius as multiplier of moon size")]
        [SerializeField] private float glowRadius = 2.5f;

        [Tooltip("Glow opacity")]
        [Range(0f, 1f)]
        [SerializeField] private float glowIntensity = 0.15f;

        [SerializeField] private Color glowColor = new Color(0.8f, 0.85f, 0.9f, 1f);

        [Header("Full Moon Halo")]
        [Tooltip("Halo ring radius as multiplier of moon size")]
        [SerializeField] private float haloRadius = 4f;

        [Tooltip("Halo ring thickness as fraction of radius")]
        [SerializeField] private float haloThickness = 0.15f;

        [Tooltip("Halo opacity at full moon")]
        [Range(0f, 0.5f)]
        [SerializeField] private float haloIntensity = 0.08f;

        [SerializeField] private Color haloColor = new Color(0.7f, 0.75f, 0.85f, 1f);

        // Lunar calculation constants
        private const double SynodicMonth = 29.530588853; // Days
        private static readonly DateTime ReferenceNewMoon = new DateTime(2000, 1, 6, 18, 14, 0, DateTimeKind.Utc);

        // Current moon state
        private float _phaseProgress;
        private float _illumination;
        private MoonPhase _currentPhase;

        // Meshes
        private Mesh _moonMesh;
        private Mesh _glowMesh;
        private Mesh _haloMesh;

        // Mesh data
        private List<Vector3> _vertices = new List<Vector3>();
        private List<Color> _colors = new List<Color>();
        private List<int> _triangles = new List<int>();
        private List<Vector2> _uvs = new List<Vector2>();

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            CreateDefaultMaterials();
        }

        private void Start()
        {
            CalculateMoonPhase();
            BuildMeshes();
        }

        private void CreateDefaultMaterials()
        {
            // Moon surface material (unlit, slightly transparent for soft edge)
            if (moonMaterial == null)
            {
                var shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Unlit/Color");

                moonMaterial = new Material(shader);
                moonMaterial.color = moonColor;
                moonMaterial.renderQueue = (int)RenderQueue.Transparent + 5;
            }

            // Glow material (additive blend)
            if (glowMaterial == null)
            {
                var shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");

                glowMaterial = new Material(shader);
                glowMaterial.SetFloat("_Mode", 4);
                glowMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                glowMaterial.SetInt("_DstBlend", (int)BlendMode.One);
                glowMaterial.SetInt("_ZWrite", 0);
                glowMaterial.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        private void CalculateMoonPhase()
        {
            // Get current UTC time
            DateTime now = DateTime.UtcNow;

            // Calculate days since reference new moon
            double daysSinceReference = (now - ReferenceNewMoon).TotalDays;

            // Calculate phase progress (0-1 through the lunar cycle)
            double lunarCycles = daysSinceReference / SynodicMonth;
            _phaseProgress = (float)(lunarCycles - Math.Floor(lunarCycles));

            // Calculate illumination (0 at new moon, 1 at full moon, 0 at new moon again)
            // Uses cosine curve: 0.5 - 0.5 * cos(2 * PI * phase)
            _illumination = 0.5f - 0.5f * Mathf.Cos(_phaseProgress * Mathf.PI * 2f);

            // Determine discrete phase
            int phaseIndex = Mathf.FloorToInt(_phaseProgress * 8f) % 8;
            _currentPhase = (MoonPhase)phaseIndex;
        }

        private void Update()
        {
            // Recalculate phase periodically (every frame is fine, it's cheap)
            CalculateMoonPhase();

            // Rebuild meshes (phase affects shape)
            BuildMeshes();

            // Render
            RenderMoon();
        }

        private void BuildMeshes()
        {
            Vector3 playerPos = targetCamera != null ? targetCamera.transform.position : Vector3.zero;

            // Calculate moon world position
            float aziRad = moonAzimuth * Mathf.Deg2Rad;
            float elevRad = moonElevation * Mathf.Deg2Rad;

            float cosElev = Mathf.Cos(elevRad);
            float sinElev = Mathf.Sin(elevRad);
            float cosAzi = Mathf.Cos(aziRad);
            float sinAzi = Mathf.Sin(aziRad);

            Vector3 moonPos = new Vector3(
                playerPos.x + cosElev * sinAzi * renderDistance,
                playerPos.y + sinElev * renderDistance,
                playerPos.z + cosElev * cosAzi * renderDistance
            );

            // Calculate moon size in world units
            float moonWorldSize = renderDistance * Mathf.Tan(angularSize * Mathf.Deg2Rad * 0.5f) * 2f;

            // Get billboard vectors
            Vector3 toCamera = (targetCamera != null ? targetCamera.transform.position : Vector3.zero) - moonPos;
            toCamera.Normalize();
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, toCamera).normalized;
            up = Vector3.Cross(toCamera, right).normalized;

            // Build moon disc mesh with phase shadow
            BuildMoonDisc(moonPos, right, up, moonWorldSize);

            // Build glow mesh
            BuildGlowDisc(moonPos, right, up, moonWorldSize);

            // Build halo ring (only visible near full moon)
            BuildHaloRing(moonPos, right, up, moonWorldSize);
        }

        private void BuildMoonDisc(Vector3 center, Vector3 right, Vector3 up, float size)
        {
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();

            float radius = size * 0.5f;
            int segments = moonResolution;

            // Center vertex
            _vertices.Add(center);
            _colors.Add(moonColor);

            // Edge vertices with phase-based illumination
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float y = Mathf.Sin(angle);

                Vector3 edgePos = center + (right * x + up * y) * radius;

                // Calculate illumination for this point based on phase
                // The terminator (shadow line) moves across the moon
                float illuminationAtPoint = CalculatePointIllumination(x);

                Color vertColor = moonColor;
                vertColor.a = illuminationAtPoint;

                _vertices.Add(edgePos);
                _colors.Add(vertColor);
            }

            // Triangles (fan from center)
            for (int i = 1; i <= segments; i++)
            {
                _triangles.Add(0);
                _triangles.Add(i);
                _triangles.Add(i + 1);
            }

            // Create or update mesh
            if (_moonMesh == null)
            {
                _moonMesh = new Mesh();
                _moonMesh.name = "MoonDisc";
            }

            _moonMesh.Clear();
            _moonMesh.SetVertices(_vertices);
            _moonMesh.SetColors(_colors);
            _moonMesh.SetTriangles(_triangles, 0);
            _moonMesh.RecalculateNormals();
        }

        private float CalculatePointIllumination(float x)
        {
            // x is horizontal position on moon disc (-1 to 1)
            // Phase progress determines where the terminator line is

            // Convert phase to terminator position
            // At new moon (0), entire moon is dark
            // At full moon (0.5), entire moon is lit
            // We use the x coordinate to determine if point is in shadow

            float phase = _phaseProgress;

            if (phase < 0.5f)
            {
                // Waxing: right side lit, shadow recedes from right to left
                // Terminator position: starts at right (-1), moves to left (1)
                float terminator = -1f + phase * 4f; // -1 to 1 as phase goes 0 to 0.5
                return x > terminator ? 1f : ShadowFade(x, terminator);
            }
            else
            {
                // Waning: left side goes dark, shadow grows from right
                // Terminator position: starts at left (1), moves to right (-1)
                float terminator = 3f - phase * 4f; // 1 to -1 as phase goes 0.5 to 1
                return x < terminator ? 1f : ShadowFade(x, terminator);
            }
        }

        private float ShadowFade(float x, float terminator)
        {
            // Soft edge at terminator
            float distance = Mathf.Abs(x - terminator);
            float fade = 1f - Mathf.Clamp01(distance / 0.3f);
            return fade * 0.2f; // Dark side still slightly visible (earthshine)
        }

        private void BuildGlowDisc(Vector3 center, Vector3 right, Vector3 up, float moonSize)
        {
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();

            float innerRadius = moonSize * 0.5f;
            float outerRadius = moonSize * 0.5f * glowRadius;
            int segments = 24;

            // Scale glow based on illumination
            float glowScale = glowIntensity * (0.3f + _illumination * 0.7f);

            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float y = Mathf.Sin(angle);

                Vector3 innerPos = center + (right * x + up * y) * innerRadius;
                Vector3 outerPos = center + (right * x + up * y) * outerRadius;

                // Inner edge: brighter
                Color innerColor = glowColor;
                innerColor.a = glowScale;
                _vertices.Add(innerPos);
                _colors.Add(innerColor);

                // Outer edge: fades to transparent
                Color outerColor = glowColor;
                outerColor.a = 0f;
                _vertices.Add(outerPos);
                _colors.Add(outerColor);
            }

            // Triangles (ring)
            for (int i = 0; i < segments; i++)
            {
                int baseIdx = i * 2;
                _triangles.Add(baseIdx);
                _triangles.Add(baseIdx + 2);
                _triangles.Add(baseIdx + 1);

                _triangles.Add(baseIdx + 1);
                _triangles.Add(baseIdx + 2);
                _triangles.Add(baseIdx + 3);
            }

            if (_glowMesh == null)
            {
                _glowMesh = new Mesh();
                _glowMesh.name = "MoonGlow";
            }

            _glowMesh.Clear();
            _glowMesh.SetVertices(_vertices);
            _glowMesh.SetColors(_colors);
            _glowMesh.SetTriangles(_triangles, 0);
        }

        private void BuildHaloRing(Vector3 center, Vector3 right, Vector3 up, float moonSize)
        {
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();

            // Halo only appears near full moon
            // Calculate how close we are to full moon (phase 0.5)
            float distanceToFull = Mathf.Abs(_phaseProgress - 0.5f);
            float haloStrength = 1f - Mathf.Clamp01(distanceToFull * 5f); // Visible within ~0.2 of full

            if (haloStrength < 0.01f)
            {
                // No halo needed
                if (_haloMesh != null)
                {
                    _haloMesh.Clear();
                }
                return;
            }

            float innerRadius = moonSize * 0.5f * haloRadius * (1f - haloThickness);
            float outerRadius = moonSize * 0.5f * haloRadius;
            int segments = 32;

            float haloAlpha = haloIntensity * haloStrength;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float y = Mathf.Sin(angle);

                Vector3 innerPos = center + (right * x + up * y) * innerRadius;
                Vector3 midPos = center + (right * x + up * y) * ((innerRadius + outerRadius) * 0.5f);
                Vector3 outerPos = center + (right * x + up * y) * outerRadius;

                // Inner edge: fade in
                Color innerColor = haloColor;
                innerColor.a = 0f;
                _vertices.Add(innerPos);
                _colors.Add(innerColor);

                // Middle: peak brightness
                Color midColor = haloColor;
                midColor.a = haloAlpha;
                _vertices.Add(midPos);
                _colors.Add(midColor);

                // Outer edge: fade out
                Color outerColor = haloColor;
                outerColor.a = 0f;
                _vertices.Add(outerPos);
                _colors.Add(outerColor);
            }

            // Triangles (double ring: inner-mid and mid-outer)
            for (int i = 0; i < segments; i++)
            {
                int baseIdx = i * 3;

                // Inner to mid ring
                _triangles.Add(baseIdx);
                _triangles.Add(baseIdx + 3);
                _triangles.Add(baseIdx + 1);

                _triangles.Add(baseIdx + 1);
                _triangles.Add(baseIdx + 3);
                _triangles.Add(baseIdx + 4);

                // Mid to outer ring
                _triangles.Add(baseIdx + 1);
                _triangles.Add(baseIdx + 4);
                _triangles.Add(baseIdx + 2);

                _triangles.Add(baseIdx + 2);
                _triangles.Add(baseIdx + 4);
                _triangles.Add(baseIdx + 5);
            }

            if (_haloMesh == null)
            {
                _haloMesh = new Mesh();
                _haloMesh.name = "MoonHalo";
            }

            _haloMesh.Clear();
            _haloMesh.SetVertices(_vertices);
            _haloMesh.SetColors(_colors);
            _haloMesh.SetTriangles(_triangles, 0);
        }

        private void RenderMoon()
        {
            // Draw glow first (behind moon)
            if (_glowMesh != null && glowMaterial != null)
            {
                Graphics.DrawMesh(_glowMesh, Matrix4x4.identity, glowMaterial, 0);
            }

            // Draw moon disc
            if (_moonMesh != null && moonMaterial != null)
            {
                Graphics.DrawMesh(_moonMesh, Matrix4x4.identity, moonMaterial, 0);
            }

            // Draw halo (in front, additive)
            if (_haloMesh != null && _haloMesh.vertexCount > 0 && glowMaterial != null)
            {
                Graphics.DrawMesh(_haloMesh, Matrix4x4.identity, glowMaterial, 0);
            }
        }

        private void OnDestroy()
        {
            DestroyMesh(ref _moonMesh);
            DestroyMesh(ref _glowMesh);
            DestroyMesh(ref _haloMesh);
        }

        private void DestroyMesh(ref Mesh mesh)
        {
            if (mesh != null)
            {
                if (Application.isPlaying)
                    Destroy(mesh);
                else
                    DestroyImmediate(mesh);
                mesh = null;
            }
        }

        // Editor visualization and debug info
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 center = transform.position;

            float aziRad = moonAzimuth * Mathf.Deg2Rad;
            float elevRad = moonElevation * Mathf.Deg2Rad;

            Vector3 moonDir = new Vector3(
                Mathf.Cos(elevRad) * Mathf.Sin(aziRad),
                Mathf.Sin(elevRad),
                Mathf.Cos(elevRad) * Mathf.Cos(aziRad)
            );

            Gizmos.DrawLine(center, center + moonDir * 100f);
            Gizmos.DrawWireSphere(center + moonDir * renderDistance, renderDistance * Mathf.Tan(angularSize * Mathf.Deg2Rad * 0.5f));
        }

        /// <summary>
        /// Debug: Get current moon phase info for UI display.
        /// </summary>
        public string GetPhaseInfo()
        {
            string phaseName = _currentPhase switch
            {
                MoonPhase.NewMoon => "New Moon",
                MoonPhase.WaxingCrescent => "Waxing Crescent",
                MoonPhase.FirstQuarter => "First Quarter",
                MoonPhase.WaxingGibbous => "Waxing Gibbous",
                MoonPhase.FullMoon => "Full Moon",
                MoonPhase.WaningGibbous => "Waning Gibbous",
                MoonPhase.LastQuarter => "Last Quarter",
                MoonPhase.WaningCrescent => "Waning Crescent",
                _ => "Unknown"
            };

            return $"{phaseName} ({_illumination * 100f:F0}% illuminated)";
        }
    }
}
