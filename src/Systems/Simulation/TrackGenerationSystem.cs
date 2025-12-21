// ============================================================================
// Nightflow - Track Generation System
// Execution Order: 1 (Simulation Group - runs first)
// ============================================================================

using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;
using Nightflow.Utilities;

namespace Nightflow.Systems
{
    /// <summary>
    /// Generates procedural freeway segments using Hermite splines.
    /// Maintains buffer of segments ahead of player, culls behind.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TrackGenerationSystem : ISystem
    {
        // Generation parameters
        private const float SegmentLength = 200f;         // meters per segment
        private const int SegmentsAhead = 5;              // buffer ahead of player
        private const int SegmentsBehind = 2;             // keep behind before cull
        private const float LaneWidth = 3.6f;             // meters
        private const int NumLanes = 4;

        // Curvature limits
        private const float MinCurveRadius = 300f;        // minimum turn radius
        private const float MaxYawChange = 0.15f;         // max yaw per segment (radians)
        private const float MaxPitchChange = 0.02f;       // max pitch change

        // Spline parameters
        private const float TangentAlpha = 0.5f;          // Hermite tangent scale
        private const int SamplesPerSegment = 20;         // spline resolution

        // Difficulty scaling
        private const float BaseDifficulty = 0.3f;
        private const float DifficultyPerKm = 0.1f;

        private uint _globalSeed;
        private int _nextSegmentIndex;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _globalSeed = 12345u;
            _nextSegmentIndex = 0;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get player position for generation decisions
            float playerZ = 0f;
            foreach (var transform in SystemAPI.Query<RefRO<WorldTransform>>().WithAll<PlayerVehicleTag>())
            {
                playerZ = transform.ValueRO.Position.z;
                break;
            }

            // =============================================================
            // Find Current Track State
            // =============================================================

            float furthestZ = 0f;
            Entity furthestSegment = Entity.Null;
            HermiteSpline furthestSpline = default;
            int highestIndex = -1;

            foreach (var (segment, spline, entity) in
                SystemAPI.Query<RefRO<TrackSegment>, RefRO<HermiteSpline>>()
                    .WithAll<TrackSegmentTag>()
                    .WithEntityAccess())
            {
                if (segment.ValueRO.Index > highestIndex)
                {
                    highestIndex = segment.ValueRO.Index;
                    furthestZ = segment.ValueRO.EndZ;
                    furthestSegment = entity;
                    furthestSpline = spline.ValueRO;
                }
            }

            // Update next segment index
            if (highestIndex >= _nextSegmentIndex)
            {
                _nextSegmentIndex = highestIndex + 1;
            }

            // =============================================================
            // Generate New Segments Ahead
            // =============================================================

            float targetZ = playerZ + SegmentsAhead * SegmentLength;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            while (furthestZ < targetZ && furthestSegment != Entity.Null)
            {
                // Get end state of furthest segment
                float3 startPos = furthestSpline.P1;
                float3 startTangent = SplineUtilities.EvaluateTangent(
                    furthestSpline.P0, furthestSpline.T0,
                    furthestSpline.P1, furthestSpline.T1, 1f);
                startTangent = math.normalizesafe(startTangent);

                // Generate new segment
                furthestSpline = GenerateSegment(
                    ref ecb,
                    _nextSegmentIndex,
                    startPos,
                    startTangent,
                    furthestZ
                );

                furthestZ += SegmentLength;
                _nextSegmentIndex++;
            }

            // =============================================================
            // Cull Segments Behind Player
            // =============================================================

            float cullZ = playerZ - SegmentsBehind * SegmentLength;

            foreach (var (segment, entity) in
                SystemAPI.Query<RefRO<TrackSegment>>()
                    .WithAll<TrackSegmentTag>()
                    .WithEntityAccess())
            {
                if (segment.ValueRO.EndZ < cullZ)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private HermiteSpline GenerateSegment(
            ref EntityCommandBuffer ecb,
            int segmentIndex,
            float3 startPos,
            float3 startTangent,
            float currentZ)
        {
            // =============================================================
            // Deterministic Random Generation
            // =============================================================

            uint segHash = SplineUtilities.Hash(_globalSeed, (uint)segmentIndex);

            // Calculate difficulty based on distance
            float distanceKm = currentZ / 1000f;
            float difficulty = math.saturate(BaseDifficulty + DifficultyPerKm * distanceKm);

            // =============================================================
            // Generate Curve Parameters
            // =============================================================

            // Yaw change (horizontal curve)
            float yawChange = SplineUtilities.HashToFloatRange(
                SplineUtilities.Hash(segHash, 1),
                -MaxYawChange * difficulty,
                MaxYawChange * difficulty
            );

            // Pitch change (vertical curve) - less common
            float pitchChange = 0f;
            if (SplineUtilities.HashToFloat(SplineUtilities.Hash(segHash, 2)) > 0.7f)
            {
                pitchChange = SplineUtilities.HashToFloatRange(
                    SplineUtilities.Hash(segHash, 3),
                    -MaxPitchChange,
                    MaxPitchChange
                );
            }

            // Segment length variation
            float lengthVariation = SplineUtilities.HashToFloatRange(
                SplineUtilities.Hash(segHash, 4),
                0.8f, 1.2f
            );
            float length = SegmentLength * lengthVariation;

            // =============================================================
            // Calculate Spline Points
            // =============================================================

            // Apply rotation to get end tangent direction
            quaternion yawRot = quaternion.RotateY(yawChange);
            quaternion pitchRot = quaternion.RotateX(pitchChange);
            quaternion totalRot = math.mul(yawRot, pitchRot);

            float3 endTangentDir = math.mul(totalRot, startTangent);

            // Calculate end position
            // Use average of start and end directions for smooth curves
            float3 avgDir = math.normalize(startTangent + endTangentDir);
            float3 endPos = startPos + avgDir * length;

            // Scale tangents for Hermite spline
            float3 t0 = startTangent * length * TangentAlpha;
            float3 t1 = endTangentDir * length * TangentAlpha;

            // =============================================================
            // Validate Curvature
            // =============================================================

            float maxCurvature = 0f;
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float curvature = SplineUtilities.EvaluateCurvature(startPos, t0, endPos, t1, t);
                maxCurvature = math.max(maxCurvature, curvature);
            }

            // If curvature too high, reduce the turn
            if (maxCurvature > 1f / MinCurveRadius)
            {
                float reductionFactor = (1f / MinCurveRadius) / maxCurvature;
                yawChange *= reductionFactor;

                // Recalculate
                yawRot = quaternion.RotateY(yawChange);
                totalRot = math.mul(yawRot, pitchRot);
                endTangentDir = math.mul(totalRot, startTangent);
                avgDir = math.normalize(startTangent + endTangentDir);
                endPos = startPos + avgDir * length;
                t0 = startTangent * length * TangentAlpha;
                t1 = endTangentDir * length * TangentAlpha;
            }

            // =============================================================
            // Create Segment Entity
            // =============================================================

            Entity segmentEntity = ecb.CreateEntity();

            // Determine segment type
            int segmentType = 0; // Straight
            if (math.abs(yawChange) > 0.05f) segmentType = 1; // Curve

            ecb.AddComponent(segmentEntity, new TrackSegment
            {
                Index = segmentIndex,
                Type = segmentType,
                StartZ = startPos.z,
                EndZ = endPos.z,
                Length = length,
                NumLanes = NumLanes,
                Difficulty = difficulty
            });

            var spline = new HermiteSpline
            {
                P0 = startPos,
                P1 = endPos,
                T0 = t0,
                T1 = t1
            };
            ecb.AddComponent(segmentEntity, spline);

            ecb.AddComponent(segmentEntity, new WorldTransform
            {
                Position = startPos,
                Rotation = quaternion.LookRotation(startTangent, new float3(0, 1, 0))
            });

            // =============================================================
            // Pre-sample Spline for Fast Queries
            // =============================================================

            var buffer = ecb.AddBuffer<SplineSample>(segmentEntity);

            for (int i = 0; i <= SamplesPerSegment; i++)
            {
                float t = i / (float)SamplesPerSegment;

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

            return spline;
        }
    }
}
