// ============================================================================
// Nightflow - Gameplay Configuration
// ScriptableObject for vehicle physics, lane magnetism, scoring, and more
// ============================================================================

using UnityEngine;
using Unity.Mathematics;

namespace Nightflow.Config
{
    /// <summary>
    /// Master gameplay configuration containing all tunable parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayConfig", menuName = "Nightflow/Gameplay Config")]
    public class GameplayConfig : ScriptableObject
    {
        [Header("=== VEHICLE PHYSICS ===")]
        public VehiclePhysicsConfig vehiclePhysics = new VehiclePhysicsConfig();

        [Header("=== LANE MAGNETISM ===")]
        public LaneMagnetismConfig laneMagnetism = new LaneMagnetismConfig();

        [Header("=== TRAFFIC AI ===")]
        public TrafficAIConfig trafficAI = new TrafficAIConfig();

        [Header("=== HAZARD SPAWNING ===")]
        public HazardSpawnConfig hazardSpawn = new HazardSpawnConfig();

        [Header("=== SCORING ===")]
        public ScoringConfig scoring = new ScoringConfig();

        [Header("=== CAMERA ===")]
        public CameraConfig camera = new CameraConfig();

        [Header("=== DIFFICULTY ===")]
        public DifficultyConfig difficulty = new DifficultyConfig();
    }

    /// <summary>
    /// Vehicle physics parameters.
    /// </summary>
    [System.Serializable]
    public class VehiclePhysicsConfig
    {
        [Header("Speed")]
        [Tooltip("Maximum speed in km/h")]
        public float maxSpeed = 280f;

        [Tooltip("Acceleration in m/s²")]
        public float acceleration = 12f;

        [Tooltip("Braking deceleration in m/s²")]
        public float braking = 25f;

        [Tooltip("Coasting deceleration in m/s²")]
        public float coastDeceleration = 3f;

        [Header("Handling")]
        [Tooltip("Base steering sensitivity")]
        public float steeringSensitivity = 2.5f;

        [Tooltip("Steering sensitivity reduction at high speed (0-1)")]
        public float highSpeedSteeringReduction = 0.4f;

        [Tooltip("Speed at which steering reduction is fully applied (km/h)")]
        public float steeringReductionSpeed = 200f;

        [Header("Lane Changing")]
        [Tooltip("Time to complete lane change (seconds)")]
        public float laneChangeTime = 0.4f;

        [Tooltip("Lane change cooldown (seconds)")]
        public float laneChangeCooldown = 0.2f;

        [Tooltip("Lane width in meters")]
        public float laneWidth = 3.5f;

        [Header("Boost")]
        [Tooltip("Speed multiplier when boosting")]
        public float boostSpeedMultiplier = 1.3f;

        [Tooltip("Boost duration (seconds)")]
        public float boostDuration = 3f;

        [Tooltip("Boost cooldown (seconds)")]
        public float boostCooldown = 8f;

        [Header("Collision")]
        [Tooltip("Damage multiplier from collisions")]
        public float collisionDamageMultiplier = 1f;

        [Tooltip("Speed reduction on collision (0-1)")]
        public float collisionSpeedPenalty = 0.3f;

        [Tooltip("Invulnerability time after collision (seconds)")]
        public float collisionInvulnerability = 0.5f;
    }

    /// <summary>
    /// Lane magnetism (assist) settings.
    /// </summary>
    [System.Serializable]
    public class LaneMagnetismConfig
    {
        [Tooltip("Enable lane magnetism assist")]
        public bool enabled = true;

        [Tooltip("Magnetism strength (0-1)")]
        [Range(0f, 1f)]
        public float strength = 0.7f;

        [Tooltip("Distance from lane center to start magnetism (meters)")]
        public float activationDistance = 1.5f;

        [Tooltip("Speed at which magnetism is reduced (km/h)")]
        public float reductionSpeed = 150f;

        [Tooltip("Minimum magnetism at high speed (0-1)")]
        [Range(0f, 1f)]
        public float minimumStrength = 0.3f;

        [Tooltip("Magnetism disabled during manual steering")]
        public bool disableOnManualInput = true;

        [Tooltip("Time to re-enable after manual input (seconds)")]
        public float reEnableDelay = 0.5f;
    }

    /// <summary>
    /// Traffic AI behavior configuration.
    /// </summary>
    [System.Serializable]
    public class TrafficAIConfig
    {
        [Header("Spawning")]
        [Tooltip("Base traffic density (vehicles per 100m)")]
        public float baseDensity = 3f;

