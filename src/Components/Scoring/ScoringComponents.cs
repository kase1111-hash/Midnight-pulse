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
}
