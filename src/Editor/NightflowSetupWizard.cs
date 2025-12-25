// ============================================================================
// Nightflow - Project Setup Wizard
// Editor tool for initializing scene hierarchy and config assets
// ============================================================================

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using Nightflow.Config;
using Nightflow.Rendering;
using Nightflow.Save;
using Nightflow.UI;
using Nightflow.Audio;

namespace Nightflow.Editor
{
    /// <summary>
    /// Setup wizard for creating scene hierarchy and config assets.
    /// Access via menu: Nightflow > Setup Wizard
    /// </summary>
    public class NightflowSetupWizard : EditorWindow
    {
        private const string ConfigPath = "Assets/Config";
        private const string ScenesPath = "Assets/Scenes";

        private bool _createConfigs = true;
        private bool _createScene = true;
        private bool _setupRendering = true;
        private bool _setupUI = true;
        private bool _setupAudio = true;

        private Vector2 _scrollPos;

        [MenuItem("Nightflow/Setup Wizard", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<NightflowSetupWizard>("Nightflow Setup");
            window.minSize = new Vector2(400, 500);
        }

        [MenuItem("Nightflow/Quick Setup (All)", false, 1)]
        public static void QuickSetupAll()
        {
            if (EditorUtility.DisplayDialog("Nightflow Quick Setup",
                "This will create:\n" +
                "- Config ScriptableObjects\n" +
                "- Main game scene with hierarchy\n" +
                "- All required GameObjects\n\n" +
                "Continue?", "Setup", "Cancel"))
            {
                CreateAllConfigs();
                CreateMainScene();
                EditorUtility.DisplayDialog("Setup Complete",
                    "Nightflow project has been set up!\n\n" +
                    "Next steps:\n" +
                    "1. Assign audio clips to AudioManager\n" +
                    "2. Configure game settings in Config assets\n" +
                    "3. Press Play to test!", "OK");
            }
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Nightflow Project Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This wizard will create all required assets and scene hierarchy for Nightflow.",
                MessageType.Info);

            GUILayout.Space(20);

            // Config section
            EditorGUILayout.LabelField("Configuration Assets", EditorStyles.boldLabel);
            _createConfigs = EditorGUILayout.Toggle("Create Config Assets", _createConfigs);
            if (_createConfigs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Will create:", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("  - NightflowConfig (master)", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("  - GameplayConfig", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("  - VisualConfig", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("  - AudioConfig", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(10);

            // Scene section
            EditorGUILayout.LabelField("Scene Setup", EditorStyles.boldLabel);
            _createScene = EditorGUILayout.Toggle("Create Main Scene", _createScene);
            if (_createScene)
            {
                EditorGUI.indentLevel++;
                _setupRendering = EditorGUILayout.Toggle("Setup Rendering", _setupRendering);
                _setupUI = EditorGUILayout.Toggle("Setup UI", _setupUI);
                _setupAudio = EditorGUILayout.Toggle("Setup Audio", _setupAudio);
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(20);

            // Action buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Create Configs Only", GUILayout.Height(30)))
            {
                CreateAllConfigs();
            }

            if (GUILayout.Button("Create Scene Only", GUILayout.Height(30)))
            {
                CreateMainScene();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Setup Everything", GUILayout.Height(40)))
            {
                if (_createConfigs) CreateAllConfigs();
                if (_createScene) CreateMainScene();

                EditorUtility.DisplayDialog("Setup Complete",
                    "Nightflow project setup complete!", "OK");
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(20);

            // Status section
            EditorGUILayout.LabelField("Current Status", EditorStyles.boldLabel);
            DrawStatusLine("NightflowConfig", AssetExists($"{ConfigPath}/NightflowConfig.asset"));
            DrawStatusLine("GameplayConfig", AssetExists($"{ConfigPath}/GameplayConfig.asset"));
            DrawStatusLine("VisualConfig", AssetExists($"{ConfigPath}/VisualConfig.asset"));
            DrawStatusLine("AudioConfig", AssetExists($"{ConfigPath}/AudioConfig.asset"));
            DrawStatusLine("Main Scene", AssetExists($"{ScenesPath}/NightflowMain.unity"));

            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusLine(string name, bool exists)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, GUILayout.Width(150));

            if (exists)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField("OK", EditorStyles.boldLabel);
            }
            else
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("Missing", EditorStyles.boldLabel);
            }
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private static bool AssetExists(string path)
        {
            return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
        }

        [MenuItem("Nightflow/Create/All Config Assets", false, 100)]
        public static void CreateAllConfigs()
        {
            EnsureDirectory(ConfigPath);

            // Create individual configs first
            var gameplayConfig = CreateAssetIfNotExists<GameplayConfig>(
                $"{ConfigPath}/GameplayConfig.asset");
            var visualConfig = CreateAssetIfNotExists<VisualConfig>(
                $"{ConfigPath}/VisualConfig.asset");
            var audioConfig = CreateAssetIfNotExists<AudioConfig>(
                $"{ConfigPath}/AudioConfig.asset");

            // Create master config and link them
            var masterConfig = CreateAssetIfNotExists<NightflowConfig>(
                $"{ConfigPath}/NightflowConfig.asset");

            if (masterConfig != null)
            {
                masterConfig.gameplay = gameplayConfig;
                masterConfig.visual = visualConfig;
                masterConfig.audio = audioConfig;
                EditorUtility.SetDirty(masterConfig);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Nightflow] Config assets created successfully!");
        }

        [MenuItem("Nightflow/Create/Main Scene", false, 101)]
        public static void CreateMainScene()
        {
            EnsureDirectory(ScenesPath);

            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load configs
            var masterConfig = AssetDatabase.LoadAssetAtPath<NightflowConfig>(
                $"{ConfigPath}/NightflowConfig.asset");
            var visualConfig = AssetDatabase.LoadAssetAtPath<VisualConfig>(
                $"{ConfigPath}/VisualConfig.asset");

            // Create hierarchy
            CreateManagersHierarchy(masterConfig);
            CreateCameraHierarchy(visualConfig);
            CreateUIHierarchy();
            CreateRenderingHierarchy();
            CreateLightingHierarchy();

            // Save scene
            string scenePath = $"{ScenesPath}/NightflowMain.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            // Add to build settings
            AddSceneToBuildSettings(scenePath);

            Debug.Log("[Nightflow] Main scene created successfully!");
        }

        private static void CreateManagersHierarchy(NightflowConfig masterConfig)
        {
            // Managers root
            var managersRoot = new GameObject("[Managers]");

            // SaveManager
            var saveManagerGO = new GameObject("SaveManager");
            saveManagerGO.transform.SetParent(managersRoot.transform);
            saveManagerGO.AddComponent<SaveManager>();

            // ConfigManager
            var configManagerGO = new GameObject("ConfigManager");
            configManagerGO.transform.SetParent(managersRoot.transform);
            var configManager = configManagerGO.AddComponent<ConfigManager>();

            // Assign master config if available
            if (masterConfig != null)
            {
                var serializedObject = new SerializedObject(configManager);
                var configProp = serializedObject.FindProperty("masterConfig");
                if (configProp != null)
                {
                    configProp.objectReferenceValue = masterConfig;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // AudioManager
            var audioManagerGO = new GameObject("AudioManager");
            audioManagerGO.transform.SetParent(managersRoot.transform);
            audioManagerGO.AddComponent<AudioManager>();
        }

        private static void CreateCameraHierarchy(VisualConfig visualConfig)
        {
            // Camera root
            var cameraRoot = new GameObject("[Camera]");

            // Main Camera
            var cameraGO = new GameObject("Main Camera");
            cameraGO.transform.SetParent(cameraRoot.transform);
            cameraGO.tag = "MainCamera";

            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            camera.fieldOfView = 75f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;

            // Add URP camera data
            var cameraData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;

            // Audio Listener
            cameraGO.AddComponent<AudioListener>();

            // Post Processing
            var postProcessGO = new GameObject("PostProcessing");
            postProcessGO.transform.SetParent(cameraGO.transform);

            var volume = postProcessGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0;

            var ppController = postProcessGO.AddComponent<PostProcessingController>();

            // Assign visual config if available
            if (visualConfig != null)
            {
                var serializedObject = new SerializedObject(ppController);
                var configProp = serializedObject.FindProperty("visualConfig");
                if (configProp != null)
                {
                    configProp.objectReferenceValue = visualConfig;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void CreateUIHierarchy()
        {
            // UI root
            var uiRoot = new GameObject("[UI]");

            // UI Document
            var uiDocGO = new GameObject("HUD");
            uiDocGO.transform.SetParent(uiRoot.transform);

            var uiDocument = uiDocGO.AddComponent<UIDocument>();

            // Try to find and assign the UXML
            var uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/src/UI/NightflowHUD.uxml");
            if (uxmlAsset == null)
            {
                // Try alternative path
                string[] guids = AssetDatabase.FindAssets("NightflowHUD t:VisualTreeAsset");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                }
            }

            if (uxmlAsset != null)
            {
                uiDocument.visualTreeAsset = uxmlAsset;
            }

            // UI Controller
            uiDocGO.AddComponent<UIController>();
        }

        private static void CreateRenderingHierarchy()
        {
            // Rendering root
            var renderingRoot = new GameObject("[Rendering]");

            // Star Field
            var starFieldGO = new GameObject("StarField");
            starFieldGO.transform.SetParent(renderingRoot.transform);
            starFieldGO.AddComponent<StarFieldRenderer>();

            // City Skyline
            var skylineGO = new GameObject("CitySkyline");
            skylineGO.transform.SetParent(renderingRoot.transform);
            skylineGO.AddComponent<CitySkylineRenderer>();

            // Moon
            var moonGO = new GameObject("Moon");
            moonGO.transform.SetParent(renderingRoot.transform);
            moonGO.AddComponent<MoonRenderer>();

            // Ground Fog
            var fogGO = new GameObject("GroundFog");
            fogGO.transform.SetParent(renderingRoot.transform);
            fogGO.AddComponent<GroundFogRenderer>();
        }

        private static void CreateLightingHierarchy()
        {
            // Lighting root
            var lightingRoot = new GameObject("[Lighting]");

            // Directional Light (moon light)
            var moonLightGO = new GameObject("Moon Light");
            moonLightGO.transform.SetParent(lightingRoot.transform);
            moonLightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var moonLight = moonLightGO.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.6f, 0.7f, 0.9f);
            moonLight.intensity = 0.1f;
            moonLight.shadows = LightShadows.Soft;

            // Ambient light settings via RenderSettings
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.01f, 0.01f, 0.02f);
            RenderSettings.fogDensity = 0.002f;
        }

        private static T CreateAssetIfNotExists<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                Debug.Log($"[Nightflow] Asset already exists: {path}");
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[Nightflow] Created: {path}");
            return asset;
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string currentPath = parts[0];

                for (int i = 1; i < parts.Length; i++)
                {
                    string newPath = $"{currentPath}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }
                    currentPath = newPath;
                }
            }
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;

            // Check if already added
            foreach (var scene in scenes)
            {
                if (scene.path == scenePath)
                    return;
            }

            // Add to build settings
            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(newScenes, 0);
            newScenes[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;

            Debug.Log($"[Nightflow] Added {scenePath} to build settings");
        }
    }
}
