// Nightflow - Challenge Controller
// Handles daily/weekly challenge panel display and progress tracking

using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using Nightflow.Components;

namespace Nightflow.UI
{
    /// <summary>
    /// Manages the challenge panel: daily challenge rows, weekly challenge,
    /// streak counter, and expiration timer.
    /// </summary>
    public class ChallengeController
    {
        private VisualElement challengePanel;
        private Label streakValue;
        private Label timerValue;
        private VisualElement[] challengeRows = new VisualElement[3];
        private Label[] challengeDescs = new Label[3];
        private VisualElement[] challengeProgressFills = new VisualElement[3];
        private Label[] challengeProgressTexts = new Label[3];
        private Label[] challengeRewards = new Label[3];
        private VisualElement[] challengeChecks = new VisualElement[3];
        private VisualElement[] challengeIcons = new VisualElement[3];

        // Weekly challenge
        private VisualElement weeklyRow;
        private Label weeklyDesc;
        private VisualElement weeklyProgressFill;
        private Label weeklyProgressText;
        private Label weeklyReward;
        private VisualElement weeklyCheck;

        private EntityManager entityManager;
        private EntityQuery challengeQuery;

        public void Initialize(VisualElement root, EntityManager em, EntityQuery query)
        {
            entityManager = em;
            challengeQuery = query;

            challengePanel = root.Q<VisualElement>("challenge-panel");
            streakValue = root.Q<Label>("streak-value");
            timerValue = root.Q<Label>("timer-value");

            for (int i = 0; i < 3; i++)
            {
                challengeRows[i] = root.Q<VisualElement>($"challenge-{i}");
                challengeDescs[i] = root.Q<Label>($"challenge-{i}-desc");
                challengeProgressFills[i] = root.Q<VisualElement>($"challenge-{i}-progress-fill");
                challengeProgressTexts[i] = root.Q<Label>($"challenge-{i}-progress");
                challengeRewards[i] = root.Q<Label>($"challenge-{i}-reward");
                challengeChecks[i] = root.Q<VisualElement>($"challenge-{i}-check");
                challengeIcons[i] = root.Q<VisualElement>($"challenge-{i}-icon");
            }

            weeklyRow = root.Q<VisualElement>("weekly-challenge");
            weeklyDesc = root.Q<Label>("weekly-desc");
            weeklyProgressFill = root.Q<VisualElement>("weekly-progress-fill");
            weeklyProgressText = root.Q<Label>("weekly-progress");
            weeklyReward = root.Q<Label>("weekly-reward");
            weeklyCheck = root.Q<VisualElement>("weekly-check");
        }

        public void Update()
        {
            if (challengePanel == null || challengeQuery.IsEmpty)
                return;

            var entity = challengeQuery.GetSingletonEntity();
            var state = entityManager.GetComponentData<DailyChallengeState>(entity);
            var buffer = entityManager.GetBuffer<ChallengeBuffer>(entity);

            // Hide when no challenges exist
            if (buffer.Length == 0 && state.TotalCompleted == 0)
            {
                challengePanel.AddToClassList("hidden");
                return;
            }

            UpdateStreak(state);
            UpdateExpirationTimer();
            UpdateChallengeRows(buffer);
        }

        private void UpdateStreak(DailyChallengeState state)
        {
            if (streakValue != null)
            {
                streakValue.text = state.CurrentStreak.ToString();
            }
        }

        private void UpdateExpirationTimer()
        {
            if (timerValue == null) return;

            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int currentDay = DailyChallengeState.GetCurrentDay(now);
            long dayEnd = (currentDay + 1) * 86400L;
            long secondsRemaining = dayEnd - now;

            if (secondsRemaining > 0)
            {
                int hours = (int)(secondsRemaining / 3600);
                int minutes = (int)((secondsRemaining % 3600) / 60);
                int seconds = (int)(secondsRemaining % 60);
                timerValue.text = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            }
            else
            {
                timerValue.text = "00:00:00";
            }
        }

        private void UpdateChallengeRows(DynamicBuffer<ChallengeBuffer> buffer)
        {
            int dailyIndex = 0;
            Challenge weeklyChallenge = default;
            bool hasWeekly = false;

            for (int i = 0; i < buffer.Length; i++)
            {
                var challenge = buffer[i].Value;

                if (challenge.IsWeekly)
                {
                    weeklyChallenge = challenge;
                    hasWeekly = true;
                }
                else if (dailyIndex < 3)
                {
                    UpdateChallengeRow(dailyIndex, challenge);
                    dailyIndex++;
                }
            }

            // Hide unused daily rows
            for (int i = dailyIndex; i < 3; i++)
            {
                if (challengeRows[i] != null)
                    challengeRows[i].style.display = DisplayStyle.None;
            }

            // Weekly challenge
            if (hasWeekly)
                UpdateWeeklyChallenge(weeklyChallenge);
            else if (weeklyRow != null)
                weeklyRow.style.display = DisplayStyle.None;
        }

