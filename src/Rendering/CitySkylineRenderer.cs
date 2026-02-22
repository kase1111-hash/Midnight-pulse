// ============================================================================
// Nightflow - City Skyline Renderer
// MonoBehaviour that renders the procedural city skyline backdrop
// Works in conjunction with CitySkylineSystem (ECS) for data
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using Nightflow.Components;

namespace Nightflow.Rendering
{
    /// <summary>
    /// Renders the city skyline as a procedural backdrop.
    /// Creates mesh geometry for building silhouettes and renders lit windows.
    /// Attach to a GameObject in the scene to enable skyline rendering.
    /// </summary>
    [ExecuteAlways]
    public class CitySkylineRenderer : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private Material silhouetteMaterial;
        [SerializeField] private Material windowMaterial;
        [SerializeField] private Camera targetCamera;

        [Header("Skyline Settings")]
        [SerializeField] private float skylineDistance = 800f;
        [SerializeField] private float minBuildingHeight = 40f;
        [SerializeField] private float maxBuildingHeight = 280f;
        [SerializeField] private int buildingCount = 120;

        [Header("Window Colors")]
        [SerializeField] private Color windowYellow = new Color(1f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color windowWhite = new Color(0.95f, 0.95f, 1f, 1f);
        [SerializeField] private Color windowOrange = new Color(1f, 0.6f, 0.2f, 1f);
        [SerializeField] private Color windowCyan = new Color(0.4f, 0.9f, 1f, 1f);
        [SerializeField] private Color silhouetteColor = new Color(0.02f, 0.02f, 0.05f, 1f);

        [Header("Animation")]
        // TODO: Window toggle should synchronize with music beat when BPM detection is available
        [SerializeField] private float windowToggleInterval = 8f;
        [SerializeField] private float windowLitRatio = 0.65f;

        // Procedural mesh data
        private Mesh _silhouetteMesh;
        private Mesh _windowMesh;
        private List<Vector3> _silhouetteVertices = new List<Vector3>();
        private List<int> _silhouetteTriangles = new List<int>();
        private List<Vector3> _windowVertices = new List<Vector3>();
        private List<Color> _windowColors = new List<Color>();
        private List<int> _windowTriangles = new List<int>();

        // Building data (generated once, windows animated)
        private BuildingData[] _buildings;
        private float _animationTime;
        private bool _needsRebuild = true;

        // ECS access
        private EntityManager _entityManager;
        private EntityQuery _skylineQuery;
        private bool _ecsInitialized;

        private struct BuildingData
        {
            public float Angle;
            public float WidthAngle;
            public float Height;
            public int WindowColumns;
            public int WindowRows;
            public uint WindowSeed;
            public bool[] WindowLit;
            public int[] WindowColorIndex;
            public float[] NextToggleTime;
        }

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
            TryInitializeECS();
            GenerateSkyline();
        }

