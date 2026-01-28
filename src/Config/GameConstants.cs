// ============================================================================
// Nightflow - Game Constants
// Centralized constants to avoid magic numbers and duplicated values
// ============================================================================

using Unity.Mathematics;

namespace Nightflow.Config
{
    /// <summary>
    /// Centralized game constants used across multiple systems.
    /// This ensures consistency and makes tuning easier.
    /// </summary>
    public static class GameConstants
    {
        // =====================================================================
        // LANE & ROAD GEOMETRY
        // =====================================================================

        /// <summary>
        /// Width of each lane in meters.
        /// Used by: TrackGeneration, Traffic, Steering, LaneMagnetism, Hazards, etc.
        /// </summary>
        public const float LaneWidth = 3.6f;

        /// <summary>
        /// Default number of lanes on the freeway.
        /// </summary>
        public const int DefaultNumLanes = 4;

        /// <summary>
        /// Total road width (LaneWidth * DefaultNumLanes).
        /// </summary>
        public const float RoadWidth = LaneWidth * DefaultNumLanes;

        // =====================================================================
        // VEHICLE PHYSICS
        // =====================================================================

        /// <summary>
        /// Minimum forward speed in m/s.
        /// CRITICAL: Spins never stall the run - forward velocity is always maintained.
        /// </summary>
        public const float MinForwardSpeed = 8f;

        /// <summary>
        /// Maximum forward speed in m/s (for normal mode).
        /// </summary>
        public const float MaxForwardSpeed = 80f;

        /// <summary>
        /// Maximum vehicle damage before crash.
        /// </summary>
        public const float MaxDamage = 100f;

        // =====================================================================
        // TRACK GENERATION
        // =====================================================================

        /// <summary>
        /// Length of each track segment in meters.
        /// </summary>
        public const float SegmentLength = 200f;

        /// <summary>
        /// Number of track segments to maintain ahead of player.
        /// </summary>
        public const int SegmentsAhead = 5;

        /// <summary>
        /// Number of track segments to keep behind player before culling.
        /// </summary>
        public const int SegmentsBehind = 2;

        // =====================================================================
        // UNIT CONVERSIONS
        // =====================================================================

        /// <summary>
        /// Conversion factor from m/s to km/h.
        /// </summary>
        public const float MsToKmh = 3.6f;

        /// <summary>
        /// Conversion factor from m/s to mph.
        /// </summary>
        public const float MsToMph = 2.237f;

        /// <summary>
        /// Conversion factor from km/h to m/s.
        /// </summary>
        public const float KmhToMs = 1f / 3.6f;

        // =====================================================================
        // DAMAGE SYSTEM
        // =====================================================================

        /// <summary>
        /// Default severity for damage calculations.
        /// </summary>
        public const float DefaultDamageSeverity = 0.3f;

        /// <summary>
        /// Fallback severity when no specific value is provided.
        /// </summary>
        public const float FallbackDamageSeverity = 0.25f;

        /// <summary>
        /// Default mass factor for impulse calculations.
        /// </summary>
        public const float DefaultMassFactor = 0.3f;

        // =====================================================================
        // IMPULSE PHYSICS
        // =====================================================================

        /// <summary>
        /// Base risk multiplier cap.
        /// </summary>
        public const float BaseRiskCap = 2f;

        /// <summary>
        /// Minimum risk multiplier cap.
        /// </summary>
        public const float MinRiskCap = 0.5f;

        /// <summary>
        /// Minimum rebuild rate after damage.
        /// </summary>
        public const float MinRebuildRate = 0.3f;

        // =====================================================================
        // UI PARAMETERS
        // =====================================================================

        /// <summary>
        /// Score display smoothing factor.
        /// </summary>
        public const float ScoreDisplaySmoothing = 8f;

        /// <summary>
        /// Warning flash rate in Hz.
        /// </summary>
        public const float WarningFlashRate = 4f;

        // =====================================================================
        // CRASH SYSTEM TIMING
        // =====================================================================

        /// <summary>
        /// Time in seconds after crash before autopilot engages.
        /// </summary>
        public const float CrashFadeToAutopilotTime = 1.5f;

        /// <summary>
        /// Default autopilot target speed after crash (m/s).
        /// </summary>
        public const float AutopilotRecoverySpeed = 20f;

        // =====================================================================
        // CRASH FLASH TIMING
        // =====================================================================

        /// <summary>
        /// Flash-in duration in seconds.
        /// </summary>
        public const float CrashFlashInDuration = 0.05f;

        /// <summary>
        /// Flash hold duration in seconds.
        /// </summary>
        public const float CrashFlashHoldDuration = 0.08f;

        /// <summary>
        /// Flash fade-out duration in seconds.
        /// </summary>
        public const float CrashFlashFadeOutDuration = 0.4f;

        // =====================================================================
        // HELPER METHODS
        // =====================================================================

        /// <summary>
        /// Converts speed from m/s to km/h.
        /// </summary>
        public static float ToKmh(float metersPerSecond)
        {
            return metersPerSecond * MsToKmh;
        }

        /// <summary>
        /// Converts speed from m/s to mph.
        /// </summary>
        public static float ToMph(float metersPerSecond)
        {
            return metersPerSecond * MsToMph;
        }

        /// <summary>
        /// Converts speed from km/h to m/s.
        /// </summary>
        public static float FromKmh(float kmh)
        {
            return kmh * KmhToMs;
        }

        /// <summary>
        /// Gets the center X position for a lane (0-indexed).
        /// </summary>
        public static float GetLaneCenterX(int laneIndex, int numLanes = DefaultNumLanes)
        {
            float roadCenter = 0f;
            float leftEdge = roadCenter - (numLanes * LaneWidth * 0.5f);
            return leftEdge + (laneIndex + 0.5f) * LaneWidth;
        }
    }
}
