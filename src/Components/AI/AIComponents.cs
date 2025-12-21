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
        /// <summary>Target cruising speed (m/s).</summary>
        public float TargetSpeed;

        /// <summary>Timer until next decision evaluation (seconds).</summary>
        public float DecisionTimer;

        /// <summary>Timer for lane change commitment lock (seconds remaining).</summary>
        public float LaneChangeTimer;

        /// <summary>Whether in lane change commitment lock.</summary>
        public bool LaneChangeLock;

        /// <summary>Preferred lane index (for return behavior).</summary>
        public int PreferredLane;

        /// <summary>Whether currently yielding to emergency vehicle.</summary>
        public bool Yielding;
    }

    /// <summary>
    /// Lane score calculation cache for traffic AI.
    /// Stores per-lane scores for debugging and visualization.
    /// </summary>
    public struct LaneScoreCache : IComponentData
    {
        /// <summary>Score for lane 0 (leftmost).</summary>
        public float Lane0;

        /// <summary>Score for lane 1.</summary>
        public float Lane1;

        /// <summary>Score for lane 2.</summary>
        public float Lane2;

        /// <summary>Score for lane 3 (rightmost).</summary>
        public float Lane3;
    }

    /// <summary>
    /// AI behavior for emergency vehicles (ambulance, police).
    /// Creates pressure on player and traffic to clear lane.
    /// </summary>
    public struct EmergencyAI : IComponentData
    {
        /// <summary>Whether siren/lights are active.</summary>
        public bool SirenActive;

        /// <summary>Distance to the nearest vehicle ahead (player or traffic).</summary>
        public float ApproachDistance;

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
        /// <summary>Whether an emergency vehicle is approaching from behind.</summary>
        public bool ApproachingFromBehind;

        /// <summary>Distance to nearest emergency vehicle.</summary>
        public float NearestDistance;

        /// <summary>Lane of the approaching emergency vehicle.</summary>
        public int EmergencyLane;

        /// <summary>Estimated time until emergency arrives (seconds).</summary>
        public float TimeToArrival;

        /// <summary>Whether warning should be displayed to player.</summary>
        public bool WarningActive;

        /// <summary>Calculated urgency scalar [0, 1].</summary>
        public float Urgency;

        /// <summary>Avoidance offset to apply to lane magnetism target.</summary>
        public float AvoidanceOffset;
    }
}
