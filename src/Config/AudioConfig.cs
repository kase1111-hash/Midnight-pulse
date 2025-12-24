// ============================================================================
// Nightflow - Audio Configuration
// ScriptableObject for engine curves, doppler, reverb, and music settings
// ============================================================================

using UnityEngine;

namespace Nightflow.Config
{
    /// <summary>
    /// Master audio configuration containing all sound-related parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Nightflow/Audio Config")]
    public class AudioConfigAsset : ScriptableObject
    {
        [Header("=== MASTER VOLUME ===")]
        public MasterVolumeConfig masterVolume = new MasterVolumeConfig();

        [Header("=== ENGINE AUDIO ===")]
        public EngineAudioConfig engineAudio = new EngineAudioConfig();

        [Header("=== COLLISION AUDIO ===")]
        public CollisionAudioConfig collisionAudio = new CollisionAudioConfig();

        [Header("=== SIREN & DOPPLER ===")]
        public SirenAudioConfig sirenAudio = new SirenAudioConfig();

        [Header("=== REVERB ZONES ===")]
        public ReverbConfig reverb = new ReverbConfig();

        [Header("=== AMBIENT ===")]
        public AmbientAudioConfig ambient = new AmbientAudioConfig();

        [Header("=== MUSIC ===")]
        public MusicConfig music = new MusicConfig();

        [Header("=== UI AUDIO ===")]
        public UIAudioConfig uiAudio = new UIAudioConfig();
    }

    /// <summary>
    /// Master volume settings.
    /// </summary>
    [System.Serializable]
    public class MasterVolumeConfig
    {
        [Range(0f, 1f)]
        public float master = 1f;

        [Range(0f, 1f)]
        public float music = 0.7f;

        [Range(0f, 1f)]
        public float sfx = 1f;

        [Range(0f, 1f)]
        public float engine = 0.8f;

        [Range(0f, 1f)]
        public float ambient = 0.5f;

        [Range(0f, 1f)]
        public float ui = 0.8f;
    }

    /// <summary>
    /// Engine audio configuration with pitch/volume curves.
    /// </summary>
    [System.Serializable]
    public class EngineAudioConfig
    {
        [Header("RPM Range")]
        [Tooltip("Idle RPM")]
        public float idleRPM = 800f;

        [Tooltip("Redline RPM")]
        public float redlineRPM = 7500f;

        [Header("Pitch Curves")]
        [Tooltip("Pitch at idle")]
        public float idlePitch = 0.8f;

        [Tooltip("Pitch at redline")]
        public float redlinePitch = 2.0f;

        [Tooltip("Pitch curve (0=linear, 1=exponential)")]
        [Range(0f, 1f)]
        public float pitchCurveExponent = 0.7f;

        [Header("Volume Curves")]
        [Tooltip("Base engine volume")]
        [Range(0f, 1f)]
        public float baseVolume = 0.6f;

        [Tooltip("Volume increase under load")]
        [Range(0f, 0.5f)]
        public float loadVolumeBonus = 0.2f;

        [Tooltip("Volume at redline")]
        [Range(0f, 1f)]
        public float redlineVolume = 1f;

        [Header("Layer Crossfades")]
        [Tooltip("RPM for low layer center")]
        public float lowLayerCenter = 2500f;

        [Tooltip("RPM for mid layer center")]
        public float midLayerCenter = 4500f;

        [Tooltip("RPM for high layer center")]
        public float highLayerCenter = 6500f;

        [Tooltip("Crossfade width in RPM")]
        public float crossfadeWidth = 1500f;

        [Header("Tire Audio")]
        [Tooltip("Tire roll volume at max speed")]
        [Range(0f, 1f)]
        public float tireRollMaxVolume = 0.5f;

        [Tooltip("Speed for max tire volume (km/h)")]
        public float tireRollMaxSpeed = 150f;

        [Tooltip("Tire skid volume multiplier")]
        [Range(0f, 2f)]
        public float tireSkidVolumeMultiplier = 1.2f;

        [Header("Wind Audio")]
        [Tooltip("Wind volume at max speed")]
        [Range(0f, 1f)]
        public float windMaxVolume = 0.6f;

        [Tooltip("Speed for wind onset (km/h)")]
        public float windOnsetSpeed = 60f;

        [Tooltip("Speed for max wind (km/h)")]
        public float windMaxSpeed = 200f;

        [Header("Smoothing")]
        [Tooltip("RPM smoothing speed")]
        public float rpmSmoothSpeed = 8f;

        [Tooltip("Volume smoothing speed")]
        public float volumeSmoothSpeed = 10f;

        [Tooltip("Pitch smoothing speed")]
        public float pitchSmoothSpeed = 12f;
    }

