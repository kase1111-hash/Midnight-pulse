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

namespace Nightflow.Systems
{
    /// <summary>
    /// Generates procedural freeway segments using Hermite splines.
    /// Maintains buffer of segments ahead of camera, culls behind.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TrackGenerationSystem : ISystem
    {
        // Generation parameters
        private const float SegmentLength = 200f;         // meters per segment
        private const int SegmentsAhead = 5;              // buffer ahead of camera
        private const int SegmentsBehind = 2;             // keep behind before cull
        private const float MinCurveRadius = 300f;        // minimum turn radius
        private const float MaxCurvature = 0.003f;        // 1/MinRadius

        // Spline parameters
        private const float TangentScale = 0.5f;          // Hermite tangent multiplier
        private const int SamplesPerSegment = 20;         // spline resolution

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TrackSegment>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get camera position for LOD decisions
            float3 cameraPos = float3.zero;
            foreach (var camera in SystemAPI.Query<RefRO<CameraState>>())
            {
                cameraPos = camera.ValueRO.Position;
                break;
            }

            // =============================================================
            // Generate New Segments Ahead
            // =============================================================

            float furthestZ = 0f;
            Entity lastSegment = Entity.Null;

            foreach (var (segment, entity) in
                SystemAPI.Query<RefRO<TrackSegment>>()
                    .WithEntityAccess())
            {
                if (segment.ValueRO.EndZ > furthestZ)
                {
                    furthestZ = segment.ValueRO.EndZ;
                    lastSegment = entity;
                }
            }

            float targetZ = cameraPos.z + SegmentsAhead * SegmentLength;

            // TODO: Generate segments up to targetZ
            // while (furthestZ < targetZ)
            // {
            //     GenerateSegment(ref state, furthestZ, ref lastSegment);
            //     furthestZ += SegmentLength;
            // }

            // =============================================================
            // Cull Segments Behind
            // =============================================================

            float cullZ = cameraPos.z - SegmentsBehind * SegmentLength;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (segment, entity) in
                SystemAPI.Query<RefRO<TrackSegment>>()
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

        /// <summary>
        /// Evaluates Hermite spline at parameter t.
        /// P(t) = (2t³ - 3t² + 1)P₀ + (t³ - 2t² + t)T₀ + (-2t³ + 3t²)P₁ + (t³ - t²)T₁
        /// </summary>
        private static float3 EvaluateHermite(float3 p0, float3 t0, float3 p1, float3 t1, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float h00 = 2f * t3 - 3f * t2 + 1f;
            float h10 = t3 - 2f * t2 + t;
            float h01 = -2f * t3 + 3f * t2;
            float h11 = t3 - t2;

            return h00 * p0 + h10 * t0 + h01 * p1 + h11 * t1;
        }

        /// <summary>
        /// Evaluates Hermite spline tangent at parameter t.
        /// P'(t) = (6t² - 6t)P₀ + (3t² - 4t + 1)T₀ + (-6t² + 6t)P₁ + (3t² - 2t)T₁
        /// </summary>
        private static float3 EvaluateHermiteTangent(float3 p0, float3 t0, float3 p1, float3 t1, float t)
        {
            float t2 = t * t;

            float h00 = 6f * t2 - 6f * t;
            float h10 = 3f * t2 - 4f * t + 1f;
            float h01 = -6f * t2 + 6f * t;
            float h11 = 3f * t2 - 2f * t;

            return h00 * p0 + h10 * t0 + h01 * p1 + h11 * t1;
        }

        /// <summary>
        /// Builds orthonormal Frenet frame at spline point.
        /// </summary>
        private static void BuildFrenetFrame(float3 tangent, out float3 forward, out float3 right, out float3 up)
        {
            forward = math.normalize(tangent);
            up = new float3(0, 1, 0);
            right = math.normalize(math.cross(up, forward));
            up = math.cross(forward, right);
        }
    }
}
