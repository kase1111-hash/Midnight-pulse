// ============================================================================
// Nightflow - Material Registry
// Maps material types to Unity materials for runtime mesh rendering
// ============================================================================

using UnityEngine;

namespace Nightflow.Components
{
    /// <summary>
    /// Material type identifiers matching sub-mesh MaterialType values.
    /// </summary>
    public enum MaterialType
    {
        RoadSurface = 0,        // Dark road with lane markings
        TrafficVehicle = 1,     // Magenta wireframe
        EmergencyVehicle = 2,   // Red/Blue strobe
        Barrier = 3,            // Orange wireframe
        TunnelOverpass = 4,     // Dark structural
        Hazard = 5,             // Orange warning
        VehicleBody = 6,        // Player cyan / traffic magenta
        VehicleLights = 7,      // Headlights and taillights
        EmergencyLightBar = 8,  // Strobing light bar
        LightStructure = 9,     // Poles and arms
        LightEmitter = 10       // Glowing light surfaces
    }

    /// <summary>
    /// Scriptable object containing all game materials.
    /// Assign this in the Unity inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "MaterialRegistry", menuName = "Nightflow/Material Registry")]
    public class MaterialRegistry : ScriptableObject
    {
        [Header("Vehicles")]
        [Tooltip("Player vehicle - Neon Cyan #00FFFF")]
        public Material playerVehicle;

        [Tooltip("Traffic vehicles - Neon Magenta #FF00FF")]
        public Material trafficVehicle;

        [Tooltip("Ghost/replay vehicle - Dim Cyan 50% alpha")]
        public Material ghostVehicle;

        [Tooltip("Police vehicle with Red/Blue strobe")]
        public Material emergencyPolice;

        [Tooltip("Ambulance vehicle with Red/White strobe")]
        public Material emergencyAmbulance;

        [Header("Vehicle Lights")]
        [Tooltip("Headlights - Warm white glow")]
        public Material headlight;

        [Tooltip("Taillights - Red glow")]
        public Material taillight;

        [Header("Road & Environment")]
        [Tooltip("Road surface with lane markings")]
        public Material roadSurface;

        [Tooltip("Highway barriers - Orange #FF8800")]
        public Material barrier;

        [Tooltip("Tunnel interior walls/ceiling")]
        public Material tunnelInterior;

        [Tooltip("Tunnel ceiling lights - Cool fluorescent")]
        public Material tunnelLight;

        [Tooltip("Overpass structure")]
        public Material overpass;

        [Header("Streetlights")]
        [Tooltip("Streetlight pole/arm - Dark metallic")]
        public Material streetlightPole;

        [Tooltip("Streetlight fixture - Warm Sodium #FFD080")]
        public Material streetlight;

        [Header("Hazards")]
        [Tooltip("Traffic cone - Neon Orange #FF8800")]
        public Material hazardCone;

        [Tooltip("Debris pile - Dim orange/yellow")]
        public Material hazardDebris;

        [Tooltip("Loose tire - Dark gray")]
        public Material hazardTire;

        /// <summary>
        /// Gets the appropriate material for a given material type.
        /// </summary>
        public Material GetMaterial(MaterialType type)
        {
            return type switch
            {
                MaterialType.RoadSurface => roadSurface,
                MaterialType.TrafficVehicle => trafficVehicle,
                MaterialType.EmergencyVehicle => emergencyPolice,
                MaterialType.Barrier => barrier,
                MaterialType.TunnelOverpass => tunnelInterior,
                MaterialType.Hazard => hazardCone,
                MaterialType.VehicleBody => playerVehicle,
                MaterialType.VehicleLights => headlight,
                MaterialType.EmergencyLightBar => emergencyPolice,
                MaterialType.LightStructure => streetlightPole,
                MaterialType.LightEmitter => streetlight,
                _ => playerVehicle
            };
        }

        /// <summary>
        /// Gets material for a specific hazard type.
        /// </summary>
        public Material GetHazardMaterial(int hazardType)
        {
            return hazardType switch
            {
                0 => hazardCone,      // Cone
                1 => hazardTire,      // Loose tire
                2 => hazardDebris,    // Debris
                3 => barrier,         // Barrier block
                4 => trafficVehicle,  // Crashed car
                _ => hazardCone
            };
        }

        /// <summary>
        /// Gets material for a specific vehicle type.
        /// </summary>
        public Material GetVehicleMaterial(bool isPlayer, bool isEmergency, bool isGhost, int emergencyType)
        {
            if (isGhost) return ghostVehicle;
            if (isPlayer) return playerVehicle;
            if (isEmergency)
            {
                return emergencyType == 3 ? emergencyPolice : emergencyAmbulance;
            }
            return trafficVehicle;
        }
    }
}
