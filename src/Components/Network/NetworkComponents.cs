// ============================================================================
// Nightflow - Unity DOTS Components: Network Multiplayer System
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace Nightflow.Components
{
    // =========================================================================
    // Network State & Configuration
    // =========================================================================

    /// <summary>
    /// Singleton for global network state.
    /// Manages connection status, tick synchronization, and session info.
    ///
    /// From spec:
    /// - Network replication via input logs + deterministic seeds
    /// - Replicate ECS state deltas across network
    /// </summary>
    public struct NetworkState : IComponentData
    {
        /// <summary>Whether connected to a network session.</summary>
        public bool IsConnected;

        /// <summary>Whether this client is the session host.</summary>
        public bool IsHost;

        /// <summary>Current network tick (60 ticks/second).</summary>
        public uint CurrentTick;

        /// <summary>Last confirmed tick from server.</summary>
        public uint LastConfirmedTick;

        /// <summary>Round-trip time in milliseconds.</summary>
        public float RTT;

        /// <summary>Smoothed RTT for display.</summary>
        public float SmoothedRTT;

        /// <summary>Jitter (RTT variance) in milliseconds.</summary>
        public float Jitter;

        /// <summary>Unique session identifier.</summary>
        public uint SessionId;

        /// <summary>Global seed for deterministic simulation (shared).</summary>
        public uint SessionSeed;

        /// <summary>Local player's network ID.</summary>
        public int LocalPlayerId;

        /// <summary>Number of connected players.</summary>
        public int PlayerCount;

        /// <summary>Maximum players allowed in session.</summary>
        public int MaxPlayers;

        /// <summary>Network mode.</summary>
        public NetworkMode Mode;

        /// <summary>Connection quality (0 = poor, 1 = excellent).</summary>
        public float ConnectionQuality;

        /// <summary>Packets sent per second.</summary>
        public int PacketsSentPerSecond;

        /// <summary>Packets received per second.</summary>
        public int PacketsReceivedPerSecond;

        /// <summary>Packet loss percentage.</summary>
        public float PacketLossPercent;

        public static NetworkState Default => new NetworkState
        {
            IsConnected = false,
            IsHost = false,
            CurrentTick = 0,
            LastConfirmedTick = 0,
            RTT = 0f,
            SmoothedRTT = 0f,
            Jitter = 0f,
            SessionId = 0,
            SessionSeed = 0,
            LocalPlayerId = -1,
            PlayerCount = 0,
            MaxPlayers = 8,
            Mode = NetworkMode.Offline,
            ConnectionQuality = 1f,
            PacketsSentPerSecond = 0,
            PacketsReceivedPerSecond = 0,
            PacketLossPercent = 0f
        };
    }

    /// <summary>
    /// Network operation mode.
    /// </summary>
    public enum NetworkMode : byte
    {
        Offline = 0,        // Single player
        Host = 1,           // Hosting a session
        Client = 2,         // Connected to a host
        Spectator = 3,      // Spectating only
        GhostRace = 4       // Async ghost racing (no live connection)
    }

    // =========================================================================
    // Player Network Identity
    // =========================================================================

    /// <summary>
    /// Network identity for a player entity.
    /// Attached to both local and remote player vehicles.
    /// </summary>
    public struct NetworkPlayer : IComponentData
    {
        /// <summary>Unique network player ID.</summary>
        public int PlayerId;

        /// <summary>Whether this is the local player.</summary>
        public bool IsLocal;

        /// <summary>Whether this player is the session host.</summary>
        public bool IsHost;

        /// <summary>Player display name hash (for quick comparison).</summary>
        public uint DisplayNameHash;

        /// <summary>Last tick we received input from this player.</summary>
        public uint LastInputTick;

        /// <summary>Last tick we sent state to this player.</summary>
        public uint LastSyncTick;

        /// <summary>Player's reported latency.</summary>
        public float Latency;

        /// <summary>Connection state.</summary>
        public PlayerConnectionState ConnectionState;

        /// <summary>Team/color index for identification.</summary>
        public int TeamIndex;

        /// <summary>Whether player is ready to start.</summary>
        public bool IsReady;
    }

    /// <summary>
    /// Player connection state.
    /// </summary>
    public enum PlayerConnectionState : byte
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Loading = 3,
        Ready = 4,
        Playing = 5,
        Spectating = 6
    }

    // =========================================================================
    // Network Input Replication
    // =========================================================================

    /// <summary>
    /// Networked player input with tick synchronization.
    /// Extends PlayerInput with network timing info.
    /// </summary>
    public struct NetworkInput : IComponentData
    {
        /// <summary>Steering input [-1, 1].</summary>
        public float Steer;

        /// <summary>Throttle input [0, 1].</summary>
        public float Throttle;

        /// <summary>Brake input [0, 1].</summary>
        public float Brake;

        /// <summary>Handbrake state.</summary>
        public bool Handbrake;

        /// <summary>Network tick when this input was generated.</summary>
        public uint Tick;

        /// <summary>Sequence number for ordering.</summary>
        public uint SequenceNumber;

        /// <summary>Whether this input has been acknowledged by server.</summary>
        public bool Acknowledged;
    }

    /// <summary>
    /// Network transform for state synchronization.
    /// Used for position correction and interpolation.
    /// </summary>
    public struct NetworkTransform : IComponentData
    {
        /// <summary>Authoritative position from network.</summary>
        public float3 NetworkPosition;

        /// <summary>Authoritative rotation from network.</summary>
        public quaternion NetworkRotation;

        /// <summary>Velocity for prediction.</summary>
        public float3 NetworkVelocity;

        /// <summary>Yaw offset for drift state sync.</summary>
        public float NetworkYawOffset;

        /// <summary>Tick when this state was recorded.</summary>
        public uint StateTick;

        /// <summary>Interpolation progress (0-1).</summary>
        public float InterpolationT;

        /// <summary>Previous position for interpolation.</summary>
        public float3 PreviousPosition;

        /// <summary>Previous rotation for interpolation.</summary>
        public quaternion PreviousRotation;

        /// <summary>Whether correction is needed.</summary>
        public bool NeedsCorrection;

        /// <summary>Position error magnitude (for smoothing).</summary>
        public float PositionError;
    }

    /// <summary>
    /// Input buffer for network prediction and rollback.
    /// Stores unacknowledged inputs for reconciliation.
    /// </summary>
    public struct InputPredictionState : IComponentData
    {
        /// <summary>Oldest unacknowledged input tick.</summary>
        public uint OldestPendingTick;

        /// <summary>Newest input tick sent.</summary>
        public uint NewestSentTick;

        /// <summary>Number of pending inputs.</summary>
        public int PendingInputCount;

        /// <summary>Whether rollback is in progress.</summary>
        public bool RollbackInProgress;

        /// <summary>Tick to rollback to.</summary>
        public uint RollbackTargetTick;
    }

    // =========================================================================
    // Ghost Racing (Async Multiplayer)
    // =========================================================================

    /// <summary>
    /// Ghost race configuration and state.
    /// For async multiplayer ghost racing.
    ///
    /// From spec:
    /// - Ghost racing (async multiplayer)
    /// </summary>
    public struct GhostRaceState : IComponentData
    {
        /// <summary>Whether a ghost race is active.</summary>
        public bool IsRacing;

        /// <summary>Number of ghost opponents.</summary>
        public int GhostCount;

        /// <summary>Current race position (1 = first).</summary>
        public int CurrentPosition;

        /// <summary>Distance ahead of nearest ghost (negative = behind).</summary>
        public float DistanceToNearest;

        /// <summary>Best time for this track (seconds).</summary>
        public float PersonalBest;

        /// <summary>Current run time.</summary>
        public float CurrentTime;

        /// <summary>Track/route identifier.</summary>
        public uint TrackId;

        /// <summary>Difficulty level of ghosts.</summary>
        public GhostDifficulty Difficulty;
    }

    /// <summary>
    /// Ghost difficulty presets.
    /// </summary>
    public enum GhostDifficulty : byte
    {
        PersonalBest = 0,   // Race your own best
        Easy = 1,           // 90th percentile times
        Medium = 2,         // 75th percentile times
        Hard = 3,           // 50th percentile times
        Expert = 4,         // Top 10% times
        WorldRecord = 5     // Current world record
    }

    /// <summary>
    /// Downloaded ghost data header.
    /// Metadata for a ghost run.
    /// </summary>
    public struct GhostRunData : IComponentData
    {
        /// <summary>Unique run identifier.</summary>
        public uint RunId;

        /// <summary>Player ID who recorded this run.</summary>
        public int PlayerId;

        /// <summary>Player name hash.</summary>
        public uint PlayerNameHash;

        /// <summary>Track this run was recorded on.</summary>
        public uint TrackId;

        /// <summary>Session seed used (for determinism).</summary>
        public uint SessionSeed;

        /// <summary>Total run time (seconds).</summary>
        public float TotalTime;

        /// <summary>Distance traveled (meters).</summary>
        public float TotalDistance;

        /// <summary>Maximum speed achieved (m/s).</summary>
        public float MaxSpeed;

        /// <summary>Total score achieved.</summary>
        public int FinalScore;

        /// <summary>Number of inputs in the recording.</summary>
        public int InputCount;

        /// <summary>When this run was recorded (Unix timestamp).</summary>
        public long RecordedTimestamp;

        /// <summary>Leaderboard rank at time of recording.</summary>
        public int LeaderboardRank;
    }

    // =========================================================================
    // Spectator Mode
    // =========================================================================

    /// <summary>
    /// Spectator camera state.
    /// For live spectator mode.
    ///
    /// From spec:
    /// - Live spectator mode
    /// </summary>
    public struct SpectatorState : IComponentData
    {
        /// <summary>Whether spectator mode is active.</summary>
        public bool IsSpectating;

        /// <summary>Entity being spectated.</summary>
        public Entity TargetEntity;

        /// <summary>Target player ID.</summary>
        public int TargetPlayerId;

        /// <summary>Spectator camera mode.</summary>
        public SpectatorCameraMode CameraMode;

        /// <summary>Free camera position (for free cam mode).</summary>
        public float3 FreeCamPosition;

        /// <summary>Free camera rotation.</summary>
        public quaternion FreeCamRotation;

        /// <summary>Free camera movement speed.</summary>
        public float FreeCamSpeed;

        /// <summary>Auto-switch delay (seconds, 0 = manual only).</summary>
        public float AutoSwitchDelay;

        /// <summary>Time since last target switch.</summary>
        public float TimeSinceSwitch;

        /// <summary>Whether to follow action (auto-switch to interesting events).</summary>
        public bool FollowAction;
    }

    /// <summary>
    /// Spectator camera modes.
    /// </summary>
    public enum SpectatorCameraMode : byte
    {
        FollowTarget = 0,       // Standard follow cam on target
        Cinematic = 1,          // Smooth cinematic angles
        Overhead = 2,           // Bird's eye view
        Trackside = 3,          // Fixed trackside cameras
        FreeCam = 4,            // Free-flying camera
        FirstPerson = 5,        // Target vehicle cockpit
        Chase = 6               // Chase cam behind target
    }

    // =========================================================================
    // Leaderboard Integration
    // =========================================================================

    /// <summary>
    /// Leaderboard state and configuration.
    ///
    /// From spec:
    /// - Leaderboard integration
    /// </summary>
    public struct LeaderboardState : IComponentData
    {
        /// <summary>Whether leaderboard is available.</summary>
        public bool IsAvailable;

        /// <summary>Whether currently fetching data.</summary>
        public bool IsFetching;

        /// <summary>Last fetch time (seconds since start).</summary>
        public float LastFetchTime;

        /// <summary>Current leaderboard type.</summary>
        public LeaderboardType CurrentType;

        /// <summary>Current time filter.</summary>
        public LeaderboardTimeFilter TimeFilter;

        /// <summary>Local player's current rank.</summary>
        public int LocalPlayerRank;

        /// <summary>Local player's best score.</summary>
        public int LocalPlayerBestScore;

        /// <summary>Total entries on current leaderboard.</summary>
        public int TotalEntries;

        /// <summary>Current page being viewed.</summary>
        public int CurrentPage;

        /// <summary>Entries per page.</summary>
        public int PageSize;
    }

    /// <summary>
    /// Leaderboard entry data.
    /// </summary>
    public struct LeaderboardEntry : IComponentData
    {
        /// <summary>Global rank.</summary>
        public int Rank;

        /// <summary>Player ID.</summary>
        public int PlayerId;

        /// <summary>Player name hash.</summary>
        public uint PlayerNameHash;

        /// <summary>Score value.</summary>
        public int Score;

        /// <summary>Best time (seconds).</summary>
        public float BestTime;

        /// <summary>Max speed achieved (m/s).</summary>
        public float MaxSpeed;

        /// <summary>Total distance (meters).</summary>
        public float TotalDistance;

        /// <summary>When this score was set (Unix timestamp).</summary>
        public long Timestamp;

        /// <summary>Country/region code.</summary>
        public uint RegionCode;
    }

    /// <summary>
    /// Leaderboard type categories.
    /// </summary>
    public enum LeaderboardType : byte
    {
        HighScore = 0,          // Overall high scores
        BestTime = 1,           // Fastest runs
        LongestRun = 2,         // Longest distance
        MaxSpeed = 3,           // Highest speed achieved
        TotalDistance = 4,      // Cumulative distance
        Weekly = 5,             // Weekly challenge
        Friends = 6             // Friends only
    }

    /// <summary>
    /// Leaderboard time filters.
    /// </summary>
    public enum LeaderboardTimeFilter : byte
    {
        AllTime = 0,
        Today = 1,
        ThisWeek = 2,
        ThisMonth = 3,
        ThisSeason = 4
    }

    // =========================================================================
    // Network Events
    // =========================================================================

    /// <summary>
    /// Pending network event for processing.
    /// </summary>
    public struct NetworkEvent : IComponentData
    {
        /// <summary>Event type.</summary>
        public NetworkEventType Type;

        /// <summary>Player ID associated with event.</summary>
        public int PlayerId;

        /// <summary>Event data (context-dependent).</summary>
        public int Data;

        /// <summary>Tick when event occurred.</summary>
        public uint Tick;

        /// <summary>Whether event has been processed.</summary>
        public bool Processed;
    }

    /// <summary>
    /// Network event types.
    /// </summary>
    public enum NetworkEventType : byte
    {
        None = 0,
        PlayerJoined = 1,
        PlayerLeft = 2,
        PlayerReady = 3,
        GameStart = 4,
        GameEnd = 5,
        PlayerCrashed = 6,
        PlayerFinished = 7,
        PositionUpdate = 8,
        ChatMessage = 9,
        Ping = 10,
        StateSync = 11
    }
}

