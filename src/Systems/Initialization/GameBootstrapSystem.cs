// ============================================================================
// Nightflow - Game Bootstrap System
// Runs once at startup to create initial game state
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Nightflow.Components;
using Nightflow.Buffers;
using Nightflow.Tags;
using Nightflow.Utilities;

namespace Nightflow.Systems
{
    /// <summary>
    /// Initializes the game world with player vehicle, initial track segments,
    /// and camera. Runs once in InitializationSystemGroup.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct GameBootstrapSystem : ISystem
    {
        private bool _initialized;

        // Track parameters
        private const int InitialSegments = 5;
        private const float SegmentLength = 200f;
        private const int LanesPerSegment = 4;
        private const float LaneWidth = 3.6f;

        // Player spawn
        private const float PlayerStartZ = 50f;
        private const int PlayerStartLane = 1;

        public void OnCreate(ref SystemState state)
        {
            _initialized = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized) return;
            _initialized = true;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // =============================================================
            // Create Initial Track Segments
            // =============================================================

            uint globalSeed = 12345u;
            float3 currentPos = float3.zero;
            float3 currentTangent = new float3(0, 0, 1); // Start heading forward

            for (int seg = 0; seg < InitialSegments; seg++)
            {
                Entity segmentEntity = CreateTrackSegment(
                    ref ecb,
                    ref state,
                    seg,
                    globalSeed,
                    currentPos,
                    currentTangent,
                    out float3 endPos,
                    out float3 endTangent
                );

                currentPos = endPos;
                currentTangent = endTangent;
            }

            // =============================================================
            // Create Player Vehicle
            // =============================================================

            Entity playerEntity = ecb.CreateEntity();

            // Core transform - spawn on starting lane
            float3 playerPos = new float3((PlayerStartLane - 1.5f) * LaneWidth, 0.5f, PlayerStartZ);
            ecb.AddComponent(playerEntity, new WorldTransform
            {
                Position = playerPos,
                Rotation = quaternion.identity
            });

            ecb.AddComponent(playerEntity, new Velocity
            {
                Forward = 20f,  // Start moving
                Lateral = 0f,
                Angular = 0f
            });

            ecb.AddComponent(playerEntity, new PreviousTransform
            {
                Position = playerPos,
                Rotation = quaternion.identity
            });

            // Vehicle control
            ecb.AddComponent(playerEntity, new PlayerInput());
            ecb.AddComponent(playerEntity, new Autopilot { Enabled = false });
            ecb.AddComponent(playerEntity, new SteeringState
            {
                CurrentAngle = 0f,
                TargetAngle = 0f,
                Smoothness = 8f
            });
            ecb.AddComponent(playerEntity, new LaneTransition());
            ecb.AddComponent(playerEntity, new DriftState());
            ecb.AddComponent(playerEntity, new SpeedTier { Tier = 0, Multiplier = 1f });

            // Lane following
            ecb.AddComponent(playerEntity, new LaneFollower
            {
                CurrentLane = PlayerStartLane,
                TargetLane = PlayerStartLane,
                LateralOffset = 0f,
                MagnetStrength = 1f,
                SplineParameter = PlayerStartZ / SegmentLength
            });

            // Damage & crash
            ecb.AddComponent(playerEntity, new DamageState());
            ecb.AddComponent(playerEntity, new Crashable
            {
                CrashThreshold = 100f,
                CrashSpeed = 50f,
                YawFailThreshold = 2.5f
            });
            ecb.AddComponent(playerEntity, new CollisionShape
            {
                Size = new float3(1f, 0.6f, 2.2f)
            });
            ecb.AddComponent(playerEntity, new CollisionEvent());
            ecb.AddComponent(playerEntity, new ImpulseData());
            ecb.AddComponent(playerEntity, new CrashState());

            // Scoring
            ecb.AddComponent(playerEntity, new ScoreSession
            {
                Active = true,
                Distance = 0f,
                Score = 0f,
                Multiplier = 1f,
                RiskMultiplier = 0f,
                HighestMultiplier = 1f
            });
            ecb.AddComponent(playerEntity, new RiskState
            {
                Value = 0f,
                Cap = 1f,
                RebuildRate = 1f,
                BrakePenaltyActive = false
            });
            ecb.AddComponent(playerEntity, new ScoreSummary
            {
                FinalScore = 0f,
                TotalDistance = 0f,
                HighestSpeed = 0f,
                ClosePasses = 0,
                HazardsDodged = 0,
                DriftRecoveries = 0,
                TimeSurvived = 0f,
                EndReason = CrashReason.None
            });

            // Signaling & detection
            ecb.AddComponent(playerEntity, new LightEmitter
            {
                Color = new float3(0f, 1f, 0.8f), // Cyan
                Intensity = 1f,
                Radius = 5f
            });
            ecb.AddComponent(playerEntity, new EmergencyDetection());

            // Headlights
            ecb.AddComponent(playerEntity, new Headlight
            {
                Color = new float3(1f, 0.95f, 0.9f),    // Warm white
                Intensity = 2f,
                Range = 80f,
                SpotAngle = 35f,
                LeftOffset = new float3(-0.8f, 0.4f, 2f),
                RightOffset = new float3(0.8f, 0.4f, 2f),
                HighBeam = false
            });

            // Tags
            ecb.AddComponent<PlayerVehicleTag>(playerEntity);

            // Input log buffer for replay
            ecb.AddBuffer<InputLogEntry>(playerEntity);

            // =============================================================
            // Add Audio Components to Player
            // =============================================================

            ecb.AddComponent(playerEntity, new EngineAudio
            {
                IsActive = true,
                RPM = 800f,
                TargetRPM = 800f,
                ThrottleInput = 0f,
                Load = 0f,
                IdleVolume = 0.5f,
                LowRPMVolume = 0f,
                MidRPMVolume = 0f,
                HighRPMVolume = 0f,
                BasePitch = 1f,
                CurrentPitch = 1f,
                State = EngineState.Idle,
                StateTimer = 0f
            });

            ecb.AddComponent(playerEntity, new TireAudio
            {
                IsActive = true,
                Speed = 0f,
                SlipRatio = 0f,
                SurfaceType = 0f,
                RollVolume = 0f,
                SkidVolume = 0f,
                WheelSlip = float4.zero
            });

            ecb.AddComponent(playerEntity, new WindAudio
            {
                IsActive = true,
                Speed = 0f,
                Volume = 0f,
                Pitch = 1f,
                TurbulenceAmount = 0f
            });

            ecb.AddComponent(playerEntity, new ScrapeAudio
            {
                IsActive = false,
                ContactPoint = float3.zero,
                Intensity = 0f,
                Volume = 0f,
                Pitch = 1f,
                Duration = 0f
            });

            // =============================================================
            // Create Camera
            // =============================================================

            Entity cameraEntity = ecb.CreateEntity();

            ecb.AddComponent(cameraEntity, new CameraState
            {
                Position = playerPos - new float3(0, -3f, 8f),
                Rotation = quaternion.identity,
                FOV = 60f,
                FollowDistance = 8f,
                FollowHeight = 3f,
                LateralOffset = 0f,
                Roll = 0f,
                Mode = 0
            });

            ecb.AddComponent(cameraEntity, new WorldTransform
            {
                Position = playerPos - new float3(0, -3f, 8f),
                Rotation = quaternion.identity
            });

            // Audio listener on camera
            ecb.AddComponent(cameraEntity, new AudioListener
            {
                Position = playerPos - new float3(0, -3f, 8f),
                Velocity = float3.zero,
                Forward = new float3(0, 0, 1),
                Up = new float3(0, 1, 0)
            });

            // =============================================================
            // Create Audio Controller Entity
            // =============================================================

            Entity audioEntity = ecb.CreateEntity();

            // Audio configuration singleton
            ecb.AddComponent(audioEntity, new AudioConfig
            {
                MasterVolume = 1f,
                MusicVolume = 0.7f,
                SFXVolume = 0.8f,
                EngineVolume = 0.8f,
                AmbientVolume = 0.5f,
                DopplerScale = 1f,
                SpeedOfSound = 343f,
                MinDistance = 2f,
                MaxDistance = 100f,
                RolloffFactor = 1f,
                EngineRPMSmoothing = 5f,
                EnginePitchRange = 1.2f,
                MusicIntensitySmoothing = 2f,
                MusicCrossfadeDuration = 2f
            });

            // Music state
            ecb.AddComponent(audioEntity, new MusicState
            {
                IsPlaying = true,
                CurrentTrack = MusicTrack.MainGameplay,
                Intensity = 0.3f,
                TargetIntensity = 0.3f,
                IntensitySmoothing = 2f,
                BaseLayerVolume = 0.8f,
                LowIntensityVolume = 0f,
                HighIntensityVolume = 0f,
                StingerVolume = 0f,
                BPM = 120f,
                CurrentBeat = 0f,
                MeasurePosition = 0f,
                PendingTransition = MusicTransition.None,
                TransitionProgress = 0f
            });

            // Ambient audio - start with open road
            ecb.AddComponent(audioEntity, new AmbientAudio
            {
                IsActive = true,
                Type = AmbientType.OpenRoad,
                Volume = 0.3f,
                TargetVolume = 0.3f,
                FadeSpeed = 1f,
                Pitch = 1f
            });

            // Audio event buffers
            ecb.AddBuffer<UIAudioEvent>(audioEntity);
            ecb.AddBuffer<OneShotAudioRequest>(audioEntity);
            ecb.AddBuffer<CollisionAudioEvent>(audioEntity);
            ecb.AddBuffer<MusicIntensityEvent>(audioEntity);

            // =============================================================
            // Create UI Controller Entity
            // =============================================================

            Entity uiEntity = ecb.CreateEntity();

            ecb.AddComponent(uiEntity, new UIState
            {
                SpeedKmh = 0f,
                SpeedMph = 0f,
                SpeedTier = 0,
                Score = 0f,
                DisplayScore = 0f,
                Multiplier = 1f,
                HighestMultiplier = 1f,
                MultiplierFlash = false,
                RiskValue = 0f,
                RiskCap = 1f,
                RiskPercent = 0f,
                DamageTotal = 0f,
                DamageFront = 0f,
                DamageRear = 0f,
                DamageLeft = 0f,
                DamageRight = 0f,
                DamageFlash = false,
                CriticalDamage = false,
                WarningPriority = 0,
                WarningFlash = false,
                EmergencyDistance = 0f,
                EmergencyETA = 0f,
                DistanceKm = 0f,
                TimeSurvived = 0f,
                SignalCount = 0,
                Signal0 = float4.zero,
                Signal1 = float4.zero,
                Signal2 = float4.zero,
                Signal3 = float4.zero,
                ShowPauseMenu = false,
                ShowCrashOverlay = false,
                ShowScoreSummary = false,
                ShowModeSelect = false,
                ShowMainMenu = true,      // Start at main menu
                ShowCredits = false,
                OverlayAlpha = 0f,
                ShowPressStart = true,    // Show "Press Start" initially
                MainMenuSelection = 0
            });

            ecb.AddComponent(uiEntity, new GameState
            {
                IsPaused = true,          // Game paused at main menu
                PauseCooldown = 0f,
                PauseCooldownMax = 5f,
                CrashPhase = CrashFlowPhase.None,
                CrashPhaseTimer = 0f,
                FadeAlpha = 0f,
                AutopilotQueued = false,
                PlayerControlActive = false,  // No player control at menu
                IdleTimer = 0f,
                CurrentMenu = MenuState.MainMenu,  // Start at main menu
                MenuVisible = true,
                TimeScale = 0f            // Time stopped at menu
            });

            ecb.AddComponent<UIControllerTag>(uiEntity);
            ecb.AddBuffer<HUDNotification>(uiEntity);

            // Copy audio buffers to UI entity for event system access
            ecb.AddBuffer<UIAudioEvent>(uiEntity);
            ecb.AddBuffer<OneShotAudioRequest>(uiEntity);

            // =============================================================
            // Create Force Feedback Event Entity (for wheel support)
            // =============================================================

            Entity ffbEntity = ecb.CreateEntity();
            ecb.AddComponent(ffbEntity, new ForceFeedbackEvent
            {
                EventType = ForceFeedbackEventType.None,
                Intensity = 0,
                Direction = 0,
                Triggered = false
            });

            // =============================================================
            // Create Score Summary Display Entity
            // Singleton for end-of-run statistics display
            // =============================================================

            Entity summaryDisplayEntity = ecb.CreateEntity();
            ecb.AddComponent(summaryDisplayEntity, new ScoreSummaryDisplay
            {
                FinalScore = 0f,
                TotalDistance = 0f,
                MaxSpeed = 0f,
                TimeSurvived = 0f,
                ClosePasses = 0,
                HazardsDodged = 0,
                DriftRecoveries = 0,
                PerfectSegments = 0,
                RiskBonusTotal = 0f,
                SpeedBonusTotal = 0f,
                EndReason = CrashReason.None,
                IsNewHighScore = false,
                LeaderboardRank = 0
            });

            // =============================================================
            // Create Main Menu State Entity
            // =============================================================

            Entity menuEntity = ecb.CreateEntity();

            ecb.AddComponent(menuEntity, new MainMenuState
            {
                SelectedIndex = 0,
                ItemCount = 5,  // Play, Leaderboard, Settings, Credits, Quit
                TitleAnimationComplete = false,
                DisplayTime = 0f,
                InputReceived = false,
                BlinkTimer = 0f,
                ShowPressStart = true
            });

            ecb.AddComponent(menuEntity, new GameSessionState
            {
                CurrentPhase = GameFlowPhase.TitleScreen,
                PreviousPhase = GameFlowPhase.TitleScreen,
                TransitionProgress = 0f,
                SessionActive = false,
                RunCount = 0,
                CreditsViewed = false,
                LastSelectedMode = GameMode.Nightflow
            });

            // =============================================================
            // Create Visual Effects Singleton Entities
            // Required by presentation systems for proper boot
            // =============================================================

            // Crash Flash Effect - for screen flash on impact
            Entity crashFlashEntity = ecb.CreateEntity();
            ecb.AddComponent(crashFlashEntity, new CrashFlashEffect
            {
                IsActive = false,
                Intensity = 0f,
                Duration = 0.3f,
                Timer = 0f,
                FlashColor = new float4(1f, 0.3f, 0.2f, 1f),  // Red-orange flash
                Phase = CrashFlashPhase.None
            });

            // Speed Line Effect - for speed streaks at high velocity
            Entity speedLineEntity = ecb.CreateEntity();
            ecb.AddComponent(speedLineEntity, new SpeedLineEffect
            {
                IsActive = false,
                Intensity = 0f,
                SpeedThreshold = 55.5f,     // ~200 km/h
                LineLength = 2.0f,
                LineDensity = 30f,
                LineColor = new float4(0.7f, 0.9f, 1f, 0.6f),  // Cyan-white
                FadeSpeed = 3.0f
            });

            // Particle System Config - global particle settings
            Entity particleConfigEntity = ecb.CreateEntity();
            ecb.AddComponent(particleConfigEntity, new ParticleSystemConfig
            {
                // Spark settings (orange-yellow sparks)
                SparkColorStart = new float4(1f, 0.8f, 0.3f, 1f),
                SparkColorEnd = new float4(1f, 0.4f, 0.1f, 0f),
                SparkSizeMin = 0.02f,
                SparkSizeMax = 0.08f,
                SparkSpeedMin = 5f,
                SparkSpeedMax = 15f,
                SparkLifetime = 0.8f,
                SparkGravity = 9.8f,

                // Tire smoke settings (gray smoke)
                SmokeColorStart = new float4(0.6f, 0.6f, 0.65f, 0.4f),
                SmokeColorEnd = new float4(0.4f, 0.4f, 0.45f, 0f),
                SmokeSizeStart = 0.3f,
                SmokeSizeEnd = 1.5f,
                SmokeSpeed = 2f,
                SmokeLifetime = 1.5f,
                SmokeDrag = 2f,

                // Speed line settings
                SpeedLineThreshold = 55.5f,
                SpeedLineMaxIntensity = 1f,
                SpeedLineColor = new float4(0.8f, 0.9f, 1f, 0.5f),

                // Global settings
                MaxTotalParticles = 500,
                GlobalIntensityMultiplier = 1f
            });

            // City Skyline Controller - for background city rendering
            Entity skylineEntity = ecb.CreateEntity();
            ecb.AddComponent<CitySkylineTag>(skylineEntity);

            // Playback command buffer
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private Entity CreateTrackSegment(
            ref EntityCommandBuffer ecb,
            ref SystemState state,
            int segmentIndex,
            uint globalSeed,
            float3 startPos,
            float3 startTangent,
            out float3 endPos,
            out float3 endTangent)
        {
            // Generate segment parameters deterministically
            uint segHash = SplineUtilities.Hash(globalSeed, (uint)segmentIndex);

            // For initial segments, keep them straight
            float yawChange = 0f;
            if (segmentIndex > 2)
            {
                // Add gentle curves after first few segments
                yawChange = SplineUtilities.HashToFloatRange(
                    SplineUtilities.Hash(segHash, 0),
                    -0.1f, 0.1f
                );
            }

            // Apply yaw rotation to tangent
            quaternion yawRot = quaternion.RotateY(yawChange);
            endTangent = math.mul(yawRot, startTangent);

            // Calculate end position
            float length = SegmentLength;
            endPos = startPos + math.normalize(startTangent) * length;

            // Scale tangents for Hermite (alpha = 0.5)
            float alpha = 0.5f;
            float3 t0 = startTangent * length * alpha;
            float3 t1 = endTangent * length * alpha;

            // Create segment entity
            Entity segmentEntity = ecb.CreateEntity();

            ecb.AddComponent(segmentEntity, new TrackSegment
            {
                Index = segmentIndex,
                Type = 0, // Straight
                StartZ = startPos.z,
                EndZ = endPos.z,
                Length = length,
                NumLanes = LanesPerSegment
            });

            ecb.AddComponent(segmentEntity, new HermiteSpline
            {
                P0 = startPos,
                P1 = endPos,
                T0 = t0,
                T1 = t1
            });

            ecb.AddComponent(segmentEntity, new WorldTransform
            {
                Position = startPos,
                Rotation = quaternion.LookRotation(startTangent, new float3(0, 1, 0))
            });

            // Add spline sample buffer
            var buffer = ecb.AddBuffer<SplineSample>(segmentEntity);

            // Pre-sample spline at regular intervals
            const int samples = 20;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;

                SplineUtilities.BuildFrameAtT(startPos, t0, endPos, t1, t,
                    out float3 position, out float3 forward, out float3 right, out float3 up);

                buffer.Add(new SplineSample
                {
                    Position = position,
                    Forward = forward,
                    Right = right,
                    Up = up,
                    Parameter = t
                });
            }

            ecb.AddComponent<TrackSegmentTag>(segmentEntity);

            return segmentEntity;
        }
    }
}
