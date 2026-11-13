// ============================================================================
// Nightflow - Vehicle Dynamics
// Pure, Burst-friendly rules for how the player car builds momentum and how it
// eventually loses control. Shared by VehicleMovementSystem,
// LaneMagnetismSystem, CrashSystem, the camera and the EditMode tests.
// No ECS access in here.
//
// Momentum - there is no terminal velocity:
//
//   pace(t, d) = PaceStart + PaceTimeRate * t^2 / (t + PaceEaseInTime)
//                          + PaceDistanceRate * d
//
//   t = seconds the player has been driving this run, d = metres driven.
//   Off the pedals the car is pulled up to the pace; throttle adds a boost
//   that shrinks with speed but never reaches zero; the brake trims speed but
//   never below MomentumFloorRatio * pace. Nothing slows the car on its own
//   (no drag), so every run keeps getting faster until the driver can't hold
//   it: ~195 km/h after 2 min, ~340 km/h after 4 min, ~500 km/h after 6 min.
//
// Grip - why it ends:
//
//   grip(v) = GripBase * tyres * suspension / (1 + (v / LiftSpeed)^2)
//   load    = v^2 * curvature + LaneLoadFactor(v) * lane-spring acceleration
//
//   The car goes light as it gets faster while corners and lane changes ask
//   for more. Past usage 1 the tyres let go: the car runs wide, the rear steps
//   out and the nose stops following the road. Catch it (brake, counter-steer,
//   stop asking for more) and the tyres bite again; let the slip angle pass
//   LostControlSlip(v) and the run is over. Tightest corners (R = 300 m) start
//   to slide at ~85 m/s; lane changes on a straight at ~110 m/s.
//
// Every integrator here is frame-rate independent: callers split a frame into
// SubSteps() of at most MaxSubStep, and damping uses exact exponentials. A
// hitch longer than MaxSubSteps * MaxSubStep only integrates that long.
// ============================================================================

using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Config;

namespace Nightflow.Systems
{
    /// <summary>
    /// Per-step inputs for the lateral (lane-frame) integrator.
    /// </summary>
    public struct LateralInputs
    {
        /// <summary>Where the lane spring wants the car (m from road centre, + = right).</summary>
        public float TargetOffset;

        /// <summary>Rate of change of TargetOffset (m/s), e.g. mid lane change.</summary>
        public float TargetVelocity;

        /// <summary>Lane magnetism modulation (autopilot, speed, handbrake, drift, damage).</summary>
        public float Magnetism;

        /// <summary>Acceleration needed to follow the road's curve (v^2 * curvature, + = right).</summary>
        public float CurveAccel;

        /// <summary>Current grip limit (m/s^2), see GripLimit.</summary>
        public float Grip;

        /// <summary>Forward speed along the lane (m/s).</summary>
        public float ForwardSpeed;

        /// <summary>Half the road width (m) for the barrier push.</summary>
        public float HalfRoadWidth;

        /// <summary>Handbrake drift slip gain (velocity follows the nose); 0 when not drifting.</summary>
        public float SlipCoupling;

        /// <summary>Car yaw relative to the lane (rad), for the drift slip coupling.</summary>
        public float Yaw;
    }

    /// <summary>
    /// Lateral state of the car in the lane frame.
    /// </summary>
    public struct LateralState
    {
        /// <summary>Offset from the road centre line (m, + = right).</summary>
        public float Offset;

        /// <summary>Lateral velocity (m/s, + = right).</summary>
        public float Velocity;

        /// <summary>Smoothed tyre load / grip (0 = cruising, 1 = at the limit).</summary>
        public float Usage;

        /// <summary>Direction of the lateral load (-1 left, 0, +1 right).</summary>
        public float LoadDirection;
    }

    /// <summary>
    /// Per-step inputs for the yaw integrator.
    /// </summary>
    public struct YawInputs
    {
        /// <summary>Forward speed along the lane (m/s).</summary>
        public float ForwardSpeed;

        /// <summary>Lateral speed in the lane frame (m/s).</summary>
        public float LateralSpeed;

