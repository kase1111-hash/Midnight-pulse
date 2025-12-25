// ============================================================================
// Nightflow - Unity DOTS Components: Scoring
// ============================================================================

using Unity.Entities;

namespace Nightflow.Components
{
    /// <summary>
    /// Active scoring session state.
    /// One per player vehicle.
    /// </summary>
    public struct ScoreSession : IComponentData
    {
        /// <summary>Total distance traveled this run (m).</summary>
        public float Distance;

        /// <summary>Base multiplier from speed tier.</summary>
        public float Multiplier;

        /// <summary>Temporary risk multiplier from close calls, dodges, etc.</summary>
        public float RiskMultiplier;

        /// <summary>Whether scoring is currently active.</summary>
        public bool Active;

        /// <summary>Current total score.</summary>
        public float Score;

        /// <summary>Highest combo/multiplier reached this run.</summary>
        public float HighestMultiplier;

        /// <summary>Internal accumulator for persistence calibration.</summary>
        public double _k0;
    }

    /// <summary>
    /// Risk multiplier dynamics.
    /// </summary>
    public struct RiskState : IComponentData
    {
        /// <summary>Current risk multiplier value.</summary>
        public float Value;

        /// <summary>Maximum risk multiplier cap (reduced by damage).</summary>
        public float Cap;

        /// <summary>Rebuild rate (affected by damage).</summary>
        public float RebuildRate;

        /// <summary>Cooldown after braking (2s delay).</summary>
        public float BrakeCooldown;

        /// <summary>Whether brake penalty is active.</summary>
        public bool BrakePenaltyActive;
    }

    /// <summary>
    /// Risk event for scoring spikes.
    /// </summary>
    public struct RiskEvent : IComponentData
    {
        /// <summary>Type of risk event.</summary>
        public RiskEventType Type;

        /// <summary>Multiplier boost from this event.</summary>
        public float Boost;

        /// <summary>Time remaining for this event's effect.</summary>
        public float TimeRemaining;
    }

    /// <summary>
    /// Types of risk events that spike the multiplier.
    /// </summary>
    public enum RiskEventType : byte
    {
        None = 0,
        ClosePass = 1,
        HazardDodge = 2,
        EmergencyClear = 3,
        DriftRecovery = 4,
        PerfectSegment = 5,
        FullSpin = 6
    }

    /// <summary>
    /// Final score data for summary screen.
    /// </summary>
    public struct ScoreSummary : IComponentData
    {
        /// <summary>Final score value.</summary>
        public float FinalScore;

        /// <summary>Total distance traveled (m).</summary>
        public float TotalDistance;

        /// <summary>Highest speed reached (m/s).</summary>
        public float HighestSpeed;

        /// <summary>Number of close passes.</summary>
        public int ClosePasses;

        /// <summary>Number of hazards dodged.</summary>
        public int HazardsDodged;

        /// <summary>Number of drift recoveries.</summary>
        public int DriftRecoveries;

        /// <summary>Number of perfect segments (no damage).</summary>
        public int PerfectSegments;

        /// <summary>Time survived (seconds).</summary>
        public float TimeSurvived;

        /// <summary>Reason for run ending.</summary>
        public CrashReason EndReason;
    }

    /// <summary>
    /// Adaptive difficulty profile tracking player skill and performance.
    /// Used to dynamically adjust hazard density, traffic, and challenge level.
    /// </summary>
    public struct DifficultyProfile : IComponentData
    {
        /// <summary>
        /// Player skill rating [0, 1]. Derived from performance metrics.
        /// 0 = struggling, 0.5 = average, 1 = highly skilled.
        /// </summary>
        public float SkillRating;

        /// <summary>
        /// Current difficulty modifier applied to spawn rates.
        /// 1.0 = normal, &lt;1.0 = easier, &gt;1.0 = harder.
        /// Smoothly interpolates toward target based on performance.
        /// </summary>
        public float DifficultyModifier;

        /// <summary>
        /// Target difficulty modifier based on recent performance.
        /// DifficultyModifier lerps toward this value.
        /// </summary>
        public float TargetDifficulty;

        /// <summary>
        /// Rolling average of multiplier values (skill indicator).
        /// Higher average = better sustained performance.
        /// </summary>
        public float AverageMultiplier;

        /// <summary>
        /// Rolling average of survival time per run (seconds).
        /// Longer survival = better skill.
        /// </summary>
        public float AverageSurvivalTime;

        /// <summary>
        /// Recent crash rate (crashes per minute of play).
        /// Lower = better performance.
        /// </summary>
        public float CrashRate;

        /// <summary>
        /// Number of consecutive runs where player reached high multiplier.
        /// Used to detect skill improvement streaks.
        /// </summary>
        public int HighPerformanceStreak;

        /// <summary>
        /// Number of consecutive runs where player crashed early.
        /// Used to detect struggling patterns.
        /// </summary>
        public int StrugglingStreak;

        /// <summary>
        /// Time since last difficulty adjustment (seconds).
        /// Prevents rapid oscillation.
        /// </summary>
        public float AdjustmentCooldown;

        /// <summary>
        /// Total time played this session (seconds).
        /// Used for warm-up period detection.
        /// </summary>
        public float SessionPlayTime;

        /// <summary>
        /// Number of runs completed this session.
        /// </summary>
        public int RunsCompleted;

        /// <summary>
        /// Best distance achieved this session (meters).
        /// </summary>
        public float SessionBestDistance;

        /// <summary>
        /// Hazard avoidance success rate [0, 1].
        /// Ratio of hazards dodged to hazards encountered.
        /// </summary>
        public float HazardAvoidanceRate;

        /// <summary>
        /// Creates a default difficulty profile for new players.
        /// </summary>
        public static DifficultyProfile CreateDefault()
        {
            return new DifficultyProfile
            {
                SkillRating = 0.5f,
                DifficultyModifier = 1.0f,
                TargetDifficulty = 1.0f,
                AverageMultiplier = 1.0f,
                AverageSurvivalTime = 60f,
                CrashRate = 0f,
                HighPerformanceStreak = 0,
                StrugglingStreak = 0,
                AdjustmentCooldown = 0f,
                SessionPlayTime = 0f,
                RunsCompleted = 0,
                SessionBestDistance = 0f,
                HazardAvoidanceRate = 0.5f
            };
        }
    }
}
