// ============================================================================
// Nightflow - Audio Clip Collection
// ScriptableObject containing all game audio clips for easy management
// ============================================================================

using UnityEngine;

namespace Nightflow.Audio
{
    /// <summary>
    /// Central collection of all audio clips used in Nightflow.
    /// Assign this to AudioManager to automatically populate all clip fields.
    /// Create via: Assets > Create > Nightflow > Audio Clip Collection
    /// </summary>
    [CreateAssetMenu(fileName = "AudioClipCollection", menuName = "Nightflow/Audio Clip Collection")]
    public class AudioClipCollection : ScriptableObject
    {
        [Header("=== ENGINE SOUNDS ===")]
        [Tooltip("Engine idle sound (low RPM, stationary)")]
        public AudioClip engineIdle;

        [Tooltip("Engine at low RPM (cruising)")]
        public AudioClip engineLowRPM;

        [Tooltip("Engine at mid RPM (accelerating)")]
        public AudioClip engineMidRPM;

        [Tooltip("Engine at high RPM (full throttle)")]
        public AudioClip engineHighRPM;

        [Tooltip("Tire rolling on road surface")]
        public AudioClip tireRoll;

        [Tooltip("Tire skidding/screeching")]
        public AudioClip tireSkid;

        [Tooltip("Wind rushing past at high speed")]
        public AudioClip windLoop;

        [Header("=== COLLISION SOUNDS ===")]
        [Tooltip("Light impact sounds (small bumps)")]
        public AudioClip[] lightImpacts;

        [Tooltip("Medium impact sounds (moderate collisions)")]
        public AudioClip[] mediumImpacts;

        [Tooltip("Heavy impact sounds (major crashes)")]
        public AudioClip[] heavyImpacts;

        [Tooltip("Metal scraping/grinding")]
        public AudioClip metalScrape;

        [Tooltip("Glass shattering")]
        public AudioClip glassShatter;

        [Header("=== SIREN SOUNDS ===")]
        [Tooltip("Police siren")]
        public AudioClip policeSiren;

        [Tooltip("Ambulance siren")]
        public AudioClip ambulanceSiren;

        [Tooltip("Fire truck horn")]
        public AudioClip fireHorn;

        [Header("=== AMBIENT SOUNDS ===")]
        [Tooltip("Open road ambience (wind, distant cars)")]
        public AudioClip openRoadAmbience;

        [Tooltip("Distant traffic sounds")]
        public AudioClip distantTraffic;

        [Tooltip("Tunnel interior drone/reverb")]
        public AudioClip tunnelDrone;

        [Header("=== MUSIC ===")]
        [Tooltip("Base music layer (always playing)")]
        public AudioClip musicBase;

        [Tooltip("Low intensity music layer")]
        public AudioClip musicLowIntensity;

        [Tooltip("High intensity music layer (danger/speed)")]
        public AudioClip musicHighIntensity;

        [Tooltip("Terminal/game over music")]
        public AudioClip musicTerminal;

        [Tooltip("Main menu music")]
        public AudioClip musicMenu;

        [Header("=== UI SOUNDS ===")]
        [Tooltip("Score tick/increment sound")]
        public AudioClip scoreTick;

        [Tooltip("Multiplier increased")]
        public AudioClip multiplierUp;

        [Tooltip("Multiplier lost/reset")]
        public AudioClip multiplierLost;

        [Tooltip("Damage warning beep")]
        public AudioClip damageWarning;

        [Tooltip("Near miss swoosh")]
        public AudioClip nearMiss;

        [Tooltip("Lane change whoosh")]
        public AudioClip laneChange;

        [Tooltip("Menu item selected")]
        public AudioClip menuSelect;

        [Tooltip("Menu back/cancel")]
        public AudioClip menuBack;

        [Tooltip("Game paused")]
        public AudioClip pauseSound;

        [Tooltip("New high score achieved")]
        public AudioClip highScore;

        [Tooltip("Game over")]
        public AudioClip gameOver;