        /// <summary>Steering input [-1, 1].</summary>
        public float Steer;

        /// <summary>Handbrake held.</summary>
        public bool Handbrake;

        /// <summary>Tyre usage from the lateral integrator (above 1 = sliding).</summary>
        public float Usage;

        /// <summary>Direction of the lateral load (-1, 0, +1): the way the rear steps out.</summary>
        public float LoadDirection;

        /// <summary>Steering health and front damage (1 = healthy).</summary>
        public float SteerResponse;

        /// <summary>Transmission health for drift recovery (1 = healthy).</summary>
        public float RecoveryResponse;
    }

    /// <summary>
    /// Momentum, grip, yaw and loss-of-control rules for the player car.
    /// </summary>
    public static class VehicleDynamics
    {
        // =====================================================================
        // Momentum (no terminal velocity)
        // =====================================================================

        /// <summary>Pace at the start of a run (m/s, ~90 km/h, the autopilot's cruise).</summary>
        public const float PaceStart = 25f;

        /// <summary>How fast the pace climbs once eased in (m/s per second of driving).</summary>
        public const float PaceTimeRate = 0.25f;

        /// <summary>Seconds over which the climb eases in (gentle first half minute).</summary>
        public const float PaceEaseInTime = 30f;

        /// <summary>Pace gained per metre driven: driving fast makes momentum build faster.</summary>
        public const float PaceDistanceRate = 0.0012f;

        /// <summary>How hard the car is pulled up to the pace (1/s of the gap).</summary>
        public const float PaceCatchUpRate = 0.8f;

        /// <summary>Upper bound of the pull toward the pace (m/s^2).</summary>
        public const float PaceCatchUpMaxAccel = 3f;

        /// <summary>Throttle boost at a standstill (m/s^2).</summary>
        public const float BoostAccel = 7f;

        /// <summary>Speed at which the throttle boost is halved; it never reaches zero.</summary>
        public const float BoostFalloffSpeed = 25f;

        /// <summary>Brake deceleration (m/s^2).</summary>
        public const float BrakeDecel = 25f;

        /// <summary>The brake can never take the car below this share of the pace.</summary>
        public const float MomentumFloorRatio = 0.7f;

        /// <summary>Pedal input below this counts as "off the pedals".</summary>
        public const float PedalDeadzone = 0.05f;

        /// <summary>Redline mode: pace climbs this much faster.</summary>
        public const float RedlinePaceScale = 1.3f;

        /// <summary>Redline mode: throttle boost multiplier.</summary>
        public const float RedlineBoostScale = 1.6f;

        // =====================================================================
        // Grip
        // =====================================================================

        /// <summary>Lateral grip at low speed (m/s^2, arcade tyres).</summary>
        public const float GripBase = 32f;

        /// <summary>Grip halves at this speed: the car goes light, nothing holds it down.</summary>
        public const float LiftSpeed = 140f;

        /// <summary>Grip share left on fully worn (not failed) tyres.</summary>
        public const float TireGripFloor = 0.7f;

        /// <summary>Grip share on failed tyres.</summary>
        public const float FailedTireGrip = 0.55f;

        /// <summary>Grip share left on fully worn (not failed) suspension.</summary>
        public const float SuspensionGripFloor = 0.9f;

        /// <summary>Grip share on failed suspension.</summary>
        public const float FailedSuspensionGrip = 0.8f;

        /// <summary>The handbrake lets the rear go.</summary>
        public const float HandbrakeGripFactor = 0.35f;

        /// <summary>The autopilot drives with perfect tyres: attract mode never slides.</summary>
        public const float AutopilotGripFactor = 2f;

        /// <summary>Share of the lane spring's acceleration counted as tyre load at speed.</summary>
        public const float LaneLoadScale = 0.5f;

        /// <summary>Lane changes below this speed put (almost) no load on the tyres.</summary>
        public const float LaneLoadSpeedOffset = 20f;

        /// <summary>Speed span over which the lane-change load share grows by LaneLoadScale.</summary>
        public const float LaneLoadReferenceSpeed = 40f;

