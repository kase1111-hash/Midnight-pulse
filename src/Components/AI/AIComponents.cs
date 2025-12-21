// ============================================================================
// Nightflow - Unity DOTS Components: AI (Traffic & Emergency)
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace Nightflow.Components
{
    /// <summary>
    /// AI behavior for regular traffic vehicles.
    /// Uses weighted lane desirability scoring.
    /// </summary>
    public struct TrafficAI : IComponentData
    {
        /// <summary>Preferred cruising speed (m/s).</summary>
        public float PreferredSpeed;

        /// <summary>Aggression factor [0, 1]. Higher = more lane changes, closer following.</summary>
        public float Aggression;

        /// <summary>Reaction time delay (seconds). Time before responding to events.</summary>
        public float ReactionTime;

        /// <summary>Current target lane index.</summary>
        public int TargetLane;

        /// <summary>Timer for lane change commitment lock (seconds remaining).</summary>
        public float LaneChangeLockTimer;

        /// <summary>Whether currently yielding to emergency vehicle.</summary>
        public bool Yielding;
    }

    /// <summary>
    /// Lane score calculation cache for traffic AI.
    /// </summary>
    public struct LaneScoreCache : IComponentData
    {
        /// <summary>Score for left lane.</summary>
        public float LeftScore;

        /// <summary>Score for current lane.</summary>
        public float CurrentScore;

        /// <summary>Score for right lane.</summary>
        public float RightScore;

        /// <summary>Best lane choice (-1 = left, 0 = current, 1 = right).</summary>
        public int BestLane;
    }

    /// <summary>
    /// AI behavior for emergency vehicles (ambulance, police).
    /// Creates pressure on player and traffic to clear lane.
    /// </summary>
    public struct EmergencyAI : IComponentData
    {
        /// <summary>Whether siren/lights are active.</summary>
        public bool SirenActive;

        /// <summary>Overtake bias strength [0, 1]. Higher = more aggressive passing.</summary>
        public float OvertakeBias;

        /// <summary>Target vehicle to overtake (usually player).</summary>
        public Entity TargetVehicle;

        /// <summary>Current urgency level [0, 1]. Increases as distance closes.</summary>
        public float Urgency;

        /// <summary>Time spent pressuring current target (seconds).</summary>
        public float PressureTime;

        /// <summary>Whether in aggressive overtake mode (player ignored too long).</summary>
        public bool AggressiveOvertake;
    }

    /// <summary>
    /// Emergency vehicle detection state for player/traffic.
    /// </summary>
    public struct EmergencyDetection : IComponentData
    {
        /// <summary>Whether an emergency vehicle is detected behind.</summary>
        public bool Detected;

        /// <summary>Entity reference to detected emergency vehicle.</summary>
        public Entity EmergencyVehicle;

        /// <summary>Forward distance to emergency (negative = behind).</summary>
        public float ForwardDistance;

        /// <summary>Lateral offset of emergency vehicle.</summary>
        public float LateralOffset;

        /// <summary>Calculated urgency scalar [0, 1].</summary>
        public float Urgency;

        /// <summary>Avoidance offset to apply to lane magnetism target.</summary>
        public float AvoidanceOffset;
    }
}
