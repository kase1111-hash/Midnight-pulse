// ============================================================================
// Nightflow - Automatic Setup on Play Mode
// Validates scene setup and auto-creates missing components when entering play mode
// ============================================================================

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Nightflow.Config;
using Nightflow.Rendering;
using Nightflow.Save;
using Nightflow.UI;
using Nightflow.Audio;
using Nightflow.Utilities;

namespace Nightflow.Editor
{
    /// <summary>
    /// Automatically validates and sets up the scene when entering play mode.
    /// This eliminates manual setup steps and ensures the game is always ready to run.
    /// </summary>
    [InitializeOnLoad]
    public static class NightflowAutoSetup
    {
        private const string ConfigPath = "Assets/Config";
        private const string EnabledPrefKey = "Nightflow_AutoSetup_Enabled";
        private const string SilentModePrefKey = "Nightflow_AutoSetup_Silent";

        /// <summary>
        /// Whether auto-setup is enabled. Default: true
        /// </summary>
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, true);
            set => EditorPrefs.SetBool(EnabledPrefKey, value);
        }

        /// <summary>
        /// Whether to run silently without dialogs. Default: true
        /// </summary>
        public static bool SilentMode
        {
            get => EditorPrefs.GetBool(SilentModePrefKey, true);
            set => EditorPrefs.SetBool(SilentModePrefKey, value);
        }

        private const string AutoCreateConfigsPrefKey = "Nightflow_AutoSetup_AutoCreateConfigs";

        /// <summary>
        /// Whether to auto-create config assets on editor startup. Default: true
        /// </summary>
        public static bool AutoCreateConfigs
        {
            get => EditorPrefs.GetBool(AutoCreateConfigsPrefKey, true);
            set => EditorPrefs.SetBool(AutoCreateConfigsPrefKey, value);
        }

        static NightflowAutoSetup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // Delay the config check to ensure AssetDatabase is ready
            EditorApplication.delayCall += OnEditorStartup;
        }

        private static void OnEditorStartup()
        {
            if (!AutoCreateConfigs)
                return;

            // Check if config assets exist, create if missing
            EnsureConfigAssetsExist();
        }

        /// <summary>
        /// Ensures all required config assets exist. Creates them if missing.
        /// Called automatically on editor startup.
        /// </summary>
        public static void EnsureConfigAssetsExist()
        {
            bool created = false;

            // Check master config
            var masterConfig = AssetDatabase.LoadAssetAtPath<NightflowConfig>(
                $"{ConfigPath}/NightflowConfig.asset");

            if (masterConfig == null)
            {
                // Create all configs via setup wizard
                NightflowSetupWizard.CreateAllConfigs();
                created = true;
            }
            else
            {
                // Master exists, check sub-configs
                bool needsUpdate = false;

                if (masterConfig.gameplay == null)
                {
                    var gameplay = CreateConfigIfNotExists<GameplayConfig>("GameplayConfig");
                    if (gameplay != null)
                    {
                        masterConfig.gameplay = gameplay;
                        needsUpdate = true;
                    }
                }

                if (masterConfig.visual == null)
                {
                    var visual = CreateConfigIfNotExists<VisualConfig>("VisualConfig");
                    if (visual != null)
                    {
                        masterConfig.visual = visual;
                        needsUpdate = true;
                    }
                }

                if (masterConfig.audio == null)
                {
                    var audio = CreateConfigIfNotExists<AudioConfigAsset>("AudioConfig");
                    if (audio != null)
                    {
                        masterConfig.audio = audio;
                        needsUpdate = true;
                    }
                }

                if (needsUpdate)
                {
                    EditorUtility.SetDirty(masterConfig);
                    AssetDatabase.SaveAssets();
                    created = true;
                }
            }

            // Check AudioClipCollection
            var clipCollection = AssetDatabase.LoadAssetAtPath<AudioClipCollection>(
                $"{ConfigPath}/AudioClipCollection.asset");
            if (clipCollection == null)
            {
                CreateAudioClipCollection();
                created = true;
            }

            if (created)
            {
                Log.System("NightflowAutoSetup", "Config assets auto-created on editor startup");
            }
        }

        private static T CreateConfigIfNotExists<T>(string name) where T : ScriptableObject
        {
            string path = $"{ConfigPath}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder(ConfigPath))
            {
                AssetDatabase.CreateFolder("Assets", "Config");
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Log.System("NightflowAutoSetup", $"Created config: {path}");
            return asset;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            if (!Enabled)
                return;

            // Validate and setup if needed
            var validation = ValidateScene();

            if (validation.IsValid)
            {
                // Scene is ready, nothing to do
                return;
            }

            // Scene needs setup
            if (SilentMode)
            {
                // Auto-fix everything silently
                PerformAutoSetup(validation);
            }
            else
            {
                // Ask user
                string message = BuildValidationMessage(validation);
                int choice = EditorUtility.DisplayDialogComplex(
                    "Nightflow Auto-Setup",
                    message + "\n\nWould you like to auto-setup the missing components?",
                    "Auto-Setup",
                    "Cancel Play",
                    "Play Anyway");

                switch (choice)
                {
                    case 0: // Auto-Setup
                        PerformAutoSetup(validation);
                        break;
                    case 1: // Cancel Play
                        EditorApplication.isPlaying = false;
                        break;
                    case 2: // Play Anyway
                        // Do nothing, let it play
                        break;
                }
            }
        }

        /// <summary>
        /// Validation result containing all missing components.
        /// </summary>
        public class ValidationResult
        {
            public bool HasConfigs;
            public bool HasSaveManager;
            public bool HasConfigManager;
            public bool HasConfigAssigned;
            public bool HasAudioManager;
            public bool HasAudioClipCollection;
            public bool HasMainCamera;
            public bool HasCameraSyncBridge;
            public bool HasProceduralMeshRenderer;
            public bool HasUIController;
            public bool HasPostProcessingController;

            public bool IsValid =>
                HasConfigs &&
                HasSaveManager &&
                HasConfigManager &&
                HasConfigAssigned &&
                HasAudioManager &&
                HasAudioClipCollection &&
                HasMainCamera &&
                HasCameraSyncBridge &&
                HasProceduralMeshRenderer &&
                HasUIController;

            public List<string> MissingItems
            {
                get
                {
                    var items = new List<string>();
                    if (!HasConfigs) items.Add("Config assets");
                    if (!HasSaveManager) items.Add("SaveManager");
                    if (!HasConfigManager) items.Add("ConfigManager");
                    if (!HasConfigAssigned) items.Add("Config not assigned to ConfigManager");
                    if (!HasAudioManager) items.Add("AudioManager");
                    if (!HasAudioClipCollection) items.Add("AudioClipCollection not assigned");
                    if (!HasMainCamera) items.Add("Main Camera");
                    if (!HasCameraSyncBridge) items.Add("CameraSyncBridge");
                    if (!HasProceduralMeshRenderer) items.Add("ProceduralMeshRenderer");
                    if (!HasUIController) items.Add("UIController");
                    return items;
                }
            }
        }

        /// <summary>
        /// Validates the current scene for all required components.
        /// </summary>
        public static ValidationResult ValidateScene()
        {
            var result = new ValidationResult();

            // Check config assets exist
            result.HasConfigs = AssetDatabase.LoadAssetAtPath<NightflowConfig>(
                $"{ConfigPath}/NightflowConfig.asset") != null;

            // Check scene components
            result.HasSaveManager = Object.FindFirstObjectByType<SaveManager>() != null;

            var configManager = Object.FindFirstObjectByType<ConfigManager>();
            result.HasConfigManager = configManager != null;

            // Check if config is assigned via SerializedObject
            if (configManager != null)
            {
                var so = new SerializedObject(configManager);
                var configProp = so.FindProperty("masterConfig");
                result.HasConfigAssigned = configProp != null && configProp.objectReferenceValue != null;
            }

            var audioManager = Object.FindFirstObjectByType<AudioManager>();
            result.HasAudioManager = audioManager != null;

            // Check if audio clip collection is assigned
            if (audioManager != null)
            {
                var so = new SerializedObject(audioManager);
                var clipCollectionProp = so.FindProperty("clipCollection");
                result.HasAudioClipCollection = clipCollectionProp != null && clipCollectionProp.objectReferenceValue != null;
            }
            else
            {
                result.HasAudioClipCollection = false;
            }

            result.HasMainCamera = Camera.main != null;
            result.HasCameraSyncBridge = Object.FindFirstObjectByType<CameraSyncBridge>() != null;
            result.HasProceduralMeshRenderer = Object.FindFirstObjectByType<ProceduralMeshRenderer>() != null;
            result.HasUIController = Object.FindFirstObjectByType<UIController>() != null;
            result.HasPostProcessingController = Object.FindFirstObjectByType<PostProcessingController>() != null;

            return result;
        }

        private static string BuildValidationMessage(ValidationResult validation)
        {
            var missing = validation.MissingItems;
            if (missing.Count == 0)
                return "Scene is properly configured.";

            return $"Missing {missing.Count} required component(s):\n• " +
                   string.Join("\n• ", missing);
        }

        /// <summary>
        /// Performs automatic setup based on validation results.
        /// </summary>
        private static void PerformAutoSetup(ValidationResult validation)
        {
            bool sceneModified = false;

            // Step 1: Ensure config assets exist
            if (!validation.HasConfigs)
            {
                NightflowSetupWizard.CreateAllConfigs();
            }

            // Load the master config for assignment
            var masterConfig = AssetDatabase.LoadAssetAtPath<NightflowConfig>(
                $"{ConfigPath}/NightflowConfig.asset");
            var visualConfig = AssetDatabase.LoadAssetAtPath<VisualConfig>(
                $"{ConfigPath}/VisualConfig.asset");

            // Ensure AudioClipCollection exists
            var audioClipCollection = AssetDatabase.LoadAssetAtPath<AudioClipCollection>(
                $"{ConfigPath}/AudioClipCollection.asset");
            if (audioClipCollection == null)
            {
                audioClipCollection = CreateAudioClipCollection();
            }

            // Step 2: Create Managers hierarchy if needed
            if (!validation.HasSaveManager || !validation.HasConfigManager || !validation.HasAudioManager)
            {
                var managersRoot = FindOrCreateRoot("[Managers]");

                if (!validation.HasSaveManager)
                {
                    CreateChildWithComponent<SaveManager>(managersRoot, "SaveManager");
                    sceneModified = true;
                }

                if (!validation.HasConfigManager)
                {
                    var configManagerGO = CreateChildWithComponent<ConfigManager>(managersRoot, "ConfigManager");
                    AssignConfigToManager(configManagerGO.GetComponent<ConfigManager>(), masterConfig);
                    sceneModified = true;
                }
                else if (!validation.HasConfigAssigned)
                {
                    // ConfigManager exists but config not assigned
                    var configManager = Object.FindFirstObjectByType<ConfigManager>();
                    AssignConfigToManager(configManager, masterConfig);
                    sceneModified = true;
                }

                if (!validation.HasAudioManager)
                {
                    var audioManagerGO = CreateChildWithComponent<AudioManager>(managersRoot, "AudioManager");
                    AssignAudioClipCollection(audioManagerGO.GetComponent<AudioManager>(), audioClipCollection);
                    sceneModified = true;
                }
                else if (!validation.HasAudioClipCollection)
                {
                    // AudioManager exists but collection not assigned
                    var audioManager = Object.FindFirstObjectByType<AudioManager>();
                    AssignAudioClipCollection(audioManager, audioClipCollection);
                    sceneModified = true;
                }
            }
            else if (!validation.HasConfigAssigned || !validation.HasAudioClipCollection)
            {
                // ConfigManager or AudioManager exists but not properly configured
                if (!validation.HasConfigAssigned)
                {
                    var configManager = Object.FindFirstObjectByType<ConfigManager>();
                    AssignConfigToManager(configManager, masterConfig);
                    sceneModified = true;
                }

                if (!validation.HasAudioClipCollection)
                {
                    var audioManager = Object.FindFirstObjectByType<AudioManager>();
                    AssignAudioClipCollection(audioManager, audioClipCollection);
                    sceneModified = true;
                }
            }

            // Step 3: Create Camera hierarchy if needed
            if (!validation.HasMainCamera || !validation.HasCameraSyncBridge || !validation.HasPostProcessingController)
            {
                var cameraRoot = FindOrCreateRoot("[Camera]");

                if (!validation.HasMainCamera)
                {
                    var cameraGO = new GameObject("Main Camera");
                    cameraGO.transform.SetParent(cameraRoot.transform);
                    cameraGO.tag = "MainCamera";

                    var camera = cameraGO.AddComponent<Camera>();
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
                    camera.fieldOfView = 75f;
                    camera.nearClipPlane = 0.3f;
                    camera.farClipPlane = 1000f;

                    cameraGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    cameraGO.AddComponent<AudioListener>();
                    cameraGO.AddComponent<CameraSyncBridge>();

                    // Post processing child
                    var postProcessGO = new GameObject("PostProcessing");
                    postProcessGO.transform.SetParent(cameraGO.transform);
                    postProcessGO.AddComponent<UnityEngine.Rendering.Volume>().isGlobal = true;
                    var ppController = postProcessGO.AddComponent<PostProcessingController>();
                    AssignVisualConfig(ppController, visualConfig);

                    sceneModified = true;
                }
                else
                {
                    // Camera exists, just add missing components
                    var mainCamera = Camera.main.gameObject;

                    if (!validation.HasCameraSyncBridge)
                    {
                        mainCamera.AddComponent<CameraSyncBridge>();
                        sceneModified = true;
                    }

                    if (!validation.HasPostProcessingController)
                    {
                        var existingPP = mainCamera.GetComponentInChildren<PostProcessingController>();
                        if (existingPP == null)
                        {
                            var postProcessGO = new GameObject("PostProcessing");
                            postProcessGO.transform.SetParent(mainCamera.transform);
                            postProcessGO.AddComponent<UnityEngine.Rendering.Volume>().isGlobal = true;
                            var ppController = postProcessGO.AddComponent<PostProcessingController>();
                            AssignVisualConfig(ppController, visualConfig);
                            sceneModified = true;
                        }
                    }
                }
            }

            // Step 4: Create Rendering hierarchy if needed
            if (!validation.HasProceduralMeshRenderer)
            {
                var renderingRoot = FindOrCreateRoot("[Rendering]");

                CreateChildWithComponent<ProceduralMeshRenderer>(renderingRoot, "ProceduralMeshRenderer");
                CreateChildWithComponent<StarFieldRenderer>(renderingRoot, "StarField");
                CreateChildWithComponent<CitySkylineRenderer>(renderingRoot, "CitySkyline");
                CreateChildWithComponent<MoonRenderer>(renderingRoot, "Moon");
                CreateChildWithComponent<GroundFogRenderer>(renderingRoot, "GroundFog");

                sceneModified = true;
            }

            // Step 5: Create UI hierarchy if needed
            if (!validation.HasUIController)
            {
                var uiRoot = FindOrCreateRoot("[UI]");

                var uiDocGO = new GameObject("HUD");
                uiDocGO.transform.SetParent(uiRoot.transform);

                var uiDocument = uiDocGO.AddComponent<UnityEngine.UIElements.UIDocument>();

                // Try to find and assign the UXML
                string[] guids = AssetDatabase.FindAssets("NightflowHUD t:VisualTreeAsset");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var uxmlAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(path);
                    if (uxmlAsset != null)
                    {
                        uiDocument.visualTreeAsset = uxmlAsset;
                    }
                }

                uiDocGO.AddComponent<UIController>();
                sceneModified = true;
            }

            // Step 6: Create Lighting if no directional light exists
            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var lightingRoot = FindOrCreateRoot("[Lighting]");

                var moonLightGO = new GameObject("Moon Light");
                moonLightGO.transform.SetParent(lightingRoot.transform);
                moonLightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                var moonLight = moonLightGO.AddComponent<Light>();
                moonLight.type = LightType.Directional;
                moonLight.color = new Color(0.6f, 0.7f, 0.9f);
                moonLight.intensity = 0.1f;

                sceneModified = true;
            }

            if (sceneModified)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Log.System("NightflowAutoSetup", "Scene auto-configured for play mode!");
            }
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
                return existing;

            return new GameObject(name);
        }

        private static GameObject CreateChildWithComponent<T>(GameObject parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.AddComponent<T>();
            return go;
        }

        private static void AssignConfigToManager(ConfigManager configManager, NightflowConfig config)
        {
            if (configManager == null || config == null) return;

            var so = new SerializedObject(configManager);
            var configProp = so.FindProperty("masterConfig");
            if (configProp != null)
            {
                configProp.objectReferenceValue = config;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssignVisualConfig(PostProcessingController controller, VisualConfig config)
        {
            if (controller == null || config == null) return;

            var so = new SerializedObject(controller);
            var configProp = so.FindProperty("visualConfig");
            if (configProp != null)
            {
                configProp.objectReferenceValue = config;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AssignAudioClipCollection(AudioManager audioManager, AudioClipCollection collection)
        {
            if (audioManager == null || collection == null) return;

            var so = new SerializedObject(audioManager);
            var collectionProp = so.FindProperty("clipCollection");
            if (collectionProp != null)
            {
                collectionProp.objectReferenceValue = collection;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static AudioClipCollection CreateAudioClipCollection()
        {
            // Ensure config directory exists
            if (!AssetDatabase.IsValidFolder(ConfigPath))
            {
                AssetDatabase.CreateFolder("Assets", "Config");
            }

            // Create the audio clip collection asset
            var collection = ScriptableObject.CreateInstance<AudioClipCollection>();
            string assetPath = $"{ConfigPath}/AudioClipCollection.asset";
            AssetDatabase.CreateAsset(collection, assetPath);
            AssetDatabase.SaveAssets();

            Log.System("NightflowAutoSetup", $"Created AudioClipCollection at {assetPath}");
            return collection;
        }

        // Menu items for configuration
        [MenuItem("Nightflow/Auto-Setup/Enable Auto-Setup", false, 200)]
        private static void EnableAutoSetup()
        {
            Enabled = true;
            Log.System("NightflowAutoSetup", "Auto-setup enabled");
        }

        [MenuItem("Nightflow/Auto-Setup/Enable Auto-Setup", true)]
        private static bool EnableAutoSetupValidate()
        {
            Menu.SetChecked("Nightflow/Auto-Setup/Enable Auto-Setup", Enabled);
            return !Enabled;
        }

        [MenuItem("Nightflow/Auto-Setup/Disable Auto-Setup", false, 201)]
        private static void DisableAutoSetup()
        {
            Enabled = false;
            Log.System("NightflowAutoSetup", "Auto-setup disabled");
        }

        [MenuItem("Nightflow/Auto-Setup/Disable Auto-Setup", true)]
        private static bool DisableAutoSetupValidate()
        {
            return Enabled;
        }

        [MenuItem("Nightflow/Auto-Setup/Silent Mode (No Dialogs)", false, 210)]
        private static void ToggleSilentMode()
        {
            SilentMode = !SilentMode;
            Log.System("NightflowAutoSetup", $"Silent mode: {(SilentMode ? "enabled" : "disabled")}");
        }

        [MenuItem("Nightflow/Auto-Setup/Silent Mode (No Dialogs)", true)]
        private static bool ToggleSilentModeValidate()
        {
            Menu.SetChecked("Nightflow/Auto-Setup/Silent Mode (No Dialogs)", SilentMode);
            return true;
        }

        [MenuItem("Nightflow/Auto-Setup/Auto-Create Configs on Startup", false, 215)]
        private static void ToggleAutoCreateConfigs()
        {
            AutoCreateConfigs = !AutoCreateConfigs;
            Log.System("NightflowAutoSetup", $"Auto-create configs: {(AutoCreateConfigs ? "enabled" : "disabled")}");
        }

        [MenuItem("Nightflow/Auto-Setup/Auto-Create Configs on Startup", true)]
        private static bool ToggleAutoCreateConfigsValidate()
        {
            Menu.SetChecked("Nightflow/Auto-Setup/Auto-Create Configs on Startup", AutoCreateConfigs);
            return true;
        }

        [MenuItem("Nightflow/Auto-Setup/Validate Current Scene", false, 220)]
        private static void ValidateCurrentScene()
        {
            var validation = ValidateScene();

            if (validation.IsValid)
            {
                EditorUtility.DisplayDialog("Scene Validation",
                    "Scene is properly configured and ready to play!", "OK");
            }
            else
            {
                string message = BuildValidationMessage(validation);
                EditorUtility.DisplayDialog("Scene Validation", message, "OK");
            }
        }

        [MenuItem("Nightflow/Auto-Setup/Force Setup Now", false, 221)]
        private static void ForceSetupNow()
        {
            var validation = ValidateScene();
            PerformAutoSetup(validation);

            EditorUtility.DisplayDialog("Auto-Setup Complete",
                "Scene has been configured with all required components.", "OK");
        }

        [MenuItem("Nightflow/Auto-Setup/Create Missing Configs", false, 222)]
        private static void CreateMissingConfigs()
        {
            EnsureConfigAssetsExist();
            EditorUtility.DisplayDialog("Config Creation",
                "All config assets have been created/verified.", "OK");
        }

        [MenuItem("Nightflow/Auto-Setup/Run Setup Validation", false, 230)]
        private static void RunSetupValidation()
        {
            var result = SetupValidator.ValidateAll();
            string summary = result.GetSummary();

            if (result.IsValid && result.Warnings.Count == 0)
            {
                EditorUtility.DisplayDialog("Setup Validation",
                    "All validations passed! Your project is properly configured.", "OK");
            }
            else
            {
                // Show in console for copy/paste
                if (!result.IsValid)
                {
                    Debug.LogError("Nightflow Setup Validation Failed:\n" + summary);
                }
                else
                {
                    Debug.LogWarning("Nightflow Setup Validation Warnings:\n" + summary);
                }

                EditorUtility.DisplayDialog("Setup Validation",
                    summary + "\n\nSee Console for full details.", "OK");
            }
        }
    }
}