        /// <summary>Lane-spring acceleration counted toward load is clamped to this (m/s^2).</summary>
        public const float MaxSpringLoad = 60f;

        /// <summary>How quickly tyre load builds and fades (1/s).</summary>
        public const float TractionResponse = 12f;

        /// <summary>Usage above this is shown/heard as the tyres working near the limit.</summary>
        public const float TractionWarningUsage = 0.85f;

        // =====================================================================
        // Lateral (lane frame)
        // =====================================================================

        /// <summary>Lane spring natural frequency (rad/s), critically damped.</summary>
        public const float LaneOmega = 8f;

        /// <summary>Steering past this starts a lane change (SteeringSystem).</summary>
        public const float LaneChangeSteerThreshold = 0.35f;

        /// <summary>How far analog steering below the lane-change threshold leans the car (m).</summary>
        public const float InLaneOffsetMax = 0.9f;

        /// <summary>Lateral speed limit while the tyres grip (m/s).</summary>
        public const float MaxGripLateralSpeed = 6f;

        /// <summary>Lateral speed limit while sliding past the limit (m/s).</summary>
        public const float MaxSlideLateralSpeed = 10f;

        /// <summary>Lateral speed limit during a handbrake drift (m/s).</summary>
        public const float MaxDriftLateralSpeed = 15f;

        /// <summary>How fast excess lateral speed bleeds off when a limit tightens (1/s).</summary>
        public const float LateralLimitRecovery = 6f;

        /// <summary>Drift slip gain (k_slip): the velocity follows the nose.</summary>
        public const float SlipGain = 1.1f;

        /// <summary>Barrier push past the soft edge (m/s^2 per m^2).</summary>
        public const float EdgeStiffness = 60f;

        /// <summary>Soft edge as a share of half the road width.</summary>
        public const float EdgeSoftZoneRatio = 0.85f;

        // =====================================================================
        // Yaw
        // =====================================================================

        /// <summary>Steering torque gain (k_s).</summary>
        public const float SteeringGain = 1.2f;

        /// <summary>Reference speed for steering torque and yaw damping (m/s).</summary>
        public const float YawReferenceSpeed = 40f;

        /// <summary>Handbrake drift torque gain (k_d).</summary>
        public const float DriftGain = 2.5f;

        /// <summary>Drift recovery torque gain (k_r).</summary>
        public const float RecoveryGain = 2f;

        /// <summary>Tyres pull the nose back in line with the velocity (rad/s^2 per rad of slip).</summary>
        public const float AlignStiffness = 40f;

        /// <summary>Yaw damping at the reference speed (1/s); falls off with speed.</summary>
        public const float YawDamping = 9f;

        /// <summary>Yaw damping never falls below this share: twitchy, not unstable.</summary>
        public const float MinYawDampingScale = 0.45f;

        /// <summary>Oversteer torque per unit of usage past the limit (rad/s^2).</summary>
        public const float SlideTorque = 6f;

        /// <summary>How quickly the tyres stop straightening the car past the limit.</summary>
        public const float SlideAlignLoss = 4f;

        /// <summary>Excess usage beyond this adds no further oversteer.</summary>
        public const float MaxSlideExcess = 1.5f;

        /// <summary>Yaw rate limit (rad/s).</summary>
        public const float MaxYawRate = 6f;

        /// <summary>Alignment left while the handbrake is held.</summary>
        public const float HandbrakeAlignFactor = 0.15f;

        /// <summary>Steering needed for the handbrake to start a drift.</summary>
        public const float DriftSteerThreshold = 0.1f;

        /// <summary>Drift ends below this yaw (rad)...</summary>
        public const float DriftExitYaw = 0.1f;

        /// <summary>...and this yaw rate (rad/s).</summary>
        public const float DriftExitRate = 0.5f;

        // =====================================================================
        // Loss of control
        // =====================================================================

        /// <summary>At or below this speed spins are survivable (they never stall the run).</summary>
        public const float SpinSafeSpeed = 35f;

        /// <summary>At or above this speed a slide past MaxSafeSlipAtSpeed is a crash.</summary>
        public const float SpinDeadlySpeed = 75f;

