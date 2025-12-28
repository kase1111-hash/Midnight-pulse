// ============================================================================
// Nightflow - Procedural Mesh Renderer
// Bridges ECS mesh buffers to Unity rendering via Graphics.DrawMesh
// This is the CRITICAL missing link for rendering procedural geometry
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;
using Nightflow.Systems;

namespace Nightflow.Rendering
{
    /// <summary>
    /// Renders procedurally generated meshes from ECS buffers.
    /// Queries entities with ProceduralMeshData and converts their mesh buffers
    /// to Unity Mesh objects for rendering with Graphics.DrawMesh.
    ///
    /// This MonoBehaviour bridges the gap between ECS mesh generation and Unity rendering.
    /// </summary>
    [ExecuteAlways]
    public class ProceduralMeshRenderer : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material roadMaterial;
        [SerializeField] private Material barrierMaterial;
        [SerializeField] private Material laneMarkingMaterial;
        [SerializeField] private Material tunnelMaterial;
        [SerializeField] private Material overpassMaterial;
        [SerializeField] private Material hazardMaterial;
        [SerializeField] private Material vehicleMaterial;

        [Header("Rendering Settings")]
        [SerializeField] private int renderLayer = 0;
        [SerializeField] private bool castShadows = false;
        [SerializeField] private bool receiveShadows = false;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool forceRefreshMeshes = false;

        // Cached Unity meshes keyed by entity index
        private Dictionary<int, CachedMesh> _trackMeshes = new Dictionary<int, CachedMesh>();
        private Dictionary<int, CachedMesh> _hazardMeshes = new Dictionary<int, CachedMesh>();
        private Dictionary<int, CachedMesh> _vehicleMeshes = new Dictionary<int, CachedMesh>();

        // Mesh data lists for building meshes
        private List<Vector3> _vertices = new List<Vector3>();
        private List<Vector3> _normals = new List<Vector3>();
        private List<Vector2> _uvs = new List<Vector2>();
        private List<Color> _colors = new List<Color>();
        private List<int> _triangles = new List<int>();

        // ECS world reference
        private World _world;
        private EntityManager _entityManager;
        private bool _isInitialized;

        private int _renderedMeshCount;
        private int _lastFrameRenderedCount;

        private struct CachedMesh
        {
            public Mesh Mesh;
            public Material[] Materials;
            public int SubMeshCount;
            public int Version;
            public Bounds Bounds;
        }

        private void Awake()
        {
            CreateDefaultMaterials();
        }

        private void OnEnable()
        {
            TryInitialize();
        }

        private void Update()
        {
            TryInitialize();

            if (!_isInitialized)
                return;

            if (forceRefreshMeshes)
            {
                ClearAllCachedMeshes();
                forceRefreshMeshes = false;
            }

            _renderedMeshCount = 0;

            // Query and render track segment meshes
            RenderTrackSegments();

            // Query and render hazard meshes
            RenderHazards();

            // Query and render vehicle meshes
            RenderVehicles();

            _lastFrameRenderedCount = _renderedMeshCount;
        }

        private void TryInitialize()
        {
            if (_isInitialized && _world != null && _world.IsCreated)
                return;

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
            {
                _isInitialized = false;
                return;
            }

            _entityManager = _world.EntityManager;
            _isInitialized = true;

            if (showDebugInfo)
            {
                Debug.Log("[ProceduralMeshRenderer] Initialized with ECS world");
            }
        }

        private void CreateDefaultMaterials()
        {
            // Road surface: dark with slight blue tint
            if (roadMaterial == null)
            {
                roadMaterial = CreateWireframeMaterial(
                    new Color(0.1f, 0.1f, 0.15f, 1f),
                    new Color(0.2f, 0.3f, 0.5f, 1f),
                    1.0f
                );
                roadMaterial.name = "Road_Generated";
            }

            // Barriers: gray metallic
            if (barrierMaterial == null)
            {
                barrierMaterial = CreateWireframeMaterial(
                    new Color(0.3f, 0.3f, 0.35f, 1f),
                    new Color(0.5f, 0.5f, 0.55f, 1f),
                    0.8f
                );
                barrierMaterial.name = "Barrier_Generated";
            }

            // Lane markings: neon blue glow
            if (laneMarkingMaterial == null)
            {
                laneMarkingMaterial = CreateEmissiveMaterial(
                    new Color(0.27f, 0.53f, 1f, 1f),
                    2.0f
                );
                laneMarkingMaterial.name = "LaneMarking_Generated";
            }

            // Tunnel walls: darker with cyan accent
            if (tunnelMaterial == null)
            {
                tunnelMaterial = CreateWireframeMaterial(
                    new Color(0.05f, 0.08f, 0.1f, 1f),
                    new Color(0f, 0.6f, 0.8f, 1f),
                    0.6f
                );
                tunnelMaterial.name = "Tunnel_Generated";
            }

            // Overpass: similar to road but with orange accents
            if (overpassMaterial == null)
            {
                overpassMaterial = CreateWireframeMaterial(
                    new Color(0.12f, 0.1f, 0.08f, 1f),
                    new Color(1f, 0.6f, 0.2f, 1f),
                    0.9f
                );
                overpassMaterial.name = "Overpass_Generated";
            }

            // Hazards: bright orange/red warning
            if (hazardMaterial == null)
            {
                hazardMaterial = CreateEmissiveMaterial(
                    new Color(1f, 0.4f, 0f, 1f),
                    3.0f
                );
                hazardMaterial.name = "Hazard_Generated";
            }

            // Vehicles: cyan wireframe
            if (vehicleMaterial == null)
            {
                vehicleMaterial = CreateWireframeMaterial(
                    new Color(0.05f, 0.1f, 0.15f, 1f),
                    new Color(0f, 1f, 0.8f, 1f),
                    1.5f
                );
                vehicleMaterial.name = "Vehicle_Generated";
            }
        }