        private void TryInitializeECS()
        {
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                _skylineQuery = _entityManager.CreateEntityQuery(typeof(CitySkylineTag));
                _ecsInitialized = true;
            }
        }

        private void CreateDefaultMaterials()
        {
            // Create silhouette material if not assigned
            if (silhouetteMaterial == null)
            {
                silhouetteMaterial = new Material(Shader.Find("Unlit/Color"));
                silhouetteMaterial.color = silhouetteColor;
                silhouetteMaterial.renderQueue = (int)RenderQueue.Background;
            }

            // Create window material if not assigned (additive for glow effect)
            if (windowMaterial == null)
            {
                // Try to find an additive shader, fall back to unlit
                var shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");

                windowMaterial = new Material(shader);
                windowMaterial.SetFloat("_Mode", 2); // Additive
                windowMaterial.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        private void GenerateSkyline()
        {
            // Initialize building array
            _buildings = new BuildingData[buildingCount];

            var random = new System.Random(54321);
            float angleStep = Mathf.PI * 2f / buildingCount;
            float currentAngle = 0f;

            for (int i = 0; i < buildingCount; i++)
            {
                // Building dimensions
                float widthAngle = Mathf.Lerp(0.015f, 0.045f, (float)random.NextDouble());

                // Height with distribution favoring medium heights
                float heightRoll = (float)random.NextDouble();
                float height;
                if (heightRoll < 0.5f)
                    height = 0.2f + heightRoll * 0.6f;
                else if (heightRoll < 0.85f)
                    height = 0.5f + (heightRoll - 0.5f) * 0.86f;
                else
                    height = 0.8f + (heightRoll - 0.85f) * 1.33f;

                // Window grid
                int windowCols = Mathf.RoundToInt(Mathf.Lerp(4, 12, widthAngle / 0.045f));
                int windowRows = Mathf.RoundToInt(Mathf.Lerp(8, 40, height));
                int totalWindows = windowCols * windowRows;

                // Generate initial window states
                bool[] windowLit = new bool[totalWindows];
                int[] windowColors = new int[totalWindows];
                float[] nextToggle = new float[totalWindows];

                for (int w = 0; w < totalWindows; w++)
                {
                    windowLit[w] = random.NextDouble() < windowLitRatio;

                    // Color distribution: 75% yellow, 10% white, 10% orange, 5% cyan
                    float colorRoll = (float)random.NextDouble();
                    if (colorRoll < 0.75f)
                        windowColors[w] = 0;
                    else if (colorRoll < 0.85f)
                        windowColors[w] = 1;
                    else if (colorRoll < 0.95f)
                        windowColors[w] = 2;
                    else
                        windowColors[w] = 3;

                    // Random toggle time
                    nextToggle[w] = (float)random.NextDouble() * windowToggleInterval * 2f;
                }

                _buildings[i] = new BuildingData
                {
                    Angle = currentAngle,
                    WidthAngle = widthAngle,
                    Height = height,
                    WindowColumns = windowCols,
                    WindowRows = windowRows,
                    WindowSeed = (uint)random.Next(),
                    WindowLit = windowLit,
                    WindowColorIndex = windowColors,
                    NextToggleTime = nextToggle
                };

                currentAngle += widthAngle + Mathf.Lerp(0.005f, 0.015f, (float)random.NextDouble());
            }

            _needsRebuild = true;
        }

        private void Update()
        {
            if (_buildings == null || _buildings.Length == 0)
            {
                GenerateSkyline();
                return;
            }

            // Animate windows
            _animationTime += Time.deltaTime;
            AnimateWindows();

            // Rebuild meshes
            BuildMeshes();

            // Render
            RenderSkyline();
        }

        private void AnimateWindows()
        {
            var random = new System.Random((int)(_animationTime * 100));

            for (int b = 0; b < _buildings.Length; b++)
            {
                ref var building = ref _buildings[b];
                int totalWindows = building.WindowColumns * building.WindowRows;

                for (int w = 0; w < totalWindows; w++)
                {
                    if (_animationTime >= building.NextToggleTime[w])
                    {
                        // Toggle window
                        building.WindowLit[w] = !building.WindowLit[w];

                        // Schedule next toggle
                        float variance = windowToggleInterval * 0.5f;
                        building.NextToggleTime[w] = _animationTime +
                            windowToggleInterval + (float)(random.NextDouble() - 0.5) * variance * 2f;

                        _needsRebuild = true;
                    }
                }
            }
        }

        private void BuildMeshes()
        {
            if (!_needsRebuild) return;

            Vector3 playerPos = targetCamera != null ? targetCamera.transform.position : Vector3.zero;

            // Clear lists
            _silhouetteVertices.Clear();
            _silhouetteTriangles.Clear();
            _windowVertices.Clear();
            _windowColors.Clear();
            _windowTriangles.Clear();

            // Generate geometry for each building
            for (int b = 0; b < _buildings.Length; b++)
            {
                ref var building = ref _buildings[b];

                float buildingHeight = minBuildingHeight + building.Height * (maxBuildingHeight - minBuildingHeight);

                // Calculate corner positions
                Vector3 GetPos(float angle, float height)
                {
                    float x = playerPos.x + Mathf.Cos(angle) * skylineDistance;
                    float z = playerPos.z + Mathf.Sin(angle) * skylineDistance;
                    return new Vector3(x, height, z);
                }

                Vector3 bl = GetPos(building.Angle, 0);
                Vector3 br = GetPos(building.Angle + building.WidthAngle, 0);
                Vector3 tl = GetPos(building.Angle, buildingHeight);
                Vector3 tr = GetPos(building.Angle + building.WidthAngle, buildingHeight);

                // Add silhouette quad
                int baseVert = _silhouetteVertices.Count;
                _silhouetteVertices.Add(bl);
                _silhouetteVertices.Add(br);
                _silhouetteVertices.Add(tr);
                _silhouetteVertices.Add(tl);

                _silhouetteTriangles.Add(baseVert);
                _silhouetteTriangles.Add(baseVert + 2);
                _silhouetteTriangles.Add(baseVert + 1);
                _silhouetteTriangles.Add(baseVert);
                _silhouetteTriangles.Add(baseVert + 3);
                _silhouetteTriangles.Add(baseVert + 2);

                // Add lit windows
                int totalWindows = building.WindowColumns * building.WindowRows;
                for (int w = 0; w < totalWindows; w++)
                {
                    if (!building.WindowLit[w]) continue;

                    int col = w % building.WindowColumns;
                    int row = w / building.WindowColumns;

                    float u = (col + 0.5f) / building.WindowColumns;
                    float v = (row + 0.5f) / building.WindowRows;

                    // Window position
                    Vector3 windowPos = Vector3.Lerp(
                        Vector3.Lerp(bl, br, u),
                        Vector3.Lerp(tl, tr, u),
                        v
                    );

                    // Window size (billboard quad)
                    float windowSize = 2.5f;
                    Vector3 right = (br - bl).normalized * windowSize * 0.5f;
                    Vector3 up = Vector3.up * windowSize * 0.5f;

                    // Window color
                    Color color = GetWindowColor(building.WindowColorIndex[w]);

                    // Fade based on height (lower windows dimmer due to "fog")
                    float fade = Mathf.Lerp(0.3f, 1f, v);
                    color *= fade;
                    color.a = 1f;

                    // Add window quad
                    int wBase = _windowVertices.Count;
                    _windowVertices.Add(windowPos - right - up);
                    _windowVertices.Add(windowPos + right - up);
                    _windowVertices.Add(windowPos + right + up);
                    _windowVertices.Add(windowPos - right + up);

                    _windowColors.Add(color);
                    _windowColors.Add(color);
                    _windowColors.Add(color);
                    _windowColors.Add(color);

                    _windowTriangles.Add(wBase);
                    _windowTriangles.Add(wBase + 2);
                    _windowTriangles.Add(wBase + 1);
                    _windowTriangles.Add(wBase);
                    _windowTriangles.Add(wBase + 3);
                    _windowTriangles.Add(wBase + 2);
                }
            }

            // Build meshes
            if (_silhouetteMesh == null)
            {
                _silhouetteMesh = new Mesh();
                _silhouetteMesh.name = "SkylineSilhouette";
            }
            _silhouetteMesh.Clear();
            _silhouetteMesh.SetVertices(_silhouetteVertices);
            _silhouetteMesh.SetTriangles(_silhouetteTriangles, 0);
            _silhouetteMesh.RecalculateNormals();

            if (_windowMesh == null)
            {
                _windowMesh = new Mesh();
                _windowMesh.name = "SkylineWindows";
            }
            _windowMesh.Clear();
            _windowMesh.SetVertices(_windowVertices);
            _windowMesh.SetColors(_windowColors);
            _windowMesh.SetTriangles(_windowTriangles, 0);

            _needsRebuild = false;
        }

        private Color GetWindowColor(int colorIndex)
        {
            return colorIndex switch
            {
                0 => windowYellow,
                1 => windowWhite,
                2 => windowOrange,
                3 => windowCyan,
                _ => windowYellow
            };
        }

        private void RenderSkyline()
        {
            if (_silhouetteMesh == null || _windowMesh == null) return;
            if (silhouetteMaterial == null || windowMaterial == null) return;

            // Draw silhouettes (background layer)
            Graphics.DrawMesh(_silhouetteMesh, Matrix4x4.identity, silhouetteMaterial, 0);

            // Draw windows (transparent/additive layer)
            Graphics.DrawMesh(_windowMesh, Matrix4x4.identity, windowMaterial, 0);
        }

        private void OnDestroy()
        {
            if (_silhouetteMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_silhouetteMesh);
                else
                    DestroyImmediate(_silhouetteMesh);
            }

            if (_windowMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_windowMesh);
                else
                    DestroyImmediate(_windowMesh);
            }
        }

        // Editor visualization
        private void OnDrawGizmosSelected()
        {
            if (_buildings == null) return;

            Gizmos.color = Color.cyan;
            Vector3 center = transform.position;

            // Draw skyline circle
            int segments = 64;
            for (int i = 0; i < segments; i++)
            {
                float a1 = (i / (float)segments) * Mathf.PI * 2f;
                float a2 = ((i + 1) / (float)segments) * Mathf.PI * 2f;

                Vector3 p1 = center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * skylineDistance;
                Vector3 p2 = center + new Vector3(Mathf.Cos(a2), 0, Mathf.Sin(a2)) * skylineDistance;

                Gizmos.DrawLine(p1, p2);
            }
        }
    }
}