        /// <summary>Largest survivable slip angle at high speed (rad, ~49 degrees).</summary>
        public const float MaxSafeSlipAtSpeed = 0.85f;

        /// <summary>Above any wrapped slip angle: "no limit".</summary>
        public const float NoSpinLimit = math.PI + 0.1f;

        // =====================================================================
        // Integration
        // =====================================================================

        /// <summary>Largest integration step (s).</summary>
        public const float MaxSubStep = 1f / 120f;

        /// <summary>
        /// Sub-step cap: a frame longer than MaxSubSteps * MaxSubStep (a hitch)
        /// integrates lateral and yaw motion for only that long, so the springs
        /// never see one huge unstable step (forward motion still covers the
        /// whole frame).
        /// </summary>
        public const int MaxSubSteps = 16;

        private const float TwoPi = 2f * math.PI;

        // =====================================================================
        // Momentum
        // =====================================================================

        /// <summary>
        /// Speed the run carries the car at (m/s). Grows without bound: eases in
        /// over the first half minute, then climbs at PaceTimeRate plus a little
        /// more for every metre driven.
        /// </summary>
        /// <param name="runTime">Seconds the player has been driving this run.</param>
        /// <param name="runDistance">Metres driven this run.</param>
        /// <param name="growthScale">Mode scale on the climb (0 = no climb, Redline &gt; 1).</param>
        public static float PaceSpeed(float runTime, float runDistance, float growthScale = 1f)
        {
            // Comparisons against NaN are false, so NaN/negative inputs read as 0
            float t = runTime > 0f ? runTime : 0f;
            float d = runDistance > 0f ? runDistance : 0f;
            float climb = PaceTimeRate * t * t / (t + PaceEaseInTime) + PaceDistanceRate * d;
            return PaceStart + math.max(growthScale, 0f) * climb;
        }

        /// <summary>Lowest speed the brake can trim the car to at this pace (m/s).</summary>
        public static float MomentumFloor(float pace)
        {
            return math.max(GameConstants.MinForwardSpeed, MomentumFloorRatio * pace);
        }

        /// <summary>Full-throttle boost at this speed (m/s^2): shrinks with speed, never zero.</summary>
        public static float BoostAcceleration(float speed)
        {
            return BoostAccel / (1f + math.max(speed, 0f) / BoostFalloffSpeed);
        }

        /// <summary>
        /// One frame of forward speed. Below the pace the car is pulled up to it;
        /// throttle adds a boost on top; the brake trims toward the floor but no
        /// lower; nothing ever pulls the car down on its own.
        /// </summary>
        /// <param name="speed">Current forward speed (m/s).</param>
        /// <param name="pace">Run pace (m/s); 0 disables the pull (autopilot, free speed).</param>
        /// <param name="floor">Lowest speed the brake may reach (m/s).</param>
        /// <param name="throttle">Throttle [0, 1].</param>
        /// <param name="brake">Brake [0, 1].</param>
        /// <param name="powerModifier">Engine/transmission health share (1 = healthy).</param>
        /// <param name="boostScale">Mode multiplier on the throttle boost.</param>
        /// <param name="deltaTime">Frame time (s).</param>
        public static float StepForwardSpeed(float speed, float pace, float floor, float throttle,
            float brake, float powerModifier, float boostScale, float deltaTime)
        {
            float dt = math.max(deltaTime, 0f);
            bool braking = brake > PedalDeadzone;
            float next = speed;

            // Momentum pulls the car up to the pace, never past it on its own
            if (!braking && speed < pace)
            {
                float pull = math.min(PaceCatchUpMaxAccel, (pace - speed) * PaceCatchUpRate) * powerModifier;
                next = math.min(pace, speed + pull * dt);
            }

            if (throttle > PedalDeadzone)
            {
                next += throttle * BoostAcceleration(speed) * boostScale * powerModifier * dt;
            }

            if (braking && next > floor)
            {
                next = math.max(floor, next - brake * BrakeDecel * dt);
            }

            return math.max(next, GameConstants.MinForwardSpeed);
        }

