// ============================================================================
// Nightflow - Setup Validator
// Runtime validation with clear, actionable error messages
// ============================================================================

using UnityEngine;
using System.Collections.Generic;
using System.Text;

namespace Nightflow.Utilities
{
    /// <summary>
    /// Provides runtime validation for Nightflow components with clear error messages.
    /// Use this to validate setup and provide actionable feedback to developers.
    /// </summary>
    public static class SetupValidator
    {
        /// <summary>
        /// Validation result containing all issues found.
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid => Errors.Count == 0;
            public List<ValidationError> Errors { get; } = new List<ValidationError>();
            public List<ValidationWarning> Warnings { get; } = new List<ValidationWarning>();

            public void AddError(string component, string issue, string fix)
            {
                Errors.Add(new ValidationError(component, issue, fix));
            }

            public void AddWarning(string component, string issue, string fix)
            {
                Warnings.Add(new ValidationWarning(component, issue, fix));
            }

            public void LogAll()
            {
                foreach (var error in Errors)
                {
                    Log.SystemError(error.Component, $"{error.Issue}\n→ FIX: {error.Fix}");
                }

                foreach (var warning in Warnings)
                {
                    Log.SystemWarning(warning.Component, $"{warning.Issue}\n→ FIX: {warning.Fix}");
                }
            }

            public string GetSummary()
            {
                var sb = new StringBuilder();

                if (Errors.Count > 0)
                {
                    sb.AppendLine($"=== {Errors.Count} ERROR(S) FOUND ===\n");
                    for (int i = 0; i < Errors.Count; i++)
                    {
                        var e = Errors[i];
                        sb.AppendLine($"{i + 1}. [{e.Component}] {e.Issue}");
                        sb.AppendLine($"   → FIX: {e.Fix}\n");
                    }
                }

                if (Warnings.Count > 0)
                {
                    sb.AppendLine($"=== {Warnings.Count} WARNING(S) ===\n");
                    for (int i = 0; i < Warnings.Count; i++)
                    {
                        var w = Warnings[i];
                        sb.AppendLine($"{i + 1}. [{w.Component}] {w.Issue}");
                        sb.AppendLine($"   → FIX: {w.Fix}\n");
                    }
                }

                if (IsValid && Warnings.Count == 0)
                {
                    sb.AppendLine("✓ All validations passed!");
                }

                return sb.ToString();
            }
        }

        public struct ValidationError
        {
            public string Component;
            public string Issue;
            public string Fix;

            public ValidationError(string component, string issue, string fix)
            {
                Component = component;
                Issue = issue;
                Fix = fix;
            }
        }

        public struct ValidationWarning
        {
            public string Component;
            public string Issue;
            public string Fix;

            public ValidationWarning(string component, string issue, string fix)
            {
                Component = component;
                Issue = issue;
                Fix = fix;
            }
        }

        /// <summary>
        /// Validates all core Nightflow components at runtime.
        /// Call this on game start to catch configuration issues early.
        /// </summary>
        public static ValidationResult ValidateAll()
        {
            var result = new ValidationResult();

            ValidateConfigManager(result);
            ValidateAudioManager(result);
            ValidateCamera(result);
            ValidateRendering(result);
            ValidateUI(result);

            return result;
        }

