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

        // FOV parameters
        private const float BaseFOV = 60f;                // degrees at low speed
        private const float MaxFOV = 75f;                 // degrees at max speed
        private const float SpeedForMaxFOV = 70f;         // m/s

        // Drift camera
        private const float DriftOffsetMax = 2f;          // lateral offset when drifting
        private const float DriftTiltMax = 10f;           // degrees of roll

        // Crash camera
        private const float CrashZoomSpeed = 2f;          // zoom out speed
        private const float CrashMaxDistance = 20f;       // max zoom distance
        private const float CrashSlowMotion = 0.3f;       // time scale

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get player state
            float3 playerPos = float3.zero;
            quaternion playerRot = quaternion.identity;
            float playerSpeed = 0f;
            float yawOffset = 0f;
            bool isDrifting = false;
            bool isCrashed = false;

            foreach (var (transform, velocity, driftState) in
                SystemAPI.Query<RefRO<WorldTransform>, RefRO<Velocity>, RefRO<DriftState>>()
                    .WithAll<PlayerVehicleTag>())
            {
                playerPos = transform.ValueRO.Position;
                playerRot = transform.ValueRO.Rotation;
                playerSpeed = velocity.ValueRO.Forward;
                yawOffset = driftState.ValueRO.YawOffset;
                isDrifting = driftState.ValueRO.IsDrifting;
                break;
            }

            foreach (var crashState in SystemAPI.Query<RefRO<CrashState>>().WithAll<PlayerVehicleTag>())
            {
                isCrashed = crashState.ValueRO.IsCrashed;
                break;
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

                float3 targetPos = playerPos
                    - forward * camera.ValueRO.FollowDistance
                    + new float3(0, camera.ValueRO.FollowHeight, 0)
                    + right * camera.ValueRO.LateralOffset;

                // Smooth follow
                camera.ValueRW.Position = math.lerp(
                    camera.ValueRO.Position,
                    targetPos,
                    FollowSmoothing * deltaTime
                );

                // Look at player (slightly ahead)
                float3 lookTarget = playerPos + forward * 10f;
                float3 lookDir = math.normalize(lookTarget - camera.ValueRO.Position);

                quaternion targetRot = quaternion.LookRotation(lookDir, new float3(0, 1, 0));

                // Apply roll
                if (math.abs(camera.ValueRO.Roll) > 0.001f)
                {
                    quaternion rollRot = quaternion.RotateZ(camera.ValueRO.Roll);
                    targetRot = math.mul(targetRot, rollRot);
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
