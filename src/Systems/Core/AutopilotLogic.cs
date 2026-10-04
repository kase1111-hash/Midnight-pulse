// ============================================================================
// Nightflow - Autopilot Logic
// Pure, Burst-friendly hazard reading for AutopilotSystem and the EditMode
// tests. No ECS access in here.
//
// The road curves away from world x, so "is it in my lane" is answered with
// lane indices (Hazard.Lane, LaneFollower lanes), never with world x. Distance
// ahead uses world z: the track generator keeps the road heading within ~25
// degrees of +Z, so z under-reads true road distance by at most ~10%.
// ============================================================================

using Unity.Mathematics;

namespace Nightflow.Systems
{
    public static class AutopilotLogic
    {
        /// <summary>Lanes tracked per frame (GameConstants.DefaultNumLanes).</summary>
        public const int MaxLanes = 4;

        /// <summary>An escape lane must be at least this much safer than staying.</summary>
        public const float EscapeMargin = 0.1f;

        /// <summary>
        /// Threat of one hazard: severity scaled by how close it is, 0 outside
        /// (0, range).
        /// </summary>
        public static float HazardThreat(float severity, float forwardDistance, float range)
        {
            if (!(forwardDistance > 0f) || !(forwardDistance < range))
                return 0f;

            return severity * (1f - forwardDistance / range);
        }

        /// <summary>
        /// The lane the car is effectively in: mid lane change it is already
        /// committed to the target lane, so that is the one to watch.
        /// </summary>
        public static int DrivingLane(int currentLane, int targetLane, bool changingLanes)
        {
            return changingLanes ? targetLane : currentLane;
        }

        /// <summary>Folds one hazard into the per-lane worst-threat table.</summary>
        public static float4 AccumulateThreat(float4 laneThreat, int lane, float threat)
        {
            if (lane >= 0 && lane < MaxLanes && threat > laneThreat[lane])
                laneThreat[lane] = threat;
            return laneThreat;
        }

        /// <summary>
        /// Which way to change lanes to get away from a threat in this lane:
        /// -1 left, +1 right, 0 stay (no neighbour is meaningfully safer, so
        /// slow down instead). Ties go toward the middle of the road, where
        /// there are more ways out next time, then left.
        /// </summary>
        public static int ChooseEscapeDirection(float4 laneThreat, int lane, int laneCount)
        {
            int lanes = math.min(laneCount, MaxLanes);
            if (lane < 0 || lane >= lanes)
                return 0;

            float own = laneThreat[lane];
            int bestDir = 0;
            float bestThreat = own - EscapeMargin;
            float bestCentreDist = float.MaxValue;
            float centre = (lanes - 1) * 0.5f;

            for (int dir = -1; dir <= 1; dir += 2)
            {
                int candidate = lane + dir;
                if (candidate < 0 || candidate >= lanes)
                    continue;

                float threat = laneThreat[candidate];
                float centreDist = math.abs(candidate - centre);

                bool safer = threat < bestThreat;
                bool tieButMoreCentral = bestDir != 0 && threat == bestThreat && centreDist < bestCentreDist;

                if (safer || tieButMoreCentral)
                {
                    bestDir = dir;
                    bestThreat = threat;
                    bestCentreDist = centreDist;
                }
            }

            return bestDir;
        }
    }
}
