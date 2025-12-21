// ============================================================================
// Nightflow - Game Bootstrap System
// Runs once at startup to create initial game state
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;
using Nightflow.Utilities;

namespace Nightflow.Systems
{
    /// <summary>
    /// Initializes the game world with player vehicle, initial track segments,
    /// and camera. Runs once in InitializationSystemGroup.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct GameBootstrapSystem : ISystem
    {
        private bool _initialized;

        // Track parameters
        private const int InitialSegments = 5;
        private const float SegmentLength = 200f;
        private const int LanesPerSegment = 4;
        private const float LaneWidth = 3.6f;

        // Player spawn
        private const float PlayerStartZ = 50f;
        private const int PlayerStartLane = 1;

        public void OnCreate(ref SystemState state)
        {
            _initialized = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized) return;
            _initialized = true;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // =============================================================
            // Create Initial Track Segments
            // =============================================================

            uint globalSeed = 12345u;
            float3 currentPos = float3.zero;
            float3 currentTangent = new float3(0, 0, 1); // Start heading forward

            for (int seg = 0; seg < InitialSegments; seg++)
            {
                Entity segmentEntity = CreateTrackSegment(
                    ref ecb,
                    ref state,
                    seg,
                    globalSeed,
                    currentPos,
                    currentTangent,
                    out float3 endPos,
                    out float3 endTangent
                );

                currentPos = endPos;
                currentTangent = endTangent;
            }

            // =============================================================
            // Create Player Vehicle
            // =============================================================

            Entity playerEntity = ecb.CreateEntity();

            // Core transform - spawn on starting lane
            float3 playerPos = new float3((PlayerStartLane - 1.5f) * LaneWidth, 0.5f, PlayerStartZ);
            ecb.AddComponent(playerEntity, new WorldTransform
            {
                Position = playerPos,
                Rotation = quaternion.identity
            });

            ecb.AddComponent(playerEntity, new Velocity
            {
                Forward = 20f,  // Start moving
                Lateral = 0f,
                Angular = 0f
            });

            ecb.AddComponent(playerEntity, new PreviousTransform
            {
                Position = playerPos,
                Rotation = quaternion.identity
            });

            // Vehicle control
            ecb.AddComponent(playerEntity, new PlayerInput());
            ecb.AddComponent(playerEntity, new Autopilot { Enabled = false });
            ecb.AddComponent(playerEntity, new SteeringState
            {
                CurrentAngle = 0f,
                TargetAngle = 0f,
                Smoothness = 8f
            });
            ecb.AddComponent(playerEntity, new LaneTransition());
            ecb.AddComponent(playerEntity, new DriftState());
            ecb.AddComponent(playerEntity, new SpeedTier { Tier = 0, Multiplier = 1f });

            // Lane following
            ecb.AddComponent(playerEntity, new LaneFollower
            {
                CurrentLane = PlayerStartLane,
                TargetLane = PlayerStartLane,
                LateralOffset = 0f,
                MagnetStrength = 1f,
                SplineT = PlayerStartZ / SegmentLength
            });

            // Damage & crash
            ecb.AddComponent(playerEntity, new DamageState());
            ecb.AddComponent(playerEntity, new Crashable
            {
                CrashThreshold = 1f,
                CrashSpeed = 50f,
                YawFailThreshold = 2.5f
            });
            ecb.AddComponent(playerEntity, new CollisionShape
            {
                HalfExtents = new float3(1f, 0.6f, 2.2f)
            });
            ecb.AddComponent(playerEntity, new CollisionEvent());
            ecb.AddComponent(playerEntity, new ImpulseData());
            ecb.AddComponent(playerEntity, new CrashState());

            // Scoring
            ecb.AddComponent(playerEntity, new ScoreSession
            {
                Active = true,
                Distance = 0f,
                Score = 0f,
                Multiplier = 1f,
                RiskMultiplier = 0f,
                HighestMultiplier = 1f
            });
            ecb.AddComponent(playerEntity, new RiskState
            {
                Value = 0f,
                Cap = 1f,
                BrakePenaltyActive = false
            });

            // Signaling & detection
            ecb.AddComponent(playerEntity, new LightEmitter
            {
                Color = new float4(0f, 1f, 0.8f, 1f), // Cyan
                Intensity = 1f
            });
            ecb.AddComponent(playerEntity, new EmergencyDetection());

            // Tags
            ecb.AddComponent<PlayerVehicleTag>(playerEntity);

            // Input log buffer for replay
            ecb.AddBuffer<InputLogEntry>(playerEntity);

            // =============================================================
            // Create Camera
            // =============================================================

            Entity cameraEntity = ecb.CreateEntity();

            ecb.AddComponent(cameraEntity, new CameraState
            {
                Position = playerPos - new float3(0, -3f, 8f),
                Rotation = quaternion.identity,
                FOV = 60f,
                FollowDistance = 8f,
                FollowHeight = 3f,
                LateralOffset = 0f,
                Roll = 0f,
                Mode = 0
            });

            ecb.AddComponent(cameraEntity, new WorldTransform
            {
                Position = playerPos - new float3(0, -3f, 8f),
                Rotation = quaternion.identity
            });

            // =============================================================
            // Create Score Summary (singleton for UI)
            // =============================================================

            Entity scoreEntity = ecb.CreateEntity();
            ecb.AddComponent(scoreEntity, new ScoreSummary());

            // Playback command buffer
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private Entity CreateTrackSegment(
            ref EntityCommandBuffer ecb,
            ref SystemState state,
            int segmentIndex,
            uint globalSeed,
            float3 startPos,
            float3 startTangent,
            out float3 endPos,
            out float3 endTangent)
        {
            // Generate segment parameters deterministically
            uint segHash = SplineUtilities.Hash(globalSeed, (uint)segmentIndex);

            // For initial segments, keep them straight
            float yawChange = 0f;
            if (segmentIndex > 2)
            {
                // Add gentle curves after first few segments
                yawChange = SplineUtilities.HashToFloatRange(
                    SplineUtilities.Hash(segHash, 0),
                    -0.1f, 0.1f
                );
            }

            // Apply yaw rotation to tangent
            quaternion yawRot = quaternion.RotateY(yawChange);
            endTangent = math.mul(yawRot, startTangent);

            // Calculate end position
            float length = SegmentLength;
            endPos = startPos + math.normalize(startTangent) * length;

            // Scale tangents for Hermite (alpha = 0.5)
            float alpha = 0.5f;
            float3 t0 = startTangent * length * alpha;
            float3 t1 = endTangent * length * alpha;

            // Create segment entity
            Entity segmentEntity = ecb.CreateEntity();

            ecb.AddComponent(segmentEntity, new TrackSegment
            {
                Index = segmentIndex,
                Type = 0, // Straight
                StartZ = startPos.z,
                EndZ = endPos.z,
                Length = length,
                NumLanes = LanesPerSegment
            });

            ecb.AddComponent(segmentEntity, new HermiteSpline
            {
                P0 = startPos,
                P1 = endPos,
                T0 = t0,
                T1 = t1
            });

            ecb.AddComponent(segmentEntity, new WorldTransform
            {
                Position = startPos,
                Rotation = quaternion.LookRotation(startTangent, new float3(0, 1, 0))
            });

            // Add spline sample buffer
            var buffer = ecb.AddBuffer<SplineSample>(segmentEntity);

            // Pre-sample spline at regular intervals
            const int samples = 20;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;

                SplineUtilities.BuildFrameAtT(startPos, t0, endPos, t1, t,
                    out float3 position, out float3 forward, out float3 right, out float3 up);

                buffer.Add(new SplineSample
                {
                    Position = position,
                    Forward = forward,
                    Right = right,
                    Up = up,
                    T = t
                });
            }

            ecb.AddComponent<TrackSegmentTag>(segmentEntity);

            return segmentEntity;
        }
    }
}
