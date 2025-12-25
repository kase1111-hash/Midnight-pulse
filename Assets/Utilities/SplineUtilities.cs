// ============================================================================
// Nightflow - Spline Utilities
// Hermite spline evaluation and frame generation
// ============================================================================

using Unity.Mathematics;
using Unity.Burst;

namespace Nightflow.Utilities
{
    /// <summary>
    /// Static utilities for Hermite spline operations.
    /// Used by TrackGenerationSystem and LaneMagnetismSystem.
    /// </summary>
    [BurstCompile]
    public static class SplineUtilities
    {
        /// <summary>
        /// Evaluates cubic Hermite spline position at parameter t.
        /// S(t) = (2t³-3t²+1)P₀ + (t³-2t²+t)T₀ + (-2t³+3t²)P₁ + (t³-t²)T₁
        /// </summary>
        [BurstCompile]
        public static float3 EvaluatePosition(float3 p0, float3 t0, float3 p1, float3 t1, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float h00 = 2f * t3 - 3f * t2 + 1f;   // (2t³-3t²+1)
            float h10 = t3 - 2f * t2 + t;          // (t³-2t²+t)
            float h01 = -2f * t3 + 3f * t2;        // (-2t³+3t²)
            float h11 = t3 - t2;                   // (t³-t²)

            return h00 * p0 + h10 * t0 + h01 * p1 + h11 * t1;
        }

        /// <summary>
        /// Evaluates cubic Hermite spline first derivative (tangent) at parameter t.
        /// S'(t) = (6t²-6t)P₀ + (3t²-4t+1)T₀ + (-6t²+6t)P₁ + (3t²-2t)T₁
        /// </summary>
        [BurstCompile]
        public static float3 EvaluateTangent(float3 p0, float3 t0, float3 p1, float3 t1, float t)
        {
            float t2 = t * t;

            float h00 = 6f * t2 - 6f * t;          // (6t²-6t)
            float h10 = 3f * t2 - 4f * t + 1f;     // (3t²-4t+1)
            float h01 = -6f * t2 + 6f * t;         // (-6t²+6t)
            float h11 = 3f * t2 - 2f * t;          // (3t²-2t)

            return h00 * p0 + h10 * t0 + h01 * p1 + h11 * t1;
        }

        /// <summary>
        /// Evaluates cubic Hermite spline second derivative at parameter t.
        /// S''(t) = (12t-6)P₀ + (6t-4)T₀ + (-12t+6)P₁ + (6t-2)T₁
        /// </summary>
        [BurstCompile]
        public static float3 EvaluateSecondDerivative(float3 p0, float3 t0, float3 p1, float3 t1, float t)
        {
            float h00 = 12f * t - 6f;              // (12t-6)
            float h10 = 6f * t - 4f;               // (6t-4)
            float h01 = -12f * t + 6f;             // (-12t+6)
            float h11 = 6f * t - 2f;               // (6t-2)

            return h00 * p0 + h10 * t0 + h01 * p1 + h11 * t1;
        }

        /// <summary>
        /// Computes curvature at parameter t.
        /// κ(t) = ‖S'(t) × S''(t)‖ / ‖S'(t)‖³
        /// </summary>
        [BurstCompile]
        public static float EvaluateCurvature(float3 p0, float3 t0, float3 p1, float3 t1, float t)
        {
            float3 d1 = EvaluateTangent(p0, t0, p1, t1, t);
            float3 d2 = EvaluateSecondDerivative(p0, t0, p1, t1, t);

            float3 cross = math.cross(d1, d2);
            float crossMag = math.length(cross);
            float d1Mag = math.length(d1);

            if (d1Mag < 0.0001f) return 0f;

            return crossMag / (d1Mag * d1Mag * d1Mag);
        }

