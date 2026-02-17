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
using Nightflow.Config;

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
        // Generation parameters - using centralized GameConstants
        // SegmentLength, SegmentsAhead, SegmentsBehind, LaneWidth, NumLanes are in GameConstants

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

        // Special segment spawn chances (increase with difficulty)
        private const float TunnelBaseChance = 0.05f;
        private const float TunnelMaxChance = 0.15f;
        private const float OverpassBaseChance = 0.03f;
        private const float OverpassMaxChance = 0.12f;
        private const float ForkBaseChance = 0.02f;
        private const float ForkMaxChance = 0.08f;

        // Tunnel parameters
        private const float TunnelHeight = 6f;
        private const float TunnelWidth = 16f;
        private const float TunnelLightSpacing = 20f;

        // Overpass parameters
        private const float OverpassElevation = 8f;

        // Fork parameters
        private const float ForkAngle = 0.1f;            // radians
        private const float ForkSeparationDist = 50f;

        // Cooldowns (segments since last special)
        private int _segmentsSinceTunnel;
        private int _segmentsSinceOverpass;
        private int _segmentsSinceFork;
        private bool _inTunnel;
        private int _tunnelRemaining;
        private int _tunnelTotal;

        private uint _globalSeed;
        private int _nextSegmentIndex;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _globalSeed = 12345u;
            _nextSegmentIndex = 0;
            _segmentsSinceTunnel = 10;
            _segmentsSinceOverpass = 10;
            _segmentsSinceFork = 10;
            _inTunnel = false;
            _tunnelRemaining = 0;
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

            float targetZ = playerZ + GameConstants.SegmentsAhead * GameConstants.SegmentLength;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            try
            {
                // Handle initial segment creation when no segments exist
                if (furthestSegment == Entity.Null)
                {
                    // Create the first segment with a valid initial spline
                    float3 initialPos = new float3(0f, 0f, 0f);
                    float3 initialTangent = new float3(0f, 0f, 1f); // Forward along Z-axis

                    furthestSpline = GenerateSegment(
                        ref ecb,
                        _nextSegmentIndex,
                        initialPos,
                        initialTangent,
                        0f
                    );

                    furthestZ = GameConstants.SegmentLength;
                    _nextSegmentIndex++;
                    furthestSegment = Entity.Null; // Still null but we'll continue generating
                }

                // Continue generating segments until we reach target
                while (furthestZ < targetZ)
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

                    furthestZ += GameConstants.SegmentLength;
                    _nextSegmentIndex++;
                }

                // =============================================================
                // Cull Segments Behind Player
                // =============================================================

                float cullZ = playerZ - GameConstants.SegmentsBehind * GameConstants.SegmentLength;

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
            }
            finally
            {
                // Ensure ECB is always disposed, even if an exception occurs
                ecb.Dispose();
            }
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
            float length = GameConstants.SegmentLength * lengthVariation;

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
            // Determine Segment Type
            // =============================================================

            int segmentType = 0; // Default: Straight
            if (math.abs(yawChange) > 0.05f) segmentType = 1; // Curve

            // Check for special segment spawns (with cooldowns)
            float typeRoll = SplineUtilities.HashToFloat(SplineUtilities.Hash(segHash, 10));
            float tunnelChance = math.lerp(TunnelBaseChance, TunnelMaxChance, difficulty);
            float overpassChance = math.lerp(OverpassBaseChance, OverpassMaxChance, difficulty);
            float forkChance = math.lerp(ForkBaseChance, ForkMaxChance, difficulty);

            // Continue tunnel if in progress
            if (_inTunnel && _tunnelRemaining > 0)
            {
                segmentType = 2; // Tunnel
                _tunnelRemaining--;
                if (_tunnelRemaining == 0) _inTunnel = false;
            }
            // Check for new tunnel (min 5 segments since last)
            else if (_segmentsSinceTunnel >= 5 && typeRoll < tunnelChance)
            {
                segmentType = 2; // Tunnel
                _inTunnel = true;
                // Tunnel length: 2-4 segments (this first segment counts as one)
                int totalTunnelLength = 2 + (int)(SplineUtilities.HashToFloat(
                    SplineUtilities.Hash(segHash, 11)) * 3);
                _tunnelTotal = totalTunnelLength;
                _tunnelRemaining = totalTunnelLength - 1; // minus this segment
                _segmentsSinceTunnel = 0;
                if (_tunnelRemaining == 0) _inTunnel = false;
            }
            // Check for overpass (min 8 segments since last, not in tunnel)
            else if (_segmentsSinceOverpass >= 8 && !_inTunnel &&
                     typeRoll >= tunnelChance && typeRoll < tunnelChance + overpassChance)
            {
                segmentType = 3; // Overpass
                _segmentsSinceOverpass = 0;
            }
            // Check for fork (min 12 segments since last, not in tunnel)
            else if (_segmentsSinceFork >= 12 && !_inTunnel &&
                     typeRoll >= tunnelChance + overpassChance &&
                     typeRoll < tunnelChance + overpassChance + forkChance)
            {
                segmentType = 4; // Fork
                _segmentsSinceFork = 0;
            }

            // Update cooldowns
            _segmentsSinceTunnel++;
            _segmentsSinceOverpass++;
            _segmentsSinceFork++;

            // =============================================================
            // Create Segment Entity
            // =============================================================

            Entity segmentEntity = ecb.CreateEntity();

            ecb.AddComponent(segmentEntity, new TrackSegment
            {
                Index = segmentIndex,
                Type = segmentType,
                StartZ = startPos.z,
                EndZ = endPos.z,
                Length = length,
                NumLanes = GameConstants.DefaultNumLanes,
                Difficulty = difficulty
            });

            // Initialize procedural mesh data (will be generated by ProceduralRoadMeshSystem)
            ecb.AddComponent(segmentEntity, new ProceduralMeshData
            {
                IsGenerated = false,
                VertexCount = 0,
                TriangleCount = 0,
                RoadWidth = GameConstants.RoadWidth,
                LengthSegments = 0,
                WidthSegments = 0,
                LODLevel = 0
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
            // Add Mesh Buffers for ProceduralRoadMeshSystem
            // =============================================================

            // These buffers are required by ProceduralRoadMeshSystem.
            // Without them, the query won't match and meshes won't generate.
            ecb.AddBuffer<MeshVertex>(segmentEntity);
            ecb.AddBuffer<MeshTriangle>(segmentEntity);
            ecb.AddBuffer<SubMeshRange>(segmentEntity);
            ecb.AddComponent(segmentEntity, new MeshBounds());

            // =============================================================
            // Pre-sample Spline for Fast Queries
            // =============================================================

            var buffer = ecb.AddBuffer<SplineSample>(segmentEntity);

            for (int i = 0; i <= SamplesPerSegment; i++)
            {
                float t = i / (float)SamplesPerSegment;

                SplineUtilities.BuildFrameAtT(startPos, t0, endPos, t1, t,
                    out float3 position, out float3 forward, out float3 right, out float3 up);

                // Calculate arc length (approximate using segment fraction)
                float arcLength = t * length;

                buffer.Add(new SplineSample
                {
                    Position = position,
                    Forward = forward,
                    Right = right,
                    ArcLength = arcLength,
                    Parameter = t
                });
            }

            ecb.AddComponent<TrackSegmentTag>(segmentEntity);

            // =============================================================
            // Add Special Segment Components & Tags
            // =============================================================

            switch (segmentType)
            {
                case 2: // Tunnel
                    ecb.AddComponent<TunnelTag>(segmentEntity);
                    ecb.AddComponent(segmentEntity, new TunnelData
                    {
                        Height = TunnelHeight,
                        Width = TunnelWidth,
                        LightSpacing = TunnelLightSpacing,
                        LightReduction = 0.3f,
                        IsEntry = _tunnelRemaining == _tunnelTotal - 1 || _tunnelTotal == 1,
                        IsExit = _tunnelRemaining == 0
                    });
                    break;

                case 3: // Overpass
                    ecb.AddComponent<OverpassTag>(segmentEntity);
                    ecb.AddComponent(segmentEntity, new OverpassData
                    {
                        ElevationAmplitude = OverpassElevation,
                        IsElevated = true,
                        OtherLayer = Entity.Null,
                        PlayerOnLayer = true
                    });
                    break;

                case 4: // Fork
                    ecb.AddComponent<ForkSegmentTag>(segmentEntity);
                    ecb.AddComponent(segmentEntity, new ForkData
                    {
                        LeftBranch = Entity.Null,
                        RightBranch = Entity.Null,
                        ForkAngle = ForkAngle,
                        ForkStartDistance = length * 0.3f,
                        Committed = false,
                        ChosenBranch = 0
                    });
                    break;
            }

            return spline;
        }
    }
}
