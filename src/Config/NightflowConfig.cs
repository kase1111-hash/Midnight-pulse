// ============================================================================
// Nightflow - Master Configuration
// Central config asset and runtime configuration system
// ============================================================================

using UnityEngine;
using Unity.Entities;
using System.IO;
using Nightflow.Utilities;

namespace Nightflow.Config
{
    /// <summary>
    /// Master configuration asset that references all config categories.
    /// </summary>
    [CreateAssetMenu(fileName = "NightflowConfig", menuName = "Nightflow/Master Config")]
    public class NightflowConfig : ScriptableObject
    {
        [Header("Configuration Assets")]
        public GameplayConfig gameplay;
        public AudioConfigAsset audio;
        public VisualConfig visual;

        [Header("Debug")]
        [Tooltip("Enable config hot-reloading in editor")]
        public bool enableHotReload = true;

        [Tooltip("Log config loading")]
        public bool verboseLogging = false;

        /// <summary>
        /// Validates all config references are assigned.
        /// </summary>
        public bool Validate()
        {
            bool valid = true;

            if (gameplay == null)
            {
                Log.SystemError("NightflowConfig", "Gameplay config is not assigned!");
                valid = false;
            }

            if (audio == null)
            {
                Log.SystemError("NightflowConfig", "Audio config is not assigned!");
                valid = false;
            }

            if (visual == null)
            {
                Log.SystemError("NightflowConfig", "Visual config is not assigned!");
                valid = false;
            }

            return valid;
        }
    }

    /// <summary>
    /// Runtime configuration manager that loads and provides access to configs.
    /// </summary>
    public class ConfigManager : MonoBehaviour
    {
        private static ConfigManager instance;
        public static ConfigManager Instance => instance;

        [SerializeField]
        private NightflowConfig masterConfig;

        // Cached references
        public static GameplayConfig Gameplay => Instance?.masterConfig?.gameplay;
        public static AudioConfigAsset Audio => Instance?.masterConfig?.audio;
        public static VisualConfig Visual => Instance?.masterConfig?.visual;

        // Events
        public delegate void ConfigChangedHandler();
        public static event ConfigChangedHandler OnConfigChanged;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (masterConfig == null)
            {
                Log.SystemError("ConfigManager", "Master config not assigned!");
                return;
            }

            if (!masterConfig.Validate())
            {
                Log.SystemError("ConfigManager", "Config validation failed!");
            }

            ApplyConfigs();
        }

        /// <summary>
        /// Applies all configurations to the game systems.
        /// </summary>
        public void ApplyConfigs()
        {
            if (masterConfig == null) return;

            ApplyGameplayConfig();
            ApplyAudioConfig();
            ApplyVisualConfig();

            OnConfigChanged?.Invoke();

            if (masterConfig.verboseLogging)
            {
                Log.System("ConfigManager", "All configs applied successfully.");
            }
        }

        private void ApplyGameplayConfig()
        {
            if (Gameplay == null) return;

            // Apply to ECS singletons if they exist
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            // Gameplay config would be applied to relevant systems
            // This is a hook for systems to read from
        }

        private void ApplyAudioConfig()
        {
            if (Audio == null) return;

            // Apply audio mixer volumes
            // This would set AudioMixer parameters
        }

        private void ApplyVisualConfig()
        {
            if (Visual == null) return;

            // Apply shader globals
            ApplyShaderGlobals();
        }

        private void ApplyShaderGlobals()
        {
            var v = Visual;
            if (v == null) return;

            // Set global shader properties
            Shader.SetGlobalColor("_PlayerColor", v.colorPalette.playerCyan);
            Shader.SetGlobalColor("_TrafficColor", v.colorPalette.trafficMagenta);
            Shader.SetGlobalColor("_HazardColor", v.colorPalette.hazardOrange);
            Shader.SetGlobalColor("_LaneColor", v.colorPalette.laneBlue);

            Shader.SetGlobalFloat("_WireThickness", v.wireframe.lineThickness);
            Shader.SetGlobalFloat("_GlowIntensity", v.wireframe.glowIntensity);
            Shader.SetGlobalFloat("_GlowFalloff", v.wireframe.glowFalloff);
            Shader.SetGlobalFloat("_FillAlpha", v.wireframe.fillAlpha);

            Shader.SetGlobalFloat("_BloomThreshold", v.postProcess.bloomThreshold);
            Shader.SetGlobalFloat("_BloomIntensity", v.postProcess.bloomIntensity);
        }

        /// <summary>
        /// Exports current config to JSON for external editing.
        /// </summary>
        public void ExportToJson(string path)
        {
            var exportData = new ConfigExportData
            {
                gameplay = Gameplay,
                audio = Audio,
                visual = Visual
            };

            string json = JsonUtility.ToJson(exportData, true);
            File.WriteAllText(path, json);

            Log.System("ConfigManager", $"Exported config to {path}");
        }

        /// <summary>
        /// Reloads config (for hot-reload in editor).
        /// </summary>
        public void ReloadConfig()
        {
            ApplyConfigs();
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (masterConfig != null && masterConfig.enableHotReload)
            {
                ApplyConfigs();
            }
        }
        #endif
    }

    /// <summary>
    /// Data structure for JSON export.
    /// </summary>
    [System.Serializable]
    public class ConfigExportData
    {
        public GameplayConfig gameplay;
        public AudioConfigAsset audio;
        public VisualConfig visual;
    }

    /// <summary>
    /// ECS component for accessing config from systems.
    /// </summary>
    public struct GameplayConfigData : IComponentData
    {
        // Vehicle
        public float MaxSpeed;
        public float Acceleration;
        public float LaneWidth;
        public float LaneChangeTime;

        // Scoring
        public float PointsPerMeter;
        public float MaxMultiplier;
        public float NearMissDistance;
        public float NearMissPoints;

        // Traffic
        public float BaseDensity;
        public float BaseTrafficSpeed;

        // Hazards
        public float BaseHazardRate;
    }

    /// <summary>
    /// System to sync ScriptableObject config to ECS.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ConfigSyncSystem : SystemBase
    {
        protected override void OnCreate()
        {
            // Create config singleton
            var configEntity = EntityManager.CreateEntity(typeof(GameplayConfigData));
            EntityManager.SetName(configEntity, "GameplayConfig");
        }

        protected override void OnUpdate()
        {
            // Sync from ScriptableObject to ECS each frame (or on change)
            var gameplay = ConfigManager.Gameplay;
            if (gameplay == null) return;

            var configData = new GameplayConfigData
            {
                MaxSpeed = gameplay.vehiclePhysics.maxSpeed,
                Acceleration = gameplay.vehiclePhysics.acceleration,
                LaneWidth = gameplay.vehiclePhysics.laneWidth,
                LaneChangeTime = gameplay.vehiclePhysics.laneChangeTime,
                PointsPerMeter = gameplay.scoring.pointsPerMeter,
                MaxMultiplier = gameplay.scoring.maxMultiplier,
                NearMissDistance = gameplay.scoring.nearMissDistance,
                NearMissPoints = gameplay.scoring.nearMissPoints,
                BaseDensity = gameplay.trafficAI.baseDensity,
                BaseTrafficSpeed = gameplay.trafficAI.baseSpeed,
                BaseHazardRate = gameplay.hazardSpawn.baseSpawnRate
            };

            // Update singleton
            if (SystemAPI.TryGetSingletonRW<GameplayConfigData>(out var existing))
            {
                existing.ValueRW = configData;
            }
        }
    }
}
