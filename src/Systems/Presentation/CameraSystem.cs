// ============================================================================
// Nightflow - Camera System
// Execution Order: 1 (Presentation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Config;

namespace Nightflow.Systems
{
    /// <summary>
    /// Chase camera: follows where the car is going, not where it points, and
    /// sells the momentum as it builds.
    ///
    /// - The camera sits a set distance behind the car along its smoothed
    ///   direction of travel. Its offset is smoothed, never its world position,
    ///   so it does not fall further behind the faster the car goes.
    /// - When the car slides or spins, the camera keeps facing down the road
    ///   and watches it rotate instead of orbiting with it.
    /// - FOV, pull-back and height follow VehicleDynamics.SpeedFeel, which keeps
    ///   building with no top speed; accelerating pulls the camera back a touch,
    ///   braking lets it close in, and a fine shake grows with speed and with
    ///   the tyres working near their grip limit.
    /// - Every smoothing step is frame-rate independent (exponential damping).
    /// - When the vehicle is reset under the crash fade the camera cuts
    ///   straight to its chase position, so the fade-in reveals a settled view.
    ///
    /// From spec:
    /// - Base FOV 55°, widening with speed
    /// - Pull-back: distance and height increase with speed
    /// - Impact Recoil: from collision impulse
    /// - Damage Wobble: from accumulated damage
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct CameraSystem : ISystem
    {
        // Follow parameters (spec 10: base 3 m up / 8 m back, +1.5 m / +4 m with speed)
        private const float BaseHeight = 3f;              // meters above vehicle
        private const float SpeedHeightBonus = 1.5f;
        private const float BaseDistance = 8f;            // meters behind vehicle
        private const float SpeedDistanceBonus = 4f;
        private const float LookAheadFactor = 0.5f;       // s of travel looked ahead
        private const float MaxLookAhead = 15f;           // m
        private const float LookHeight = 0.8f;            // m above the car's origin

        // Smoothing rates (1/s, exponential)
        private const float HeadingResponse = 6f;
        private const float DistanceResponse = 4f;
        private const float HeightResponse = 4f;
        private const float LateralResponse = 3f;
        private const float RollResponse = 4f;
        private const float FOVResponse = 3f;
        private const float LookResponse = 10f;

        // FOV (from spec, widening with SpeedFeel; keeps creeping up, never past MaxFOV)
        private const float BaseFOV = 55f;                // degrees at low speed
        private const float MaxFOV = 95f;                 // asymptote at extreme speed
        private const float DriftFOVBonus = 5f;

        // Momentum: accelerating pulls the camera back, braking lets it in
        private const float AccelPullBack = 0.12f;        // m per m/s²
        private const float MaxAccelPullBack = 1.2f;      // m
        private const float MaxAccelPushIn = 0.8f;        // m
        private const float AccelResponse = 4f;           // 1/s

        // Fine shake: grows with speed and with the tyres near their limit
        private const float SpeedShakeStart = 45f;        // m/s
        private const float SpeedShakeScale = 60f;        // m/s
        private const float SpeedShakeMax = 0.06f;        // m
        private const float TractionShakeMax = 0.12f;     // m
        private const float TractionShakeRoll = 0.012f;   // rad

        // Drift camera
        private const float DriftOffsetMax = 2f;          // lateral offset when drifting
        private const float DriftTiltMax = 10f;           // degrees of roll
        private const float DriftWhipMultiplier = 1.3f;   // extra rotational follow

        // Crash camera
        private const float CrashZoomResponse = 2f;       // 1/s
        private const float CrashMaxDistance = 20f;       // max zoom distance

        // Environment offsets (fork pull-back etc.) decay back to neutral
        private const float EnvironmentOffsetDecay = 2f;  // 1/s

        // Impact/Shake parameters
        private const float ImpactRecoilDecay = 8f;       // recoil decay rate
        private const float ImpactRecoilMax = 0.5f;       // max recoil offset
        private const float DamageWobbleFreq = 3f;        // wobble frequency
        private const float DamageWobbleMax = 0.15f;      // max wobble amplitude

        // Phase 2: Suspension shake parameters
        private const float SuspensionShakeBaseFreq = 12f;    // Base frequency of suspension bounce
        private const float SuspensionShakeMaxAmp = 0.25f;    // Maximum shake amplitude
        private const float SuspensionShakeSpeedScale = 0.01f;// Speed multiplier for shake intensity
        private const float SuspensionFailedShakeAmp = 0.4f;  // Shake when suspension fully failed

        // State (single player camera)
        private float3 _recoilOffset;
        private float _wobblePhase;
        private float _suspensionShakePhase;
        private float _shakeTime;
        private float3 _heading;
        private float _accelOffset;
        private float _prevSpeed;
        private bool _wasCrashed;
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            _recoilOffset = float3.zero;
            _wobblePhase = 0f;
            _suspensionShakePhase = 0f;
            _shakeTime = 0f;
            _heading = new float3(0f, 0f, 1f);
            _accelOffset = 0f;
            _prevSpeed = 0f;
            _wasCrashed = false;
            _initialized = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get player state
            float3 playerPos = float3.zero;
            quaternion playerRot = quaternion.identity;
            float playerSpeed = 0f;
            float yawOffset = 0f;
            float yawRate = 0f;
            bool isDrifting = false;
            bool isCrashed = false;
            bool foundPlayer = false;
            float totalDamage = 0f;
            float impulseMagnitude = 0f;
            float3 impulseDirection = float3.zero;
            float3 travelVelocity = float3.zero;
            float tractionUsage = 0f;

            // Phase 2: Component health for suspension shake
            float suspensionHealth = 1f;
            bool suspensionFailed = false;

            foreach (var (transform, velocity, driftState) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>, RefRO<DriftState>>()
                    .WithAll<PlayerVehicleTag>())
            {
                playerPos = transform.ValueRO.Position;
                playerRot = transform.ValueRO.Rotation;
                playerSpeed = velocity.ValueRO.Forward;
                yawOffset = driftState.ValueRO.YawOffset;
                yawRate = driftState.ValueRO.YawRate;
                isDrifting = driftState.ValueRO.IsDrifting;
                foundPlayer = true;
                break;
            }

            if (!foundPlayer)
                return;

            foreach (var vehicleVelocity in SystemAPI.Query<RefRO<VehicleVelocity>>().WithAll<PlayerVehicleTag>())
            {
                travelVelocity = vehicleVelocity.ValueRO.Linear;
                break;
            }

            foreach (var crashState in SystemAPI.Query<RefRO<CrashState>>().WithAll<PlayerVehicleTag>())
            {
                isCrashed = crashState.ValueRO.IsCrashed;
                break;
            }

            foreach (var traction in SystemAPI.Query<RefRO<TractionState>>().WithAll<PlayerVehicleTag>())
            {
                tractionUsage = traction.ValueRO.Usage;
                break;
            }

            // Get damage state for wobble
            foreach (var damage in SystemAPI.Query<RefRO<DamageState>>().WithAll<PlayerVehicleTag>())
            {
                totalDamage = damage.ValueRO.Total;
                break;
            }

            // Get impulse for recoil
            foreach (var impulse in SystemAPI.Query<RefRO<ImpulseData>>().WithAll<PlayerVehicleTag>())
            {
                if (impulse.ValueRO.Magnitude > 0.1f)
                {
                    impulseMagnitude = impulse.ValueRO.Magnitude;
                    impulseDirection = impulse.ValueRO.Direction;
                }
                break;
            }

            // Phase 2: Get component health for suspension shake
            foreach (var (health, failures) in
                SystemAPI.Query<RefRO<ComponentHealth>, RefRO<ComponentFailureState>>()
                    .WithAll<PlayerVehicleTag>())
            {
                suspensionHealth = health.ValueRO.Suspension;
                suspensionFailed = failures.ValueRO.HasFailed(ComponentFailures.Suspension);
                break;
            }

            // =============================================================
            // Direction of travel (smoothed) and cuts
            // =============================================================

            float3 carForward = math.mul(playerRot, new float3(0f, 0f, 1f));
            float3 travel = math.lengthsq(travelVelocity) > 1f
                ? math.normalize(travelVelocity)
                : carForward;

            // Cut on the first frame and when the wreck is reset under the fade
            bool cut = !_initialized || (_wasCrashed && !isCrashed);
            _initialized = true;
            _wasCrashed = isCrashed;

            if (cut)
            {
                _heading = travel;
                _accelOffset = 0f;
                _prevSpeed = playerSpeed;
                _recoilOffset = float3.zero;
            }
            else
            {
                _heading = math.normalizesafe(
                    math.lerp(_heading, travel, VehicleDynamics.Damp(HeadingResponse, deltaTime)),
                    travel);
            }

            // =============================================================
            // Momentum: acceleration pull-back
            // =============================================================

            float accel = deltaTime > 0f ? (playerSpeed - _prevSpeed) / deltaTime : 0f;
            _prevSpeed = playerSpeed;
            float accelTarget = isCrashed
                ? 0f
                : math.clamp(accel * AccelPullBack, -MaxAccelPushIn, MaxAccelPullBack);
            _accelOffset = math.lerp(_accelOffset, accelTarget, VehicleDynamics.Damp(AccelResponse, deltaTime));

            // =============================================================
            // Impact Recoil
            // =============================================================

            if (impulseMagnitude > 0.1f)
            {
                // Add recoil in direction of impact
                float recoilAmount = math.min(impulseMagnitude * 0.1f, ImpactRecoilMax);
                _recoilOffset += impulseDirection * recoilAmount;
            }

            // Decay recoil (exponential, frame-rate independent)
            _recoilOffset *= math.exp(-ImpactRecoilDecay * deltaTime);

            // =============================================================
            // Damage Wobble
            // =============================================================

            float wobbleAmount = 0f;
            if (totalDamage > 10f)
            {
                _wobblePhase += DamageWobbleFreq * deltaTime * math.PI * 2f;
                float damageNorm = math.saturate(totalDamage / 100f);
                wobbleAmount = math.sin(_wobblePhase) * DamageWobbleMax * damageNorm;
            }

            // =============================================================
            // Phase 2: Suspension Shake
            // =============================================================
            // Damaged suspension causes vertical bouncing that increases with speed
            // Failed suspension causes severe, erratic shaking

            float3 suspensionShake = float3.zero;
            float suspensionDamage = 1f - suspensionHealth;

            if (suspensionDamage > 0.1f || suspensionFailed)
            {
                // Advance shake phase - faster with more damage
                float shakeFreq = SuspensionShakeBaseFreq * (1f + suspensionDamage);
                _suspensionShakePhase += shakeFreq * deltaTime * math.PI * 2f;

                // Base amplitude from suspension damage
                float baseAmp = suspensionFailed
                    ? SuspensionFailedShakeAmp
                    : suspensionDamage * SuspensionShakeMaxAmp;

                // Increase with speed (damaged suspension bounces more at high speed)
                float speedFactor = 1f + playerSpeed * SuspensionShakeSpeedScale;
                float amplitude = baseAmp * speedFactor;

                // Primary vertical bounce (sinusoidal)
                float verticalBounce = math.sin(_suspensionShakePhase) * amplitude;

                // Secondary horizontal sway (different frequency for asymmetry)
                float horizontalSway = math.sin(_suspensionShakePhase * 0.7f) * amplitude * 0.3f;

                // Add some chaos when failed (high frequency noise)
                if (suspensionFailed)
                {
                    float noise = math.sin(_suspensionShakePhase * 3.7f) * 0.15f;
                    verticalBounce += noise;
                    horizontalSway += math.sin(_suspensionShakePhase * 2.3f) * 0.1f;
                }

                suspensionShake = new float3(horizontalSway, verticalBounce, 0f);
            }

            // =============================================================
            // Speed and traction shake (fine, high frequency)
            // =============================================================

            _shakeTime += deltaTime;
            if (_shakeTime > 1000f)
                _shakeTime -= 1000f;   // keep the sine arguments small

            float tractionEdge = math.saturate((tractionUsage - 0.8f) / 0.5f);
            tractionEdge = tractionEdge * tractionEdge * (3f - 2f * tractionEdge);
            float shakeAmp = isCrashed ? 0f
                : SpeedShakeMax * (1f - math.exp(-math.max(0f, playerSpeed - SpeedShakeStart) / SpeedShakeScale))
                  + TractionShakeMax * tractionEdge;
            float shakeX = (math.sin(_shakeTime * 31.7f) * 0.6f + math.sin(_shakeTime * 57.3f) * 0.4f) * shakeAmp;
            float shakeY = (math.sin(_shakeTime * 43.1f) * 0.5f + math.sin(_shakeTime * 71.9f) * 0.5f) * shakeAmp;
            float shakeRoll = math.sin(_shakeTime * 23.3f) * TractionShakeRoll * tractionEdge;

            // =============================================================
            // Update Camera State
            // =============================================================

            float speedFeel = VehicleDynamics.SpeedFeel(playerSpeed);

            foreach (var camera in SystemAPI.Query<RefRW<CameraState>>())
            {
                float targetDistance;
                float targetHeight;
                float targetLateral = 0f;
                float targetRoll = 0f;
                float targetFOV;
                float distanceResponse = DistanceResponse;
                float heightResponse = HeightResponse;

                float followFOV = BaseFOV + (MaxFOV - BaseFOV) * speedFeel + camera.ValueRO.FOVOffset;

                if (isCrashed)
                {
                    // =========================================================
                    // Crash Camera: pull out and rise to watch the wreck
                    // =========================================================
                    camera.ValueRW.Mode = CameraMode.Crash;
                    targetDistance = CrashMaxDistance;
                    targetHeight = BaseHeight * 2f;
                    targetFOV = camera.ValueRO.FOV;
                    distanceResponse = CrashZoomResponse;
                    heightResponse = CrashZoomResponse;
                }
                else
                {
                    targetDistance = BaseDistance + SpeedDistanceBonus * speedFeel
                        + camera.ValueRO.DistanceOffset + _accelOffset;
                    targetHeight = BaseHeight + SpeedHeightBonus * speedFeel;
                    targetFOV = followFOV;

                    if (isDrifting && math.abs(yawOffset) > 0.3f)
                    {
                        // =====================================================
                        // Drift Camera: slide out to the side and lean in
                        // =====================================================
                        camera.ValueRW.Mode = CameraMode.Drift;
                        float driftSign = math.sign(yawOffset);
                        targetLateral = driftSign * DriftOffsetMax;
                        targetRoll = -driftSign * math.radians(DriftTiltMax);
                        targetFOV += DriftFOVBonus;
                    }
                    else
                    {
                        camera.ValueRW.Mode = CameraMode.Follow;
                    }
                }

                if (cut)
                {
                    camera.ValueRW.FollowDistance = targetDistance;
                    camera.ValueRW.FollowHeight = targetHeight;
                    camera.ValueRW.LateralOffset = targetLateral;
                    camera.ValueRW.Roll = targetRoll;
                    camera.ValueRW.FOV = targetFOV;
                }
                else
                {
                    camera.ValueRW.FollowDistance = math.lerp(camera.ValueRO.FollowDistance, targetDistance,
                        VehicleDynamics.Damp(distanceResponse, deltaTime));
                    camera.ValueRW.FollowHeight = math.lerp(camera.ValueRO.FollowHeight, targetHeight,
                        VehicleDynamics.Damp(heightResponse, deltaTime));
                    camera.ValueRW.LateralOffset = math.lerp(camera.ValueRO.LateralOffset, targetLateral,
                        VehicleDynamics.Damp(LateralResponse, deltaTime));
                    camera.ValueRW.Roll = math.lerp(camera.ValueRO.Roll, targetRoll,
                        VehicleDynamics.Damp(RollResponse, deltaTime));
                    camera.ValueRW.FOV = math.lerp(camera.ValueRO.FOV, targetFOV,
                        VehicleDynamics.Damp(FOVResponse, deltaTime));
                }

                // Environment requests are per-frame: decay the pull-back, clear the squeeze
                camera.ValueRW.DistanceOffset *= math.exp(-EnvironmentOffsetDecay * deltaTime);
                camera.ValueRW.FOVOffset = 0f;

                // =============================================================
                // Camera Transform
                // =============================================================

                float3 up = new float3(0f, 1f, 0f);
                float3 right = math.normalizesafe(math.cross(up, _heading), new float3(1f, 0f, 0f));

                // Rigid offset from the car (smoothed above), so the camera
                // never lags further behind as the speed builds
                float3 cameraPos = playerPos
                    - _heading * camera.ValueRO.FollowDistance
                    + up * camera.ValueRO.FollowHeight
                    + right * camera.ValueRO.LateralOffset
                    + camera.ValueRO.TargetOffset;

                // Recoil, damage wobble, suspension shake, speed/traction shake
                cameraPos += _recoilOffset;
                cameraPos += right * (wobbleAmount + suspensionShake.x + shakeX);
                cameraPos += up * (suspensionShake.y + shakeY);

                camera.ValueRW.Position = cameraPos;

                // Look down the road just ahead of the car
                float lookAhead = math.min(MaxLookAhead, math.max(playerSpeed, 0f) * LookAheadFactor);
                float3 lookTarget = playerPos + _heading * lookAhead + up * LookHeight;
                float3 lookDir = math.normalizesafe(lookTarget - cameraPos, _heading);

                quaternion targetRot = quaternion.LookRotationSafe(lookDir, up);

                // Apply roll from drift + damage wobble + traction shake
                float totalRoll = camera.ValueRO.Roll + wobbleAmount * 0.5f + shakeRoll;
                if (math.abs(totalRoll) > 0.0001f)
                {
                    targetRot = math.mul(targetRot, quaternion.RotateZ(totalRoll));
                }

                // Drift whip: extra yaw follow during drift
                float whipYaw = 0f;
                if (isDrifting && math.abs(yawRate) > 0.1f)
                {
                    whipYaw = yawRate * DriftWhipMultiplier * 0.1f;
                    targetRot = math.mul(targetRot, quaternion.RotateY(whipYaw));
                }
                camera.ValueRW.YawOffset = whipYaw;

                camera.ValueRW.Rotation = cut
                    ? targetRot
                    : math.slerp(camera.ValueRO.Rotation, targetRot, VehicleDynamics.Damp(LookResponse, deltaTime));
            }
        }
    }

}
