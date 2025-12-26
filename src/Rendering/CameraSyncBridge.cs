// ============================================================================
// Nightflow - Camera Sync Bridge
// MonoBehaviour that syncs ECS CameraState to Unity Camera transform
// ============================================================================

using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;

namespace Nightflow.Rendering
{
    /// <summary>
    /// Bridges ECS CameraState component to Unity Camera GameObject.
    /// Reads camera position, rotation, and FOV from ECS and applies to the Camera.
    /// This is essential for rendering - without it, the camera stays at origin.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraSyncBridge : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Smoothing applied to camera movement (0 = instant, higher = smoother)")]
        [Range(0f, 20f)]
        [SerializeField] private float positionSmoothing = 0f;

        [Tooltip("Smoothing applied to camera rotation (0 = instant, higher = smoother)")]
        [Range(0f, 20f)]
        [SerializeField] private float rotationSmoothing = 0f;

        [Tooltip("Smoothing applied to FOV changes (0 = instant, higher = smoother)")]
        [Range(0f, 10f)]
        [SerializeField] private float fovSmoothing = 5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Cached references
        private Camera _camera;
        private EntityManager _entityManager;
        private EntityQuery _cameraStateQuery;
        private bool _ecsInitialized;

        // Cached state for smoothing
        private Vector3 _currentPosition;
        private Quaternion _currentRotation;
        private float _currentFOV;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _currentPosition = transform.position;
            _currentRotation = transform.rotation;
            _currentFOV = _camera.fieldOfView;
        }

        private void Start()
        {
            TryInitializeECS();
        }

        private void OnEnable()
        {
            TryInitializeECS();
        }

        /// <summary>
        /// Initialize ECS query for CameraState component.
        /// </summary>
        private void TryInitializeECS()
        {
            if (_ecsInitialized) return;

            if (World.DefaultGameObjectInjectionWorld == null)
                return;

            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _cameraStateQuery = _entityManager.CreateEntityQuery(typeof(CameraState));
            _ecsInitialized = true;

            if (showDebugInfo)
            {
                Debug.Log("[CameraSyncBridge] ECS initialized successfully");
            }
        }

        private void LateUpdate()
        {
            if (!_ecsInitialized)
            {
                TryInitializeECS();
                return;
            }

            // Get CameraState from ECS
            if (_cameraStateQuery.CalculateEntityCount() == 0)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("[CameraSyncBridge] No CameraState entity found");
                }
                return;
            }

            using var entities = _cameraStateQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) return;

            var cameraState = _entityManager.GetComponentData<CameraState>(entities[0]);

            // Convert ECS types to Unity types
            Vector3 targetPosition = new Vector3(
                cameraState.Position.x,
                cameraState.Position.y,
                cameraState.Position.z
            );

            Quaternion targetRotation = new Quaternion(
                cameraState.Rotation.value.x,
                cameraState.Rotation.value.y,
                cameraState.Rotation.value.z,
                cameraState.Rotation.value.w
            );

            float targetFOV = cameraState.FOV;

            // Apply smoothing if enabled
            float deltaTime = Time.deltaTime;

            if (positionSmoothing > 0f)
            {
                _currentPosition = Vector3.Lerp(
                    _currentPosition,
                    targetPosition,
                    positionSmoothing * deltaTime
                );
            }
            else
            {
                _currentPosition = targetPosition;
            }

            if (rotationSmoothing > 0f)
            {
                _currentRotation = Quaternion.Slerp(
                    _currentRotation,
                    targetRotation,
                    rotationSmoothing * deltaTime
                );
            }
            else
            {
                _currentRotation = targetRotation;
            }

            if (fovSmoothing > 0f)
            {
                _currentFOV = Mathf.Lerp(
                    _currentFOV,
                    targetFOV,
                    fovSmoothing * deltaTime
                );
            }
            else
            {
                _currentFOV = targetFOV;
            }

            // Apply to camera transform
            transform.position = _currentPosition;
            transform.rotation = _currentRotation;
            _camera.fieldOfView = _currentFOV;

            if (showDebugInfo)
            {
                Debug.Log($"[CameraSyncBridge] Pos: {_currentPosition}, FOV: {_currentFOV:F1}, Mode: {cameraState.Mode}");
            }
        }

        /// <summary>
        /// Force an immediate sync without smoothing.
        /// Useful for teleporting the camera.
        /// </summary>
        public void ForceSync()
        {
            if (!_ecsInitialized) return;

            if (_cameraStateQuery.CalculateEntityCount() == 0) return;

            using var entities = _cameraStateQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) return;

            var cameraState = _entityManager.GetComponentData<CameraState>(entities[0]);

            _currentPosition = new Vector3(
                cameraState.Position.x,
                cameraState.Position.y,
                cameraState.Position.z
            );

            _currentRotation = new Quaternion(
                cameraState.Rotation.value.x,
                cameraState.Rotation.value.y,
                cameraState.Rotation.value.z,
                cameraState.Rotation.value.w
            );

            _currentFOV = cameraState.FOV;

            transform.position = _currentPosition;
            transform.rotation = _currentRotation;
            _camera.fieldOfView = _currentFOV;
        }

        private void OnDestroy()
        {
            // EntityQuery cleanup is handled by EntityManager
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showDebugInfo) return;

            // Draw camera frustum in scene view
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;

            float aspect = _camera != null ? _camera.aspect : 16f / 9f;
            float fov = _camera != null ? _camera.fieldOfView : 60f;
            float near = _camera != null ? _camera.nearClipPlane : 0.3f;
            float far = 20f; // Only draw partial frustum

            Gizmos.DrawFrustum(Vector3.zero, fov, far, near, aspect);
        }
#endif
    }
}
