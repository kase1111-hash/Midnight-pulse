// ============================================================================
// Nightflow - Input Bootstrap
// Ensures InputBindingManager and wheel support are created at game start
// ============================================================================

using UnityEngine;
using Nightflow.Save;

namespace Nightflow.Input
{
    /// <summary>
    /// Bootstrap component that ensures InputBindingManager and wheel support exist.
    /// Add this to a scene object or it will self-instantiate via RuntimeInitializeOnLoadMethod.
    /// </summary>
    public class InputBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            // Check if InputBindingManager already exists
            if (InputBindingManager.Instance != null) return;

            // Wait for SaveManager to exist, then create input managers
            if (SaveManager.Instance != null)
            {
                CreateInputManagers();
            }
            else
            {
                // Create a temporary object to wait for SaveManager
                var waitObj = new GameObject("[InputBootstrapWait]");
                waitObj.AddComponent<InputBootstrapWait>();
            }
        }

        private static void CreateInputManagers()
        {
            // Create InputBindingManager
            if (InputBindingManager.Instance == null)
            {
                var bindingGo = new GameObject("[InputBindingManager]");
                bindingGo.AddComponent<InputBindingManager>();
                Debug.Log("InputBootstrap: Created InputBindingManager");
            }

            // Create WheelInputManager for Logitech G920/G29 support
            if (WheelInputManager.Instance == null)
            {
                var wheelGo = new GameObject("[WheelInputManager]");
                wheelGo.AddComponent<WheelInputManager>();
                Debug.Log("InputBootstrap: Created WheelInputManager");
            }

            // Create ForceFeedbackController for wheel force feedback effects
            if (ForceFeedbackController.Instance == null)
            {
                var ffbGo = new GameObject("[ForceFeedbackController]");
                ffbGo.AddComponent<ForceFeedbackController>();
                Debug.Log("InputBootstrap: Created ForceFeedbackController");
            }
        }

        /// <summary>
        /// Helper component that waits for SaveManager then creates input managers.
        /// </summary>
        private class InputBootstrapWait : MonoBehaviour
        {
            private void Update()
            {
                if (SaveManager.Instance != null)
                {
                    CreateInputManagers();
                    Destroy(gameObject);
                }
            }
        }
    }
}
