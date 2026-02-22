// Nightflow - HUD Controller
// Handles score, speed, damage display, lane indicator, emergency warnings

using UnityEngine;
using UnityEngine.UIElements;
using Unity.Mathematics;
using System.Collections.Generic;
using Nightflow.Components;

namespace Nightflow.UI
{
    /// <summary>
    /// Manages the in-game HUD elements: score, multiplier, speed, damage,
    /// lane indicator, status indicators, and emergency warnings.
    /// </summary>
    public class HUDController
    {
        // Configuration (set from UIController SerializeFields)
        private float scoreAnimationSpeed;
        private float damageFlashDuration;
        private float warningFlashRate;
        private float[] speedTiers;

        // Top bar elements
        private Label scoreValue;
        private Label multiplierValue;
        private VisualElement multiplierFill;
        private Label distanceValue;

        // Damage indicator elements
        private VisualElement damageBarFill;
        private VisualElement damageIndicator;
        private VisualElement warningIndicator;
        private Label warningText;
        private List<VisualElement> damageZones = new List<VisualElement>();

        // Speedometer elements
        private VisualElement speedometer;
        private Label speedValue;
        private VisualElement speedTierIndicator;
        private List<VisualElement> laneDots = new List<VisualElement>();

        // Status indicators
        private VisualElement autopilotIndicator;
        private VisualElement boostIndicator;

        // Emergency arrows
        private VisualElement leftArrow;
        private VisualElement rightArrow;
        private VisualElement behindArrow;

        // Animation state
        private float displayedScore;
        private float damageFlashTimer;

        // Cached state
        private float lastDamage;
        private int lastMultiplier;

        // Notification callback (wired by UIController)
        private System.Action<string, string, string> onShowNotification;

        public void Initialize(VisualElement root, float scoreAnimSpeed, float dmgFlashDur,
            float warnFlashRate, float[] tiers, System.Action<string, string, string> notificationCallback)
        {
            scoreAnimationSpeed = scoreAnimSpeed;
            damageFlashDuration = dmgFlashDur;
            warningFlashRate = warnFlashRate;
            speedTiers = tiers;
            onShowNotification = notificationCallback;

            // Top bar
            scoreValue = root.Q<Label>("score-value");
            multiplierValue = root.Q<Label>("multiplier-value");
            multiplierFill = root.Q<VisualElement>("multiplier-fill");
            distanceValue = root.Q<Label>("distance-value");

            // Damage indicator
            damageIndicator = root.Q<VisualElement>("damage-indicator");
            damageBarFill = root.Q<VisualElement>("damage-bar-fill");
            warningIndicator = root.Q<VisualElement>("warning-indicator");
            warningText = root.Q<Label>("warning-text");

            var zones = root.Q<VisualElement>("damage-zones");
            if (zones != null)
            {
                zones.Query<VisualElement>(className: "damage-zone").ForEach(zone => damageZones.Add(zone));
            }

            // Speedometer
            speedometer = root.Q<VisualElement>("speedometer");
            speedValue = root.Q<Label>("speed-value");
            speedTierIndicator = root.Q<VisualElement>("speed-tier-indicator");

            // Lane indicator
            var laneIndicator = root.Q<VisualElement>("lane-indicator");
            if (laneIndicator != null)
            {
                laneIndicator.Query<VisualElement>(className: "lane-dot").ForEach(dot => laneDots.Add(dot));
            }

            // Status indicators
            autopilotIndicator = root.Q<VisualElement>("autopilot-indicator");
            boostIndicator = root.Q<VisualElement>("boost-indicator");

            // Emergency arrows
            leftArrow = root.Q<VisualElement>("emergency-left");
            rightArrow = root.Q<VisualElement>("emergency-right");
            behindArrow = root.Q<VisualElement>("emergency-behind");
        }

        public void Update(UIState state, float deltaTime)
        {
            UpdateScore(state, deltaTime);
            UpdateMultiplier(state);
            UpdateDistance(state);
            UpdateSpeed(state);
            UpdateDamage(state);
            UpdateLaneIndicator(0);
            UpdateStatusIndicators(false, state.SpeedTier >= 2);
            UpdateEmergencyWarnings(new float2(0, 1), state.EmergencyDistance);
            UpdateWarningIndicator(state);
            UpdateAnimations(deltaTime);
        }