        [Tooltip("Maximum traffic density")]
        public float maxDensity = 8f;

        [Tooltip("Density increase per km traveled")]
        public float densityIncreaseRate = 0.1f;

        [Tooltip("Minimum spawn distance ahead (meters)")]
        public float minSpawnDistance = 150f;

        [Tooltip("Maximum spawn distance ahead (meters)")]
        public float maxSpawnDistance = 300f;

        [Tooltip("Despawn distance behind player (meters)")]
        public float despawnDistance = 100f;

        [Header("Speed")]
        [Tooltip("Base traffic speed (km/h)")]
        public float baseSpeed = 100f;

        [Tooltip("Speed variation (±km/h)")]
        public float speedVariation = 20f;

        [Tooltip("Slow vehicle spawn chance (0-1)")]
        [Range(0f, 1f)]
        public float slowVehicleChance = 0.15f;

        [Tooltip("Slow vehicle speed multiplier")]
        public float slowVehicleSpeedMultiplier = 0.6f;

        [Header("Lane Behavior")]
        [Tooltip("Chance to change lanes per second")]
        [Range(0f, 1f)]
        public float laneChangeChance = 0.05f;

        [Tooltip("Lane change duration (seconds)")]
        public float laneChangeDuration = 1.5f;

        [Tooltip("Minimum distance to player for lane change (meters)")]
        public float safeLaneChangeDistance = 30f;

        [Header("Emergency Vehicles")]
        [Tooltip("Emergency vehicle spawn chance per minute")]
        [Range(0f, 1f)]
        public float emergencySpawnChance = 0.1f;

        [Tooltip("Emergency vehicle speed (km/h)")]
        public float emergencySpeed = 180f;

        [Tooltip("Emergency spawn distance behind player (meters)")]
        public float emergencySpawnDistance = 200f;

        [Header("Vehicle Types")]
        [Tooltip("Sedan spawn weight")]
        public float sedanWeight = 0.5f;

        [Tooltip("SUV spawn weight")]
        public float suvWeight = 0.3f;

        [Tooltip("Truck spawn weight")]
        public float truckWeight = 0.2f;
    }

    /// <summary>
    /// Hazard spawning configuration.
    /// </summary>
    [System.Serializable]
    public class HazardSpawnConfig
    {
        [Header("General")]
        [Tooltip("Base hazard spawn rate (per 100m)")]
        public float baseSpawnRate = 0.5f;

        [Tooltip("Maximum hazard density")]
        public float maxSpawnRate = 2f;

        [Tooltip("Spawn rate increase per km")]
        public float spawnRateIncrease = 0.05f;

        [Tooltip("Minimum distance between hazards (meters)")]
        public float minHazardSpacing = 20f;

        [Header("Hazard Types")]
        [Tooltip("Traffic cone spawn weight")]
        public float coneWeight = 0.4f;

        [Tooltip("Debris spawn weight")]
        public float debrisWeight = 0.25f;

        [Tooltip("Tire spawn weight")]
        public float tireWeight = 0.2f;

        [Tooltip("Barrier block spawn weight")]
        public float barrierWeight = 0.1f;

        [Tooltip("Crashed car spawn weight")]
        public float crashedCarWeight = 0.05f;

        [Header("Damage Values")]
        [Tooltip("Damage from traffic cone (0-1)")]
        [Range(0f, 1f)]
        public float coneDamage = 0.05f;

        [Tooltip("Damage from debris (0-1)")]
        [Range(0f, 1f)]
        public float debrisDamage = 0.1f;

        [Tooltip("Damage from loose tire (0-1)")]
        [Range(0f, 1f)]
        public float tireDamage = 0.15f;

        [Tooltip("Damage from barrier block (0-1)")]
        [Range(0f, 1f)]
        public float barrierDamage = 0.25f;

        [Tooltip("Damage from crashed car (0-1)")]
        [Range(0f, 1f)]
        public float crashedCarDamage = 0.4f;

        [Header("Special Events")]
        [Tooltip("Construction zone spawn chance per km")]
        [Range(0f, 1f)]
        public float constructionZoneChance = 0.1f;

        [Tooltip("Multi-lane hazard pattern chance")]
        [Range(0f, 1f)]
        public float multiLanePatternChance = 0.2f;
    }

    /// <summary>
    /// Scoring and multiplier configuration.
    /// </summary>
    [System.Serializable]
    public class ScoringConfig
    {
        [Header("Base Scoring")]
        [Tooltip("Points per meter traveled")]
        public float pointsPerMeter = 1f;

