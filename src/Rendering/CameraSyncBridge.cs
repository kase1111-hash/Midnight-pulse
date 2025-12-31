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

        [Header("Fallback Settings")]
        [Tooltip("Use fallback transform when CameraState entity is missing")]
        [SerializeField] private bool useFallbackWhenMissing = true;

        [Tooltip("Fallback position when no CameraState entity exists")]
        [SerializeField] private Vector3 fallbackPosition = new Vector3(0, 5, -10);

        [Tooltip("Fallback rotation when no CameraState entity exists")]
        [SerializeField] private Vector3 fallbackEulerRotation = new Vector3(15, 0, 0);

        [Tooltip("Fallback FOV when no CameraState entity exists")]
        [SerializeField] private float fallbackFOV = 60f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Cached references
        private Camera _camera;
        private EntityManager _entityManager;
        private EntityQuery _cameraStateQuery;
        private bool _ecsInitialized;
        private bool _hasWarnedAboutMissingState;
        private int _missingStateFrameCount;

        // Cached state for smoothing
        private Vector3 _currentPosition;
        private Quaternion _currentRotation;
        private float _currentFOV;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                Debug.LogError("[CameraSyncBridge] No Camera component found on this GameObject. This script requires a Camera component.");
                enabled = false;
                return;
            }
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
                _missingStateFrameCount++;

                // Only warn once, and only after a few frames to allow ECS initialization
                if (!_hasWarnedAboutMissingState && _missingStateFrameCount > 10)
                {
                    Debug.LogWarning("[CameraSyncBridge] No CameraState entity found. " +
                        "Ensure GameBootstrapSystem creates the CameraState entity. " +
                        (useFallbackWhenMissing ? "Using fallback position." : "Camera will stay at origin."));
                    _hasWarnedAboutMissingState = true;
                }

                if (useFallbackWhenMissing)
                {
                    ApplyFallbackTransform();
                }
                return;
            }

            // Reset warning state when CameraState is found
            _missingStateFrameCount = 0;
            _hasWarnedAboutMissingState = false;

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
        /// Apply fallback transform values when CameraState entity is missing.
        /// This ensures the camera is positioned sensibly during initialization.
        /// </summary>
        private void ApplyFallbackTransform()
        {
            Vector3 targetPosition = fallbackPosition;
            Quaternion targetRotation = Quaternion.Euler(fallbackEulerRotation);
            float targetFOV = fallbackFOV;

            float deltaTime = Time.deltaTime;

            // Apply smoothing if enabled
            if (positionSmoothing > 0f)
            {
                _currentPosition = Vector3.Lerp(_currentPosition, targetPosition, positionSmoothing * deltaTime);
            }
            else
            {
                _currentPosition = targetPosition;
            }

            if (rotationSmoothing > 0f)
            {
                _currentRotation = Quaternion.Slerp(_currentRotation, targetRotation, rotationSmoothing * deltaTime);
            }
            else
            {
                _currentRotation = targetRotation;
            }

            if (fovSmoothing > 0f)
            {
                _currentFOV = Mathf.Lerp(_currentFOV, targetFOV, fovSmoothing * deltaTime);
            }
            else
            {
                _currentFOV = targetFOV;
            }

            transform.position = _currentPosition;
            transform.rotation = _currentRotation;
            _camera.fieldOfView = _currentFOV;
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
