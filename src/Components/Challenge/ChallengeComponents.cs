// ============================================================================
// Nightflow - Unity DOTS Components: Daily Challenges
// Tracks rotating daily/weekly challenges with progress and rewards
// ============================================================================

using Unity.Entities;
using Unity.Collections;

namespace Nightflow.Components
{
    /// <summary>
    /// Types of challenges that can be assigned.
    /// </summary>
    public enum ChallengeType : byte
    {
        /// <summary>Reach a specific score threshold.</summary>
        ReachScore = 0,

        /// <summary>Survive for a minimum time.</summary>
        SurviveTime = 1,

        /// <summary>Travel a minimum distance.</summary>
        TravelDistance = 2,

        /// <summary>Perform a number of close passes.</summary>
        ClosePasses = 3,

        /// <summary>Dodge a number of hazards.</summary>
        DodgeHazards = 4,

        /// <summary>Reach a multiplier threshold.</summary>
        ReachMultiplier = 5,

        /// <summary>Complete perfect segments (no damage).</summary>
        PerfectSegments = 6,

        /// <summary>Perform lane weave maneuvers.</summary>
        LaneWeaves = 7,

        /// <summary>Thread the needle between hazards.</summary>
        ThreadNeedle = 8,

        /// <summary>Build a combo chain.</summary>
        ComboChain = 9,

        /// <summary>Reach a minimum speed.</summary>
        ReachSpeed = 10,

        /// <summary>Complete runs without braking.</summary>
        NoBrakeRun = 11
    }

    /// <summary>
    /// Difficulty tiers for challenges.
    /// </summary>
    public enum ChallengeDifficulty : byte
    {
        /// <summary>Easy challenge, smaller reward.</summary>
        Bronze = 0,

        /// <summary>Medium challenge, moderate reward.</summary>
        Silver = 1,

        /// <summary>Hard challenge, large reward.</summary>
        Gold = 2
    }

    /// <summary>
    /// Single challenge definition and progress.
    /// </summary>
    public struct Challenge : IComponentData
    {
        /// <summary>Unique ID for this challenge instance.</summary>
        public int ChallengeId;

        /// <summary>Type of challenge objective.</summary>
        public ChallengeType Type;

        /// <summary>Difficulty tier affecting rewards.</summary>
        public ChallengeDifficulty Difficulty;

        /// <summary>Target value to reach (score, distance, count, etc.).</summary>
        public float TargetValue;

        /// <summary>Current progress toward target.</summary>
        public float CurrentProgress;

        /// <summary>Whether this challenge is completed.</summary>
        public bool Completed;

        /// <summary>Whether the reward has been claimed.</summary>
        public bool RewardClaimed;

        /// <summary>Score bonus reward for completion.</summary>
        public int ScoreReward;

        /// <summary>Expiration timestamp (Unix seconds).</summary>
        public long ExpiresAt;

        /// <summary>Whether this is a weekly (vs daily) challenge.</summary>
        public bool IsWeekly;

        /// <summary>Progress ratio [0, 1].</summary>
        public float ProgressRatio => TargetValue > 0 ? CurrentProgress / TargetValue : 0f;

        /// <summary>
        /// Check if challenge has expired.
        /// </summary>
        public bool IsExpired(long currentTimestamp) => currentTimestamp >= ExpiresAt;
    }

    /// <summary>
    /// Buffer element for storing multiple challenges.
    /// </summary>
    public struct ChallengeBuffer : IBufferElementData
    {
        public Challenge Value;
    }

    /// <summary>
    /// Singleton managing daily challenge state.
    /// </summary>
    public struct DailyChallengeState : IComponentData
    {
        /// <summary>Current day's seed for deterministic generation.</summary>
        public uint DaySeed;

        /// <summary>Last day challenges were generated (days since epoch).</summary>
        public int LastGeneratedDay;

        /// <summary>Total challenges completed lifetime.</summary>
        public int TotalCompleted;

        /// <summary>Current streak of consecutive days with completions.</summary>
        public int CurrentStreak;

        /// <summary>Best streak ever achieved.</summary>
        public int BestStreak;

        /// <summary>Last day a challenge was completed (for streak tracking).</summary>
        public int LastCompletionDay;

        /// <summary>Total bonus score earned from challenges.</summary>
        public long TotalBonusEarned;

        /// <summary>Number of active challenges available.</summary>
        public int ActiveChallengeCount;

        /// <summary>
        /// Get current day number since Unix epoch.
        /// </summary>
        public static int GetCurrentDay(long unixTimestamp) =>
            (int)(unixTimestamp / 86400);

        /// <summary>
        /// Generate seed for a specific day.
        /// </summary>
        public static uint GetDaySeed(int dayNumber) =>
            (uint)(dayNumber * 2654435761); // Knuth multiplicative hash
    }

    /// <summary>
    /// Tag for the challenge manager entity.
    /// </summary>
    public struct ChallengeManagerTag : IComponentData { }

    /// <summary>
    /// Event raised when a challenge is completed.
    /// </summary>
    public struct ChallengeCompletedEvent : IComponentData
    {
        public int ChallengeId;
        public ChallengeType Type;
        public ChallengeDifficulty Difficulty;
        public int ScoreReward;
        public bool IsWeekly;
    }

    /// <summary>
    /// Pending challenge progress update from gameplay.
    /// Applied at end of run or on specific events.
    /// </summary>
    public struct ChallengeProgressUpdate : IComponentData
    {
        // Accumulated values from current run
        public float Score;
        public float TimeSurvived;
        public float Distance;
        public int ClosePasses;
        public int HazardsDodged;
        public float HighestMultiplier;
        public int PerfectSegments;
        public int LaneWeaves;
        public int Threadings;
        public int HighestCombo;
        public float HighestSpeed;
        public bool UsedBrake;

        /// <summary>
        /// Reset all progress values.
        /// </summary>
        public void Reset()
        {
            Score = 0;
            TimeSurvived = 0;
            Distance = 0;
            ClosePasses = 0;
            HazardsDodged = 0;
            HighestMultiplier = 0;
            PerfectSegments = 0;
            LaneWeaves = 0;
            Threadings = 0;
            HighestCombo = 0;
            HighestSpeed = 0;
            UsedBrake = false;
        }
    }
}

namespace Nightflow.Tags
{
    /// <summary>
    /// Tag for entities tracking challenge progress.
    /// </summary>
    public struct ChallengeTrackerTag : IComponentData { }
}
