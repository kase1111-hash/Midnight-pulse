// ============================================================================
// Nightflow - Save Manager
// Handles all persistent data operations: settings, high scores, ghost recordings
// ============================================================================

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Entities;
using Nightflow.Components;
using Nightflow.Utilities;

namespace Nightflow.Save
{
    /// <summary>
    /// Central save system managing all persistent data.
    /// Uses JSON files for main data and PlayerPrefs for quick settings access.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Header("Save Configuration")]
        [SerializeField] private bool autoSave = true;
        [SerializeField] private float autoSaveInterval = 60f;
        [SerializeField] private bool saveOnQuit = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        // Current save data
        private NightflowSaveData saveData;
        private float autoSaveTimer;
        private bool isDirty;

        // File paths
        private string SaveDirectory => Path.Combine(Application.persistentDataPath, "Nightflow");
        private string MainSavePath => Path.Combine(SaveDirectory, "save.json");
        private string GhostDirectory => Path.Combine(SaveDirectory, "ghosts");
        private string BackupPath => Path.Combine(SaveDirectory, "save.backup.json");

        // Constants
        private const string PREFS_MASTER_VOLUME = "nf_master_volume";
        private const string PREFS_MUSIC_VOLUME = "nf_music_volume";
        private const string PREFS_SFX_VOLUME = "nf_sfx_volume";
        private const string PREFS_FULLSCREEN = "nf_fullscreen";
        private const string PREFS_QUALITY = "nf_quality";
        private const string PREFS_SPEED_UNIT = "nf_speed_unit";

        // Events
        public event Action OnSaveCompleted;
        public event Action OnLoadCompleted;
        public event Action<HighScoreEntry> OnNewHighScore;
        public event Action<GhostRecording> OnGhostSaved;