        /// <summary>
        /// Validates that essential clips are assigned.
        /// Returns the number of missing clips.
        /// </summary>
        public int ValidateClips(out string[] missingClips)
        {
            var missing = new System.Collections.Generic.List<string>();

            // Engine (optional but recommended)
            if (engineIdle == null) missing.Add("engineIdle");
            if (engineLowRPM == null) missing.Add("engineLowRPM");
            if (engineMidRPM == null) missing.Add("engineMidRPM");
            if (engineHighRPM == null) missing.Add("engineHighRPM");
            if (tireRoll == null) missing.Add("tireRoll");
            if (tireSkid == null) missing.Add("tireSkid");
            if (windLoop == null) missing.Add("windLoop");

            // Collision (optional)
            if (lightImpacts == null || lightImpacts.Length == 0) missing.Add("lightImpacts[]");
            if (mediumImpacts == null || mediumImpacts.Length == 0) missing.Add("mediumImpacts[]");
            if (heavyImpacts == null || heavyImpacts.Length == 0) missing.Add("heavyImpacts[]");
            if (metalScrape == null) missing.Add("metalScrape");
            if (glassShatter == null) missing.Add("glassShatter");

            // Siren (optional)
            if (policeSiren == null) missing.Add("policeSiren");
            if (ambulanceSiren == null) missing.Add("ambulanceSiren");
            if (fireHorn == null) missing.Add("fireHorn");

            // Ambient (optional)
            if (openRoadAmbience == null) missing.Add("openRoadAmbience");
            if (distantTraffic == null) missing.Add("distantTraffic");
            if (tunnelDrone == null) missing.Add("tunnelDrone");

            // Music (optional)
            if (musicBase == null) missing.Add("musicBase");
            if (musicLowIntensity == null) missing.Add("musicLowIntensity");
            if (musicHighIntensity == null) missing.Add("musicHighIntensity");
            if (musicTerminal == null) missing.Add("musicTerminal");
            if (musicMenu == null) missing.Add("musicMenu");

            // UI (recommended)
            if (scoreTick == null) missing.Add("scoreTick");
            if (multiplierUp == null) missing.Add("multiplierUp");
            if (multiplierLost == null) missing.Add("multiplierLost");
            if (damageWarning == null) missing.Add("damageWarning");
            if (nearMiss == null) missing.Add("nearMiss");
            if (laneChange == null) missing.Add("laneChange");
            if (menuSelect == null) missing.Add("menuSelect");
            if (menuBack == null) missing.Add("menuBack");
            if (pauseSound == null) missing.Add("pauseSound");
            if (highScore == null) missing.Add("highScore");
            if (gameOver == null) missing.Add("gameOver");

            missingClips = missing.ToArray();
            return missing.Count;
        }

        /// <summary>
        /// Gets the total number of clip slots (for progress tracking).
        /// </summary>
        public int TotalClipSlots => 33; // 7 + 5 + 3 arrays + 3 + 3 + 5 + 11

        /// <summary>
        /// Gets the number of assigned clips.
        /// </summary>
        public int AssignedClipCount
        {
            get
            {
                int count = 0;

                // Engine
                if (engineIdle != null) count++;
                if (engineLowRPM != null) count++;
                if (engineMidRPM != null) count++;
                if (engineHighRPM != null) count++;
                if (tireRoll != null) count++;
                if (tireSkid != null) count++;
                if (windLoop != null) count++;

                // Collision
                if (lightImpacts != null && lightImpacts.Length > 0) count++;
                if (mediumImpacts != null && mediumImpacts.Length > 0) count++;
                if (heavyImpacts != null && heavyImpacts.Length > 0) count++;
                if (metalScrape != null) count++;
                if (glassShatter != null) count++;

                // Siren
                if (policeSiren != null) count++;
                if (ambulanceSiren != null) count++;
                if (fireHorn != null) count++;

                // Ambient
                if (openRoadAmbience != null) count++;
                if (distantTraffic != null) count++;
                if (tunnelDrone != null) count++;

                // Music
                if (musicBase != null) count++;
                if (musicLowIntensity != null) count++;
                if (musicHighIntensity != null) count++;
                if (musicTerminal != null) count++;
                if (musicMenu != null) count++;

                // UI
                if (scoreTick != null) count++;
                if (multiplierUp != null) count++;
                if (multiplierLost != null) count++;
                if (damageWarning != null) count++;
                if (nearMiss != null) count++;
                if (laneChange != null) count++;
                if (menuSelect != null) count++;
                if (menuBack != null) count++;
                if (pauseSound != null) count++;
                if (highScore != null) count++;
                if (gameOver != null) count++;

                return count;
            }
        }
    }
}