        // =====================================================================
        // Grip
        // =====================================================================

        /// <summary>
        /// Lateral grip available at this speed (m/s^2). Falls with speed (the
        /// car goes light), with tyre and suspension wear, under the handbrake.
        /// </summary>
        public static float GripLimit(float speed, float tireHealth, bool tiresFailed,
            float suspensionHealth, bool suspensionFailed, bool handbrake, bool autopilot)
        {
            float tyres = tiresFailed
                ? FailedTireGrip
                : TireGripFloor + (1f - TireGripFloor) * math.saturate(tireHealth);
            float suspension = suspensionFailed
                ? FailedSuspensionGrip
                : SuspensionGripFloor + (1f - SuspensionGripFloor) * math.saturate(suspensionHealth);

            float x = math.max(speed, 0f) / LiftSpeed;
            float grip = GripBase * tyres * suspension / (1f + x * x);

            if (handbrake)
                grip *= HandbrakeGripFactor;
            if (autopilot)
                grip *= AutopilotGripFactor;

            return grip;
        }

        /// <summary>Acceleration needed to follow a curve (m/s^2, sign of the curvature).</summary>
        public static float CurveAcceleration(float speed, float signedCurvature)
        {
            return speed * speed * signedCurvature;
        }

        /// <summary>Share of the lane spring's acceleration that loads the tyres at this speed.</summary>
        public static float LaneLoadFactor(float speed)
        {
            return LaneLoadScale * math.max(0f, speed - LaneLoadSpeedOffset) / LaneLoadReferenceSpeed;
        }

        /// <summary>Signed lateral tyre load (m/s^2): the curve plus a speed-weighted share of the lane spring.</summary>
        public static float TractionDemand(float springAccel, float curveAccel, float speed)
        {
            float spring = math.clamp(springAccel, -MaxSpringLoad, MaxSpringLoad);
            return curveAccel + LaneLoadFactor(speed) * spring;
        }

        /// <summary>Load as a share of grip (1 = at the limit).</summary>
        public static float TractionUsage(float demand, float grip)
        {
            return math.abs(demand) / math.max(grip, 0.001f);
        }

        /// <summary>Tyre load builds and fades over a short time instead of jumping.</summary>
        public static float SmoothUsage(float current, float target, float deltaTime)
        {
            if (!(current >= 0f))
                current = 0f;   // NaN guard
            if (!(target >= 0f))
                target = 0f;
            return math.lerp(current, target, Damp(TractionResponse, deltaTime));
        }

        /// <summary>
        /// Lane-frame lateral acceleration the tyres actually deliver. Within grip
        /// the lane spring gets what it asks for; past the limit the whole tyre
        /// force is scaled back, so the car runs wide of the curve and lags the
        /// lane it wanted.
        /// </summary>
        public static float ResolveLateralAcceleration(float springAccel, float curveAccel, float usage)
        {
            if (!(usage > 1f))
                return springAccel;

            return (springAccel + curveAccel) / usage - curveAccel;
        }

        // =====================================================================
        // Lateral
        // =====================================================================

        /// <summary>
        /// Analog steering below the lane-change threshold leans the car inside
        /// its lane (m, + = right).
        /// </summary>
        public static float InLaneOffset(float steer)
        {
            return math.clamp(steer / LaneChangeSteerThreshold, -1f, 1f) * InLaneOffsetMax;
        }

        /// <summary>
        /// Smoothstep lane-change blend (0 = old lane, 1 = new lane) and its rate
        /// of change (1/s) at this point of the change.
        /// </summary>
        public static float LaneBlend(float timer, float duration, out float blendRate)
        {
            float d = math.max(duration, 0.01f);
            float s = math.saturate(timer / d);
            blendRate = s < 1f ? 6f * s * (1f - s) / d : 0f;
            return s * s * (3f - 2f * s);
        }