        // ECS access
        private EntityManager entityManager;
        private EntityQuery audioConfigQuery;
        private bool ecsInitialized;

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureDirectoriesExist();
            LoadAll();
        }

        private void Start()
        {
            TryInitializeECS();
            ApplySettingsToGame();
        }

        private void Update()
        {
            if (!ecsInitialized)
            {
                TryInitializeECS();
            }

            if (autoSave && isDirty)
            {
                autoSaveTimer += Time.unscaledDeltaTime;
                if (autoSaveTimer >= autoSaveInterval)
                {
                    SaveAll();
                    autoSaveTimer = 0f;
                }
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && isDirty)
            {
                SaveAll();
            }
        }

        private void OnApplicationQuit()
        {
            if (saveOnQuit)
            {
                SaveAll();
            }
        }

        private void TryInitializeECS()
        {
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                audioConfigQuery = entityManager.CreateEntityQuery(typeof(AudioConfig));
                ecsInitialized = true;
            }
        }

        #endregion

        #region Directory Management

        private void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(SaveDirectory))
                {
                    Directory.CreateDirectory(SaveDirectory);
                }
                if (!Directory.Exists(GhostDirectory))
                {
                    Directory.CreateDirectory(GhostDirectory);
                }
            }
            catch (Exception e)
            {
                Log.SystemError("SaveManager", $"Failed to create directories: {e.Message}");
            }
        }

        #endregion

        #region Save/Load All

        /// <summary>
        /// Save all data to disk.
        /// </summary>
        public void SaveAll()
        {
            if (saveData == null)
            {
                saveData = new NightflowSaveData();
            }

            saveData.LastSaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            try
            {
                // Create backup of existing save
                if (File.Exists(MainSavePath))
                {
                    File.Copy(MainSavePath, BackupPath, true);
                }

                // Save main data
                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(MainSavePath, json);

                // Also save critical settings to PlayerPrefs for quick access
                SaveSettingsToPrefs();

                isDirty = false;
                autoSaveTimer = 0f;

                Log.System("SaveManager", $"Saved to {MainSavePath}");

                OnSaveCompleted?.Invoke();
            }
            catch (Exception e)
            {
                Log.SystemError("SaveManager", $"Failed to save: {e.Message}");
            }
        }

        /// <summary>
        /// Load all data from disk.
        /// </summary>
        public void LoadAll()
        {
            try
            {
                if (File.Exists(MainSavePath))
                {
                    string json = File.ReadAllText(MainSavePath);
                    saveData = JsonUtility.FromJson<NightflowSaveData>(json);

                    Log.System("SaveManager", $"Loaded from {MainSavePath}");
                }
                else
                {
                    // Try backup
                    if (File.Exists(BackupPath))
                    {
                        string json = File.ReadAllText(BackupPath);
                        saveData = JsonUtility.FromJson<NightflowSaveData>(json);
                        Log.System("SaveManager", "Loaded from backup");
                    }
                    else
                    {
                        // Create fresh save data
                        saveData = new NightflowSaveData();
                        LoadSettingsFromPrefs();

                        Log.System("SaveManager", "Created new save data");
                    }
                }

                // Load ghost recordings from separate files
                LoadGhostRecordings();

                OnLoadCompleted?.Invoke();
            }
            catch (Exception e)
            {
                Log.SystemError("SaveManager", $"Failed to load: {e.Message}");
                saveData = new NightflowSaveData();
            }
        }

        /// <summary>
        /// Reset all save data.
        /// </summary>
        public void ResetAll()
        {
            saveData = new NightflowSaveData();
            isDirty = true;
            SaveAll();
        }

        #endregion

        #region Settings

        /// <summary>
        /// Get current settings.
        /// </summary>
        public SettingsData GetSettings()
        {
            return saveData?.Settings ?? new SettingsData();
        }

        /// <summary>
        /// Update settings and apply to game.
        /// </summary>
        public void UpdateSettings(SettingsData settings)
        {
            if (saveData == null) saveData = new NightflowSaveData();

            saveData.Settings = settings;
            isDirty = true;

            ApplySettingsToGame();
            SaveSettingsToPrefs();
        }

        /// <summary>
        /// Update audio settings specifically.
        /// </summary>
        public void UpdateAudioSettings(AudioSettings audio)
        {
            if (saveData == null) saveData = new NightflowSaveData();

            saveData.Settings.Audio = audio;
            isDirty = true;

            ApplyAudioSettingsToECS();
            SaveSettingsToPrefs();
        }

        /// <summary>
        /// Apply settings to the running game.
        /// </summary>
        private void ApplySettingsToGame()
        {
            var settings = GetSettings();

            // Audio
            ApplyAudioSettingsToECS();
            AudioListener.volume = settings.Audio.MasterVolume;

            // Display
            if (settings.Display.Fullscreen != Screen.fullScreen ||
                settings.Display.ResolutionWidth != Screen.width ||
                settings.Display.ResolutionHeight != Screen.height)
            {
                Screen.SetResolution(
                    settings.Display.ResolutionWidth,
                    settings.Display.ResolutionHeight,
                    settings.Display.Fullscreen,
                    settings.Display.RefreshRate
                );
            }

            QualitySettings.SetQualityLevel(settings.Display.QualityLevel);
            QualitySettings.vSyncCount = settings.Display.VSync ? 1 : 0;
            Application.targetFrameRate = settings.Display.VSync ? -1 : settings.Display.TargetFrameRate;
        }

        /// <summary>
        /// Apply audio settings to ECS AudioConfig.
        /// </summary>
        private void ApplyAudioSettingsToECS()
        {
            if (!ecsInitialized || audioConfigQuery.IsEmpty) return;

            var settings = GetSettings().Audio;
            var entity = audioConfigQuery.GetSingletonEntity();
            var config = entityManager.GetComponentData<AudioConfig>(entity);

            config.MasterVolume = settings.MasterVolume;
            config.MusicVolume = settings.MusicVolume;
            config.SFXVolume = settings.SFXVolume;
            config.EngineVolume = settings.EngineVolume;
            config.AmbientVolume = settings.AmbientVolume;

            entityManager.SetComponentData(entity, config);
        }

        /// <summary>
        /// Save critical settings to PlayerPrefs.
        /// </summary>
        private void SaveSettingsToPrefs()
        {
            var settings = GetSettings();

            PlayerPrefs.SetFloat(PREFS_MASTER_VOLUME, settings.Audio.MasterVolume);
            PlayerPrefs.SetFloat(PREFS_MUSIC_VOLUME, settings.Audio.MusicVolume);
            PlayerPrefs.SetFloat(PREFS_SFX_VOLUME, settings.Audio.SFXVolume);
            PlayerPrefs.SetInt(PREFS_FULLSCREEN, settings.Display.Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(PREFS_QUALITY, settings.Display.QualityLevel);
            PlayerPrefs.SetInt(PREFS_SPEED_UNIT, (int)settings.Gameplay.SpeedUnit);

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Load settings from PlayerPrefs (fallback).
        /// </summary>
        private void LoadSettingsFromPrefs()
        {
            if (saveData == null) saveData = new NightflowSaveData();

            var audio = saveData.Settings.Audio;
            audio.MasterVolume = PlayerPrefs.GetFloat(PREFS_MASTER_VOLUME, 1f);
            audio.MusicVolume = PlayerPrefs.GetFloat(PREFS_MUSIC_VOLUME, 0.7f);
            audio.SFXVolume = PlayerPrefs.GetFloat(PREFS_SFX_VOLUME, 1f);

            var display = saveData.Settings.Display;
            display.Fullscreen = PlayerPrefs.GetInt(PREFS_FULLSCREEN, 1) == 1;
            display.QualityLevel = PlayerPrefs.GetInt(PREFS_QUALITY, 2);

            var gameplay = saveData.Settings.Gameplay;
            gameplay.SpeedUnit = (SpeedUnit)PlayerPrefs.GetInt(PREFS_SPEED_UNIT, 0);
        }

        #endregion

        #region Leaderboard

        /// <summary>
        /// Get high scores for a specific mode (or all if GameMode not specified).
        /// </summary>
        public List<HighScoreEntry> GetHighScores(GameMode? mode = null)
        {
            if (saveData == null) return new List<HighScoreEntry>();

            var leaderboard = saveData.Leaderboard;

            if (!mode.HasValue)
            {
                return leaderboard.AllScores
                    .OrderByDescending(e => e.Score)
                    .Take(LeaderboardData.MaxEntriesPerMode)
                    .ToList();
            }

            return mode.Value switch
            {
                GameMode.Nightflow => leaderboard.NightflowScores.OrderByDescending(e => e.Score).ToList(),
                GameMode.Redline => leaderboard.RedlineScores.OrderByDescending(e => e.Score).ToList(),
                GameMode.Ghost => leaderboard.GhostScores.OrderByDescending(e => e.Score).ToList(),
                GameMode.Freeflow => leaderboard.FreeflowScores.OrderByDescending(e => e.Score).ToList(),
                _ => new List<HighScoreEntry>()
            };
        }

        /// <summary>
        /// Check if a score qualifies for the leaderboard.
        /// </summary>
        public bool IsHighScore(int score, GameMode mode)
        {
            var scores = GetHighScores(mode);
            if (scores.Count < LeaderboardData.MaxEntriesPerMode)
                return true;

            return score > scores.Last().Score;
        }

        /// <summary>
        /// Get the rank a score would achieve (1-based, 0 = not on board).
        /// </summary>
        public int GetPotentialRank(int score, GameMode mode)
        {
            var scores = GetHighScores(mode);

            for (int i = 0; i < scores.Count; i++)
            {
                if (score > scores[i].Score)
                    return i + 1;
            }

            if (scores.Count < LeaderboardData.MaxEntriesPerMode)
                return scores.Count + 1;

            return 0;
        }

        /// <summary>
        /// Add a new high score entry.
        /// </summary>
        public void AddHighScore(HighScoreEntry entry)
        {
            if (saveData == null) saveData = new NightflowSaveData();

            var leaderboard = saveData.Leaderboard;

            // Add to mode-specific list
            var modeList = entry.Mode switch
            {
                GameMode.Nightflow => leaderboard.NightflowScores,
                GameMode.Redline => leaderboard.RedlineScores,
                GameMode.Ghost => leaderboard.GhostScores,
                GameMode.Freeflow => leaderboard.FreeflowScores,
                _ => null
            };

            if (modeList != null)
            {
                modeList.Add(entry);
                modeList.Sort((a, b) => b.Score.CompareTo(a.Score));
                while (modeList.Count > LeaderboardData.MaxEntriesPerMode)
                {
                    modeList.RemoveAt(modeList.Count - 1);
                }
            }

            // Add to all-scores list
            leaderboard.AllScores.Add(entry);
            leaderboard.AllScores.Sort((a, b) => b.Score.CompareTo(a.Score));
            while (leaderboard.AllScores.Count > LeaderboardData.MaxEntriesPerMode * 4)
            {
                leaderboard.AllScores.RemoveAt(leaderboard.AllScores.Count - 1);
            }

            // Update progress
            UpdateProgressFromScore(entry);

            isDirty = true;

            Log.System("SaveManager", $"Added high score {entry.Score} for {entry.Initials}");

            OnNewHighScore?.Invoke(entry);
        }

        /// <summary>
        /// Update lifetime progress from a new score.
        /// </summary>
        private void UpdateProgressFromScore(HighScoreEntry entry)
        {
            var progress = saveData.Progress;

            // Update lifetime stats
            progress.TotalRuns++;
            progress.TotalDistance += entry.Distance;
            progress.TotalTimePlayed += entry.TimeSurvived;
            progress.TotalCrashes++;
            progress.TotalClosePasses += entry.ClosePasses;

            // Update records
            if (entry.Score > progress.HighestScore)
                progress.HighestScore = entry.Score;
            if (entry.MaxSpeed > progress.HighestSpeed)
                progress.HighestSpeed = entry.MaxSpeed;
            if (entry.TimeSurvived > progress.LongestRun)
                progress.LongestRun = entry.TimeSurvived;
            if (entry.Distance > progress.FarthestDistance)
                progress.FarthestDistance = entry.Distance;

            // Update mode-specific stats
            var modeStats = entry.Mode switch
            {
                GameMode.Nightflow => progress.NightflowStats,
                GameMode.Redline => progress.RedlineStats,
                GameMode.Ghost => progress.GhostStats,
                GameMode.Freeflow => progress.FreeflowStats,
                _ => null
            };

            if (modeStats != null)
            {
                modeStats.Runs++;
                if (entry.Score > modeStats.HighScore)
                    modeStats.HighScore = entry.Score;
                if (entry.MaxSpeed > modeStats.BestSpeed)
                    modeStats.BestSpeed = entry.MaxSpeed;
                if (entry.Distance > modeStats.BestDistance)
                    modeStats.BestDistance = entry.Distance;
                if (entry.TimeSurvived > modeStats.BestTime)
                    modeStats.BestTime = entry.TimeSurvived;
            }
        }

        #endregion

        #region Ghost Recordings

        /// <summary>
        /// Get all saved ghost recordings.
        /// </summary>
        public List<GhostRecording> GetGhostRecordings()
        {
            return saveData?.GhostRecordings ?? new List<GhostRecording>();
        }

        /// <summary>
        /// Get a specific ghost recording by ID.
        /// </summary>
        public GhostRecording GetGhostRecording(string id)
        {
            return saveData?.GhostRecordings.FirstOrDefault(g => g.Id == id);
        }

        /// <summary>
        /// Save a ghost recording.
        /// </summary>
        public void SaveGhostRecording(GhostRecording ghost)
        {
            if (saveData == null) saveData = new NightflowSaveData();

            // Add to list
            saveData.GhostRecordings.Add(ghost);

            // Limit total ghost recordings (keep best scores)
            const int maxGhosts = 20;
            if (saveData.GhostRecordings.Count > maxGhosts)
            {
                saveData.GhostRecordings.Sort((a, b) => b.Score.CompareTo(a.Score));
                saveData.GhostRecordings.RemoveRange(maxGhosts, saveData.GhostRecordings.Count - maxGhosts);
            }

            // Save ghost to separate file (for large recordings)
            try
            {
                string ghostPath = Path.Combine(GhostDirectory, $"{ghost.Id}.json");
                string json = JsonUtility.ToJson(ghost, false);
                File.WriteAllText(ghostPath, json);

                Log.System("SaveManager", $"Saved ghost {ghost.Id} ({ghost.Inputs.Count} frames)");
            }
            catch (Exception e)
            {
                Log.SystemError("SaveManager", $"Failed to save ghost: {e.Message}");
            }

            isDirty = true;
            OnGhostSaved?.Invoke(ghost);
        }

        /// <summary>
        /// Delete a ghost recording.
        /// </summary>
        public void DeleteGhostRecording(string id)
        {
            if (saveData == null) return;

            var ghost = saveData.GhostRecordings.FirstOrDefault(g => g.Id == id);
            if (ghost != null)
            {
                saveData.GhostRecordings.Remove(ghost);

                // Delete file
                try
                {
                    string ghostPath = Path.Combine(GhostDirectory, $"{id}.json");
                    if (File.Exists(ghostPath))
                    {
                        File.Delete(ghostPath);
                    }
                }
                catch (Exception e)
                {
                    Log.SystemError("SaveManager", $"Failed to delete ghost file: {e.Message}");
                }

                isDirty = true;
            }
        }

        /// <summary>
        /// Load ghost recordings from disk.
        /// </summary>
        private void LoadGhostRecordings()
        {
            if (saveData == null) return;

            try
            {
                if (Directory.Exists(GhostDirectory))
                {
                    var ghostFiles = Directory.GetFiles(GhostDirectory, "*.json");
                    foreach (var file in ghostFiles)
                    {
                        try
                        {
                            string json = File.ReadAllText(file);
                            var ghost = JsonUtility.FromJson<GhostRecording>(json);

                            // Ensure it's not already in the list
                            if (!saveData.GhostRecordings.Exists(g => g.Id == ghost.Id))
                            {
                                saveData.GhostRecordings.Add(ghost);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.SystemWarn("SaveManager", $"Failed to load ghost {file}: {e.Message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.SystemError("SaveManager", $"Failed to load ghost recordings: {e.Message}");
            }
        }

        /// <summary>
        /// Create a ghost recording from current ECS input buffer.
        /// </summary>
        public GhostRecording CreateGhostFromInputBuffer(
            Entity playerEntity,
            uint seed,
            GameMode mode,
            int score,
            Vector3 startPos,
            Quaternion startRot)
        {
            if (!ecsInitialized) return null;

            var ghost = new GhostRecording
            {
                Seed = seed,
                Mode = mode,
                Score = score,
                StartPosition = startPos,
                StartRotation = startRot,
                Name = $"{mode} - Score {score}"
            };

            // Copy input buffer from ECS
            if (entityManager.HasBuffer<Nightflow.Buffers.InputLogEntry>(playerEntity))
            {
                var buffer = entityManager.GetBuffer<Nightflow.Buffers.InputLogEntry>(playerEntity);

                ghost.Duration = buffer.Length > 0 ? buffer[buffer.Length - 1].Timestamp : 0f;

                for (int i = 0; i < buffer.Length; i++)
                {
                    var entry = buffer[i];
                    ghost.Inputs.Add(new GhostInputFrame(
                        entry.Timestamp,
                        entry.Steer,
                        entry.Throttle,
                        entry.Brake,
                        entry.Handbrake
                    ));
                }
            }

            return ghost;
        }

        #endregion

        #region Progress / Statistics

        /// <summary>
        /// Get current progress data.
        /// </summary>
        public ProgressData GetProgress()
        {
            return saveData?.Progress ?? new ProgressData();
        }

        /// <summary>
        /// Increment a specific statistic.
        /// </summary>
        public void IncrementStat(string statName, float value = 1f)
        {
            if (saveData == null) return;

            var progress = saveData.Progress;

            switch (statName.ToLower())
            {
                case "runs": progress.TotalRuns++; break;
                case "crashes": progress.TotalCrashes++; break;
                case "closepasses": progress.TotalClosePasses += (int)value; break;
                case "distance": progress.TotalDistance += value; break;
                case "time": progress.TotalTimePlayed += value; break;
            }

            isDirty = true;
        }

        #endregion

        #region Challenges

        /// <summary>
        /// Get current challenge data.
        /// </summary>
        public ChallengeData GetChallenges()
        {
            return saveData?.Challenges ?? new ChallengeData();
        }

        /// <summary>
        /// Update challenge data from ECS state.
        /// Called when challenges change or on save.
        /// </summary>
        public void UpdateChallenges(ChallengeData challenges)
        {
            if (saveData == null) saveData = new NightflowSaveData();

            saveData.Challenges = challenges;
            isDirty = true;
        }

        /// <summary>
        /// Update a single challenge's progress.
        /// </summary>
        public void UpdateChallengeProgress(int challengeId, float progress, bool completed)
        {
            if (saveData == null) return;

            var challenges = saveData.Challenges;

            // Check daily challenges
            foreach (var c in challenges.DailyChallenges)
            {
                if (c.ChallengeId == challengeId)
                {
                    c.CurrentProgress = progress;
                    c.Completed = completed;
                    isDirty = true;
                    return;
                }
            }

            // Check weekly challenges
            foreach (var c in challenges.WeeklyChallenges)
            {
                if (c.ChallengeId == challengeId)
                {
                    c.CurrentProgress = progress;
                    c.Completed = completed;
                    isDirty = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Claim reward for a completed challenge.
        /// </summary>
        public int ClaimChallengeReward(int challengeId)
        {
            if (saveData == null) return 0;

            var challenges = saveData.Challenges;

            // Find and claim the challenge
            SavedChallenge found = null;

            foreach (var c in challenges.DailyChallenges)
            {
                if (c.ChallengeId == challengeId && c.Completed && !c.RewardClaimed)
                {
                    found = c;
                    break;
                }
            }

            if (found == null)
            {
                foreach (var c in challenges.WeeklyChallenges)
                {
                    if (c.ChallengeId == challengeId && c.Completed && !c.RewardClaimed)
                    {
                        found = c;
                        break;
                    }
                }
            }

            if (found != null)
            {
                found.RewardClaimed = true;
                challenges.TotalBonusEarned += found.ScoreReward;
                isDirty = true;
                return found.ScoreReward;
            }

            return 0;
        }

        /// <summary>
        /// Sync challenge state from ECS to save data.
        /// </summary>
        public void SyncChallengesFromECS()
        {
            if (!ecsInitialized) return;

            var challengeQuery = entityManager.CreateEntityQuery(
                typeof(Nightflow.Components.DailyChallengeState),
                typeof(Nightflow.Components.ChallengeManagerTag)
            );

            if (challengeQuery.IsEmpty) return;

            var entity = challengeQuery.GetSingletonEntity();
            var state = entityManager.GetComponentData<Nightflow.Components.DailyChallengeState>(entity);
            var buffer = entityManager.GetBuffer<Nightflow.Components.ChallengeBuffer>(entity);

            // Skip sync when challenge systems are disabled (deferred to v0.2.0)
            // Avoid overwriting previously saved challenge data with empty state
            if (buffer.Length == 0 && state.TotalCompleted == 0)
                return;

            var challenges = saveData.Challenges;
            challenges.LastGeneratedDay = state.LastGeneratedDay;
            challenges.TotalCompleted = state.TotalCompleted;
            challenges.CurrentStreak = state.CurrentStreak;
            challenges.BestStreak = state.BestStreak;
            challenges.LastCompletionDay = state.LastCompletionDay;
            challenges.TotalBonusEarned = state.TotalBonusEarned;

            // Clear and rebuild challenge lists
            challenges.DailyChallenges.Clear();
            challenges.WeeklyChallenges.Clear();

            for (int i = 0; i < buffer.Length; i++)
            {
                var c = buffer[i].Value;
                var saved = new SavedChallenge(
                    c.ChallengeId,
                    (int)c.Type,
                    (int)c.Difficulty,
                    c.TargetValue,
                    c.ScoreReward,
                    c.ExpiresAt,
                    c.IsWeekly
                )
                {
                    CurrentProgress = c.CurrentProgress,
                    Completed = c.Completed,
                    RewardClaimed = c.RewardClaimed
                };

                if (c.IsWeekly)
                {
                    challenges.WeeklyChallenges.Add(saved);
                }
                else
                {
                    challenges.DailyChallenges.Add(saved);
                }
            }

            isDirty = true;
        }

        /// <summary>
        /// Load challenge state from save data into ECS.
        /// Call after ECS world is initialized.
        /// </summary>
        public void LoadChallengesIntoECS()
        {
            if (!ecsInitialized) return;

            var challengeQuery = entityManager.CreateEntityQuery(
                typeof(Nightflow.Components.DailyChallengeState),
                typeof(Nightflow.Components.ChallengeManagerTag)
            );

            if (challengeQuery.IsEmpty) return;

            var entity = challengeQuery.GetSingletonEntity();
            var challenges = saveData?.Challenges ?? new ChallengeData();

            // Update state
            var state = new Nightflow.Components.DailyChallengeState
            {
                DaySeed = Nightflow.Components.DailyChallengeState.GetDaySeed(challenges.LastGeneratedDay),
                LastGeneratedDay = challenges.LastGeneratedDay,
                TotalCompleted = challenges.TotalCompleted,
                CurrentStreak = challenges.CurrentStreak,
                BestStreak = challenges.BestStreak,
                LastCompletionDay = challenges.LastCompletionDay,
                TotalBonusEarned = challenges.TotalBonusEarned,
                ActiveChallengeCount = challenges.DailyChallenges.Count + challenges.WeeklyChallenges.Count
            };

            entityManager.SetComponentData(entity, state);

            // Load challenges into buffer
            var buffer = entityManager.GetBuffer<Nightflow.Components.ChallengeBuffer>(entity);
            buffer.Clear();

            foreach (var saved in challenges.DailyChallenges)
            {
                buffer.Add(new Nightflow.Components.ChallengeBuffer
                {
                    Value = SavedToChallenge(saved)
                });
            }

            foreach (var saved in challenges.WeeklyChallenges)
            {
                buffer.Add(new Nightflow.Components.ChallengeBuffer
                {
                    Value = SavedToChallenge(saved)
                });
            }
        }

        private Nightflow.Components.Challenge SavedToChallenge(SavedChallenge saved)
        {
            return new Nightflow.Components.Challenge
            {
                ChallengeId = saved.ChallengeId,
                Type = (Nightflow.Components.ChallengeType)saved.Type,
                Difficulty = (Nightflow.Components.ChallengeDifficulty)saved.Difficulty,
                TargetValue = saved.TargetValue,
                CurrentProgress = saved.CurrentProgress,
                Completed = saved.Completed,
                RewardClaimed = saved.RewardClaimed,
                ScoreReward = saved.ScoreReward,
                ExpiresAt = saved.ExpiresAt,
                IsWeekly = saved.IsWeekly
            };
        }

        #endregion

        #region Public Utilities

        /// <summary>
        /// Force save immediately.
        /// </summary>
        public void ForceSave()
        {
            isDirty = true;
            SaveAll();
        }

        /// <summary>
        /// Mark save data as dirty (will be saved on next auto-save).
        /// </summary>
        public void MarkDirty()
        {
            isDirty = true;
        }

        /// <summary>
        /// Check if there is existing save data.
        /// </summary>
        public bool HasSaveData()
        {
            return File.Exists(MainSavePath);
        }

        /// <summary>
        /// Get the save file path for debugging.
        /// </summary>
        public string GetSavePath()
        {
            return MainSavePath;
        }

        #endregion
    }
}