        /// <summary>
        /// Validates ConfigManager setup.
        /// </summary>
        public static void ValidateConfigManager(ValidationResult result)
        {
            var configManager = Object.FindFirstObjectByType<Config.ConfigManager>();

            if (configManager == null)
            {
                result.AddError(
                    "ConfigManager",
                    "No ConfigManager found in scene",
                    "Add a ConfigManager to your scene: GameObject > Create Empty > Add Component > ConfigManager. " +
                    "Or use Nightflow > Auto-Setup > Force Setup Now"
                );
                return;
            }

            // Check if master config is assigned (using reflection since field is private)
            var masterConfigField = typeof(Config.ConfigManager).GetField("masterConfig",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (masterConfigField != null)
            {
                var masterConfig = masterConfigField.GetValue(configManager) as Config.NightflowConfig;

                if (masterConfig == null)
                {
                    result.AddError(
                        "ConfigManager",
                        "Master config is not assigned",
                        "Assign NightflowConfig to ConfigManager in the Inspector. " +
                        "Create one via: Assets > Create > Nightflow > Master Config, or use Nightflow > Auto-Setup > Force Setup Now"
                    );
                }
                else
                {
                    // Validate sub-configs
                    if (masterConfig.gameplay == null)
                    {
                        result.AddError(
                            "ConfigManager",
                            "GameplayConfig is not assigned in NightflowConfig",
                            "Open NightflowConfig asset and assign a GameplayConfig. " +
                            "Create one via: Assets > Create > Nightflow > Gameplay Config"
                        );
                    }

                    if (masterConfig.visual == null)
                    {
                        result.AddError(
                            "ConfigManager",
                            "VisualConfig is not assigned in NightflowConfig",
                            "Open NightflowConfig asset and assign a VisualConfig. " +
                            "Create one via: Assets > Create > Nightflow > Visual Config"
                        );
                    }

                    if (masterConfig.audio == null)
                    {
                        result.AddWarning(
                            "ConfigManager",
                            "AudioConfig is not assigned in NightflowConfig",
                            "Open NightflowConfig asset and assign an AudioConfig for audio settings. " +
                            "Create one via: Assets > Create > Nightflow > Audio Config"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Validates AudioManager setup.
        /// </summary>
        public static void ValidateAudioManager(ValidationResult result)
        {
            var audioManager = Object.FindFirstObjectByType<Audio.AudioManager>();

            if (audioManager == null)
            {
                result.AddWarning(
                    "AudioManager",
                    "No AudioManager found in scene",
                    "Add an AudioManager to your scene for audio playback. " +
                    "Use Nightflow > Auto-Setup > Force Setup Now to create one automatically"
                );
                return;
            }

            // Check clip collection
            var clipCollectionField = typeof(Audio.AudioManager).GetField("clipCollection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (clipCollectionField != null)
            {
                var clipCollection = clipCollectionField.GetValue(audioManager) as Audio.AudioClipCollection;

                if (clipCollection == null)
                {
                    result.AddWarning(
                        "AudioManager",
                        "No AudioClipCollection assigned - audio will not play",
                        "Assign an AudioClipCollection to AudioManager. " +
                        "Find it at Assets/Config/AudioClipCollection.asset, or create via Assets > Create > Nightflow > Audio Clip Collection"
                    );
                }
                else
                {
                    // Validate clips in collection
                    int missingCount = clipCollection.ValidateClips(out string[] missingClips);
                    if (missingCount > 0)
                    {
                        result.AddWarning(
                            "AudioManager",
                            $"{missingCount} audio clips not assigned in AudioClipCollection ({clipCollection.AssignedClipCount}/{clipCollection.TotalClipSlots} assigned)",
                            $"Open AudioClipCollection asset and assign the missing clips: {string.Join(", ", missingClips.Length > 5 ? new[] { missingClips[0], missingClips[1], "..." } : missingClips)}"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Validates camera setup.
        /// </summary>
        public static void ValidateCamera(ValidationResult result)
        {
            var mainCamera = Camera.main;

            if (mainCamera == null)
            {
                result.AddError(
                    "Camera",
                    "No Main Camera found in scene",
                    "Add a camera tagged 'MainCamera' to your scene. " +
                    "Use Nightflow > Auto-Setup > Force Setup Now to create one automatically"
                );
                return;
            }

            // Check for CameraSyncBridge
            var cameraSyncBridge = mainCamera.GetComponent<Rendering.CameraSyncBridge>();
            if (cameraSyncBridge == null)
            {
                result.AddError(
                    "Camera",
                    "CameraSyncBridge component missing on Main Camera",
                    "Add CameraSyncBridge component to Main Camera. This bridges ECS camera state to Unity's camera. " +
                    "Use Nightflow > Auto-Setup > Force Setup Now to fix automatically"
                );
            }

            // Check for AudioListener
            var audioListener = mainCamera.GetComponent<AudioListener>();
            if (audioListener == null)
            {
                result.AddWarning(
                    "Camera",
                    "No AudioListener on Main Camera",
                    "Add an AudioListener component to Main Camera to hear audio"
                );
            }
        }

        /// <summary>
        /// Validates rendering setup.
        /// </summary>
        public static void ValidateRendering(ValidationResult result)
        {
            var proceduralMeshRenderer = Object.FindFirstObjectByType<Rendering.ProceduralMeshRenderer>();

            if (proceduralMeshRenderer == null)
            {
                result.AddError(
                    "Rendering",
                    "No ProceduralMeshRenderer found in scene - track and vehicles will be invisible!",
                    "Add a ProceduralMeshRenderer to your scene. This renders ECS-generated geometry. " +
                    "Use Nightflow > Auto-Setup > Force Setup Now to create one automatically"
                );
            }
        }

        /// <summary>
        /// Validates UI setup.
        /// </summary>
        public static void ValidateUI(ValidationResult result)
        {
            var uiController = Object.FindFirstObjectByType<UI.UIController>();

            if (uiController == null)
            {
                result.AddWarning(
                    "UI",
                    "No UIController found in scene - HUD will not update",
                    "Add a UIController to your scene with a UIDocument. " +
                    "Use Nightflow > Auto-Setup > Force Setup Now to create one automatically"
                );
            }
        }

        /// <summary>
        /// Runs validation and logs results. Returns true if valid.
        /// </summary>
        public static bool ValidateAndLog()
        {
            var result = ValidateAll();

            if (!result.IsValid || result.Warnings.Count > 0)
            {
                result.LogAll();

                if (!result.IsValid)
                {
                    Log.SystemError("SetupValidator",
                        $"Setup validation failed with {result.Errors.Count} error(s). " +
                        "Use Nightflow > Auto-Setup > Force Setup Now to fix automatically.");
                }
            }

            return result.IsValid;
        }
    }
}
