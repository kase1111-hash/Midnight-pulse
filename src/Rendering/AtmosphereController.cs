// ============================================================================
// Nightflow - Atmosphere Controller
// Owns global distance fog (RenderSettings) at runtime so builds get the
// dense night haze without relying on editor-baked lighting settings
// ============================================================================

using UnityEngine;

namespace Nightflow.Rendering
{
    /// <summary>
    /// Applies and animates the global atmospheric fog every frame.
    /// Distance fog is what makes the neon world read as "driving through
    /// haze toward city lights": all Nightflow shaders MixFog toward this
    /// color, and additive lights dim into it with distance.
    /// </summary>
    [ExecuteAlways]
    public class AtmosphereController : MonoBehaviour
    {
        [Header("Distance Fog")]
        [Tooltip("Enable exponential-squared distance fog")]
        [SerializeField] private bool enableDistanceFog = true;

        [Tooltip("Haze color - deep indigo so neon glows tint the murk")]
        [SerializeField] private Color fogColor = new Color(0.055f, 0.05f, 0.12f, 1f);

        [Tooltip("Fog density (exp2). 0.008 = world dissolves around 150-250m")]
        [Range(0.001f, 0.03f)]
        [SerializeField] private float fogDensity = 0.008f;

        [Header("Density Breathing")]
        [Tooltip("Fraction the density slowly drifts up/down, so the fog feels alive")]
        [Range(0f, 0.5f)]
        [SerializeField] private float densityDrift = 0.12f;

        [Tooltip("Seconds for one full drift cycle")]
        [SerializeField] private float driftPeriod = 17f;

        [Header("Skybox")]
        [Tooltip("Assign the Nightflow night skybox (deep gradient, city glow, stars)")]
        [SerializeField] private bool applyNightSkybox = true;

        private float _time;
        private Material _skyboxMaterial;

        private void OnEnable()
        {
            ApplySkybox();
            Apply();
        }

        private void Update()
        {
            // Per-frame writes only while playing; in edit mode the settings
            // were applied once in OnEnable (avoids dirtying the scene every
            // frame and fighting user tweaks in the Lighting window)
            if (!Application.isPlaying)
                return;

            _time += Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            RenderSettings.fog = enableDistanceFog;
            if (!enableDistanceFog)
                return;

            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;

            float drift = 1f;
            if (densityDrift > 0f && driftPeriod > 0.01f)
            {
                drift += Mathf.Sin(_time * (2f * Mathf.PI / driftPeriod)) * densityDrift;
            }

            RenderSettings.fogDensity = fogDensity * drift;
        }

        private void ApplySkybox()
        {
            if (!applyNightSkybox)
                return;

            var shader = Shader.Find("Nightflow/NightSkybox");
            if (shader == null)
                return;

            if (_skyboxMaterial == null || _skyboxMaterial.shader != shader)
            {
                _skyboxMaterial = new Material(shader);
                _skyboxMaterial.name = "NightSkybox_Generated";
            }

            RenderSettings.skybox = _skyboxMaterial;

            // Skybox only shows if the camera clears to it
            var cam = Camera.main;
            if (cam != null && cam.clearFlags != CameraClearFlags.Skybox)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
            }
        }

        private void OnDestroy()
        {
            if (_skyboxMaterial != null)
            {
                // Detach before destroying so the scene isn't left pointing
                // at a destroyed material (magenta sky / missing reference)
                if (RenderSettings.skybox == _skyboxMaterial)
                {
                    RenderSettings.skybox = null;
                }

                if (Application.isPlaying)
                    Destroy(_skyboxMaterial);
                else
                    DestroyImmediate(_skyboxMaterial);
            }
        }

        /// <summary>
        /// Current effective fog density (including drift), for systems that
        /// want to match their own falloff to the global haze.
        /// </summary>
        public float CurrentDensity => RenderSettings.fog ? RenderSettings.fogDensity : 0f;
    }
}
