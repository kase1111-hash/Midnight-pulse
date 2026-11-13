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

        /// <summary>How fast the autopilot eases its speed down after a fast handoff (m/s^2).</summary>
        public const float ComfortDecel = 4f;

        /// <summary>How fast the autopilot eases its speed up (m/s^2).</summary>
        public const float ComfortAccel = 2.5f;

        /// <summary>Hazards are watched at least this many seconds of road ahead.</summary>
        public const float DetectionSeconds = 2.5f;

        /// <summary>Speed error inside which the autopilot leaves the pedals alone (m/s).</summary>
        public const float SpeedDeadband = 0.5f;

        /// <summary>Throttle per m/s of speed deficit.</summary>
        public const float ThrottleGain = 0.5f;

        /// <summary>Brake per m/s of speed excess.</summary>
        public const float BrakeGain = 0.08f;

        /// <summary>
        /// Moves the autopilot's working speed toward its goal at comfortable
        /// rates, so taking the wheel from a player at 300 km/h eases down
        /// instead of standing on the brakes.
        /// </summary>
        public static float EaseTargetSpeed(float current, float goal, float deltaTime)
        {
            if (!math.isfinite(current))
                return goal;

            float dt = math.max(deltaTime, 0f);
            return goal < current
                ? math.max(goal, current - ComfortDecel * dt)
                : math.min(goal, current + ComfortAccel * dt);
        }

        /// <summary>
        /// EaseTargetSpeed for a car that may already have been slowed by
        /// something else (the in-place reset on RESTART, a knock): while easing
        /// down, the working target never sits above the car, or SpeedControl
        /// would floor the throttle to chase a speed the car no longer has.
        /// </summary>
        public static float EaseTargetSpeed(float current, float goal, float carSpeed, float deltaTime)
        {
            if (math.isfinite(current) && goal < current)
            {
                current = math.min(current, math.max(carSpeed, goal));
            }
            return EaseTargetSpeed(current, goal, deltaTime);
        }

        /// <summary>Hazard look-ahead (m): a fixed range or a few seconds of road, whichever is longer.</summary>
        public static float DetectionRange(float speed, float minRange)
        {
            return math.max(minRange, math.max(speed, 0f) * DetectionSeconds);
        }

        /// <summary>
        /// Pedals that track a target speed. Nothing slows the car on its own
        /// (no drag), so inside the deadband the autopilot simply coasts.
        /// </summary>
        public static void SpeedControl(float speed, float target, out float throttle, out float brake)
        {
            float error = target - speed;
            throttle = error > SpeedDeadband ? math.saturate(error * ThrottleGain) : 0f;
            brake = error < -SpeedDeadband ? math.saturate(-error * BrakeGain) : 0f;
        }

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