        /// <summary>
        /// Lane-change blend toward TargetLane, run backwards while the change is
        /// being aborted. SteeringSystem mirrors the timer on an abort
        /// (timer' = duration - timer), and because smoothstep is symmetric
        /// (1 - λ(1 - s) == λ(s)) the blend, and so the car's lateral target,
        /// carries on from exactly where it was, back to the original lane.
        /// </summary>
        public static float LaneBlend(float timer, float duration, bool aborting, out float blendRate)
        {
            float blend = LaneBlend(timer, duration, out blendRate);
            if (aborting)
            {
                blend = 1f - blend;
                blendRate = -blendRate;
            }
            return blend;
        }

        /// <summary>Barrier push back toward the road past the soft edge (m/s^2, not grip-limited).</summary>
        public static float EdgeAcceleration(float offset, float halfRoadWidth)
        {
            float over = math.abs(offset) - halfRoadWidth * EdgeSoftZoneRatio;
            if (!(over > 0f))
                return 0f;
            return -math.sign(offset) * EdgeStiffness * over * over;
        }

        /// <summary>
        /// One lateral integration step (call with a SubSteps() step): critically
        /// damped lane spring toward the moving target, grip-limited, plus the
        /// drift slip coupling and the barrier push.
        /// </summary>
        public static void StepLateral(ref LateralState state, in LateralInputs input, float h)
        {
            float error = state.Offset - input.TargetOffset;
            float springAccel = input.Magnetism *
                (-LaneOmega * LaneOmega * error - 2f * LaneOmega * (state.Velocity - input.TargetVelocity));

            float demand = TractionDemand(springAccel, input.CurveAccel, input.ForwardSpeed);
            state.Usage = SmoothUsage(state.Usage, TractionUsage(demand, input.Grip), h);
            state.LoadDirection = math.sign(demand);

            float accel = ResolveLateralAcceleration(springAccel, input.CurveAccel, state.Usage);
            accel += EdgeAcceleration(state.Offset, input.HalfRoadWidth);

            bool drifting = input.SlipCoupling > 0f;
            if (drifting)
            {
                float heading = math.atan2(state.Velocity, math.max(input.ForwardSpeed, 1f));
                accel += input.SlipCoupling * math.sin(WrapAngle(input.Yaw - heading)) * input.ForwardSpeed;
            }

            float limit = drifting
                ? MaxDriftLateralSpeed
                : (state.Usage > 1f ? MaxSlideLateralSpeed : MaxGripLateralSpeed);

            float previous = math.abs(state.Velocity);
            float next = state.Velocity + accel * h;

            if (math.abs(next) > limit)
            {
                // Coming out of a slide or drift the extra lateral speed bleeds
                // off instead of being cut in one frame
                float allowed = previous > limit
                    ? math.max(limit, previous * math.exp(-LateralLimitRecovery * h))
                    : limit;
                allowed = math.min(allowed, MaxDriftLateralSpeed);
                next = math.sign(next) * math.min(math.abs(next), allowed);
            }

            state.Velocity = next;
            state.Offset += next * h;
        }

        // =====================================================================
        // Yaw
        // =====================================================================

