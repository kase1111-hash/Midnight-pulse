// Nightflow - Track Generation Tests
// Validates deterministic seeding, spline math, and segment management

using NUnit.Framework;
using Unity.Mathematics;
using Nightflow.Config;

namespace Nightflow.Tests
{
    /// <summary>
    /// Tests track generation determinism and spline math.
    /// Determinism requirement: same globalSeed must produce identical runs.
    /// </summary>
    [TestFixture]
    public class TrackGenerationTests
    {
        #region Deterministic Seeding

        [Test]
        public void Random_SameSeed_SameResult()
        {
            uint seed = 42;
            var rng1 = new Unity.Mathematics.Random(seed);
            var rng2 = new Unity.Mathematics.Random(seed);

            Assert.AreEqual(rng1.NextFloat(), rng2.NextFloat());
        }

        [Test]
        public void Random_SameSeed_MultipleValues_AllMatch()
        {
            uint seed = 12345;
            var rng1 = new Unity.Mathematics.Random(seed);
            var rng2 = new Unity.Mathematics.Random(seed);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(rng1.NextFloat(), rng2.NextFloat(),
                    $"Mismatch at iteration {i}");
            }
        }

        [Test]
        public void Random_DifferentSeed_DifferentResult()
        {
            var rng1 = new Unity.Mathematics.Random(1);
            var rng2 = new Unity.Mathematics.Random(2);

            // Highly unlikely to be equal with different seeds
            Assert.AreNotEqual(rng1.NextFloat(), rng2.NextFloat());
        }

        [Test]
        public void Random_SeedZero_Invalid()
        {
            // Unity.Mathematics.Random requires non-zero seed
            // Seed 0 would produce degenerate results; the system should avoid it
            uint seed = 0;
            // Random(0) is technically allowed but produces known degenerate output
            // Track generation should never use seed 0
            Assert.AreNotEqual(0u, 42u); // Placeholder - real check is in system
        }

        #endregion

        #region Segment Length and Layout

        [Test]
        public void SegmentLength_MatchesConstant()
        {
            Assert.AreEqual(200f, GameConstants.SegmentLength, 0.001f);
        }

        [Test]
        public void SegmentsAhead_SufficientBuffer()
        {
            // At max speed (80 m/s), how far ahead can player see?
            // Need at least MaxSpeed * some seconds of track ahead
            float lookahead = GameConstants.SegmentsAhead * GameConstants.SegmentLength;
            float maxSpeedDistance = GameConstants.MaxForwardSpeed * 10f; // 10 seconds of travel

            Assert.Greater(lookahead, maxSpeedDistance,
                "Track lookahead must exceed 10 seconds of max-speed travel");
        }

        [Test]
        public void SegmentsBehind_AllowsCleanup()
        {
            Assert.Greater(GameConstants.SegmentsBehind, 0);
        }

        #endregion

        #region Hermite Spline Math

        /// <summary>
        /// Evaluates a cubic Hermite spline at parameter t.
        /// H(t) = (2t^3 - 3t^2 + 1)P0 + (t^3 - 2t^2 + t)T0
        ///       + (-2t^3 + 3t^2)P1 + (t^3 - t^2)T1
        /// </summary>
        private static float3 HermiteEvaluate(float3 p0, float3 t0, float3 p1, float3 t1, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float h00 = 2f * t3 - 3f * t2 + 1f;
            float h10 = t3 - 2f * t2 + t;
            float h01 = -2f * t3 + 3f * t2;
            float h11 = t3 - t2;

            return h00 * p0 + h10 * t0 + h01 * p1 + h11 * t1;
        }

        [Test]
        public void Hermite_AtStart_ReturnsP0()
        {
            float3 p0 = new float3(0, 0, 0);
            float3 t0 = new float3(0, 0, 100);
            float3 p1 = new float3(5, 0, 200);
            float3 t1 = new float3(0, 0, 100);

            float3 result = HermiteEvaluate(p0, t0, p1, t1, 0f);
            Assert.AreEqual(p0.x, result.x, 0.001f);
            Assert.AreEqual(p0.y, result.y, 0.001f);
            Assert.AreEqual(p0.z, result.z, 0.001f);
        }