namespace Nightflow.Buffers
{
    using Nightflow.Components;

    /// <summary>
    /// Network input entry for replication buffer.
    /// Extended InputLogEntry with network tick info.
    /// </summary>
    [InternalBufferCapacity(128)]
    public struct NetworkInputEntry : IBufferElementData
    {
        /// <summary>Network tick for this input.</summary>
        public uint Tick;

        /// <summary>Steering input.</summary>
        public float Steer;

        /// <summary>Throttle input.</summary>
        public float Throttle;

        /// <summary>Brake input.</summary>
        public float Brake;

        /// <summary>Handbrake state.</summary>
        public bool Handbrake;

        /// <summary>Whether acknowledged by server.</summary>
        public bool Acknowledged;
    }

    /// <summary>
    /// Leaderboard entry buffer for displaying rankings.
    /// </summary>
    [InternalBufferCapacity(50)]
    public struct LeaderboardEntryBuffer : IBufferElementData
    {
        /// <summary>Rank on leaderboard.</summary>
        public int Rank;

        /// <summary>Player name hash.</summary>
        public uint PlayerNameHash;

        /// <summary>Score value.</summary>
        public int Score;

        /// <summary>Best time (seconds).</summary>
        public float BestTime;

        /// <summary>Whether this is the local player.</summary>
        public bool IsLocalPlayer;
    }

    /// <summary>
    /// Ghost run reference for ghost race mode.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct GhostRunReference : IBufferElementData
    {
        /// <summary>Ghost entity.</summary>
        public Entity GhostEntity;

        /// <summary>Run ID.</summary>
        public uint RunId;

        /// <summary>Player name hash.</summary>
        public uint PlayerNameHash;

        /// <summary>Target time (seconds).</summary>
        public float TargetTime;

        /// <summary>Current distance.</summary>
        public float CurrentDistance;
    }
}

namespace Nightflow.Tags
{
    using Unity.Entities;

    /// <summary>Tag for remote player vehicles.</summary>
    public struct RemotePlayerTag : IComponentData { }

    /// <summary>Tag for ghost race vehicles.</summary>
    public struct GhostRaceTag : IComponentData { }

    /// <summary>Tag for spectator camera entity.</summary>
    public struct SpectatorCameraTag : IComponentData { }

    /// <summary>Tag for network synchronized entities.</summary>
    public struct NetworkSyncTag : IComponentData { }
}