        [Tooltip("Points per second survived")]
        public float pointsPerSecond = 10f;

        [Tooltip("Bonus points per km/h over 100")]
        public float speedBonusPerKmh = 0.5f;

        [Header("Multiplier")]
        [Tooltip("Starting multiplier")]
        public float baseMultiplier = 1f;

        [Tooltip("Maximum multiplier")]
        public float maxMultiplier = 5f;

        [Tooltip("Multiplier increase per near miss")]
        public float nearMissMultiplierBonus = 0.1f;

        [Tooltip("Multiplier decay rate per second")]
        public float multiplierDecayRate = 0.05f;

        [Tooltip("Multiplier reset on collision")]
        public bool resetMultiplierOnCollision = true;

        [Header("Near Miss")]
        [Tooltip("Near miss detection distance (meters)")]
        public float nearMissDistance = 2f;

        [Tooltip("Near miss points bonus")]
        public float nearMissPoints = 500f;

        [Tooltip("Perfect dodge distance (meters)")]
        public float perfectDodgeDistance = 1f;

        [Tooltip("Perfect dodge points bonus")]
        public float perfectDodgePoints = 1000f;

        [Header("Speed Bonuses")]
        [Tooltip("Speed threshold for bonus (km/h)")]
        public float speedBonusThreshold = 180f;

        [Tooltip("Points per second at speed bonus")]
        public float speedBonusPointsPerSecond = 50f;

        [Header("Survival Bonuses")]
        [Tooltip("Bonus points per 1km milestone")]
        public float kilometerBonus = 1000f;

        [Tooltip("Bonus points per 1 minute survived")]
        public float minuteBonus = 500f;
    }

    /// <summary>
    /// Camera behavior configuration.
    /// </summary>
    [System.Serializable]
    public class CameraConfig
    {
        [Header("Position")]
        [Tooltip("Base height above vehicle (meters)")]
        public float baseHeight = 3f;

        [Tooltip("Base distance behind vehicle (meters)")]
        public float baseDistance = 8f;

        [Tooltip("Height increase at max speed")]
        public float speedHeightBonus = 1.5f;

        [Tooltip("Distance increase at max speed")]
        public float speedDistanceBonus = 4f;

        [Header("Field of View")]
        [Tooltip("Base FOV (degrees)")]
        public float baseFOV = 60f;

        [Tooltip("FOV increase at max speed")]
        public float speedFOVBonus = 20f;

        [Tooltip("FOV smoothing speed")]
        public float fovSmoothSpeed = 3f;

        [Header("Look Ahead")]
        [Tooltip("Look ahead distance based on speed")]
        public float lookAheadFactor = 0.5f;

        [Tooltip("Maximum look ahead (meters)")]
        public float maxLookAhead = 15f;

        [Header("Shake")]
        [Tooltip("Screen shake on collision intensity")]
        public float collisionShakeIntensity = 0.5f;

        [Tooltip("Screen shake duration (seconds)")]
        public float collisionShakeDuration = 0.3f;

        [Tooltip("Speed shake intensity (subtle vibration)")]
        public float speedShakeIntensity = 0.02f;

        [Header("Smoothing")]
        [Tooltip("Position smoothing speed")]
        public float positionSmoothSpeed = 8f;

        [Tooltip("Rotation smoothing speed")]
        public float rotationSmoothSpeed = 5f;
    }

    /// <summary>
    /// Difficulty scaling configuration.
    /// </summary>
    [System.Serializable]
    public class DifficultyConfig
    {
        [Header("Scaling")]
        [Tooltip("Distance for full difficulty (km)")]
        public float fullDifficultyDistance = 10f;

        [Tooltip("Time for full difficulty (minutes)")]
        public float fullDifficultyTime = 5f;

        [Tooltip("Use distance or time for scaling")]
        public DifficultyScaleMode scaleMode = DifficultyScaleMode.Distance;

        [Header("Multipliers at Full Difficulty")]
        [Tooltip("Traffic density multiplier")]
        public float maxTrafficMultiplier = 2f;

        [Tooltip("Hazard spawn multiplier")]
        public float maxHazardMultiplier = 2.5f;

        [Tooltip("Traffic speed increase (km/h)")]
        public float maxTrafficSpeedBonus = 30f;

        [Tooltip("Emergency vehicle frequency multiplier")]
        public float maxEmergencyMultiplier = 3f;
    }

    public enum DifficultyScaleMode
    {
        Distance,
        Time,
        Combined
    }
}