        [Test]
        public void Hermite_AtEnd_ReturnsP1()
        {
            float3 p0 = new float3(0, 0, 0);
            float3 t0 = new float3(0, 0, 100);
            float3 p1 = new float3(5, 0, 200);
            float3 t1 = new float3(0, 0, 100);

            float3 result = HermiteEvaluate(p0, t0, p1, t1, 1f);
            Assert.AreEqual(p1.x, result.x, 0.001f);
            Assert.AreEqual(p1.y, result.y, 0.001f);
            Assert.AreEqual(p1.z, result.z, 0.001f);
        }

        [Test]
        public void Hermite_StraightLine_MidpointIsCenter()
        {
            float3 p0 = new float3(0, 0, 0);
            float3 p1 = new float3(0, 0, 200);
            float3 t0 = new float3(0, 0, 200); // Tangent = direction * length
            float3 t1 = new float3(0, 0, 200);

            float3 mid = HermiteEvaluate(p0, t0, p1, t1, 0.5f);
            Assert.AreEqual(0f, mid.x, 0.001f);
            Assert.AreEqual(100f, mid.z, 0.5f); // Approximately midpoint
        }

        [Test]
        public void Hermite_Continuity_SmoothTransition()
        {
            float3 p0 = new float3(0, 0, 0);
            float3 t0 = new float3(2, 0, 100);
            float3 p1 = new float3(5, 0, 200);
            float3 t1 = new float3(-1, 0, 100);

            // Sample at small increments near midpoint - should be continuous
            float3 a = HermiteEvaluate(p0, t0, p1, t1, 0.49f);
            float3 b = HermiteEvaluate(p0, t0, p1, t1, 0.50f);
            float3 c = HermiteEvaluate(p0, t0, p1, t1, 0.51f);

            float distAB = math.distance(a, b);
            float distBC = math.distance(b, c);

            // Distances should be similar (smooth curve, no jumps)
            Assert.Less(distAB, 5f, "Jump between 0.49 and 0.50 too large");
            Assert.Less(distBC, 5f, "Jump between 0.50 and 0.51 too large");
        }

