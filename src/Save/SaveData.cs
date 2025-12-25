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

        // Input bindings
        public InputBindingPreset KeyboardBindings;
        public InputBindingPreset GamepadBindings;

        public ControlSettings()
        {
            // Initialize with default bindings
            KeyboardBindings = InputBindingPreset.CreateDefaultKeyboard();
            GamepadBindings = InputBindingPreset.CreateDefaultGamepad();
        }

        /// <summary>
        /// Reset all bindings to default.
        /// </summary>
        public void ResetBindingsToDefault()
        {
            KeyboardBindings = InputBindingPreset.CreateDefaultKeyboard();
            GamepadBindings = InputBindingPreset.CreateDefaultGamepad();
        }

        /// <summary>
        /// Get the active binding preset based on input mode.
        /// </summary>
        public InputBindingPreset GetActiveBindings(InputMode mode)
        {
            return mode == InputMode.Gamepad ? GamepadBindings : KeyboardBindings;
        }
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

    /// <summary>
    /// Input actions that can be rebound.
    /// </summary>
    public enum InputAction
    {
        // Vehicle controls
        Accelerate = 0,
        Brake = 1,
        SteerLeft = 2,
        SteerRight = 3,
        Handbrake = 4,

        // Camera
        LookBack = 5,
        CameraToggle = 6,

        // UI/Game
        Pause = 7,
        Confirm = 8,
        Cancel = 9,
        MenuUp = 10,
        MenuDown = 11,
        MenuLeft = 12,
        MenuRight = 13
    }

    /// <summary>
    /// Key binding for a single action. Supports primary and alternate bindings.
    /// </summary>
    [Serializable]
    public class InputBinding
    {
        public InputAction Action;

        // Keyboard bindings (stored as KeyCode int values for serialization)
        public int PrimaryKey = -1;     // -1 = unbound
        public int AlternateKey = -1;

        // Gamepad bindings (stored as string for axis/button names)
        public string GamepadButton = "";
        public string GamepadAxis = "";
        public bool GamepadAxisPositive = true;  // For axis direction

        public InputBinding() { }

        public InputBinding(InputAction action, KeyCode primary, KeyCode alternate = KeyCode.None)
        {
            Action = action;
            PrimaryKey = (int)primary;
            AlternateKey = alternate == KeyCode.None ? -1 : (int)alternate;
        }

        public InputBinding(InputAction action, string gamepadButton)
        {
            Action = action;
            GamepadButton = gamepadButton;
        }

        public KeyCode GetPrimaryKeyCode() =>
            PrimaryKey >= 0 ? (KeyCode)PrimaryKey : KeyCode.None;

        public KeyCode GetAlternateKeyCode() =>
            AlternateKey >= 0 ? (KeyCode)AlternateKey : KeyCode.None;

        public bool IsKeyBound(KeyCode key)
        {
            int keyInt = (int)key;
            return PrimaryKey == keyInt || AlternateKey == keyInt;
        }

        public void SetPrimaryKey(KeyCode key)
        {
            PrimaryKey = key == KeyCode.None ? -1 : (int)key;
        }

        public void SetAlternateKey(KeyCode key)
        {
            AlternateKey = key == KeyCode.None ? -1 : (int)key;
        }
    }

    /// <summary>
    /// Complete input binding preset (keyboard + gamepad).
    /// </summary>
    [Serializable]
    public class InputBindingPreset
    {
        public string Name = "Custom";
        public List<InputBinding> Bindings = new List<InputBinding>();

        public InputBindingPreset() { }

        public InputBindingPreset(string name)
        {
            Name = name;
            Bindings = new List<InputBinding>();
        }

        /// <summary>
        /// Get binding for a specific action.
        /// </summary>
        public InputBinding GetBinding(InputAction action)
        {
            foreach (var binding in Bindings)
            {
                if (binding.Action == action)
                    return binding;
            }
            return null;
        }

        /// <summary>
        /// Set or update binding for an action.
        /// </summary>
        public void SetBinding(InputBinding binding)
        {
            for (int i = 0; i < Bindings.Count; i++)
            {
                if (Bindings[i].Action == binding.Action)
                {
                    Bindings[i] = binding;
                    return;
                }
            }
            Bindings.Add(binding);
        }

        /// <summary>
        /// Create default keyboard bindings.
        /// </summary>
        public static InputBindingPreset CreateDefaultKeyboard()
        {
            var preset = new InputBindingPreset("Keyboard Default");

            // Vehicle controls
            preset.Bindings.Add(new InputBinding(InputAction.Accelerate, KeyCode.W, KeyCode.UpArrow));
            preset.Bindings.Add(new InputBinding(InputAction.Brake, KeyCode.S, KeyCode.DownArrow));
            preset.Bindings.Add(new InputBinding(InputAction.SteerLeft, KeyCode.A, KeyCode.LeftArrow));
            preset.Bindings.Add(new InputBinding(InputAction.SteerRight, KeyCode.D, KeyCode.RightArrow));
            preset.Bindings.Add(new InputBinding(InputAction.Handbrake, KeyCode.Space));

            // Camera
            preset.Bindings.Add(new InputBinding(InputAction.LookBack, KeyCode.Q));
            preset.Bindings.Add(new InputBinding(InputAction.CameraToggle, KeyCode.C));

            // UI
            preset.Bindings.Add(new InputBinding(InputAction.Pause, KeyCode.Escape, KeyCode.P));
            preset.Bindings.Add(new InputBinding(InputAction.Confirm, KeyCode.Return, KeyCode.Space));
            preset.Bindings.Add(new InputBinding(InputAction.Cancel, KeyCode.Escape, KeyCode.Backspace));
            preset.Bindings.Add(new InputBinding(InputAction.MenuUp, KeyCode.W, KeyCode.UpArrow));
            preset.Bindings.Add(new InputBinding(InputAction.MenuDown, KeyCode.S, KeyCode.DownArrow));
            preset.Bindings.Add(new InputBinding(InputAction.MenuLeft, KeyCode.A, KeyCode.LeftArrow));
            preset.Bindings.Add(new InputBinding(InputAction.MenuRight, KeyCode.D, KeyCode.RightArrow));

            return preset;
        }

        /// <summary>
        /// Create default gamepad bindings.
        /// </summary>
        public static InputBindingPreset CreateDefaultGamepad()
        {
            var preset = new InputBindingPreset("Gamepad Default");

            // Vehicle controls - use axis for analog input
            preset.Bindings.Add(new InputBinding(InputAction.Accelerate, "RightTrigger") { GamepadAxis = "Vertical", GamepadAxisPositive = true });
            preset.Bindings.Add(new InputBinding(InputAction.Brake, "LeftTrigger") { GamepadAxis = "Vertical", GamepadAxisPositive = false });
            preset.Bindings.Add(new InputBinding(InputAction.SteerLeft, "") { GamepadAxis = "Horizontal", GamepadAxisPositive = false });
            preset.Bindings.Add(new InputBinding(InputAction.SteerRight, "") { GamepadAxis = "Horizontal", GamepadAxisPositive = true });
            preset.Bindings.Add(new InputBinding(InputAction.Handbrake, "ButtonA"));

            // Camera
            preset.Bindings.Add(new InputBinding(InputAction.LookBack, "LeftBumper"));
            preset.Bindings.Add(new InputBinding(InputAction.CameraToggle, "RightStickClick"));

            // UI
            preset.Bindings.Add(new InputBinding(InputAction.Pause, "Start"));
            preset.Bindings.Add(new InputBinding(InputAction.Confirm, "ButtonA"));
            preset.Bindings.Add(new InputBinding(InputAction.Cancel, "ButtonB"));
            preset.Bindings.Add(new InputBinding(InputAction.MenuUp, "") { GamepadAxis = "DPadVertical", GamepadAxisPositive = true });
            preset.Bindings.Add(new InputBinding(InputAction.MenuDown, "") { GamepadAxis = "DPadVertical", GamepadAxisPositive = false });
            preset.Bindings.Add(new InputBinding(InputAction.MenuLeft, "") { GamepadAxis = "DPadHorizontal", GamepadAxisPositive = false });
            preset.Bindings.Add(new InputBinding(InputAction.MenuRight, "") { GamepadAxis = "DPadHorizontal", GamepadAxisPositive = true });

            return preset;
        }
    }
}