        private void UpdateScore(UIState state, float deltaTime)
        {
            displayedScore = Mathf.Lerp(displayedScore, state.Score, deltaTime * scoreAnimationSpeed);
            if (scoreValue != null)
            {
                scoreValue.text = Mathf.RoundToInt(displayedScore).ToString("N0");
            }
        }

        private void UpdateMultiplier(UIState state)
        {
            if (multiplierValue != null)
            {
                int currentMult = Mathf.RoundToInt(state.Multiplier);
                multiplierValue.text = $"x{state.Multiplier:F1}";

                if (currentMult > lastMultiplier && lastMultiplier > 0)
                {
                    multiplierValue.AddToClassList("flash");
                    if (state.MultiplierFlash)
                    {
                        onShowNotification?.Invoke("MULTIPLIER UP!", $"x{state.Multiplier:F1}", "bonus");
                    }
                }
                else
                {
                    multiplierValue.RemoveFromClassList("flash");
                }
                lastMultiplier = currentMult;
            }

            if (multiplierFill != null)
            {
                float fillPercent = state.RiskPercent * 100f;
                multiplierFill.style.width = new StyleLength(new Length(fillPercent, LengthUnit.Percent));
            }
        }

        private void UpdateDistance(UIState state)
        {
            if (distanceValue != null)
            {
                distanceValue.text = $"{state.DistanceKm:F2} km";
            }
        }

        private void UpdateSpeed(UIState state)
        {
            if (speedValue != null)
            {
                speedValue.text = Mathf.RoundToInt(state.SpeedKmh).ToString();
            }

            if (speedometer != null)
            {
                if (state.SpeedTier >= 2)
                    speedometer.AddToClassList("boosted");
                else
                    speedometer.RemoveFromClassList("boosted");
            }

            UpdateSpeedTierIndicator(state.SpeedKmh);
        }

        private void UpdateSpeedTierIndicator(float speed)
        {
            if (speedTierIndicator == null) return;

            speedTierIndicator.Clear();

            for (int i = 0; i < speedTiers.Length; i++)
            {
                var tierDot = new VisualElement();
                tierDot.AddToClassList("speed-tier-dot");

                if (speed >= speedTiers[i])
                {
                    tierDot.AddToClassList("active");
                }

                speedTierIndicator.Add(tierDot);
            }
        }

        private void UpdateDamage(UIState state)
        {
            int damageZoneMask = GetDamageZoneMask(state);
            float damagePercent = state.DamageTotal;

            if (damageBarFill != null)
            {
                float inversePercent = (1f - damagePercent) * 100f;
                damageBarFill.style.width = new StyleLength(new Length(inversePercent, LengthUnit.Percent));

                damageBarFill.RemoveFromClassList("warning");
                damageBarFill.RemoveFromClassList("critical");

                if (damagePercent > 0.75f)
                    damageBarFill.AddToClassList("critical");
                else if (damagePercent > 0.5f)
                    damageBarFill.AddToClassList("warning");

                if (damagePercent > lastDamage + 0.01f)
                {
                    damageFlashTimer = damageFlashDuration;
                    if (damageIndicator != null)
                    {
                        damageIndicator.AddToClassList("flash");
                    }
                }
                lastDamage = damagePercent;
            }

            for (int i = 0; i < damageZones.Count && i < 8; i++)
            {
                var zone = damageZones[i];
                bool isDamaged = (damageZoneMask & (1 << i)) != 0;
                bool isCritical = (damageZoneMask & (1 << (i + 8))) != 0;

                zone.RemoveFromClassList("damaged");
                zone.RemoveFromClassList("critical");

                if (isCritical)
                    zone.AddToClassList("critical");
                else if (isDamaged)
                    zone.AddToClassList("damaged");
            }
        }

