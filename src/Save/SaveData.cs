// ============================================================================
// Nightflow - Save Data Structures
// Serializable data for persistent storage (settings, high scores, ghosts)
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nightflow.Save
{
    /// <summary>
    /// Root container for all save data.
    /// </summary>
    [Serializable]
    public class NightflowSaveData
    {
        public int Version = 1;
        public long LastSaveTimestamp;
        public SettingsData Settings = new SettingsData();
        public LeaderboardData Leaderboard = new LeaderboardData();
        public ProgressData Progress = new ProgressData();
        public List<GhostRecording> GhostRecordings = new List<GhostRecording>();
    }

    // ========================================================================
    // Settings Data
    // ========================================================================

    /// <summary>
    /// All user-configurable settings.
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        public AudioSettings Audio = new AudioSettings();
        public ControlSettings Controls = new ControlSettings();
        public DisplaySettings Display = new DisplaySettings();
        public GameplaySettings Gameplay = new GameplaySettings();
    }

    /// <summary>
    /// Audio volume and preference settings.
    /// </summary>
    [Serializable]
    public class AudioSettings
    {
        [Range(0f, 1f)] public float MasterVolume = 1f;
        [Range(0f, 1f)] public float MusicVolume = 0.7f;
        [Range(0f, 1f)] public float SFXVolume = 1f;
        [Range(0f, 1f)] public float EngineVolume = 0.8f;
        [Range(0f, 1f)] public float AmbientVolume = 0.5f;
        [Range(0f, 1f)] public float UIVolume = 0.8f;
        public bool MuteInBackground = true;
    }

    /// <summary>
    /// Control input settings.
    /// </summary>
    [Serializable]
    public class ControlSettings
    {
        // Steering
        [Range(0.5f, 2f)] public float SteeringSensitivity = 1f;
        [Range(0f, 1f)] public float SteeringDeadzone = 0.1f;
        public bool InvertSteering = false;

        // Camera
        [Range(0.5f, 2f)] public float CameraSensitivity = 1f;
        public bool InvertCameraY = false;

        // Vibration/Haptics
        public bool VibrationEnabled = true;
        [Range(0f, 1f)] public float VibrationIntensity = 1f;

        // Input mode
        public InputMode PreferredInputMode = InputMode.Auto;
    }

    /// <summary>
    /// Display and graphics settings.
    /// </summary>
    [Serializable]
    public class DisplaySettings
    {
        public bool Fullscreen = true;
        public int ResolutionWidth = 1920;
        public int ResolutionHeight = 1080;
        public int RefreshRate = 60;
        public int QualityLevel = 2;  // 0=Low, 1=Medium, 2=High, 3=Ultra
        public bool VSync = true;
        public int TargetFrameRate = 60;
        public bool ShowFPS = false;

        // HUD
        public bool ShowSpeedometer = true;
        public bool ShowMinimap = true;
        public bool ShowDamageIndicator = true;
        public HUDScale HUDScale = HUDScale.Normal;
    }

    /// <summary>
    /// Gameplay preference settings.
    /// </summary>
    [Serializable]
    public class GameplaySettings
    {
        public SpeedUnit SpeedUnit = SpeedUnit.KMH;
        public bool AutoBrake = false;
        public bool CameraShake = true;
        [Range(0f, 1f)] public float CameraShakeIntensity = 1f;
        public bool MotionBlur = true;
        public bool Tutorials = true;
        public GameMode LastPlayedMode = GameMode.Nightflow;
    }

    // ========================================================================
    // Leaderboard Data
    // ========================================================================

    /// <summary>
    /// Local leaderboard with all high scores.
    /// </summary>
    [Serializable]
    public class LeaderboardData
    {
        public List<HighScoreEntry> AllScores = new List<HighScoreEntry>();
        public List<HighScoreEntry> NightflowScores = new List<HighScoreEntry>();
        public List<HighScoreEntry> RedlineScores = new List<HighScoreEntry>();
        public List<HighScoreEntry> GhostScores = new List<HighScoreEntry>();
        public List<HighScoreEntry> FreeflowScores = new List<HighScoreEntry>();

        public const int MaxEntriesPerMode = 10;
    }

    /// <summary>
    /// Single high score entry.
    /// </summary>
    [Serializable]
    public class HighScoreEntry
    {
        public string Initials = "AAA";  // 3-letter arcade initials
        public int Score;
        public float MaxSpeed;           // m/s
        public float Distance;           // meters
        public float TimeSurvived;       // seconds
        public int ClosePasses;
        public int HazardsDodged;
        public GameMode Mode;
        public long Timestamp;           // Unix timestamp
        public uint Seed;                // For ghost replay

        public HighScoreEntry() { }

        public HighScoreEntry(string initials, int score, GameMode mode)
        {
            Initials = initials;
            Score = score;
            Mode = mode;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    // ========================================================================
    // Progress Data
    // ========================================================================

    /// <summary>
    /// Player progress and statistics.
    /// </summary>
    [Serializable]
    public class ProgressData
    {
        // Lifetime stats
        public int TotalRuns;
        public float TotalDistance;      // meters
        public float TotalTimePlayed;    // seconds
        public int TotalCrashes;
        public int TotalClosePasses;

        // Best records
        public int HighestScore;
        public float HighestSpeed;       // m/s
        public float LongestRun;         // seconds
        public float FarthestDistance;   // meters
        public float HighestMultiplier;

        // Per-mode records
        public ModeStats NightflowStats = new ModeStats();
        public ModeStats RedlineStats = new ModeStats();
        public ModeStats GhostStats = new ModeStats();
        public ModeStats FreeflowStats = new ModeStats();

        // Unlocks / achievements (future)
        public List<string> UnlockedAchievements = new List<string>();
    }

    /// <summary>
    /// Statistics for a specific game mode.
    /// </summary>
    [Serializable]
    public class ModeStats
    {
        public int Runs;
        public int HighScore;
        public float BestSpeed;
        public float BestDistance;
        public float BestTime;
    }

    // ========================================================================
    // Ghost Recording Data
    // ========================================================================

    /// <summary>
    /// Complete ghost recording for replay.
    /// </summary>
    [Serializable]
    public class GhostRecording
    {
        public string Id;                // Unique identifier
        public string Name;              // User-given name or auto-generated
        public uint Seed;                // World seed for determinism
        public GameMode Mode;
        public int Score;
        public float Duration;           // seconds
        public long Timestamp;           // When recorded

        public List<GhostInputFrame> Inputs = new List<GhostInputFrame>();

        // Starting state
        public SerializableVector3 StartPosition;
        public SerializableQuaternion StartRotation;

        public GhostRecording()
        {
            Id = Guid.NewGuid().ToString();
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    /// <summary>
    /// Single input frame for ghost playback.
    /// </summary>
    [Serializable]
    public class GhostInputFrame
    {
        public float Timestamp;
        public float Steer;
        public float Throttle;
        public float Brake;
        public bool Handbrake;

        public GhostInputFrame() { }

        public GhostInputFrame(float timestamp, float steer, float throttle, float brake, bool handbrake)
        {
            Timestamp = timestamp;
            Steer = steer;
            Throttle = throttle;
            Brake = brake;
            Handbrake = handbrake;
        }
    }

    // ========================================================================
    // Serializable Unity Types
    // ========================================================================

    /// <summary>
    /// Serializable Vector3 for JSON.
    /// </summary>
    [Serializable]
    public struct SerializableVector3
    {
        public float x, y, z;

        public SerializableVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public SerializableVector3(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }

        public Vector3 ToVector3() => new Vector3(x, y, z);

        public static implicit operator Vector3(SerializableVector3 v) => v.ToVector3();
        public static implicit operator SerializableVector3(Vector3 v) => new SerializableVector3(v);
    }

    /// <summary>
    /// Serializable Quaternion for JSON.
    /// </summary>
    [Serializable]
    public struct SerializableQuaternion
    {
        public float x, y, z, w;

        public SerializableQuaternion(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public SerializableQuaternion(Quaternion q)
        {
            x = q.x;
            y = q.y;
            z = q.z;
            w = q.w;
        }

        public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);

        public static implicit operator Quaternion(SerializableQuaternion q) => q.ToQuaternion();
        public static implicit operator SerializableQuaternion(Quaternion q) => new SerializableQuaternion(q);
    }

    // ========================================================================
    // Enums
    // ========================================================================

    public enum InputMode
    {
        Auto = 0,
        Keyboard = 1,
        Gamepad = 2,
        Touch = 3
    }

    public enum SpeedUnit
    {
        KMH = 0,
        MPH = 1
    }

    public enum HUDScale
    {
        Small = 0,
        Normal = 1,
        Large = 2
    }

    public enum GameMode
    {
        Nightflow = 0,
        Redline = 1,
        Ghost = 2,
        Freeflow = 3
    }
}
