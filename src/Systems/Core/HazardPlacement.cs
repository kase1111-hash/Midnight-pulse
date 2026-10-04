// ============================================================================
// Nightflow - Hazard Placement
// Pure, Burst-friendly fairness rule for hazard lanes, shared by
// HazardSpawnSystem and the EditMode tests. No ECS access in here.
//
// Hazards are static, so the only way to dodge one is a lane change, and a
// lane change at speed eats road: up to ~80 m at 80 m/s. Any stretch of road
// that long is therefore treated as one "row". Within a row:
//   - at least two lanes stay free, and
//   - every blocked lane has a free neighbour (no {0,1} or {2,3} pairs that
//     would force a double lane change inside a single row).
// Brute-force path search over generated hazard streams shows this removes
// every unavoidable wall at all difficulty levels without thinning hazards.
// ============================================================================

using Unity.Mathematics;

namespace Nightflow.Systems
{
    public static class HazardPlacement
    {
        /// <summary>Extra road on top of the lane-change distance (hazard + car length).</summary>
        public const float WindowMargin = 10f;

        // Must match SteeringSystem's speed-aware lane-change duration
        public const float LaneChangeBaseDuration = 0.6f;
        public const float LaneChangeMinDuration = 0.45f;
        public const float LaneChangeMaxDuration = 1.0f;
        public const float LaneChangeReferenceSpeed = 40f;

        /// <summary>Seconds a player lane change takes at this speed.</summary>
        public static float LaneChangeDuration(float speed)
        {
            return math.clamp(LaneChangeBaseDuration * (speed / LaneChangeReferenceSpeed),
                LaneChangeMinDuration, LaneChangeMaxDuration);
        }

        /// <summary>Length of road (m) treated as one row at this speed.</summary>
        public static float SafetyWindow(float speed)
        {
            float v = speed > 0f ? speed : 0f;
            return v * LaneChangeDuration(v) + WindowMargin;
        }

        /// <summary>
        /// True if a row with these blocked lanes (bit per lane) can still be
        /// driven through.
        /// </summary>
        public static bool IsPassable(int blockedMask, int numLanes)
        {
            int blockedCount = 0;

            for (int lane = 0; lane < numLanes; lane++)
            {
                if (!HasBit(blockedMask, lane))
                    continue;

                blockedCount++;

                bool leftFree = lane > 0 && !HasBit(blockedMask, lane - 1);
                bool rightFree = lane < numLanes - 1 && !HasBit(blockedMask, lane + 1);
                if (!leftFree && !rightFree)
                    return false;
            }

            return blockedCount <= numLanes - 2;
        }

        /// <summary>Lanes where a new hazard keeps the row passable (bit per lane).</summary>
        public static int AllowedLanes(int blockedMask, int numLanes)
        {
            int allowed = 0;
            for (int lane = 0; lane < numLanes; lane++)
            {
                if (IsPassable(blockedMask | (1 << lane), numLanes))
                    allowed |= 1 << lane;
            }
            return allowed;
        }

        /// <summary>
        /// Picks the roll-th allowed lane (roll in [0, 1)), or -1 if none.
        /// Uniform over allowed lanes so hazard layouts stay varied.
        /// </summary>
        public static int PickLane(int allowedMask, int numLanes, float roll)
        {
            int count = math.countbits(allowedMask);
            if (count == 0)
                return -1;

            int target = math.min((int)(roll * count), count - 1);
            for (int lane = 0; lane < numLanes; lane++)
            {
                if (!HasBit(allowedMask, lane))
                    continue;
                if (target == 0)
                    return lane;
                target--;
            }

            return -1;
        }

        private static bool HasBit(int mask, int lane)
        {
            return (mask & (1 << lane)) != 0;
        }
    }
}
