// ============================================================================
// Nightflow - Daily Challenge System
// Generates, tracks, and rewards daily/weekly challenges
// ============================================================================

using System;
using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Systems
{
    /// <summary>
    /// Generates daily challenges based on deterministic seed.
    /// Runs once at startup and when day changes.
    /// </summary>
    [DisableAutoCreation] // Deferred to v0.2.0 — scope creep cleanup
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct DailyChallengeGeneratorSystem : ISystem
    {
        // Challenge generation parameters
        private const int DailyChallengeCount = 3;
        private const int WeeklyChallengeCount = 1;

        // Reward tiers
        private const int BronzeReward = 500;
        private const int SilverReward = 1500;
        private const int GoldReward = 5000;
        private const int WeeklyReward = 10000;

        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false; // Deferred to v0.2.0 — scope creep cleanup

            // Create challenge manager singleton
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new DailyChallengeState
            {
                DaySeed = 0,
                LastGeneratedDay = -1,
                TotalCompleted = 0,
                CurrentStreak = 0,
                BestStreak = 0,
                LastCompletionDay = -1,
                TotalBonusEarned = 0,
                ActiveChallengeCount = 0
            });
            state.EntityManager.AddComponent<ChallengeManagerTag>(entity);
            state.EntityManager.AddBuffer<ChallengeBuffer>(entity);

            // Create progress tracker singleton
            var trackerEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(trackerEntity, new ChallengeProgressUpdate());
            state.EntityManager.AddComponent<ChallengeTrackerTag>(trackerEntity);
        }

        public void OnUpdate(ref SystemState state)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int currentDay = DailyChallengeState.GetCurrentDay(now);

            foreach (var (challengeState, challenges, entity) in
                SystemAPI.Query<RefRW<DailyChallengeState>, DynamicBuffer<ChallengeBuffer>>()
                    .WithAll<ChallengeManagerTag>()
                    .WithEntityAccess())
            {
                // Check if we need to generate new challenges
                if (challengeState.ValueRO.LastGeneratedDay < currentDay)
                {
                    GenerateDailyChallenges(ref state, ref challengeState.ValueRW, challenges, currentDay, now);
                }

                // Update streak on day change
                if (challengeState.ValueRO.LastCompletionDay >= 0)
                {
                    int daysSinceCompletion = currentDay - challengeState.ValueRO.LastCompletionDay;
                    if (daysSinceCompletion > 1)
                    {
                        // Streak broken
                        challengeState.ValueRW.CurrentStreak = 0;
                    }
                }
            }
        }

        private void GenerateDailyChallenges(
            ref SystemState state,
            ref DailyChallengeState challengeState,
            DynamicBuffer<ChallengeBuffer> challenges,
            int currentDay,
            long now)
        {
            // Clear expired challenges
            for (int i = challenges.Length - 1; i >= 0; i--)
            {
                if (challenges[i].Value.IsExpired(now))
                {
                    challenges.RemoveAt(i);
                }
            }

            // Generate seed for this day
            uint daySeed = DailyChallengeState.GetDaySeed(currentDay);
            challengeState.DaySeed = daySeed;
            challengeState.LastGeneratedDay = currentDay;

            var random = new Unity.Mathematics.Random(daySeed);

            // Generate daily challenges (expire at midnight)
            long dayEnd = (currentDay + 1) * 86400L;
            int challengeId = currentDay * 100; // Unique ID base

            for (int i = 0; i < DailyChallengeCount; i++)
            {
                var difficulty = (ChallengeDifficulty)(i % 3); // Bronze, Silver, Gold
                var challenge = GenerateChallenge(ref random, challengeId + i, difficulty, dayEnd, false);
                challenges.Add(new ChallengeBuffer { Value = challenge });
            }

            // Generate weekly challenge (if Monday or first generation of week)
            int weekNumber = currentDay / 7;
            bool needsWeekly = true;

            // Check if we already have a weekly challenge for this week
            for (int i = 0; i < challenges.Length; i++)
            {
                if (challenges[i].Value.IsWeekly && !challenges[i].Value.IsExpired(now))
                {
                    needsWeekly = false;
                    break;
                }
            }

            if (needsWeekly)
            {
                long weekEnd = ((weekNumber + 1) * 7) * 86400L;
                uint weeklySeed = DailyChallengeState.GetDaySeed(weekNumber * 1000);
                var weeklyRandom = new Unity.Mathematics.Random(weeklySeed);
                var weeklyChallenge = GenerateChallenge(ref weeklyRandom, weekNumber * 1000, ChallengeDifficulty.Gold, weekEnd, true);
                weeklyChallenge.ScoreReward = WeeklyReward;
                challenges.Add(new ChallengeBuffer { Value = weeklyChallenge });
            }

            // Update active count
            challengeState.ActiveChallengeCount = challenges.Length;
        }

        private Challenge GenerateChallenge(
            ref Unity.Mathematics.Random random,
            int id,
            ChallengeDifficulty difficulty,
            long expiresAt,
            bool isWeekly)
        {
            // Pick a random challenge type
            int typeCount = 12; // Number of ChallengeType values
            var type = (ChallengeType)random.NextInt(typeCount);

            // Determine target value based on type and difficulty
            float target = GetTargetValue(type, difficulty, isWeekly);

            // Determine reward
            int reward = difficulty switch
            {
                ChallengeDifficulty.Bronze => BronzeReward,
                ChallengeDifficulty.Silver => SilverReward,
                ChallengeDifficulty.Gold => GoldReward,
                _ => BronzeReward
            };

            if (isWeekly)
            {
                reward = WeeklyReward;
            }

            return new Challenge
            {
                ChallengeId = id,
                Type = type,
                Difficulty = difficulty,
                TargetValue = target,
                CurrentProgress = 0f,
                Completed = false,
                RewardClaimed = false,
                ScoreReward = reward,
                ExpiresAt = expiresAt,
                IsWeekly = isWeekly
            };
        }

        private float GetTargetValue(ChallengeType type, ChallengeDifficulty difficulty, bool isWeekly)
        {
            float multiplier = difficulty switch
            {
                ChallengeDifficulty.Bronze => 1f,
                ChallengeDifficulty.Silver => 2f,
                ChallengeDifficulty.Gold => 4f,
                _ => 1f
            };

            if (isWeekly)
            {
                multiplier *= 5f;
            }

            return type switch
            {
                ChallengeType.ReachScore => 10000f * multiplier,
                ChallengeType.SurviveTime => 60f * multiplier,
                ChallengeType.TravelDistance => 2000f * multiplier,
                ChallengeType.ClosePasses => 5f * multiplier,
                ChallengeType.DodgeHazards => 3f * multiplier,
                ChallengeType.ReachMultiplier => 2f + (0.5f * (int)difficulty),
                ChallengeType.PerfectSegments => 2f * multiplier,
                ChallengeType.LaneWeaves => 3f * multiplier,
                ChallengeType.ThreadNeedle => 1f * multiplier,
                ChallengeType.ComboChain => 3f * multiplier,
                ChallengeType.ReachSpeed => 40f + (10f * (int)difficulty),
                ChallengeType.NoBrakeRun => 1f * multiplier,
                _ => 10f * multiplier
            };
        }
    }

    /// <summary>
    /// Tracks challenge progress during gameplay.
    /// Updates progress tracker from player state.
    /// </summary>
    [DisableAutoCreation] // Deferred to v0.2.0
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ScoringSystem))]
    public partial struct ChallengeProgressTrackingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false; // Deferred to v0.2.0 — scope creep cleanup
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get the progress tracker
            foreach (var progressUpdate in SystemAPI.Query<RefRW<ChallengeProgressUpdate>>()
                .WithAll<ChallengeTrackerTag>())
            {
                // Update from active player
                foreach (var (scoreSession, summary, velocity, input, riskState) in
                    SystemAPI.Query<RefRO<ScoreSession>, RefRO<ScoreSummary>,
                                   RefRO<Velocity>, RefRO<PlayerInput>, RefRO<RiskState>>()
                        .WithAll<PlayerVehicleTag>()
                        .WithNone<CrashedTag>())
                {
                    if (!scoreSession.ValueRO.Active)
                        continue;

                    ref var progress = ref progressUpdate.ValueRW;

                    // Update cumulative values
                    progress.Score = scoreSession.ValueRO.Score;
                    progress.TimeSurvived = summary.ValueRO.TimeSurvived;
                    progress.Distance = summary.ValueRO.TotalDistance;
                    progress.ClosePasses = summary.ValueRO.ClosePasses;
                    progress.HazardsDodged = summary.ValueRO.HazardsDodged;
                    progress.PerfectSegments = summary.ValueRO.PerfectSegments;
                    progress.LaneWeaves = summary.ValueRO.LaneWeaves;
                    progress.Threadings = summary.ValueRO.Threadings;
                    progress.HighestCombo = summary.ValueRO.HighestCombo;

                    // Track highest values
                    float speed = velocity.ValueRO.Forward;
                    if (speed > progress.HighestSpeed)
                    {
                        progress.HighestSpeed = speed;
                    }

                    float totalMultiplier = scoreSession.ValueRO.Multiplier *
                                            (1f + riskState.ValueRO.Value);
                    if (totalMultiplier > progress.HighestMultiplier)
                    {
                        progress.HighestMultiplier = totalMultiplier;
                    }

                    // Track brake usage
                    if (input.ValueRO.Brake > 0.1f)
                    {
                        progress.UsedBrake = true;
                    }

                    break; // Only one player
                }
            }
        }
    }

    /// <summary>
    /// Evaluates challenge completion when a run ends.
    /// </summary>
    [DisableAutoCreation] // Deferred to v0.2.0
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CrashSystem))]
    public partial struct ChallengeCompletionSystem : ISystem
    {
        private bool wasPlayerActive;

        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false; // Deferred to v0.2.0 — scope creep cleanup
        }

        public void OnUpdate(ref SystemState state)
        {
            // Detect run end (player just crashed)
            bool playerActive = false;

            foreach (var scoreSession in SystemAPI.Query<RefRO<ScoreSession>>()
                .WithAll<PlayerVehicleTag>()
                .WithNone<CrashedTag>())
            {
                playerActive = scoreSession.ValueRO.Active;
                break;
            }

            // Check for transition from active to not active (run ended)
            if (wasPlayerActive && !playerActive)
            {
                EvaluateChallenges(ref state);
            }

            wasPlayerActive = playerActive;
        }

        private void EvaluateChallenges(ref SystemState state)
        {
            // Get progress from tracker
            ChallengeProgressUpdate progress = default;
            foreach (var p in SystemAPI.Query<RefRO<ChallengeProgressUpdate>>()
                .WithAll<ChallengeTrackerTag>())
            {
                progress = p.ValueRO;
                break;
            }

            // Evaluate each challenge
            foreach (var (challengeState, challenges) in
                SystemAPI.Query<RefRW<DailyChallengeState>, DynamicBuffer<ChallengeBuffer>>()
                    .WithAll<ChallengeManagerTag>())
            {
                bool anyCompleted = false;

                for (int i = 0; i < challenges.Length; i++)
                {
                    var challenge = challenges[i].Value;

                    if (challenge.Completed)
                        continue;

                    // Update progress based on challenge type
                    float newProgress = GetProgressValue(challenge.Type, progress);

                    // For cumulative challenges, add to existing progress
                    if (IsCumulativeChallenge(challenge.Type))
                    {
                        challenge.CurrentProgress += newProgress;
                    }
                    else
                    {
                        // For peak challenges, take the max
                        challenge.CurrentProgress = math.max(challenge.CurrentProgress, newProgress);
                    }

                    // Check for completion
                    if (challenge.CurrentProgress >= challenge.TargetValue && !challenge.Completed)
                    {
                        challenge.Completed = true;
                        anyCompleted = true;

                        // Update state
                        challengeState.ValueRW.TotalCompleted++;
                        challengeState.ValueRW.TotalBonusEarned += challenge.ScoreReward;
                    }

                    challenges[i] = new ChallengeBuffer { Value = challenge };
                }

                // Update streak if any challenge completed
                if (anyCompleted)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    int currentDay = DailyChallengeState.GetCurrentDay(now);

                    if (challengeState.ValueRO.LastCompletionDay == currentDay - 1 ||
                        challengeState.ValueRO.LastCompletionDay == -1)
                    {
                        // Streak continues
                        challengeState.ValueRW.CurrentStreak++;
                    }
                    else if (challengeState.ValueRO.LastCompletionDay != currentDay)
                    {
                        // New streak
                        challengeState.ValueRW.CurrentStreak = 1;
                    }

                    if (challengeState.ValueRO.CurrentStreak > challengeState.ValueRO.BestStreak)
                    {
                        challengeState.ValueRW.BestStreak = challengeState.ValueRO.CurrentStreak;
                    }

                    challengeState.ValueRW.LastCompletionDay = currentDay;
                }
            }

            // Reset progress tracker for next run
            foreach (var progressUpdate in SystemAPI.Query<RefRW<ChallengeProgressUpdate>>()
                .WithAll<ChallengeTrackerTag>())
            {
                progressUpdate.ValueRW.Reset();
                break;
            }
        }

        private float GetProgressValue(ChallengeType type, ChallengeProgressUpdate progress)
        {
            return type switch
            {
                ChallengeType.ReachScore => progress.Score,
                ChallengeType.SurviveTime => progress.TimeSurvived,
                ChallengeType.TravelDistance => progress.Distance,
                ChallengeType.ClosePasses => progress.ClosePasses,
                ChallengeType.DodgeHazards => progress.HazardsDodged,
                ChallengeType.ReachMultiplier => progress.HighestMultiplier,
                ChallengeType.PerfectSegments => progress.PerfectSegments,
                ChallengeType.LaneWeaves => progress.LaneWeaves,
                ChallengeType.ThreadNeedle => progress.Threadings,
                ChallengeType.ComboChain => progress.HighestCombo,
                ChallengeType.ReachSpeed => progress.HighestSpeed,
                ChallengeType.NoBrakeRun => progress.UsedBrake ? 0f : 1f,
                _ => 0f
            };
        }

        private bool IsCumulativeChallenge(ChallengeType type)
        {
            // These challenges accumulate across runs
            return type switch
            {
                ChallengeType.ClosePasses => true,
                ChallengeType.DodgeHazards => true,
                ChallengeType.PerfectSegments => true,
                ChallengeType.LaneWeaves => true,
                ChallengeType.ThreadNeedle => true,
                ChallengeType.TravelDistance => true,
                _ => false
            };
        }
    }
}