    /// <summary>
    /// Collision audio configuration.
    /// </summary>
    [System.Serializable]
    public class CollisionAudioConfig
    {
        [Header("Impact Thresholds")]
        [Tooltip("Impulse for light impact")]
        public float lightImpactThreshold = 5f;

        [Tooltip("Impulse for medium impact")]
        public float mediumImpactThreshold = 20f;

        [Tooltip("Impulse for heavy impact")]
        public float heavyImpactThreshold = 50f;

        [Tooltip("Impulse for glass shatter")]
        public float glassShatterThreshold = 80f;

        [Header("Volume")]
        [Tooltip("Light impact volume")]
        [Range(0f, 1f)]
        public float lightImpactVolume = 0.4f;

        [Tooltip("Medium impact volume")]
        [Range(0f, 1f)]
        public float mediumImpactVolume = 0.7f;

        [Tooltip("Heavy impact volume")]
        [Range(0f, 1f)]
        public float heavyImpactVolume = 1f;

        [Header("Pitch Variation")]
        [Tooltip("Random pitch variation (±)")]
        [Range(0f, 0.3f)]
        public float pitchVariation = 0.1f;

        [Header("Scrape")]
        [Tooltip("Minimum velocity for scrape sound (m/s)")]
        public float scrapeMinVelocity = 3f;

        [Tooltip("Scrape volume at max intensity")]
        [Range(0f, 1f)]
        public float scrapeMaxVolume = 0.7f;

        [Tooltip("Scrape fade out time (seconds)")]
        public float scrapeFadeTime = 0.3f;

        [Header("Cooldowns")]
        [Tooltip("Minimum time between impacts (seconds)")]
        public float impactCooldown = 0.1f;
    }

    /// <summary>
    /// Siren and Doppler effect configuration.
    /// </summary>
    [System.Serializable]
    public class SirenAudioConfig
    {
        [Header("Doppler Effect")]
        [Tooltip("Speed of sound (m/s)")]
        public float speedOfSound = 343f;

        [Tooltip("Doppler effect intensity (0=none, 1=realistic, 2=exaggerated)")]
        [Range(0f, 2f)]
        public float dopplerScale = 1f;

        [Tooltip("Minimum Doppler pitch")]
        public float minDopplerPitch = 0.5f;

        [Tooltip("Maximum Doppler pitch")]
        public float maxDopplerPitch = 2f;

        [Header("Distance Attenuation")]
        [Tooltip("Minimum audible distance (meters)")]
        public float minDistance = 5f;

        [Tooltip("Maximum audible distance (meters)")]
        public float maxDistance = 300f;

        [Tooltip("Distance rolloff factor")]
        public float rolloffFactor = 1f;

        [Tooltip("Fade distance at max range (meters)")]
        public float fadeDistance = 50f;

        [Header("Siren Patterns")]
        [Tooltip("Police wail frequency (Hz)")]
        public float policeWailFrequency = 1.5f;

        [Tooltip("Ambulance yelp frequency (Hz)")]
        public float ambulanceYelpFrequency = 4f;

        [Tooltip("Fire horn frequency (Hz)")]
        public float fireHornFrequency = 0.8f;

        [Header("Volume")]
        [Tooltip("Maximum siren volume")]
        [Range(0f, 1f)]
        public float maxVolume = 1f;

        [Tooltip("Volume fade speed")]
        public float volumeFadeSpeed = 2f;
    }

    /// <summary>
    /// Reverb zone presets.
    /// </summary>
    [System.Serializable]
    public class ReverbConfig
    {
        [Header("Open Road")]
        public float openRoadDecay = 0.5f;
        public float openRoadEarlyReflections = 0.1f;
        public float openRoadLateReverb = 0.05f;

        [Header("Tunnel")]
        public float tunnelDecay = 3.5f;
        public float tunnelEarlyReflections = 0.6f;
        public float tunnelLateReverb = 0.7f;

        [Header("Overpass")]
        public float overpassDecay = 1.5f;
        public float overpassEarlyReflections = 0.4f;
        public float overpassLateReverb = 0.3f;

        [Header("Blending")]
        [Tooltip("Blend distance from zone edge (meters)")]
        public float blendDistance = 15f;

        [Tooltip("Reverb blend speed")]
        public float blendSpeed = 2f;
    }

    /// <summary>
    /// Ambient audio configuration.
    /// </summary>
    [System.Serializable]
    public class AmbientAudioConfig
    {
        [Header("Volume Levels")]
        [Tooltip("Open road ambience volume")]
        [Range(0f, 1f)]
        public float openRoadVolume = 0.3f;

        [Tooltip("Distant traffic volume")]
        [Range(0f, 1f)]
        public float distantTrafficVolume = 0.2f;

        [Tooltip("Tunnel drone volume")]
        [Range(0f, 1f)]
        public float tunnelDroneVolume = 0.4f;

