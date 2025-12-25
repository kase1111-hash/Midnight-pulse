// ============================================================================
// Nightflow - Unity DOTS Components: UI and Game State
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace Nightflow.Components
{
    /// <summary>
    /// UI state singleton for MonoBehaviour HUD bridge.
    /// Updated by UISystem every frame.
    ///
    /// From spec:
    /// - Speed, Multiplier, Score, Damage zone indicators
    /// - Transparent overlay, minimal HUD
    /// - Off-screen threat signaling
    /// </summary>
    public struct UIState : IComponentData
    {
        // Speedometer
        public float SpeedKmh;
        public float SpeedMph;
        public int SpeedTier;           // 0=Cruise, 1=Fast, 2=Boosted

        // Score display
        public float Score;
        public float DisplayScore;      // Smoothed for animation
        public float Multiplier;
        public float HighestMultiplier;
        public bool MultiplierFlash;

        // Risk meter
        public float RiskValue;
        public float RiskCap;
        public float RiskPercent;

        // Damage indicators
        public float DamageTotal;
        public float DamageFront;
        public float DamageRear;
        public float DamageLeft;
        public float DamageRight;
        public bool DamageFlash;
        public bool CriticalDamage;

        // Warnings
        public int WarningPriority;     // 0=None, 1=Risk, 2=Damage, 3=Emergency
        public bool WarningFlash;
        public float EmergencyDistance;
        public float EmergencyETA;

        // Progress
        public float DistanceKm;
        public float TimeSurvived;

        // Off-screen signals (packed)
        public int SignalCount;
        public float4 Signal0;          // xy=screenPos, z=urgency, w=type
        public float4 Signal1;
        public float4 Signal2;
        public float4 Signal3;

        // Menu/overlay state
        public bool ShowPauseMenu;
        public bool ShowCrashOverlay;
        public bool ShowScoreSummary;
        public bool ShowModeSelect;
        public bool ShowMainMenu;
        public bool ShowCredits;
        public float OverlayAlpha;

        // Title screen state
        public bool ShowPressStart;
        public int MainMenuSelection;
    }

    /// <summary>
    /// Game state singleton for flow management.
    /// Handles pause, crash flow, and autopilot activation.
    ///
    /// From spec:
    /// - Pause with 5-second cooldown
    /// - Crash flow: impact → shake → fade → summary → reset → autopilot
    /// </summary>
    public struct GameState : IComponentData
    {
        // Pause state
        public bool IsPaused;
        public float PauseCooldown;         // Seconds until pause allowed again
        public float PauseCooldownMax;      // 5 seconds per spec

        // Crash flow state
        public CrashFlowPhase CrashPhase;
        public float CrashPhaseTimer;
        public float FadeAlpha;

        // Autopilot state
        public bool AutopilotQueued;        // Will enable after crash reset
        public bool PlayerControlActive;    // Player has taken control
        public float IdleTimer;             // Time since last input

        // Menu state
        public MenuState CurrentMenu;
        public bool MenuVisible;

        // Time scale (for crash slow-mo)
        public float TimeScale;
    }

    /// <summary>
    /// Crash flow phases per spec.
    /// </summary>
    public enum CrashFlowPhase : byte
    {
        None = 0,
        Impact = 1,         // Initial impact, shake
        ScreenShake = 2,    // Extended shake
        FadeOut = 3,        // Fade to black
        Summary = 4,        // Score breakdown display
        Reset = 5,          // Vehicle reset
        FadeIn = 6          // Fade back in, autopilot starts
    }

    /// <summary>
    /// Menu states.
    /// </summary>
    public enum MenuState : byte
    {
        None = 0,
        Pause = 1,
        ScoreSummary = 2,
        Settings = 3,
        Leaderboard = 4,
        ModeSelect = 5,
        MainMenu = 6,
        Credits = 7
    }

    /// <summary>
    /// Game flow phase for overall game state management.
    /// </summary>
    public enum GameFlowPhase : byte
    {
        /// <summary>Title screen / main menu - game not active.</summary>
        TitleScreen = 0,

        /// <summary>Mode selection before starting a run.</summary>
        ModeSelection = 1,

        /// <summary>Active gameplay in progress.</summary>
        Playing = 2,

        /// <summary>Game paused during active run.</summary>
        Paused = 3,

        /// <summary>Crash sequence in progress.</summary>
        Crashing = 4,

        /// <summary>Score summary display after crash.</summary>
        Summary = 5,

        /// <summary>Transitioning between states (fade).</summary>
        Transitioning = 6
    }

    /// <summary>
    /// Main menu navigation state for title screen.
    /// </summary>
    public struct MainMenuState : IComponentData
    {
        /// <summary>Currently highlighted menu item (0-based).</summary>
        public int SelectedIndex;

        /// <summary>Number of menu items.</summary>
        public int ItemCount;

        /// <summary>Whether title animation has completed.</summary>
        public bool TitleAnimationComplete;

        /// <summary>Time since title screen appeared.</summary>
        public float DisplayTime;

        /// <summary>Whether any input has been received.</summary>
        public bool InputReceived;

        /// <summary>"Press Start" blink timer.</summary>
        public float BlinkTimer;

        /// <summary>Whether currently showing "Press Start" prompt.</summary>
        public bool ShowPressStart;
    }

    /// <summary>
    /// Game session state - tracks current run and overall flow.
    /// </summary>
    public struct GameSessionState : IComponentData
    {
        /// <summary>Current game flow phase.</summary>
        public GameFlowPhase CurrentPhase;

        /// <summary>Previous phase for transition handling.</summary>
        public GameFlowPhase PreviousPhase;

        /// <summary>Transition progress (0-1).</summary>
        public float TransitionProgress;

        /// <summary>Whether a game session is currently active.</summary>
        public bool SessionActive;

        /// <summary>Total runs completed this session.</summary>
        public int RunCount;

        /// <summary>Whether player has seen credits.</summary>
        public bool CreditsViewed;

        /// <summary>Last selected game mode.</summary>
        public GameMode LastSelectedMode;
    }

    /// <summary>
    /// Score summary data for end-of-run display.
    /// </summary>
    public struct ScoreSummaryDisplay : IComponentData
    {
        public float FinalScore;
        public float TotalDistance;
        public float MaxSpeed;
        public float TimeSurvived;

        public int ClosePasses;
        public int HazardsDodged;
        public int DriftRecoveries;
        public int PerfectSegments;

        public float RiskBonusTotal;
        public float SpeedBonusTotal;

        public CrashReason EndReason;

        public bool IsNewHighScore;
        public int LeaderboardRank;
    }

    /// <summary>
    /// Crash reason enumeration.
    /// </summary>
    public enum CrashReason : byte
    {
        None = 0,
        TotalDamage = 1,        // Accumulated too much damage
        BarrierImpact = 2,      // Hit barrier at high speed
        HeadOnCollision = 3,    // Collided with oncoming traffic
        Rollover = 4            // Flipped/rolled vehicle
    }

    /// <summary>
    /// HUD notification for temporary display messages.
    /// </summary>
    public struct HUDNotification : IBufferElementData
    {
        /// <summary>Type of notification.</summary>
        public HUDNotificationType Type;

        /// <summary>Associated value (points, multiplier, etc).</summary>
        public float Value;

        /// <summary>Time remaining to display (seconds).</summary>
        public float TimeRemaining;

        /// <summary>Screen position for popup (0-1 normalized).</summary>
        public float2 ScreenPosition;
    }

    /// <summary>
    /// Types of HUD notifications.
    /// </summary>
    public enum HUDNotificationType : byte
    {
        NearMiss = 0,           // +500 Near Miss!
        MultiplierUp = 1,       // Multiplier x2.0!
        MultiplierLost = 2,     // Multiplier Lost
        SpeedBonus = 3,         // Speed Bonus +1000
        DamageWarning = 4,      // ! Damage Critical
        NewHighScore = 5,       // New High Score!
        PerfectDodge = 6,       // Perfect Dodge!
        DriftBonus = 7          // Drift Recovery +250
    }

    /// <summary>
    /// Singleton tag for main UI controller entity.
    /// </summary>
    public struct UIControllerTag : IComponentData { }

    // ============================================================================
    // Game Mode Components
    // ============================================================================

    /// <summary>
    /// Available game modes.
    /// </summary>
    public enum GameMode : byte
    {
        /// <summary>
        /// Standard gameplay with all features enabled.
        /// Normal speed limits, hazards, traffic, and scoring.
        /// </summary>
        Nightflow = 0,

        /// <summary>
        /// No top speed limit - acceleration diminishes asymptotically.
        /// Challenge mode for pushing speed limits. The faster you go,
        /// the harder it is to go faster. Tests driver skill at extreme velocities.
        /// Formula: acceleration = baseAccel / (1 + speed/referenceSpeed)
        /// </summary>
        Redline = 1,

        /// <summary>
        /// Race against a ghost of your previous run.
        /// Input a seed to replay and compete against your past self.
        /// Ghost vehicle shows your previous path in real-time.
        /// </summary>
        Ghost = 2,

        /// <summary>
        /// Relaxed driving with no hazards or damage.
        /// Perfect for enjoying the scenery and practicing controls.
        /// No scoring pressure, just the open road.
        /// </summary>
        Freeflow = 3
    }

    /// <summary>
    /// Current game mode state singleton.
    /// Controls gameplay rules and modifiers based on selected mode.
    /// </summary>
    public struct GameModeState : IComponentData
    {
        /// <summary>Currently active game mode.</summary>
        public GameMode CurrentMode;

        /// <summary>Whether mode selection menu is active.</summary>
        public bool ModeSelectActive;

        /// <summary>Session seed for ghost recording/playback.</summary>
        public uint SessionSeed;

        /// <summary>Whether currently recording for ghost mode.</summary>
        public bool IsRecording;

        /// <summary>Whether playing back a ghost run.</summary>
        public bool IsPlayingGhost;

        /// <summary>Current speed record for Redline mode (m/s).</summary>
        public float RedlineMaxSpeed;

        /// <summary>Best speed ever achieved in Redline mode (m/s).</summary>
        public float RedlinePersonalBest;

        /// <summary>Time spent above 100 m/s in current Redline run.</summary>
        public float RedlineExtreme;
    }

    /// <summary>
    /// Configuration for ghost mode racing.
    /// </summary>
    public struct GhostModeConfig : IComponentData
    {
        /// <summary>Seed for deterministic run generation.</summary>
        public uint RunSeed;

        /// <summary>Whether ghost vehicle is visible.</summary>
        public bool ShowGhost;

        /// <summary>Ghost vehicle transparency (0-1).</summary>
        public float GhostAlpha;

        /// <summary>Color tint for ghost vehicle.</summary>
        public float4 GhostColor;

        /// <summary>Time offset for ghost (negative = ghost ahead).</summary>
        public float TimeOffset;

        /// <summary>Whether to show ghost trail.</summary>
        public bool ShowTrail;
    }

    /// <summary>
    /// Redline mode statistics for the current run.
    /// </summary>
    public struct RedlineStats : IComponentData
    {
        /// <summary>Peak speed reached this run (m/s).</summary>
        public float PeakSpeed;

        /// <summary>Current speed (m/s).</summary>
        public float CurrentSpeed;

        /// <summary>Time to reach 100 m/s.</summary>
        public float TimeTo100;

        /// <summary>Time to reach 150 m/s.</summary>
        public float TimeTo150;

        /// <summary>Time to reach 200 m/s.</summary>
        public float TimeTo200;

        /// <summary>Total distance traveled this run.</summary>
        public float TotalDistance;

        /// <summary>Whether 100 m/s milestone was reached.</summary>
        public bool Reached100;

        /// <summary>Whether 150 m/s milestone was reached.</summary>
        public bool Reached150;

        /// <summary>Whether 200 m/s milestone was reached.</summary>
        public bool Reached200;
    }

    /// <summary>
    /// Mode-specific display data for UI.
    /// </summary>
    public struct ModeDisplayState : IComponentData
    {
        /// <summary>Mode name to display.</summary>
        public FixedString32Bytes ModeName;

        /// <summary>Mode description for UI.</summary>
        public FixedString128Bytes ModeDescription;

        /// <summary>Whether to show speed-focused HUD (Redline).</summary>
        public bool ShowSpeedHUD;

        /// <summary>Whether to show ghost comparison (Ghost mode).</summary>
        public bool ShowGhostComparison;

        /// <summary>Whether to show relaxed HUD (Freeflow).</summary>
        public bool ShowRelaxedHUD;

        /// <summary>Time difference vs ghost (positive = ahead).</summary>
        public float GhostTimeDelta;

        /// <summary>Distance difference vs ghost (positive = ahead).</summary>
        public float GhostDistanceDelta;
    }

    // ============================================================================
    // Leaderboard Components
    // ============================================================================

    /// <summary>
    /// Single leaderboard entry - arcade style with 3-letter initials.
    /// </summary>
    public struct LeaderboardEntry : IBufferElementData
    {
        /// <summary>Rank position (1-10).</summary>
        public int Rank;

        /// <summary>Player initials - 3 characters, arcade style.</summary>
        public FixedString32Bytes Initials;

        /// <summary>Score achieved.</summary>
        public int Score;

        /// <summary>Max speed reached (m/s) for display.</summary>
        public float MaxSpeed;

        /// <summary>Distance traveled (km).</summary>
        public float Distance;

        /// <summary>Game mode this score was achieved in.</summary>
        public GameMode Mode;

        /// <summary>Unix timestamp when score was set.</summary>
        public long Timestamp;

        /// <summary>Whether this is the current player's entry (for highlighting).</summary>
        public bool IsCurrentPlayer;
    }

    /// <summary>
    /// Leaderboard state singleton.
    /// </summary>
    public struct LeaderboardState : IComponentData
    {
        /// <summary>Currently selected leaderboard filter.</summary>
        public LeaderboardFilter CurrentFilter;

        /// <summary>Whether leaderboard is currently visible.</summary>
        public bool IsVisible;

        /// <summary>Currently highlighted rank (for scrolling/selection).</summary>
        public int SelectedRank;

        /// <summary>Current player's best rank (-1 if not on board).</summary>
        public int PlayerBestRank;

        /// <summary>Total number of entries.</summary>
        public int EntryCount;

        /// <summary>Whether new score entry is being made.</summary>
        public bool IsEnteringInitials;

        /// <summary>Current initial being edited (0-2).</summary>
        public int CurrentInitialIndex;

        /// <summary>Pending initials during entry.</summary>
        public FixedString32Bytes PendingInitials;

        /// <summary>Score being entered.</summary>
        public int PendingScore;
    }

    /// <summary>
    /// Leaderboard filter options.
    /// </summary>
    public enum LeaderboardFilter : byte
    {
        /// <summary>All game modes combined.</summary>
        AllModes = 0,

        /// <summary>Nightflow mode only.</summary>
        Nightflow = 1,

        /// <summary>Redline mode only.</summary>
        Redline = 2,

        /// <summary>Ghost mode only.</summary>
        Ghost = 3,

        /// <summary>Freeflow mode only.</summary>
        Freeflow = 4
    }

    /// <summary>
    /// Tag for leaderboard controller entity.
    /// </summary>
    public struct LeaderboardTag : IComponentData { }

    // ============================================================================
    // Performance Stats Components
    // ============================================================================

    /// <summary>
    /// Performance statistics singleton for debug/stats HUD display.
    /// Tracks FPS, entity counts, and other performance metrics.
    /// </summary>
    public struct PerformanceStats : IComponentData
    {
        /// <summary>Current frames per second.</summary>
        public float FPS;

        /// <summary>Smoothed FPS for stable display.</summary>
        public float SmoothedFPS;

        /// <summary>Current frame time in milliseconds.</summary>
        public float FrameTimeMs;

        /// <summary>Minimum frame time this session (best).</summary>
        public float MinFrameTimeMs;

        /// <summary>Maximum frame time this session (worst).</summary>
        public float MaxFrameTimeMs;

        /// <summary>Total entity count in the world.</summary>
        public int EntityCount;

        /// <summary>Number of traffic vehicles currently active.</summary>
        public int TrafficCount;

        /// <summary>Number of hazards currently active.</summary>
        public int HazardCount;

        /// <summary>Number of track segments currently loaded.</summary>
        public int SegmentCount;

        /// <summary>Player's current speed in m/s.</summary>
        public float PlayerSpeed;

        /// <summary>Player's current Z position (distance from origin).</summary>
        public float PlayerZ;

        /// <summary>Current game time elapsed.</summary>
        public float GameTime;

        /// <summary>Whether stats display is enabled.</summary>
        public bool DisplayEnabled;

        /// <summary>Whether to show extended stats (memory, etc).</summary>
        public bool ShowExtended;

        /// <summary>Running frame count for averaging.</summary>
        public int FrameCount;

        /// <summary>Accumulated frame time for averaging.</summary>
        public float AccumulatedTime;

        /// <summary>Creates default performance stats.</summary>
        public static PerformanceStats CreateDefault()
        {
            return new PerformanceStats
            {
                FPS = 60f,
                SmoothedFPS = 60f,
                FrameTimeMs = 16.67f,
                MinFrameTimeMs = float.MaxValue,
                MaxFrameTimeMs = 0f,
                EntityCount = 0,
                TrafficCount = 0,
                HazardCount = 0,
                SegmentCount = 0,
                PlayerSpeed = 0f,
                PlayerZ = 0f,
                GameTime = 0f,
                DisplayEnabled = false,
                ShowExtended = false,
                FrameCount = 0,
                AccumulatedTime = 0f
            };
        }
    }
}
