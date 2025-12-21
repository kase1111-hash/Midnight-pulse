// ============================================================================
// Nightflow - Unity DOTS Entity Archetypes
// ============================================================================

using Unity.Entities;
using Nightflow.Components;
using Nightflow.Tags;
using Nightflow.Buffers;

namespace Nightflow.Archetypes
{
    /// <summary>
    /// Factory for creating entity archetypes.
    /// Call Initialize() during world setup.
    /// </summary>
    public static class EntityArchetypes
    {
        public static EntityArchetype PlayerVehicle;
        public static EntityArchetype TrafficVehicle;
        public static EntityArchetype EmergencyVehicle;
        public static EntityArchetype GhostVehicle;
        public static EntityArchetype TrackSegment;
        public static EntityArchetype Lane;
        public static EntityArchetype Hazard;
        public static EntityArchetype LightSource;

        /// <summary>
        /// Initialize all archetypes. Call once during world setup.
        /// </summary>
        public static void Initialize(EntityManager entityManager)
        {
            // =================================================================
            // Player Vehicle Archetype
            // =================================================================
            PlayerVehicle = entityManager.CreateArchetype(
                // Tags
                typeof(PlayerVehicleTag),
                // Core
                typeof(WorldTransform),
                typeof(PreviousTransform),
                typeof(Velocity),
                // Vehicle Control
                typeof(PlayerInput),
                typeof(Autopilot),
                typeof(SteeringState),
                typeof(LaneTransition),
                typeof(DriftState),
                typeof(SpeedTier),
                // Lane Following
                typeof(LaneFollower),
                // Damage
                typeof(DamageState),
                typeof(Crashable),
                typeof(CollisionShape),
                typeof(CollisionEvent),
                typeof(ImpulseData),
                typeof(CrashState),
                // Scoring
                typeof(ScoreSession),
                typeof(RiskState),
                typeof(ScoreSummary),
                // Signaling
                typeof(EmergencyDetection),
                typeof(CameraState),
                // Environment (tunnel/overpass/fork state)
                typeof(EnvironmentState),
                // Replay Recording
                typeof(InputLogEntry)
            );

            // =================================================================
            // Traffic Vehicle Archetype
            // =================================================================
            TrafficVehicle = entityManager.CreateArchetype(
                // Tags
                typeof(TrafficVehicleTag),
                // Core
                typeof(WorldTransform),
                typeof(PreviousTransform),
                typeof(Velocity),
                // Vehicle Control (subset)
                typeof(SteeringState),
                typeof(LaneTransition),
                // Lane Following
                typeof(LaneFollower),
                // AI
                typeof(TrafficAI),
                typeof(LaneScoreCache),
                typeof(EmergencyDetection),
                // Collision
                typeof(CollisionShape),
                // Buffers
                typeof(NearbyVehicle)
            );

            // =================================================================
            // Emergency Vehicle Archetype
            // =================================================================
            EmergencyVehicle = entityManager.CreateArchetype(
                // Tags
                typeof(EmergencyVehicleTag),
                typeof(SirenActiveTag),
                // Core
                typeof(WorldTransform),
                typeof(PreviousTransform),
                typeof(Velocity),
                // Vehicle Control (subset)
                typeof(SteeringState),
                typeof(LaneTransition),
                // Lane Following
                typeof(LaneFollower),
                // AI
                typeof(EmergencyAI),
                // Collision
                typeof(CollisionShape),
                // Signaling
                typeof(LightEmitter),
                // Buffers
                typeof(NearbyVehicle)
            );

            // =================================================================
            // Ghost Vehicle Archetype (for replays)
            // Includes same simulation components as player for identical physics
            // =================================================================
            GhostVehicle = entityManager.CreateArchetype(
                // Tags
                typeof(GhostVehicleTag),
                // Core
                typeof(WorldTransform),
                typeof(PreviousTransform),
                typeof(Velocity),
                // Vehicle Control (same as player for identical sim)
                typeof(PlayerInput),
                typeof(SteeringState),
                typeof(LaneTransition),
                typeof(DriftState),
                typeof(SpeedTier),
                // Lane Following
                typeof(LaneFollower),
                // Environment (for tunnel/overpass compatibility)
                typeof(EnvironmentState),
                // Replay
                typeof(ReplayState),
                typeof(GhostRenderState),
                // Rendering
                typeof(LightEmitter),
                // Buffers
                typeof(InputLogEntry),
                typeof(GhostTrailPoint)
            );

            // =================================================================
            // Track Segment Archetype
            // =================================================================
            TrackSegment = entityManager.CreateArchetype(
                // Tags
                typeof(TrackSegmentTag),
                // Core
                typeof(WorldTransform),
                // Track Data
                typeof(Components.TrackSegment),
                typeof(HermiteSpline),
                // Buffers
                typeof(LaneReference),
                typeof(HazardReference)
            );

            // =================================================================
            // Lane Archetype
            // =================================================================
            Lane = entityManager.CreateArchetype(
                // Tags
                typeof(LaneTag),
                // Lane Data
                typeof(LaneSpline),
                typeof(HermiteSpline),
                // Buffers
                typeof(LaneSplinePoint),
                typeof(SplineSample)
            );

            // =================================================================
            // Hazard Archetype
            // =================================================================
            Hazard = entityManager.CreateArchetype(
                // Tags
                typeof(HazardTag),
                // Core
                typeof(WorldTransform),
                // Hazard Data
                typeof(Components.Hazard),
                // Collision
                typeof(CollisionShape)
            );

            // =================================================================
            // Light Source Archetype
            // =================================================================
            LightSource = entityManager.CreateArchetype(
                // Tags
                typeof(LightSourceTag),
                // Core
                typeof(WorldTransform),
                // Light Data
                typeof(LightEmitter)
            );
        }
    }
}