        private void UpdateChallengeRow(int index, Challenge challenge)
        {
            if (challengeRows[index] == null) return;

            challengeRows[index].style.display = DisplayStyle.Flex;

            if (challengeDescs[index] != null)
                challengeDescs[index].text = GetChallengeDescription(challenge);

            float progressPercent = Mathf.Clamp01(challenge.ProgressRatio) * 100f;
            if (challengeProgressFills[index] != null)
            {
                challengeProgressFills[index].style.width = new StyleLength(new Length(progressPercent, LengthUnit.Percent));
            }

            if (challengeProgressTexts[index] != null)
                challengeProgressTexts[index].text = GetProgressText(challenge);

            if (challengeRewards[index] != null)
                challengeRewards[index].text = $"+{challenge.ScoreReward}";

            if (challengeIcons[index] != null)
            {
                challengeIcons[index].RemoveFromClassList("bronze");
                challengeIcons[index].RemoveFromClassList("silver");
                challengeIcons[index].RemoveFromClassList("gold");

                string diffClass = challenge.Difficulty switch
                {
                    ChallengeDifficulty.Bronze => "bronze",
                    ChallengeDifficulty.Silver => "silver",
                    ChallengeDifficulty.Gold => "gold",
                    _ => "bronze"
                };
                challengeIcons[index].AddToClassList(diffClass);
            }

            if (challenge.Completed)
            {
                challengeRows[index].AddToClassList("completed");
                if (challengeChecks[index] != null)
                    challengeChecks[index].RemoveFromClassList("hidden");
            }
            else
            {
                challengeRows[index].RemoveFromClassList("completed");
                if (challengeChecks[index] != null)
                    challengeChecks[index].AddToClassList("hidden");
            }
        }

        private void UpdateWeeklyChallenge(Challenge challenge)
        {
            if (weeklyRow == null) return;

            weeklyRow.style.display = DisplayStyle.Flex;

            if (weeklyDesc != null)
                weeklyDesc.text = GetChallengeDescription(challenge);

            float progressPercent = Mathf.Clamp01(challenge.ProgressRatio) * 100f;
            if (weeklyProgressFill != null)
            {
                weeklyProgressFill.style.width = new StyleLength(new Length(progressPercent, LengthUnit.Percent));
            }

            if (weeklyProgressText != null)
                weeklyProgressText.text = GetProgressText(challenge);

            if (weeklyReward != null)
                weeklyReward.text = $"+{challenge.ScoreReward}";

            if (challenge.Completed)
            {
                weeklyRow.AddToClassList("completed");
                if (weeklyCheck != null) weeklyCheck.RemoveFromClassList("hidden");
            }
            else
            {
                weeklyRow.RemoveFromClassList("completed");
                if (weeklyCheck != null) weeklyCheck.AddToClassList("hidden");
            }
        }

        private string GetChallengeDescription(Challenge challenge)
        {
            return challenge.Type switch
            {
                ChallengeType.ReachScore => $"Score {challenge.TargetValue:N0} points",
                ChallengeType.SurviveTime => $"Survive {challenge.TargetValue / 60f:F0} minutes",
                ChallengeType.TravelDistance => $"Travel {challenge.TargetValue / 1000f:F1} km",
                ChallengeType.ClosePasses => $"Perform {challenge.TargetValue:F0} close passes",
                ChallengeType.DodgeHazards => $"Dodge {challenge.TargetValue:F0} hazards",
                ChallengeType.ReachMultiplier => $"Reach {challenge.TargetValue:F1}x multiplier",
                ChallengeType.PerfectSegments => $"Complete {challenge.TargetValue:F0} perfect segments",
                ChallengeType.LaneWeaves => $"Perform {challenge.TargetValue:F0} lane weaves",
                ChallengeType.ThreadNeedle => $"Thread the needle {challenge.TargetValue:F0} times",
                ChallengeType.ComboChain => $"Build a {challenge.TargetValue:F0}x combo chain",
                ChallengeType.ReachSpeed => $"Reach {challenge.TargetValue * 3.6f:F0} km/h",
                ChallengeType.NoBrakeRun => $"Complete {challenge.TargetValue:F0} no-brake runs",
                _ => "Complete challenge"
            };
        }

        private string GetProgressText(Challenge challenge)
        {
            return challenge.Type switch
            {
                ChallengeType.ReachScore => $"{challenge.CurrentProgress:N0} / {challenge.TargetValue:N0}",
                ChallengeType.SurviveTime =>
                    $"{challenge.CurrentProgress / 60f:F1} / {challenge.TargetValue / 60f:F0} min",
                ChallengeType.TravelDistance =>
                    $"{challenge.CurrentProgress / 1000f:F2} / {challenge.TargetValue / 1000f:F1} km",
                ChallengeType.ReachMultiplier =>
                    $"{challenge.CurrentProgress:F1}x / {challenge.TargetValue:F1}x",
                ChallengeType.ReachSpeed =>
                    $"{challenge.CurrentProgress * 3.6f:F0} / {challenge.TargetValue * 3.6f:F0} km/h",
                _ => $"{challenge.CurrentProgress:F0} / {challenge.TargetValue:F0}"
            };
        }
    }
}