        /// <summary>
        /// One yaw integration step (call with a SubSteps() step). The tyres pull
        /// the nose in line with where the car is going; steering and the
        /// handbrake twist it; past the grip limit the alignment fades and the
        /// rear steps out. Yaw damping falls with speed, so the car gets
        /// twitchier the faster it goes. Writes YawOffset (wrapped), YawRate,
        /// IsDrifting and SlipAngle; returns the slip angle.
        /// </summary>
        public static float StepYaw(ref DriftState drift, in YawInputs input, float h)
        {
            float vf = math.max(input.ForwardSpeed, 1f);
            float heading = math.atan2(input.LateralSpeed, vf);
            float slip = WrapAngle(drift.YawOffset - heading);
            float excess = math.max(0f, input.Usage - 1f);
            float speedRatio = input.ForwardSpeed / YawReferenceSpeed;

            // Steering torque: tau_steer = k_s * s * (v / v_ref)
            float torque = SteeringGain * input.Steer * speedRatio * input.SteerResponse;

            float align = 1f / (1f + SlideAlignLoss * excess);

            if (input.Handbrake && math.abs(input.Steer) > DriftSteerThreshold)
            {
                // tau_drift = k_d * sign(s) * sqrt(v_f)
                torque += DriftGain * math.sign(input.Steer) * math.sqrt(math.max(input.ForwardSpeed, 0f));
                drift.IsDrifting = true;
                align *= HandbrakeAlignFactor;
            }
            else if (drift.IsDrifting)
            {
                // tau_recover = -k_r * psi (handbrake released)
                torque -= RecoveryGain * drift.YawOffset * input.RecoveryResponse;

                if (math.abs(drift.YawOffset) < DriftExitYaw && math.abs(drift.YawRate) < DriftExitRate)
                {
                    drift.IsDrifting = false;
                }
            }

            torque -= AlignStiffness * align * slip;
            torque += SlideTorque * input.LoadDirection * math.min(excess, MaxSlideExcess) * speedRatio;

            float damping = YawDamping * math.max(MinYawDampingScale, math.sqrt(YawReferenceSpeed / vf));

            drift.YawRate = math.clamp((drift.YawRate + torque * h) * math.exp(-damping * h),
                -MaxYawRate, MaxYawRate);
            drift.YawOffset = WrapAngle(drift.YawOffset + drift.YawRate * h);
            drift.SlipAngle = WrapAngle(drift.YawOffset - heading);

            return drift.SlipAngle;
        }

        // =====================================================================
        // Loss of control
        // =====================================================================

        /// <summary>
        /// Largest survivable slip angle at this speed (rad). Slow spins never end
        /// the run; at speed a car sliding this far sideways is gone.
        /// </summary>
        public static float LostControlSlip(float speed)
        {
            float t = math.saturate((speed - SpinSafeSpeed) / (SpinDeadlySpeed - SpinSafeSpeed));
            t = t * t * (3f - 2f * t);
            return math.lerp(NoSpinLimit, MaxSafeSlipAtSpeed, t);
        }

        /// <summary>True once the car is sliding too far sideways for its speed.</summary>
        public static bool IsLostControl(float slipAngle, float speed)
        {
            return math.abs(slipAngle) > LostControlSlip(speed);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>Frame-rate independent blend factor for exponential smoothing.</summary>
        public static float Damp(float rate, float deltaTime)
        {
            return 1f - math.exp(-rate * math.max(deltaTime, 0f));
        }

        /// <summary>Wraps an angle into [-pi, pi).</summary>
        public static float WrapAngle(float angle)
        {
            if (angle >= -math.PI && angle < math.PI)
                return angle;
            if (!math.isfinite(angle))
                return 0f;
            return angle - TwoPi * math.floor((angle + math.PI) / TwoPi);
        }

        /// <summary>
        /// Splits a frame into equal integration steps of at most MaxSubStep.
        /// Frames longer than MaxSubSteps * MaxSubStep are clamped to that
        /// length (see MaxSubSteps).
        /// </summary>
        /// <returns>Number of steps (0 for a non-positive frame).</returns>
        public static int SubSteps(float deltaTime, out float step)
        {
            if (!(deltaTime > 0f))
            {
                step = 0f;
                return 0;
            }

            float dt = math.min(deltaTime, MaxSubStep * MaxSubSteps);
            int n = math.clamp((int)math.ceil(dt / MaxSubStep - 0.0001f), 1, MaxSubSteps);
            step = dt / n;
            return n;
        }

        /// <summary>
        /// Presentation speed curve: 0 at a crawl, ~0.5 at 50 m/s, ~0.8 at
        /// 90 m/s, still creeping toward 1 at any speed. FOV, pull-back, blur
        /// and speed lines all read this so they keep building together.
        /// </summary>
        public static float SpeedFeel(float speed)
        {
            float x = math.max(0f, speed - SpeedFeelStart) / SpeedFeelScale;
            return 1f - math.exp(-x);
        }

        /// <summary>Speed below which nothing feels fast (m/s).</summary>
        public const float SpeedFeelStart = 12f;

        /// <summary>Speed span of the presentation curve (m/s).</summary>
        public const float SpeedFeelScale = 50f;
    }
}
