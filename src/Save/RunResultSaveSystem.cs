// ============================================================================
// Nightflow - Run Result Save System
// Submits a finished run to the local arcade leaderboard the moment the crash
// summary appears, and flags the summary as a new high score when it is one
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;
using Nightflow.Components;
using Nightflow.Tags;

namespace Nightflow.Save
{
    /// <summary>
    /// Bridges the crash flow to SaveManager: when GameState enters the
    /// Summary phase the player's ScoreSummary is validated and stored through
    /// SaveSystemBridge, and ScoreSummaryDisplay.IsNewHighScore / LeaderboardRank
    /// are set for the game-over panel. Runs once per crash.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Nightflow.Systems.UISystem))]
    public partial class RunResultSaveSystem : SystemBase
    {
        private const string DefaultInitials = "YOU";

        private CrashFlowPhase _lastPhase;

        protected override void OnCreate()
        {
            RequireForUpdate<GameState>();
            _lastPhase = CrashFlowPhase.None;
        }

        protected override void OnUpdate()
        {
            var gameState = SystemAPI.GetSingleton<GameState>();
            CrashFlowPhase phase = gameState.CrashPhase;
            bool enteredSummary = phase == CrashFlowPhase.Summary && _lastPhase != CrashFlowPhase.Summary;
            _lastPhase = phase;

            if (!enteredSummary)
                return;

            // Read the finished run
            ScoreSummary summary = default;
            bool found = false;
            foreach (var s in SystemAPI.Query<RefRO<ScoreSummary>>().WithAll<PlayerVehicleTag>())
            {
                summary = s.ValueRO;
                found = true;
                break;
            }

            if (!found)
                return;

            Components.GameMode mode = Components.GameMode.Nightflow;
            uint seed = 0;
            if (SystemAPI.TryGetSingleton<GameModeState>(out var modeState))
            {
                mode = modeState.CurrentMode;
                seed = modeState.SessionSeed;
            }

            int score = (int)math.clamp(summary.FinalScore, 0f, 999_999_999f);

            bool isNewHighScore = false;
            int rank = 0;

            if (score > 0 && SaveManager.Instance != null)
            {
                isNewHighScore = SaveSystemBridge.IsHighScore(score, mode);
                if (isNewHighScore)
                {
                    rank = SaveSystemBridge.GetPotentialRank(score, mode);
                    SaveSystemBridge.SaveRunAsHighScore(
                        DefaultInitials,
                        score,
                        summary.HighestSpeed,
                        summary.TotalDistance,
                        summary.TimeSurvived,
                        summary.ClosePasses,
                        summary.HazardsDodged,
                        mode,
                        seed);
                }
            }

            foreach (var display in SystemAPI.Query<RefRW<ScoreSummaryDisplay>>())
            {
                display.ValueRW.IsNewHighScore = isNewHighScore;
                display.ValueRW.LeaderboardRank = rank;
                break;
            }
        }
    }
}
