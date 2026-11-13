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
        /// Any hazard in the lane this close (seconds of travel) is dodged,
        /// however minor: a loose tyre or a cone costs ~30% of the impact speed.
        /// Leaves time to wait for a gap after a lane change of up to
        /// HazardPlacement.LaneChangeMaxDuration.
        /// </summary>
        public const float LeaveLaneSeconds = 1.6f;

        /// <summary>
        /// Minimum time between the starts of two lane changes (s). It runs
        /// from the start of a change, not from the request: a request the
        /// steering system turns down (blocked lane) is simply made again the
        /// next frame. It never outlasts the look-ahead, so a hazard found in
        /// the new lane can still be dodged.
        /// </summary>
        public const float LaneChangeCooldown = LeaveLaneSeconds;

        /// <summary>
        /// Below this share of its hold speed the autopilot changes lanes as
        /// quickly as the player: the lane spring barely loads the tyres there.
        /// </summary>
        public const float QuickChangeShare = 0.6f;

        /// <summary>From this share of its hold speed its changes take the full HazardPlacement.LaneChangeMaxDuration.</summary>
        public const float CalmChangeShare = 0.75f;

        /// <summary>How fast the autopilot eases its speed down after a fast handoff (m/s^2).</summary>
        public const float ComfortDecel = 4f;

        /// <summary>
        /// How fast it sheds speed above its hold speed (m/s^2): a run paused
        /// faster than its tyres let it hold. Every second up there is one in
        /// which a dodge in a tight bend could chirp the tyres, and a hazard
        /// hit costs more, so it settles at twice the comfort rate.
        /// </summary>
        public const float SettleDecel = 8f;

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

        /// <summary>
        /// EaseTargetSpeed (with the car's speed) that sheds whatever lies
        /// above <paramref name="holdSpeed"/> at SettleDecel, then eases on
        /// at ComfortDecel; frame-rate independent across the hand-over.
        /// </summary>
        public static float EaseTargetSpeed(float current, float goal, float carSpeed, float holdSpeed, float deltaTime)
        {
            if (!math.isfinite(current) || !(goal < current))
                return EaseTargetSpeed(current, goal, carSpeed, deltaTime);

            current = math.min(current, math.max(carSpeed, goal));
            float dt = math.max(deltaTime, 0f);
            float settleTo = math.max(goal, holdSpeed);
            if (current > settleTo)
            {
                float settleTime = (current - settleTo) / SettleDecel;
                if (dt <= settleTime)
                    return current - SettleDecel * dt;
                dt -= settleTime;
                current = settleTo;
            }
            return math.max(goal, current - ComfortDecel * dt);
        }

        /// <summary>Hazard look-ahead (m): a fixed range or a few seconds of road, whichever is longer.</summary>
        public static float DetectionRange(float speed, float minRange)
        {
            return math.max(minRange, math.max(speed, 0f) * DetectionSeconds);
        }

        /// <summary>
        /// Seconds a lane change the autopilot asks for takes (SteeringSystem
        /// uses this for requested changes), set by how close the car is to
        /// <paramref name="holdSpeed"/>, the most its tyres let it hold. Well
        /// below it, as quick as the player's (HazardPlacement.LaneChangeDuration).
        /// From CalmChangeShare of it, the full HazardPlacement.LaneChangeMaxDuration:
        /// the player's quicker change there asks for more lateral speed than
        /// the tyres give while gripping, the lane spring winds up, and a
        /// damaged car held at ~50 m/s would squeal.
        /// </summary>
        /// <param name="speed">Forward speed (m/s).</param>
        /// <param name="holdSpeed">The car's suspended-run hold speed (GameFlowLogic.SuspendedRunHoldSpeed).</param>
        public static float LaneChangeDuration(float speed, float holdSpeed)
        {
            float v = math.max(speed, 0f);
            float share = v / math.max(holdSpeed, 1f);
            return math.lerp(HazardPlacement.LaneChangeDuration(v), HazardPlacement.LaneChangeMaxDuration,
                math.saturate((share - QuickChangeShare) / (CalmChangeShare - QuickChangeShare)));
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
            return ChooseEscapeDirection(laneThreat, lane, laneCount, EscapeMargin);
        }

        /// <summary>ChooseEscapeDirection with an explicit safety margin.</summary>
        public static int ChooseEscapeDirection(float4 laneThreat, int lane, int laneCount, float margin)
        {
            int lanes = math.min(laneCount, MaxLanes);
            if (lane < 0 || lane >= lanes)
                return 0;

            float own = laneThreat[lane];
            int bestDir = 0;
            float bestThreat = own - margin;
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

        /// <summary>
        /// Which way to change lanes for what is ahead in the driving lane
        /// (-1 left, +1 right, 0 stay): a serious threat anywhere in range, or
        /// any hazard at all the car would reach within LeaveLaneSeconds, for
        /// which any neighbour with under half the threat will do. Minor hazards
        /// are worth the lane change too: each hit costs ~30% of the impact
        /// speed, and attract mode (or a paused run) should look like it is
        /// driving, not bumping over loose tyres.
        /// </summary>
        public static int LaneEscape(float4 laneThreat, int lane, int laneCount,
            float nearestHazardDistance, float speed, float threatThreshold)
        {
            return LaneEscape(laneThreat, lane, laneCount, nearestHazardDistance, speed, threatThreshold, 0);
        }

        /// <summary>
        /// LaneEscape after the steering system turned down
        /// <paramref name="refusedDirection"/> (that lane is blocked, by
        /// traffic for instance, which the threat table does not see): the
        /// other side is tried this time if it is a way out too, so with both
        /// blocked the two are asked for in turn until one opens. 0 = none.
        /// </summary>
        public static int LaneEscape(float4 laneThreat, int lane, int laneCount,
            float nearestHazardDistance, float speed, float threatThreshold, int refusedDirection)
        {
            if (lane < 0 || lane >= MaxLanes)
                return 0;

            float own = laneThreat[lane];
            if (!(own > 0f))
                return 0;

            float margin;
            if (own > threatThreshold)
                margin = EscapeMargin;
            else if (nearestHazardDistance < math.max(speed, 0f) * LeaveLaneSeconds)
                margin = own * 0.5f;
            else
                return 0;

            int escape = ChooseEscapeDirection(laneThreat, lane, laneCount, margin);
            if (escape == 0 || escape != refusedDirection)
                return escape;

            int other = lane - escape;
            int lanes = math.min(laneCount, MaxLanes);
            if (other >= 0 && other < lanes && laneThreat[other] < own - margin)
                return -escape;
            return escape;
        }
    }
}