        private Material CreateWireframeMaterial(Color baseColor, Color edgeColor, float glowIntensity)
        {
            // Try to find URP unlit shader first
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.color = baseColor;
            mat.SetColor("_EmissionColor", edgeColor * glowIntensity);

            // Enable emission if using Standard shader
            if (shader.name.Contains("Standard"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            return mat;
        }

        private Material CreateEmissiveMaterial(Color emissionColor, float intensity)
        {
            // Try additive particle shader for maximum glow
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            Material mat = new Material(shader);
            mat.color = emissionColor;

            // Configure for additive blending
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 4); // Additive
            }

            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)RenderQueue.Transparent;

            return mat;
        }

        private void RenderTrackSegments()
        {
            if (!_isInitialized) return;

            var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<TrackSegmentTag>(),
                ComponentType.ReadOnly<ProceduralMeshData>()
            );

            var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                var meshData = _entityManager.GetComponentData<ProceduralMeshData>(entity);

                // Skip if mesh hasn't been generated yet
                if (!meshData.IsGenerated)
                    continue;

                int entityIndex = entity.Index;

                // Check if we need to build/update the cached mesh
                if (!_trackMeshes.TryGetValue(entityIndex, out var cachedMesh) ||
                    cachedMesh.Mesh == null)
                {
                    cachedMesh = BuildMeshFromEntity(entity, meshData);
                    _trackMeshes[entityIndex] = cachedMesh;
                }

                // Render the mesh
                if (cachedMesh.Mesh != null && cachedMesh.Mesh.vertexCount > 0)
                {
                    RenderCachedMesh(cachedMesh, Matrix4x4.identity);
                    _renderedMeshCount++;
                }
            }

            entities.Dispose();
        }

        private void RenderHazards()
        {
            if (!_isInitialized) return;

            var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<HazardTag>(),
                ComponentType.ReadOnly<HazardMeshData>()
            );

            var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (!_entityManager.HasComponent<MeshVertex>(entity))
                    continue;

                var vertices = _entityManager.GetBuffer<MeshVertex>(entity, true);
                if (vertices.Length == 0)
                    continue;

                int entityIndex = entity.Index;

                if (!_hazardMeshes.TryGetValue(entityIndex, out var cachedMesh) ||
                    cachedMesh.Mesh == null)
                {
                    cachedMesh = BuildMeshFromBuffers(entity, hazardMaterial);
                    _hazardMeshes[entityIndex] = cachedMesh;
                }

                if (cachedMesh.Mesh != null && cachedMesh.Mesh.vertexCount > 0)
                {
                    var transform = _entityManager.GetComponentData<WorldTransform>(entity);
                    Matrix4x4 matrix = Matrix4x4.TRS(
                        new Vector3(transform.Position.x, transform.Position.y, transform.Position.z),
                        new Quaternion(transform.Rotation.value.x, transform.Rotation.value.y,
                                       transform.Rotation.value.z, transform.Rotation.value.w),
                        Vector3.one
                    );

                    RenderCachedMesh(cachedMesh, matrix);
                    _renderedMeshCount++;
                }
            }

            entities.Dispose();
        }

        private void RenderVehicles()
        {
            if (!_isInitialized) return;

            // Player vehicles
            RenderVehicleType<PlayerVehicleTag>();

            // Traffic vehicles
            RenderVehicleType<TrafficVehicleTag>();

            // Emergency vehicles
            RenderVehicleType<EmergencyVehicleTag>();

            // Ghost vehicles
            RenderVehicleType<GhostVehicleTag>();
        }

        private void RenderVehicleType<T>() where T : struct, IComponentData
        {
            var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<T>(),
                ComponentType.ReadOnly<VehicleMeshData>()
            );

            var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (!_entityManager.HasComponent<MeshVertex>(entity))
                    continue;

                var vertices = _entityManager.GetBuffer<MeshVertex>(entity, true);
                if (vertices.Length == 0)
                    continue;

                int entityIndex = entity.Index;

                if (!_vehicleMeshes.TryGetValue(entityIndex, out var cachedMesh) ||
                    cachedMesh.Mesh == null)
                {
                    cachedMesh = BuildMeshFromBuffers(entity, vehicleMaterial);
                    _vehicleMeshes[entityIndex] = cachedMesh;
                }

                if (cachedMesh.Mesh != null && cachedMesh.Mesh.vertexCount > 0)
                {
                    var transform = _entityManager.GetComponentData<WorldTransform>(entity);
                    Matrix4x4 matrix = Matrix4x4.TRS(
                        new Vector3(transform.Position.x, transform.Position.y, transform.Position.z),
                        new Quaternion(transform.Rotation.value.x, transform.Rotation.value.y,
                                       transform.Rotation.value.z, transform.Rotation.value.w),
                        Vector3.one
                    );

                    RenderCachedMesh(cachedMesh, matrix);
                    _renderedMeshCount++;
                }
            }

            entities.Dispose();
        }

        private CachedMesh BuildMeshFromEntity(Entity entity, ProceduralMeshData meshData)
        {
            var cachedMesh = new CachedMesh();

            if (!_entityManager.HasComponent<MeshVertex>(entity) ||
                !_entityManager.HasComponent<MeshTriangle>(entity))
            {
                if (showDebugInfo)
                    Debug.LogWarning($"[ProceduralMeshRenderer] Entity {entity.Index} missing mesh buffers");
                return cachedMesh;
            }

            var vertexBuffer = _entityManager.GetBuffer<MeshVertex>(entity, true);
            var triangleBuffer = _entityManager.GetBuffer<MeshTriangle>(entity, true);
            var subMeshBuffer = _entityManager.HasComponent<SubMeshRange>(entity)
                ? _entityManager.GetBuffer<SubMeshRange>(entity, true)
                : default;

            if (vertexBuffer.Length == 0)
            {
                if (showDebugInfo)
                    Debug.LogWarning($"[ProceduralMeshRenderer] Entity {entity.Index} has empty vertex buffer");
                return cachedMesh;
            }

            // Build vertex arrays
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _colors.Clear();

            for (int i = 0; i < vertexBuffer.Length; i++)
            {
                var v = vertexBuffer[i];
                _vertices.Add(new Vector3(v.Position.x, v.Position.y, v.Position.z));
                _normals.Add(new Vector3(v.Normal.x, v.Normal.y, v.Normal.z));
                _uvs.Add(new Vector2(v.UV.x, v.UV.y));
                _colors.Add(new Color(v.Color.x, v.Color.y, v.Color.z, v.Color.w));
            }

            // Create mesh
            Mesh mesh = new Mesh();
            mesh.name = $"TrackSegment_{entity.Index}";
            mesh.indexFormat = _vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetColors(_colors);

            // Handle sub-meshes
            int subMeshCount = subMeshBuffer.IsCreated ? subMeshBuffer.Length : 1;
            if (subMeshCount == 0) subMeshCount = 1;

            mesh.subMeshCount = subMeshCount;

            Material[] materials = new Material[subMeshCount];

            if (subMeshBuffer.IsCreated && subMeshBuffer.Length > 0)
            {
                for (int s = 0; s < subMeshBuffer.Length; s++)
                {
                    var subMesh = subMeshBuffer[s];

                    _triangles.Clear();
                    int endIndex = subMesh.StartIndex + subMesh.IndexCount;
                    for (int i = subMesh.StartIndex; i < endIndex && i < triangleBuffer.Length; i++)
                    {
                        _triangles.Add(triangleBuffer[i].Index);
                    }

                    mesh.SetTriangles(_triangles, s);

                    // Assign material based on type
                    materials[s] = GetMaterialForType(subMesh.MaterialType);
                }
            }
            else
            {
                // Single mesh, all triangles
                _triangles.Clear();
                for (int i = 0; i < triangleBuffer.Length; i++)
                {
                    _triangles.Add(triangleBuffer[i].Index);
                }
                mesh.SetTriangles(_triangles, 0);
                materials[0] = roadMaterial;
            }

            mesh.RecalculateBounds();

            cachedMesh.Mesh = mesh;
            cachedMesh.Materials = materials;
            cachedMesh.SubMeshCount = subMeshCount;
            cachedMesh.Bounds = mesh.bounds;
            cachedMesh.Version = 1;

            if (showDebugInfo)
            {
                Debug.Log($"[ProceduralMeshRenderer] Built mesh for entity {entity.Index}: " +
                         $"{_vertices.Count} verts, {triangleBuffer.Length / 3} tris, {subMeshCount} submeshes");
            }

            return cachedMesh;
        }

        private CachedMesh BuildMeshFromBuffers(Entity entity, Material defaultMaterial)
        {
            var cachedMesh = new CachedMesh();

            var vertexBuffer = _entityManager.GetBuffer<MeshVertex>(entity, true);
            var triangleBuffer = _entityManager.GetBuffer<MeshTriangle>(entity, true);

            if (vertexBuffer.Length == 0)
                return cachedMesh;

            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _colors.Clear();

            for (int i = 0; i < vertexBuffer.Length; i++)
            {
                var v = vertexBuffer[i];
                _vertices.Add(new Vector3(v.Position.x, v.Position.y, v.Position.z));
                _normals.Add(new Vector3(v.Normal.x, v.Normal.y, v.Normal.z));
                _uvs.Add(new Vector2(v.UV.x, v.UV.y));
                _colors.Add(new Color(v.Color.x, v.Color.y, v.Color.z, v.Color.w));
            }

            _triangles.Clear();
            for (int i = 0; i < triangleBuffer.Length; i++)
            {
                _triangles.Add(triangleBuffer[i].Index);
            }

            Mesh mesh = new Mesh();
            mesh.name = $"ProceduralMesh_{entity.Index}";
            mesh.indexFormat = _vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();

            cachedMesh.Mesh = mesh;
            cachedMesh.Materials = new Material[] { defaultMaterial };
            cachedMesh.SubMeshCount = 1;
            cachedMesh.Bounds = mesh.bounds;
            cachedMesh.Version = 1;

            return cachedMesh;
        }

        private Material GetMaterialForType(int materialType)
        {
            return materialType switch
            {
                0 => roadMaterial,      // Road surface
                1 => barrierMaterial,   // Barriers
                2 => laneMarkingMaterial, // Lane markings
                3 => tunnelMaterial,    // Tunnel walls
                4 => overpassMaterial,  // Overpass
                _ => roadMaterial
            };
        }

        private void RenderCachedMesh(CachedMesh cachedMesh, Matrix4x4 matrix)
        {
            if (cachedMesh.Mesh == null)
                return;

            for (int s = 0; s < cachedMesh.SubMeshCount; s++)
            {
                Material mat = cachedMesh.Materials != null && s < cachedMesh.Materials.Length
                    ? cachedMesh.Materials[s]
                    : roadMaterial;

                if (mat == null)
                    mat = roadMaterial;

                Graphics.DrawMesh(
                    cachedMesh.Mesh,
                    matrix,
                    mat,
                    renderLayer,
                    null,
                    s,
                    null,
                    castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows
                );
            }
        }

        private void ClearAllCachedMeshes()
        {
            foreach (var kvp in _trackMeshes)
            {
                if (kvp.Value.Mesh != null)
                {
                    if (Application.isPlaying)
                        Destroy(kvp.Value.Mesh);
                    else
                        DestroyImmediate(kvp.Value.Mesh);
                }
            }
            _trackMeshes.Clear();

            foreach (var kvp in _hazardMeshes)
            {
                if (kvp.Value.Mesh != null)
                {
                    if (Application.isPlaying)
                        Destroy(kvp.Value.Mesh);
                    else
                        DestroyImmediate(kvp.Value.Mesh);
                }
            }
            _hazardMeshes.Clear();

            foreach (var kvp in _vehicleMeshes)
            {
                if (kvp.Value.Mesh != null)
                {
                    if (Application.isPlaying)
                        Destroy(kvp.Value.Mesh);
                    else
                        DestroyImmediate(kvp.Value.Mesh);
                }
            }
            _vehicleMeshes.Clear();

            if (showDebugInfo)
            {
                Debug.Log("[ProceduralMeshRenderer] Cleared all cached meshes");
            }
        }

        private void OnDisable()
        {
            ClearAllCachedMeshes();
        }

        private void OnDestroy()
        {
            ClearAllCachedMeshes();
        }

        private void OnGUI()
        {
            if (!showDebugInfo)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Label($"ProceduralMeshRenderer");
            GUILayout.Label($"  Initialized: {_isInitialized}");
            GUILayout.Label($"  Track meshes cached: {_trackMeshes.Count}");
            GUILayout.Label($"  Hazard meshes cached: {_hazardMeshes.Count}");
            GUILayout.Label($"  Vehicle meshes cached: {_vehicleMeshes.Count}");
            GUILayout.Label($"  Last frame rendered: {_lastFrameRenderedCount}");
            GUILayout.EndArea();
        }
    }
}