        [Tooltip("City ambience volume")]
        [Range(0f, 1f)]
        public float cityAmbienceVolume = 0.25f;

        [Header("Transitions")]
        [Tooltip("Ambient fade speed")]
        public float fadeSpeed = 1.5f;
    }

    /// <summary>
    /// Dynamic music configuration.
    /// </summary>
    [System.Serializable]
    public class MusicConfig
    {
        [Header("Intensity")]
        [Tooltip("Low intensity threshold")]
        [Range(0f, 1f)]
        public float lowIntensityThreshold = 0.3f;

        [Tooltip("High intensity threshold")]
        [Range(0f, 1f)]
        public float highIntensityThreshold = 0.7f;

        [Tooltip("Intensity smoothing speed")]
        public float intensitySmoothSpeed = 2f;

        [Tooltip("Intensity decay rate per second")]
        public float intensityDecayRate = 0.1f;

        [Header("Layer Volumes")]
        [Tooltip("Base layer volume")]
        [Range(0f, 1f)]
        public float baseLayerVolume = 0.7f;

        [Tooltip("Low intensity layer volume")]
        [Range(0f, 1f)]
        public float lowIntensityLayerVolume = 0.6f;

        [Tooltip("High intensity layer volume")]
        [Range(0f, 1f)]
        public float highIntensityLayerVolume = 0.8f;

        [Header("Intensity Sources")]
        [Tooltip("Speed contribution to intensity (at max speed)")]
        [Range(0f, 1f)]
        public float speedIntensityContribution = 0.5f;

        [Tooltip("Speed for max intensity (km/h)")]
        public float speedIntensityMaxSpeed = 200f;

        [Tooltip("Multiplier contribution (at max multiplier)")]
        [Range(0f, 1f)]
        public float multiplierIntensityContribution = 0.3f;

        [Tooltip("Damage contribution")]
        [Range(0f, 1f)]
        public float damageIntensityContribution = 0.2f;

        [Header("Event Boosts")]
        [Tooltip("Speed boost event intensity")]
        [Range(0f, 0.5f)]
        public float speedBoostIntensity = 0.3f;

        [Tooltip("Near miss event intensity")]
        [Range(0f, 0.5f)]
        public float nearMissIntensity = 0.2f;

        [Tooltip("Collision event intensity")]
        [Range(0f, 0.5f)]
        public float collisionIntensity = 0.4f;

        [Tooltip("Emergency proximity intensity")]
        [Range(0f, 0.5f)]
        public float emergencyIntensity = 0.35f;

        [Header("Transitions")]
        [Tooltip("Crossfade duration (seconds)")]
        public float crossfadeDuration = 2f;

        [Tooltip("Fade out duration (seconds)")]
        public float fadeOutDuration = 3f;

        [Header("Tempo")]
        [Tooltip("Base BPM")]
        public float baseBPM = 120f;
    }

    /// <summary>
    /// UI audio configuration.
    /// </summary>
    [System.Serializable]
    public class UIAudioConfig
    {
        [Header("Score")]
        [Tooltip("Score tick interval (points)")]
        public float scoreTickInterval = 100f;

        [Tooltip("Minimum score tick delay (seconds)")]
        public float scoreTickMinDelay = 0.05f;

        [Tooltip("Score tick volume")]
        [Range(0f, 1f)]
        public float scoreTickVolume = 0.3f;

        [Header("Multiplier")]
        [Tooltip("Multiplier up volume")]
        [Range(0f, 1f)]
        public float multiplierUpVolume = 0.6f;

        [Tooltip("Multiplier lost volume")]
        [Range(0f, 1f)]
        public float multiplierLostVolume = 0.5f;

        [Tooltip("Multiplier chime cooldown (seconds)")]
        public float multiplierCooldown = 0.5f;

        [Header("Warnings")]
        [Tooltip("Damage warning volume")]
        [Range(0f, 1f)]
        public float damageWarningVolume = 0.7f;

        [Tooltip("Damage warning interval (seconds)")]
        public float damageWarningInterval = 0.8f;

        [Header("Events")]
        [Tooltip("Near miss volume")]
        [Range(0f, 1f)]
        public float nearMissVolume = 0.5f;

        [Tooltip("Near miss cooldown (seconds)")]
        public float nearMissCooldown = 0.3f;

        [Tooltip("Lane change volume")]
        [Range(0f, 1f)]
        public float laneChangeVolume = 0.3f;

        [Header("Menu")]
        [Tooltip("Menu select volume")]
        [Range(0f, 1f)]
        public float menuSelectVolume = 0.5f;

        [Tooltip("Menu back volume")]
        [Range(0f, 1f)]
        public float menuBackVolume = 0.4f;
    }
}
