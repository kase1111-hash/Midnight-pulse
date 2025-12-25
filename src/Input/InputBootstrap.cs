// ============================================================================
// Nightflow - Input Bootstrap
// Ensures InputBindingManager is created at game start
// ============================================================================

using UnityEngine;
using Nightflow.Save;

namespace Nightflow.Input
{
    /// <summary>
    /// Bootstrap component that ensures InputBindingManager exists.
    /// Add this to a scene object or it will self-instantiate via RuntimeInitializeOnLoadMethod.
    /// </summary>
    public class InputBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            // Check if InputBindingManager already exists
            if (InputBindingManager.Instance != null) return;

            // Wait for SaveManager to exist, then create InputBindingManager
            if (SaveManager.Instance != null)
            {
                CreateInputBindingManager();
            }
            else
            {
                // Create a temporary object to wait for SaveManager
                var waitObj = new GameObject("[InputBootstrapWait]");
                waitObj.AddComponent<InputBootstrapWait>();
            }
        }

        private static void CreateInputBindingManager()
        {
            if (InputBindingManager.Instance != null) return;

            var go = new GameObject("[InputBindingManager]");
            go.AddComponent<InputBindingManager>();

            Debug.Log("InputBootstrap: Created InputBindingManager");
        }

        /// <summary>
        /// Helper component that waits for SaveManager then creates InputBindingManager.
        /// </summary>
        private class InputBootstrapWait : MonoBehaviour
        {
            private void Update()
            {
                if (SaveManager.Instance != null)
                {
                    CreateInputBindingManager();
                    Destroy(gameObject);
                }
            }
        }
    }
}