        private int GetDamageZoneMask(UIState state)
        {
            int mask = 0;
            float threshold = 0.3f;
            float criticalThreshold = 0.7f;

            if (state.DamageFront > threshold || state.DamageLeft > threshold) mask |= 1;
            if (state.DamageFront > threshold || state.DamageRight > threshold) mask |= 2;
            if (state.DamageRear > threshold || state.DamageLeft > threshold) mask |= 4;
            if (state.DamageRear > threshold || state.DamageRight > threshold) mask |= 8;

            if (state.DamageFront > criticalThreshold || state.DamageLeft > criticalThreshold) mask |= 256;
            if (state.DamageFront > criticalThreshold || state.DamageRight > criticalThreshold) mask |= 512;
            if (state.DamageRear > criticalThreshold || state.DamageLeft > criticalThreshold) mask |= 1024;
            if (state.DamageRear > criticalThreshold || state.DamageRight > criticalThreshold) mask |= 2048;

            return mask;
        }

        private void UpdateLaneIndicator(int currentLane)
        {
            for (int i = 0; i < laneDots.Count; i++)
            {
                if (i == currentLane)
                    laneDots[i].AddToClassList("active");
                else
                    laneDots[i].RemoveFromClassList("active");
            }
        }

        private void UpdateStatusIndicators(bool autopilot, bool boosting)
        {
            if (autopilotIndicator != null)
            {
                autopilotIndicator.style.display = autopilot ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (boostIndicator != null)
            {
                boostIndicator.style.display = boosting ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void UpdateEmergencyWarnings(float2 direction, float distance)
        {
            bool hasEmergency = distance < 200f && distance > 0f;

            if (!hasEmergency)
            {
                HideEmergencyArrows();
                return;
            }

            float angle = math.atan2(direction.y, direction.x);
            float degrees = math.degrees(angle);

            HideEmergencyArrows();

            bool shouldFlash = (Time.time * warningFlashRate) % 1f < 0.5f;

            if (degrees > -45f && degrees < 45f)
            {
                if (behindArrow != null)
                {
                    behindArrow.style.display = DisplayStyle.Flex;
                    SetArrowFlash(behindArrow, shouldFlash && distance < 100f);
                }
            }
            else if (degrees >= 45f && degrees < 135f)
            {
                if (leftArrow != null)
                {
                    leftArrow.style.display = DisplayStyle.Flex;
                    SetArrowFlash(leftArrow, shouldFlash && distance < 100f);
                }
            }
            else if (degrees <= -45f && degrees > -135f)
            {
                if (rightArrow != null)
                {
                    rightArrow.style.display = DisplayStyle.Flex;
                    SetArrowFlash(rightArrow, shouldFlash && distance < 100f);
                }
            }
        }

        private void HideEmergencyArrows()
        {
            if (leftArrow != null) leftArrow.style.display = DisplayStyle.None;
            if (rightArrow != null) rightArrow.style.display = DisplayStyle.None;
            if (behindArrow != null) behindArrow.style.display = DisplayStyle.None;
        }

        private void SetArrowFlash(VisualElement arrow, bool flash)
        {
            if (flash)
                arrow.AddToClassList("flash");
            else
                arrow.RemoveFromClassList("flash");
        }

        private void UpdateWarningIndicator(UIState state)
        {
            bool showWarning = state.WarningPriority > 0;

            if (warningIndicator != null)
            {
                warningIndicator.style.display = showWarning ? DisplayStyle.Flex : DisplayStyle.None;

                if (showWarning && warningText != null)
                {
                    if (state.WarningPriority >= 3)
                        warningText.text = "EMERGENCY VEHICLE";
                    else if (state.WarningPriority == 2 || state.CriticalDamage)
                        warningText.text = "CRITICAL DAMAGE";
                    else if (state.DamageTotal > 0.5f)
                        warningText.text = "HEAVY DAMAGE";
                    else
                        warningText.text = "HIGH RISK";

                    if (state.WarningFlash)
                        warningIndicator.AddToClassList("flash");
                    else
                        warningIndicator.RemoveFromClassList("flash");
                }
            }
        }

        private void UpdateAnimations(float deltaTime)
        {
            if (damageFlashTimer > 0f)
            {
                damageFlashTimer -= deltaTime;
                if (damageFlashTimer <= 0f && damageIndicator != null)
                {
                    damageIndicator.RemoveFromClassList("flash");
                }
            }
        }
    }
}
