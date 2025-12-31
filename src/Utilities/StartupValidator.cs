// ============================================================================
// Nightflow - Startup Validator
// Runs validation on game start to catch setup issues early
// ============================================================================

using UnityEngine;

namespace Nightflow.Utilities
{
    /// <summary>
    /// Automatically validates game setup on startup.
    /// Provides clear error messages if something is misconfigured.
    /// </summary>
    public static class StartupValidator
    {
        private static bool hasValidated = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoaded()
        {
            // Only validate once per session
            if (hasValidated) return;
            hasValidated = true;

            // Delay validation slightly to ensure all Awake/Start methods have run
            DelayedValidation();
        }

        private static async void DelayedValidation()
        {
            // Wait a frame to let components initialize
            await System.Threading.Tasks.Task.Yield();

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Run validation in editor and development builds
            var result = SetupValidator.ValidateAll();

            if (!result.IsValid)
            {
                Debug.LogError(
                    "╔══════════════════════════════════════════════════════════════╗\n" +
                    "║           NIGHTFLOW SETUP VALIDATION FAILED                   ║\n" +
                    "╚══════════════════════════════════════════════════════════════╝\n\n" +
                    result.GetSummary() +
                    "\n\nUse Nightflow > Auto-Setup > Force Setup Now to fix automatically."
                );
            }
            else if (result.Warnings.Count > 0)
            {
                Debug.LogWarning(
                    "╔══════════════════════════════════════════════════════════════╗\n" +
                    "║           NIGHTFLOW SETUP WARNINGS                            ║\n" +
                    "╚══════════════════════════════════════════════════════════════╝\n\n" +
                    result.GetSummary()
                );
            }
            #endif
        }

        /// <summary>
        /// Manually trigger validation (useful for debugging).
        /// </summary>
        public static void ValidateNow()
        {
            hasValidated = false;
            OnSceneLoaded();
        }
    }
}