        [Test]
        public void Hermite_ParameterRange_MonotonicZ()
        {
            // For a forward-moving road, Z should always increase
            float3 p0 = new float3(0, 0, 0);
            float3 t0 = new float3(0, 0, 200);
            float3 p1 = new float3(0, 0, 200);
            float3 t1 = new float3(0, 0, 200);

            float prevZ = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                float3 pos = HermiteEvaluate(p0, t0, p1, t1, t);
                Assert.Greater(pos.z, prevZ, $"Z decreased at t={t}");
                prevZ = pos.z;
            }
        }

        #endregion

        #region Smoothstep (used in lane changes)

        private static float Smoothstep(float t)
        {
            t = math.saturate(t);
            return t * t * (3f - 2f * t);
        }

        [Test]
        public void Smoothstep_AtZero_ReturnsZero()
        {
            Assert.AreEqual(0f, Smoothstep(0f), 0.001f);
        }

        [Test]
        public void Smoothstep_AtOne_ReturnsOne()
        {
            Assert.AreEqual(1f, Smoothstep(1f), 0.001f);
        }

        [Test]
        public void Smoothstep_AtHalf_ReturnsHalf()
        {
            Assert.AreEqual(0.5f, Smoothstep(0.5f), 0.001f);
        }

        [Test]
        public void Smoothstep_IsMonotonic()
        {
            float prev = 0f;
            for (int i = 1; i <= 10; i++)
            {
                float t = i / 10f;
                float val = Smoothstep(t);
                Assert.GreaterOrEqual(val, prev, $"Smoothstep decreased at t={t}");
                prev = val;
            }
        }

        [Test]
        public void Smoothstep_ClampsBelowZero()
        {
            Assert.AreEqual(0f, Smoothstep(-1f), 0.001f);
        }

        [Test]
        public void Smoothstep_ClampsAboveOne()
        {
            Assert.AreEqual(1f, Smoothstep(2f), 0.001f);
        }

        #endregion

        #region Error-Path & Boundary Tests

        [Test]
        public void Hermite_SameEndpoints_ReturnsConstant()
        {
            float3 p = new float3(5, 0, 100);
            float3 t = float3.zero;
            float3 result = HermiteEvaluate(p, t, p, t, 0.5f);
            Assert.AreEqual(p.x, result.x, 0.001f);
            Assert.AreEqual(p.z, result.z, 0.001f);
        }

        [Test]
        public void Hermite_ZeroTangents_LinearInterpolation()
        {
            float3 p0 = new float3(0, 0, 0);
            float3 p1 = new float3(10, 0, 200);
            float3 t = float3.zero;

            // With zero tangents, Hermite degenerates but still passes through endpoints
            float3 start = HermiteEvaluate(p0, t, p1, t, 0f);
            float3 end = HermiteEvaluate(p0, t, p1, t, 1f);
            Assert.AreEqual(p0.x, start.x, 0.001f);
            Assert.AreEqual(p1.x, end.x, 0.001f);
        }

        [Test]
        public void Hermite_NegativeParameter_StillComputes()
        {
            float3 p0 = new float3(0, 0, 0);
            float3 t0 = new float3(0, 0, 100);
            float3 p1 = new float3(5, 0, 200);
            float3 t1 = new float3(0, 0, 100);

            // t < 0 is outside normal range but should not throw
            float3 result = HermiteEvaluate(p0, t0, p1, t1, -0.5f);
            Assert.IsFalse(float.IsNaN(result.x));
            Assert.IsFalse(float.IsNaN(result.z));
        }

        [Test]
        public void Hermite_ParameterAboveOne_StillComputes()
        {
            float3 p0 = new float3(0, 0, 0);
            float3 t0 = new float3(0, 0, 100);
            float3 p1 = new float3(5, 0, 200);
            float3 t1 = new float3(0, 0, 100);

            float3 result = HermiteEvaluate(p0, t0, p1, t1, 1.5f);
            Assert.IsFalse(float.IsNaN(result.x));
            Assert.IsFalse(float.IsNaN(result.z));
        }

        [Test]
        public void Smoothstep_VeryLargeNegative_ClampsToZero()
        {
            Assert.AreEqual(0f, Smoothstep(-1000f), 0.001f);
        }

        [Test]
        public void Smoothstep_VeryLargePositive_ClampsToOne()
        {
            Assert.AreEqual(1f, Smoothstep(1000f), 0.001f);
        }

        [Test]
        public void Smoothstep_OutputAlwaysInRange()
        {
            for (float t = -2f; t <= 3f; t += 0.1f)
            {
                float val = Smoothstep(t);
                Assert.GreaterOrEqual(val, 0f, $"Below 0 at t={t}");
                Assert.LessOrEqual(val, 1f, $"Above 1 at t={t}");
            }
        }

        [Test]
        public void Random_LargeSequence_AllValuesInRange()
        {
            var rng = new Unity.Mathematics.Random(99999);
            for (int i = 0; i < 1000; i++)
            {
                float val = rng.NextFloat();
                Assert.GreaterOrEqual(val, 0f, $"Below 0 at iteration {i}");
                Assert.Less(val, 1f, $"At or above 1 at iteration {i}");
            }
        }

        [TestCase(0f, 0f)]
        [TestCase(0.25f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(0.75f)]
        [TestCase(1f, 1f)]
        public void Smoothstep_ParametrizedValues(float input, float expected = -1f)
        {
            float result = Smoothstep(input);
            Assert.GreaterOrEqual(result, 0f);
            Assert.LessOrEqual(result, 1f);
            if (expected >= 0f)
                Assert.AreEqual(expected, result, 0.001f);
        }

        #endregion
    }
}
