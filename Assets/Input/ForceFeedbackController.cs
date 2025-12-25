// ============================================================================
// Nightflow - Force Feedback Controller
// Integrates ECS game state with wheel force feedback effects
// ============================================================================

using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Input
{
    /// <summary>
    /// Reads game state from ECS and triggers appropriate force feedback effects.
    /// Provides immersive driving feel through steering resistance, collisions, and surface effects.
    /// </summary>
    public class ForceFeedbackController : MonoBehaviour
    {
        public static ForceFeedbackController Instance { get; private set; }

        [Header("Force Feedback Scaling")]
        [SerializeField] [Range(0f, 1f)] private float collisionIntensity = 1.0f;
        [SerializeField] [Range(0f, 1f)] private float roadSurfaceIntensity = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float speedResistanceIntensity = 0.7f;
        [SerializeField] [Range(0f, 1f)] private float driftEffectIntensity = 0.8f;

        [Header("Speed-Based Settings")]
        [SerializeField] private float minSpeedForEffects = 10f;  // m/s
        [SerializeField] private float maxSpeedReference = 60f;   // m/s for max effect
        [SerializeField] private float centeringForceBase = 20;   // Base spring force
        [SerializeField] private float damperForceBase = 30;      // Base damper force

        [Header("Surface Effects")]
        [SerializeField] private int normalRoadPeriod = 50;       // ms
        [SerializeField] private int roughRoadPeriod = 20;        // ms
        [SerializeField] private int normalRoadMagnitude = 15;
        [SerializeField] private int roughRoadMagnitude = 40;

        // ECS references
        private EntityManager entityManager;
        private EntityQuery playerQuery;
        private EntityQuery gameStateQuery;
        private bool ecsInitialized;

        // Cached state for change detection
        private float lastSpeed;
        private float lastDamage;
        private bool wasColliding;
        private bool wasDrifting;
        private float lastYaw;
        private bool wasInTunnel;

        // Effect state
        private bool surfaceEffectActive;
        private bool damperActive;
        private bool springActive;
        private float collisionCooldown;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            TryInitializeECS();
        }

        private void TryInitializeECS()
        {
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                playerQuery = entityManager.CreateEntityQuery(
                    typeof(WorldTransform),
                    typeof(Velocity),
                    typeof(DamageState),
                    typeof(DriftState),
                    typeof(CollisionEvent),
                    typeof(PlayerVehicleTag)
                );
                gameStateQuery = entityManager.CreateEntityQuery(typeof(GameState));
                ecsInitialized = true;
            }
        }

        private void Update()
        {
            if (!ecsInitialized)
            {
                TryInitializeECS();
                return;
            }

            var wheelManager = WheelInputManager.Instance;
            if (wheelManager == null || !wheelManager.IsWheelConnected)
                return;

            // Update cooldowns
            if (collisionCooldown > 0f)
                collisionCooldown -= Time.deltaTime;

            // Read game state and update effects
            UpdateForceFeedback(wheelManager);
        }

        // Event query for ECS-triggered events
        private EntityQuery ffbEventQuery;

        private void UpdateForceFeedback(WheelInputManager wheel)
        {
            if (playerQuery.IsEmpty || gameStateQuery.IsEmpty)
                return;

            // Check if game is paused
            var gameState = gameStateQuery.GetSingleton<GameState>();
            if (gameState.IsPaused || gameState.TimeScale < 0.1f)
            {
                // Stop all effects when paused
                StopAllEffects(wheel);
                return;
            }

            // Get player state
            var playerEntity = playerQuery.GetSingletonEntity();
            var velocity = entityManager.GetComponentData<Velocity>(playerEntity);
            var damage = entityManager.GetComponentData<DamageState>(playerEntity);
            var drift = entityManager.GetComponentData<DriftState>(playerEntity);
            var collision = entityManager.GetComponentData<CollisionEvent>(playerEntity);

            float speed = velocity.Forward;

            // Process any force feedback events from ECS systems
            ProcessForceFeedbackEvents(wheel);

            // Update effects based on game state
            UpdateSpeedBasedEffects(wheel, speed);
            UpdateCollisionEffects(wheel, collision, damage);
            UpdateDriftEffects(wheel, drift, speed);
            UpdateDamageEffects(wheel, damage);
            UpdateSurfaceEffects(wheel, speed);

            // Cache state for next frame
            lastSpeed = speed;
            lastDamage = damage.Total;
            wasColliding = collision.Occurred;
            wasDrifting = drift.IsDrifting;
        }

        private void ProcessForceFeedbackEvents(WheelInputManager wheel)
        {
            // Initialize query if needed
            if (ffbEventQuery == default)
            {
                ffbEventQuery = entityManager.CreateEntityQuery(typeof(ForceFeedbackEvent));
            }

            if (ffbEventQuery.IsEmpty)
                return;

            var eventEntity = ffbEventQuery.GetSingletonEntity();
            var ffbEvent = entityManager.GetComponentData<ForceFeedbackEvent>(eventEntity);

            if (!ffbEvent.Triggered)
                return;

            // Process the event based on type
            switch (ffbEvent.EventType)
            {
                case ForceFeedbackEventType.Collision:
                    wheel.PlayFrontalCollision(ffbEvent.Intensity);
                    break;

                case ForceFeedbackEventType.SideCollision:
                    wheel.PlaySideCollision(ffbEvent.Intensity * (ffbEvent.Direction < 0 ? -1 : 1));
                    break;

                case ForceFeedbackEventType.FrontalCollision:
                    wheel.PlayFrontalCollision(ffbEvent.Intensity);
                    break;

                case ForceFeedbackEventType.Crash:
                    TriggerCrashEffect();
                    break;

                case ForceFeedbackEventType.NearMiss:
                    TriggerNearMiss(ffbEvent.Direction);
                    break;

                case ForceFeedbackEventType.DriftStart:
                    wheel.PlaySlipperyRoad((int)(50 * driftEffectIntensity));
                    break;

                case ForceFeedbackEventType.DriftEnd:
                    wheel.StopSlipperyRoad();
                    break;

                case ForceFeedbackEventType.TunnelEnter:
                    TriggerTunnelEntry();
                    break;

                case ForceFeedbackEventType.TunnelExit:
                    TriggerTunnelExit();
                    break;

                case ForceFeedbackEventType.Boost:
                    TriggerBoost();
                    break;

                case ForceFeedbackEventType.DamageTaken:
                    wheel.PlayFrontalCollision(ffbEvent.Intensity);
                    break;
            }

            // Clear the event after processing
            ffbEvent.Clear();
            entityManager.SetComponentData(eventEntity, ffbEvent);
        }

        private void UpdateSpeedBasedEffects(WheelInputManager wheel, float speed)
        {
            if (speed < minSpeedForEffects)
            {
                // At low speed, minimal effects
                if (damperActive)
                {
                    wheel.StopDamperForce();
                    damperActive = false;
                }
                if (springActive)
                {
                    wheel.StopSpringForce();
                    springActive = false;
                }
                return;
            }

            // Calculate speed factor (0-1)
            float speedFactor = Mathf.Clamp01((speed - minSpeedForEffects) / (maxSpeedReference - minSpeedForEffects));

            // Damper force increases with speed (heavier steering at high speed)
            int damperForce = (int)(damperForceBase + speedFactor * 40 * speedResistanceIntensity);
            wheel.PlayDamperForce(damperForce);
            damperActive = true;

            // Centering spring force
            int springCoef = (int)(centeringForceBase + speedFactor * 30 * speedResistanceIntensity);
            int springSat = (int)(50 + speedFactor * 30);
            wheel.PlaySpringForce(0, springSat, springCoef);
            springActive = true;
        }

        private void UpdateCollisionEffects(WheelInputManager wheel, CollisionEvent collision, DamageState damage)
        {
            if (!collision.Occurred || collisionCooldown > 0f)
                return;

            // Calculate collision intensity
            float impactForce = collision.ImpactSpeed / 30f; // Normalize to ~30 m/s max
            impactForce = Mathf.Clamp01(impactForce);

            int magnitude = (int)(impactForce * 100 * collisionIntensity);

            // Determine collision direction
            float3 normal = collision.Normal;
            float lateralComponent = math.dot(normal, new float3(1, 0, 0));

            if (Mathf.Abs(lateralComponent) > 0.5f)
            {
                // Side collision
                int sideMag = (int)(magnitude * Mathf.Sign(lateralComponent));
                wheel.PlaySideCollision(sideMag);
            }
            else
            {
                // Frontal/rear collision
                wheel.PlayFrontalCollision(magnitude);
            }

            // Set cooldown to prevent effect spam
            collisionCooldown = 0.15f;
        }

        private void UpdateDriftEffects(WheelInputManager wheel, DriftState drift, float speed)
        {
            if (drift.IsDrifting && speed > minSpeedForEffects)
            {
                // Calculate drift intensity based on slip angle
                float slipMagnitude = Mathf.Abs(drift.SlipAngle);
                float driftIntensity = Mathf.Clamp01(slipMagnitude / 0.5f);

                // Reduce grip feel during drift
                int slipperyMag = (int)(driftIntensity * 70 * driftEffectIntensity);
                wheel.PlaySlipperyRoad(slipperyMag);

                // Apply counter-steer force (wheel pulls in direction of slide)
                int counterForce = (int)(drift.YawRate * 20 * driftEffectIntensity);
                counterForce = Mathf.Clamp(counterForce, -60, 60);
                wheel.PlayConstantForce(counterForce);
            }
            else if (wasDrifting && !drift.IsDrifting)
            {
                // Just exited drift - stop slippery effect
                wheel.StopSlipperyRoad();
                wheel.StopConstantForce();
            }
        }

        private void UpdateDamageEffects(WheelInputManager wheel, DamageState damage)
        {
            // Damaged vehicle has reduced steering precision
            if (damage.Total > 0.3f)
            {
                // Calculate damage-based vibration
                float damageIntensity = Mathf.Clamp01((damage.Total - 0.3f) / 0.7f);
                int vibrationMag = (int)(damageIntensity * 25 * collisionIntensity);

                // Uneven damage causes pull
                float leftRight = damage.Right - damage.Left;
                int pullForce = (int)(leftRight * 30 * collisionIntensity);

                if (Mathf.Abs(pullForce) > 5)
                {
                    wheel.PlayConstantForce(pullForce);
                }
            }

            // Heavy damage event
            if (damage.Total - lastDamage > 0.1f)
            {
                // New significant damage taken
                wheel.PlayFrontalCollision((int)(50 * collisionIntensity));
            }
        }

        private void UpdateSurfaceEffects(WheelInputManager wheel, float speed)
        {
            if (speed < minSpeedForEffects)
            {
                if (surfaceEffectActive)
                {
                    wheel.StopSurfaceEffect();
                    surfaceEffectActive = false;
                }
                return;
            }

            // Normal road surface texture
            float speedFactor = Mathf.Clamp01(speed / maxSpeedReference);
            int magnitude = (int)(normalRoadMagnitude * speedFactor * roadSurfaceIntensity);

            if (magnitude > 0)
            {
                // Type 0 = sine wave (smooth road feel)
                wheel.PlaySurfaceEffect(0, magnitude, normalRoadPeriod);
                surfaceEffectActive = true;
            }
        }

        /// <summary>
        /// Trigger a crash effect - strong jolt.
        /// </summary>
        public void TriggerCrashEffect()
        {
            var wheel = WheelInputManager.Instance;
            if (wheel == null || !wheel.IsWheelConnected)
                return;

            // Strong frontal collision
            wheel.PlayFrontalCollision((int)(100 * collisionIntensity));

            // Follow up with heavy damping (loss of control feel)
            wheel.PlayDamperForce(80);
            wheel.PlaySlipperyRoad(100);

            // Schedule effect cleanup
            Invoke(nameof(ClearCrashEffect), 0.5f);
        }

        private void ClearCrashEffect()
        {
            var wheel = WheelInputManager.Instance;
            if (wheel != null)
            {
                wheel.StopSlipperyRoad();
                wheel.PlayDamperForce(30);
            }
        }

        /// <summary>
        /// Trigger tunnel entry effect - change in road feel.
        /// </summary>
        public void TriggerTunnelEntry()
        {
            var wheel = WheelInputManager.Instance;
            if (wheel == null || !wheel.IsWheelConnected)
                return;

            // Slightly rougher surface in tunnel
            wheel.PlaySurfaceEffect(1, (int)(roughRoadMagnitude * roadSurfaceIntensity), roughRoadPeriod);
            wasInTunnel = true;
        }

        /// <summary>
        /// Trigger tunnel exit effect.
        /// </summary>
        public void TriggerTunnelExit()
        {
            if (!wasInTunnel) return;

            var wheel = WheelInputManager.Instance;
            if (wheel == null || !wheel.IsWheelConnected)
                return;

            // Return to normal road surface
            wheel.PlaySurfaceEffect(0, (int)(normalRoadMagnitude * roadSurfaceIntensity), normalRoadPeriod);
            wasInTunnel = false;
        }

        /// <summary>
        /// Trigger near-miss effect - subtle jolt.
        /// </summary>
        public void TriggerNearMiss(float direction)
        {
            var wheel = WheelInputManager.Instance;
            if (wheel == null || !wheel.IsWheelConnected)
                return;

            // Small side bump
            int magnitude = (int)(30 * Mathf.Sign(direction) * collisionIntensity);
            wheel.PlaySideCollision(Mathf.Abs(magnitude));
        }

        /// <summary>
        /// Trigger boost effect - increased road feel.
        /// </summary>
        public void TriggerBoost()
        {
            var wheel = WheelInputManager.Instance;
            if (wheel == null || !wheel.IsWheelConnected)
                return;

            // Increase surface vibration during boost
            wheel.PlaySurfaceEffect(2, (int)(40 * roadSurfaceIntensity), 30);
        }

        private void StopAllEffects(WheelInputManager wheel)
        {
            wheel.StopAllForceFeedback();
            surfaceEffectActive = false;
            damperActive = false;
            springActive = false;
        }

        private void OnDestroy()
        {
            var wheel = WheelInputManager.Instance;
            if (wheel != null)
            {
                StopAllEffects(wheel);
            }
        }
    }
}
