// ============================================================================
// Nightflow - Time Scale Driver
// Plays the crash flow's slow-motion beat (GameState.TimeScale) on Unity time
// ============================================================================

using UnityEngine;
using Unity.Entities;
using Nightflow.Components;

namespace Nightflow.Utilities
{
    /// <summary>
    /// Applies the crash flow's presentation time-scale hint to Unity time, so
    /// the moment the car is lost the world drops into slow motion (0.3x on
    /// impact) and eases back to full speed before the fade to the summary.
    ///
    /// The world never stops: the scale is clamped to [MinTimeScale, 1] and
    /// falls back to 1 whenever there is no crash flow, no ECS world or no
    /// GameState, and when this driver is disabled or destroyed.
    ///
    /// Self-instantiates after the scene loads (like InputBootstrap), so no
    /// scene setup is needed.
    /// </summary>
    public class TimeScaleDriver : MonoBehaviour
    {
        /// <summary>Slowest the crash slow motion may run.</summary>
        private const float MinTimeScale = 0.25f;

        private static TimeScaleDriver _instance;

        private World _world;
        private EntityQuery _gameStateQuery;
        private bool _hasQuery;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null)
                return;

            var go = new GameObject("[TimeScaleDriver]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TimeScaleDriver>();
        }

        private void Update()
        {
            Time.timeScale = ReadTimeScale();
        }

        private float ReadTimeScale()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                _hasQuery = false;
                return 1f;
            }

            if (!_hasQuery || _world != world)
            {
                _world = world;
                _gameStateQuery = world.EntityManager.CreateEntityQuery(typeof(GameState));
                _hasQuery = true;
            }

            if (_gameStateQuery.CalculateEntityCount() != 1)
                return 1f;

            float scale = _gameStateQuery.GetSingleton<GameState>().TimeScale;
            if (!(scale > 0f))
                return 1f;   // never zero (and NaN-safe)

            return Mathf.Clamp(scale, MinTimeScale, 1f);
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            Time.timeScale = 1f;
        }
    }
}
