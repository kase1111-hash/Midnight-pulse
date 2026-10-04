// ============================================================================
// Nightflow - Difficulty Curve
// Pure, Burst-friendly in-run difficulty ramp shared by the spawn systems,
// vehicle movement and the EditMode tests. No ECS access in here.
//
// One run = one continuous ramp (no discrete levels, see spec 08):
//
//   progress  = 0.5 * distance / 10 km + 0.5 * time / 5 min
//   intensity = 0 -> 1 over progress 0..1 (eases in, still climbing at 1)
//               then 1 -> 1 + OverdriveHeadroom asymptotically
//
// Every knob is lerp(start, full, intensity), so it reaches the spec'd "full
// difficulty" at intensity 1 and keeps tightening past it toward a cap. The
// curve is C1 with a positive slope everywhere after the start: no steps, no
// kink and no plateau where the ramp hands over to the overdrive.
// ============================================================================

using Unity.Mathematics;

namespace Nightflow.Systems
{
    /// <summary>
    /// Every difficulty knob for the current point of a run.
    /// </summary>
    public struct DifficultyLevels
    {
        /// <summary>Normalized run progress (1 = spec "full difficulty", unbounded).</summary>
        public float Progress;

        /// <summary>0 at run start, 1 at full difficulty, approaches 1 + OverdriveHeadroom.</summary>
        public float Intensity;

        /// <summary>Multiplier on target traffic count.</summary>
        public float TrafficDensityScale;

        /// <summary>m/s added to spawned traffic speed.</summary>
        public float TrafficSpeedBonus;

        /// <summary>Multiplier on hazard spawn rate (hazards per meter).</summary>
        public float HazardRateScale;

        /// <summary>Multiplier on the chance a hazard is lethal (barrier/crashed car).</summary>
        public float LethalScale;

        /// <summary>Multiplier on emergency vehicle frequency (interval = base / scale).</summary>
        public float EmergencyFrequencyScale;

        /// <summary>Speed the car eases up to when the player is off the pedals (m/s).</summary>
        public float BaseCruiseSpeed;
    }

    public static class DifficultyCurve
    {
        // Spec 10-parameters "Difficulty Scaling": full difficulty at 10 km / 5 min
        public const float FullDifficultyDistance = 10000f;   // m
        public const float FullDifficultyTime = 300f;         // s

        /// <summary>Slope of intensity at full difficulty (lower = longer overdrive tail).</summary>
        public const float HandoverSlope = 0.35f;

        /// <summary>How far past "full" the overdrive can climb (1.5x the full ramp).</summary>
        public const float OverdriveHeadroom = 0.5f;

        /// <summary>Upper bound of Intensity for very long runs.</summary>
        public const float MaxIntensity = 1f + OverdriveHeadroom;

        // start -> full at intensity 1; cap = start + (1 + headroom) * (full - start)
        public const float TrafficDensityStart = 1f;
        public const float TrafficDensityFull = 2f;           // spec: max traffic 2x  (cap 2.5x)

        public const float TrafficSpeedBonusFull = 30f / 3.6f; // spec: +30 km/h      (cap +45 km/h)

        public const float HazardRateStart = 1f;
        public const float HazardRateFull = 2.5f;             // spec: max hazard 2.5x (cap 3.25x)

        public const float LethalStart = 0.6f;                // gentler opening
        public const float LethalFull = 1f;                   //                       (cap 1.2x)

        public const float EmergencyStart = 1f;
        public const float EmergencyFull = 3f;                // spec: max emergency 3x (cap 4x)

        // spec 08: "Base Speed increases over time"
        public const float BaseCruiseStart = 25f;             // m/s (~90 km/h, autopilot cruise)
        public const float BaseCruiseFull = 50f;              // m/s (Boosted tier threshold, cap 62.5)

        /// <summary>How hard the car is pulled up to the base cruise speed (m/s²).</summary>
        public const float CruiseAssistAcceleration = 2.5f;

        /// <summary>Pedal input below this counts as "off the pedals".</summary>
        public const float PedalDeadzone = 0.05f;

        /// <summary>Traffic never exceeds this fraction of the player's top speed.</summary>
        public const float MaxTrafficSpeedRatio = 0.65f;

        /// <summary>
        /// Normalized run progress from distance and time survived.
        /// Fast drivers ramp mostly by distance, slow ones by time.
        /// </summary>
        public static float Progress(float distance, float timeSurvived)
        {
            // Comparisons against NaN are false, so NaN/negative inputs read as 0
            float d = (distance > 0f ? distance : 0f) / FullDifficultyDistance;
            float t = (timeSurvived > 0f ? timeSurvived : 0f) / FullDifficultyTime;
            return 0.5f * d + 0.5f * t;
        }

        /// <summary>
        /// Difficulty intensity for a progress value. Up to 1 it blends a
        /// smoothstep (gentle start) with an ease-in cubic so it is still
        /// climbing at HandoverSlope when it reaches full difficulty; past 1 an
        /// exponential tail continues with that same slope toward 1 + headroom.
        /// </summary>
        public static float Intensity(float progress)
        {
            float p = progress > 0f ? progress : 0f;

            if (p <= 1f)
            {
                float smooth = p * p * (3f - 2f * p);   // slope 0 at both ends
                float easeIn = p * p * (2f - p);        // slope 0 at 0, 1 at 1
                return math.lerp(smooth, easeIn, HandoverSlope);
            }

            return 1f + OverdriveHeadroom * (1f - math.exp(-HandoverSlope * (p - 1f) / OverdriveHeadroom));
        }

        /// <summary>Knob value: start at intensity 0, full at 1, extrapolated past it.</summary>
        public static float Knob(float start, float full, float intensity)
        {
            return start + (full - start) * intensity;
        }

        public static DifficultyLevels Evaluate(float distance, float timeSurvived)
        {
            float progress = Progress(distance, timeSurvived);
            float i = Intensity(progress);

            return new DifficultyLevels
            {
                Progress = progress,
                Intensity = i,
                TrafficDensityScale = Knob(TrafficDensityStart, TrafficDensityFull, i),
                TrafficSpeedBonus = Knob(0f, TrafficSpeedBonusFull, i),
                HazardRateScale = Knob(HazardRateStart, HazardRateFull, i),
                LethalScale = Knob(LethalStart, LethalFull, i),
                EmergencyFrequencyScale = Knob(EmergencyStart, EmergencyFull, i),
                BaseCruiseSpeed = Knob(BaseCruiseStart, BaseCruiseFull, i)
            };
        }

        /// <summary>
        /// Gently pulls the car up to the base cruise speed while the player is
        /// off both pedals, like Tetris gravity: the run keeps getting faster on
        /// its own, but throttle still goes higher and braking still goes lower.
        /// </summary>
        public static float ApplyCruiseAssist(float speed, float baseCruiseSpeed,
            float throttle, float brake, float deltaTime)
        {
            if (throttle > PedalDeadzone || brake > PedalDeadzone || speed >= baseCruiseSpeed)
                return speed;

            return math.min(baseCruiseSpeed, speed + CruiseAssistAcceleration * deltaTime);
        }

        /// <summary>
        /// Keeps traffic slower than the player can drive so the top end never
        /// turns into an empty road (traffic outrunning you is not "harder").
        /// </summary>
        public static float ClampTrafficSpeed(float trafficSpeed, float playerMaxSpeed)
        {
            return math.min(trafficSpeed, playerMaxSpeed * MaxTrafficSpeedRatio);
        }
    }
}
