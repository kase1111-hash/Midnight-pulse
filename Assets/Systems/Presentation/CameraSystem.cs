// ============================================================================
// Nightflow - Camera System
// Execution Order: 1 (Presentation Group)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Controls camera follow behavior, crash zoom, and replay camera.
    /// Smooth follow with speed-dependent FOV and offset.
    ///
    /// From spec:
    /// - Base FOV 55°, Max FOV 90°
    /// - Speed FOV: scales from 55° to 90° with speed
    /// - Pull-back: offset increases at high speed
    /// - Impact Recoil: from collision impulse
    /// - Damage Wobble: from accumulated damage
    /// - All motion critically damped per axis
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct CameraSystem : ISystem
    {
        // Follow parameters
        private const float BaseHeight = 3f;              // meters above vehicle
        private const float BaseDistance = 8f;            // meters behind vehicle
        private const float MaxDistance = 12f;            // at max speed
        private const float FollowSmoothing = 5f;         // position lerp speed
        private const float LookSmoothing = 8f;           // rotation lerp speed

        // FOV parameters (from spec)
        private const float BaseFOV = 55f;                // degrees at low speed
        private const float MaxFOV = 90f;                 // degrees at max speed
        private const float SpeedForMaxFOV = 70f;         // m/s

        // Drift camera
        private const float DriftOffsetMax = 2f;          // lateral offset when drifting
        private const float DriftTiltMax = 10f;           // degrees of roll
        private const float DriftWhipMultiplier = 1.3f;   // extra rotational follow

        // Crash camera
        private const float CrashZoomSpeed = 2f;          // zoom out speed
        private const float CrashMaxDistance = 20f;       // max zoom distance
        private const float CrashSlowMotion = 0.3f;       // time scale

        // Impact/Shake parameters
        private const float ImpactRecoilDecay = 8f;       // recoil decay rate
        private const float ImpactRecoilMax = 0.5f;       // max recoil offset
        private const float DamageWobbleFreq = 3f;        // wobble frequency
        private const float DamageWobbleMax = 0.15f;      // max wobble amplitude

        // State
        private float3 _recoilOffset;
        private float _wobblePhase;

        public void OnCreate(ref SystemState state)
        {
            _recoilOffset = float3.zero;
            _wobblePhase = 0f;
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
            float totalDamage = 0f;
            float impulseMagnitude = 0f;
            float3 impulseDirection = float3.zero;

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
                break;
            }

            foreach (var crashState in SystemAPI.Query<RefRO<CrashState>>().WithAll<PlayerVehicleTag>())
            {
                isCrashed = crashState.ValueRO.IsCrashed;
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

            // =============================================================
            // Impact Recoil
            // =============================================================

            if (impulseMagnitude > 0.1f)
            {
                // Add recoil in direction of impact
                float recoilAmount = math.min(impulseMagnitude * 0.1f, ImpactRecoilMax);
                _recoilOffset += impulseDirection * recoilAmount;
            }

            // Decay recoil (critically damped)
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
            // Update Camera State
            // =============================================================

            foreach (var camera in SystemAPI.Query<RefRW<CameraState>>())
            {
                // Calculate speed-dependent parameters
                float speedNorm = math.saturate(playerSpeed / SpeedForMaxFOV);

                // =============================================================
                // Crash Camera Mode
                // =============================================================

                if (isCrashed)
                {
                    camera.ValueRW.Mode = CameraMode.Crash;

                    // Zoom out
                    float targetDist = CrashMaxDistance;
                    camera.ValueRW.FollowDistance = math.lerp(
                        camera.ValueRO.FollowDistance,
                        targetDist,
                        CrashZoomSpeed * deltaTime
                    );

                    // Rise up
                    camera.ValueRW.FollowHeight = math.lerp(
                        camera.ValueRO.FollowHeight,
                        BaseHeight * 2f,
                        CrashZoomSpeed * deltaTime
                    );
                }
                // =============================================================
                // Drift Camera Mode
                // =============================================================
                else if (isDrifting && math.abs(yawOffset) > 0.3f)
                {
                    camera.ValueRW.Mode = CameraMode.Drift;

                    // Lateral offset based on drift direction
                    float driftSign = math.sign(yawOffset);
                    camera.ValueRW.LateralOffset = math.lerp(
                        camera.ValueRO.LateralOffset,
                        driftSign * DriftOffsetMax,
                        3f * deltaTime
                    );

                    // Roll camera into the drift
                    camera.ValueRW.Roll = math.lerp(
                        camera.ValueRO.Roll,
                        -driftSign * math.radians(DriftTiltMax),
                        4f * deltaTime
                    );

                    // Slightly wider FOV during drift
                    camera.ValueRW.FOV = math.lerp(camera.ValueRO.FOV, MaxFOV, 2f * deltaTime);
                }
                // =============================================================
                // Normal Follow Mode
                // =============================================================
                else
                {
                    camera.ValueRW.Mode = CameraMode.Follow;

                    // Speed-dependent distance
                    float targetDist = math.lerp(BaseDistance, MaxDistance, speedNorm);
                    camera.ValueRW.FollowDistance = math.lerp(
                        camera.ValueRO.FollowDistance,
                        targetDist,
                        FollowSmoothing * deltaTime
                    );

                    // Reset height
                    camera.ValueRW.FollowHeight = math.lerp(
                        camera.ValueRO.FollowHeight,
                        BaseHeight,
                        FollowSmoothing * deltaTime
                    );

                    // Reset lateral offset and roll
                    camera.ValueRW.LateralOffset = math.lerp(camera.ValueRO.LateralOffset, 0f, 4f * deltaTime);
                    camera.ValueRW.Roll = math.lerp(camera.ValueRO.Roll, 0f, 4f * deltaTime);

                    // Speed-dependent FOV
                    float targetFOV = math.lerp(BaseFOV, MaxFOV, speedNorm);
                    camera.ValueRW.FOV = math.lerp(camera.ValueRO.FOV, targetFOV, 3f * deltaTime);
                }

                // =============================================================
                // Calculate Camera Transform
                // =============================================================

                float3 forward = math.mul(playerRot, new float3(0, 0, 1));
                float3 right = math.mul(playerRot, new float3(1, 0, 0));
                float3 up = new float3(0, 1, 0);

                float3 targetPos = playerPos
                    - forward * camera.ValueRO.FollowDistance
                    + up * camera.ValueRO.FollowHeight
                    + right * camera.ValueRO.LateralOffset;

                // Apply impact recoil offset
                targetPos += _recoilOffset;

                // Apply damage wobble (horizontal sway)
                targetPos += right * wobbleAmount;

                // Smooth follow (critically damped)
                camera.ValueRW.Position = math.lerp(
                    camera.ValueRO.Position,
                    targetPos,
                    FollowSmoothing * deltaTime
                );

                // Look at player (slightly ahead)
                float3 lookTarget = playerPos + forward * 10f;
                float3 lookDir = math.normalize(lookTarget - camera.ValueRO.Position);

                quaternion targetRot = quaternion.LookRotation(lookDir, up);

                // Apply roll from drift + damage wobble
                float totalRoll = camera.ValueRO.Roll + wobbleAmount * 0.5f;
                if (math.abs(totalRoll) > 0.001f)
                {
                    quaternion rollRot = quaternion.RotateZ(totalRoll);
                    targetRot = math.mul(targetRot, rollRot);
                }

                // Drift whip: extra yaw follow during drift
                if (isDrifting && math.abs(yawRate) > 0.1f)
                {
                    float whipYaw = yawRate * DriftWhipMultiplier * 0.1f;
                    quaternion whipRot = quaternion.RotateY(whipYaw);
                    targetRot = math.mul(targetRot, whipRot);
                }

                camera.ValueRW.Rotation = math.slerp(
                    camera.ValueRO.Rotation,
                    targetRot,
                    LookSmoothing * deltaTime
                );
            }
        }
    }

    public enum CameraMode
    {
        Follow,
        Drift,
        Crash,
        Replay
    }
}