        /// <summary>
        /// Builds orthonormal Frenet frame at spline point.
        /// Forward: F(t) = normalize(S'(t))
        /// Right: R(t) = normalize(F(t) × Up)
        /// Up': U'(t) = R(t) × F(t)
        /// </summary>
        [BurstCompile]
        public static void BuildFrenetFrame(float3 tangent, out float3 forward, out float3 right, out float3 up)
        {
            forward = math.normalizesafe(tangent, new float3(0, 0, 1));

            // Use world up as reference
            float3 worldUp = new float3(0, 1, 0);

            // Handle near-vertical tangents
            if (math.abs(math.dot(forward, worldUp)) > 0.99f)
            {
                worldUp = new float3(0, 0, 1);
            }

            right = math.normalize(math.cross(worldUp, forward));
            up = math.cross(forward, right);
        }

        /// <summary>
        /// Builds frame at specific spline parameter.
        /// </summary>
        [BurstCompile]
        public static void BuildFrameAtT(float3 p0, float3 t0, float3 p1, float3 t1, float t,
                                         out float3 position, out float3 forward, out float3 right, out float3 up)
        {
            position = EvaluatePosition(p0, t0, p1, t1, t);
            float3 tangent = EvaluateTangent(p0, t0, p1, t1, t);
            BuildFrenetFrame(tangent, out forward, out right, out up);
        }

        /// <summary>
        /// Gets lane center position offset from spline center.
        /// S_i(t) = S(t) + R(t) × (i × w)
        /// </summary>
        [BurstCompile]
        public static float3 GetLanePosition(float3 splinePos, float3 right, int laneIndex, float laneWidth)
        {
            // Lane 0 = leftmost, lanes increase to the right
            // Center of 4 lanes: indices 0,1,2,3 → offsets -1.5, -0.5, 0.5, 1.5 lane widths
            float offset = (laneIndex - 1.5f) * laneWidth;
            return splinePos + right * offset;
        }

        /// <summary>
        /// Smoothstep interpolation for lane changes.
        /// λ(t) = 3t² - 2t³
        /// </summary>
        [BurstCompile]
        public static float Smoothstep(float t)
        {
            t = math.saturate(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Approximate arc length of spline segment using numerical integration.
        /// </summary>
        [BurstCompile]
        public static float ApproximateArcLength(float3 p0, float3 t0, float3 p1, float3 t1, int samples = 16)
        {
            float length = 0f;
            float3 prevPos = p0;

            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                float3 pos = EvaluatePosition(p0, t0, p1, t1, t);
                length += math.distance(prevPos, pos);
                prevPos = pos;
            }

            return length;
        }

        /// <summary>
        /// Finds parameter t for a given arc length along the spline.
        /// Uses binary search for accuracy.
        /// </summary>
        [BurstCompile]
        public static float ArcLengthToParameter(float3 p0, float3 t0, float3 p1, float3 t1,
                                                  float targetLength, float totalLength, int iterations = 8)
        {
            if (targetLength <= 0) return 0f;
            if (targetLength >= totalLength) return 1f;

            float tLow = 0f;
            float tHigh = 1f;

            for (int i = 0; i < iterations; i++)
            {
                float tMid = (tLow + tHigh) * 0.5f;
                float lengthAtMid = ApproximateArcLength(p0, t0, p1, t1, 8) * tMid; // Approximation

                if (lengthAtMid < targetLength)
                    tLow = tMid;
                else
                    tHigh = tMid;
            }

            return (tLow + tHigh) * 0.5f;
        }

        /// <summary>
        /// Generates deterministic random value from seed and index.
        /// </summary>
        [BurstCompile]
        public static uint Hash(uint seed, uint index)
        {
            uint hash = seed;
            hash ^= index;
            hash *= 0x85ebca6b;
            hash ^= hash >> 13;
            hash *= 0xc2b2ae35;
            hash ^= hash >> 16;
            return hash;
        }

        /// <summary>
        /// Converts hash to float in range [0, 1].
        /// </summary>
        [BurstCompile]
        public static float HashToFloat(uint hash)
        {
            return (hash & 0x7FFFFF) / (float)0x7FFFFF;
        }

        /// <summary>
        /// Converts hash to float in range [min, max].
        /// </summary>
        [BurstCompile]
        public static float HashToFloatRange(uint hash, float min, float max)
        {
            return min + HashToFloat(hash) * (max - min);
        }
    }
}
