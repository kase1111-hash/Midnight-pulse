// ============================================================================
// Nightflow - Unity DOTS Tag Components
// ============================================================================

using Unity.Entities;

namespace Nightflow.Tags
{
    // ========================================================================
    // Vehicle Tags
    // ========================================================================

    /// <summary>Tag for player-controlled vehicle.</summary>
    public struct PlayerVehicleTag : IComponentData { }

    /// <summary>Tag for AI traffic vehicles.</summary>
    public struct TrafficVehicleTag : IComponentData { }

    /// <summary>Tag for emergency vehicles (ambulance, police).</summary>
    public struct EmergencyVehicleTag : IComponentData { }

    /// <summary>Tag for ghost/replay vehicles.</summary>
    public struct GhostVehicleTag : IComponentData { }

    // ========================================================================
    // State Tags
    // ========================================================================

    /// <summary>Tag indicating autopilot is active.</summary>
    public struct AutopilotActiveTag : IComponentData { }

    /// <summary>Tag indicating entity is crashed.</summary>
    public struct CrashedTag : IComponentData { }

    /// <summary>Tag indicating entity is in lane transition.</summary>
    public struct LaneTransitionActiveTag : IComponentData { }

    /// <summary>Tag indicating entity is drifting.</summary>
    public struct DriftingTag : IComponentData { }

    // ========================================================================
    // Track Tags
    // ========================================================================

    /// <summary>Tag for track segment entities.</summary>
    public struct TrackSegmentTag : IComponentData { }

    /// <summary>Tag for lane entities.</summary>
    public struct LaneTag : IComponentData { }

    /// <summary>Tag for fork track segments.</summary>
    public struct ForkSegmentTag : IComponentData { }

    /// <summary>Tag for tunnel segments (affects audio/lighting).</summary>
    public struct TunnelTag : IComponentData { }

    /// <summary>Tag for overpass segments.</summary>
    public struct OverpassTag : IComponentData { }

    /// <summary>Tag for lane marker/line entities.</summary>
    public struct LaneMarkerTag : IComponentData { }

    // ========================================================================
    // Hazard Tags
    // ========================================================================

    /// <summary>Tag for hazard entities.</summary>
    public struct HazardTag : IComponentData { }

    /// <summary>Tag for lethal hazards (barriers, crashed cars).</summary>
    public struct LethalHazardTag : IComponentData { }

    // ========================================================================
    // Signaling Tags
    // ========================================================================

    /// <summary>Tag for light source entities.</summary>
    public struct LightSourceTag : IComponentData { }

    /// <summary>Tag for entities with active sirens.</summary>
    public struct SirenActiveTag : IComponentData { }

    // ========================================================================
    // System Control Tags
    // ========================================================================

    /// <summary>Tag to mark entities pending destruction.</summary>
    public struct DestroyTag : IComponentData { }

    /// <summary>Tag for entities that need initialization.</summary>
    public struct NeedsInitializationTag : IComponentData { }

    /// <summary>Tag for newly spawned entities.</summary>
    public struct JustSpawnedTag : IComponentData { }
}
