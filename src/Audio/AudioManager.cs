// ============================================================================
// Nightflow - Audio Manager
// MonoBehaviour bridge connecting ECS audio systems to Unity AudioSources
// ============================================================================

using UnityEngine;
using UnityEngine.Audio;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using Nightflow.Components;

namespace Nightflow.Audio
{
    /// <summary>
    /// Central audio manager that creates and manages AudioSources based on ECS audio state.
    /// Handles pooling, 3D positioning, and mixer routing.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio Clip Collection")]
        [Tooltip("Assign an AudioClipCollection to automatically load all clips. If assigned, individual clip fields below are ignored.")]
        [SerializeField] private AudioClipCollection clipCollection;

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer mainMixer;
        [SerializeField] private AudioMixerGroup masterGroup;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup engineGroup;
        [SerializeField] private AudioMixerGroup ambientGroup;

        [Header("Audio Clips - Engine (ignored if Collection is assigned)")]
        [SerializeField] private AudioClip engineIdle;
        [SerializeField] private AudioClip engineLowRPM;
        [SerializeField] private AudioClip engineMidRPM;
        [SerializeField] private AudioClip engineHighRPM;
        [SerializeField] private AudioClip tireRoll;
        [SerializeField] private AudioClip tireSkid;
        [SerializeField] private AudioClip windLoop;

        [Header("Audio Clips - Collision")]
        [SerializeField] private AudioClip[] lightImpacts;
        [SerializeField] private AudioClip[] mediumImpacts;
        [SerializeField] private AudioClip[] heavyImpacts;
        [SerializeField] private AudioClip metalScrape;
        [SerializeField] private AudioClip glassShatter;

        [Header("Audio Clips - Siren")]
        [SerializeField] private AudioClip policeSiren;
        [SerializeField] private AudioClip ambulanceSiren;
        [SerializeField] private AudioClip fireHorn;

        [Header("Audio Clips - Ambient")]
        [SerializeField] private AudioClip openRoadAmbience;
        [SerializeField] private AudioClip distantTraffic;
        [SerializeField] private AudioClip tunnelDrone;

        [Header("Audio Clips - Music")]
        [SerializeField] private AudioClip musicBase;
        [SerializeField] private AudioClip musicLowIntensity;
        [SerializeField] private AudioClip musicHighIntensity;
        [SerializeField] private AudioClip musicTerminal;
        [SerializeField] private AudioClip musicMenu;

        [Header("Audio Clips - UI")]
        [SerializeField] private AudioClip scoreTick;
        [SerializeField] private AudioClip multiplierUp;
        [SerializeField] private AudioClip multiplierLost;
        [SerializeField] private AudioClip damageWarning;
        [SerializeField] private AudioClip nearMiss;
        [SerializeField] private AudioClip laneChange;
        [SerializeField] private AudioClip menuSelect;
        [SerializeField] private AudioClip menuBack;
        [SerializeField] private AudioClip pauseSound;
        [SerializeField] private AudioClip highScore;
        [SerializeField] private AudioClip gameOver;

        [Header("Settings")]
        [SerializeField] private int oneShotPoolSize = 20;
        [SerializeField] private float dopplerLevel = 1f;
        [SerializeField] private float spatialBlend3D = 1f;

        // Audio source pools
        private List<AudioSource> oneShotPool;
        private int nextOneShotIndex;

        // Persistent sources
        private AudioSource[] engineSources;   // Idle, Low, Mid, High
        private AudioSource tireRollSource;
        private AudioSource tireSkidSource;
        private AudioSource windSource;
        private AudioSource scrapeSource;
        private Dictionary<Entity, AudioSource> sirenSources;

        // Ambient sources
        private AudioSource openRoadSource;
        private AudioSource distantTrafficSource;
        private AudioSource tunnelDroneSource;

        // Music sources
        private AudioSource musicBaseSource;
        private AudioSource musicLowSource;
        private AudioSource musicHighSource;

        // ECS access
        private EntityManager entityManager;
        private EntityQuery engineQuery;
        private EntityQuery sirenQuery;
        private EntityQuery oneShotQuery;
        private EntityQuery uiAudioQuery;
        private EntityQuery collisionAudioQuery;
        private bool ecsInitialized;

        private void Awake()
        {
            // Load clips from collection if assigned
            if (clipCollection != null)
            {
                LoadFromCollection(clipCollection);
            }

            InitializeAudioSources();
        }

        /// <summary>
        /// Loads all audio clips from an AudioClipCollection.
        /// This allows centralized clip management without manual assignment.
        /// </summary>
        public void LoadFromCollection(AudioClipCollection collection)
        {
            if (collection == null) return;

            // Engine sounds
            engineIdle = collection.engineIdle;
            engineLowRPM = collection.engineLowRPM;
            engineMidRPM = collection.engineMidRPM;
            engineHighRPM = collection.engineHighRPM;
            tireRoll = collection.tireRoll;
            tireSkid = collection.tireSkid;
            windLoop = collection.windLoop;

            // Collision sounds
            lightImpacts = collection.lightImpacts;
            mediumImpacts = collection.mediumImpacts;
            heavyImpacts = collection.heavyImpacts;
            metalScrape = collection.metalScrape;
            glassShatter = collection.glassShatter;

            // Siren sounds
            policeSiren = collection.policeSiren;
            ambulanceSiren = collection.ambulanceSiren;
            fireHorn = collection.fireHorn;

            // Ambient sounds
            openRoadAmbience = collection.openRoadAmbience;
            distantTraffic = collection.distantTraffic;
            tunnelDrone = collection.tunnelDrone;

            // Music
            musicBase = collection.musicBase;
            musicLowIntensity = collection.musicLowIntensity;
            musicHighIntensity = collection.musicHighIntensity;
            musicTerminal = collection.musicTerminal;
            musicMenu = collection.musicMenu;

            // UI sounds
            scoreTick = collection.scoreTick;
            multiplierUp = collection.multiplierUp;
            multiplierLost = collection.multiplierLost;
            damageWarning = collection.damageWarning;
            nearMiss = collection.nearMiss;
            laneChange = collection.laneChange;
            menuSelect = collection.menuSelect;
            menuBack = collection.menuBack;
            pauseSound = collection.pauseSound;
            highScore = collection.highScore;
            gameOver = collection.gameOver;
        }

        /// <summary>
        /// Gets or sets the clip collection. Setting will reload all clips.
        /// </summary>
        public AudioClipCollection ClipCollection
        {
            get => clipCollection;
            set
            {
                clipCollection = value;
                if (value != null)
                {
                    LoadFromCollection(value);
                }
            }
        }

        private void Start()
        {
            TryInitializeECS();
        }

        private void TryInitializeECS()
        {
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                engineQuery = entityManager.CreateEntityQuery(typeof(EngineAudio));
                sirenQuery = entityManager.CreateEntityQuery(typeof(SirenAudio));
                oneShotQuery = entityManager.CreateEntityQuery(typeof(OneShotAudioRequest));
                uiAudioQuery = entityManager.CreateEntityQuery(typeof(UIAudioEvent));
                collisionAudioQuery = entityManager.CreateEntityQuery(typeof(CollisionAudioEvent));
                ecsInitialized = true;
            }
        }

        private void InitializeAudioSources()
        {
            // Create one-shot pool
            oneShotPool = new List<AudioSource>(oneShotPoolSize);
            for (int i = 0; i < oneShotPoolSize; i++)
            {
                var source = CreatePooledSource($"OneShot_{i}", sfxGroup);
                oneShotPool.Add(source);
            }

            // Create engine sources
            engineSources = new AudioSource[4];
            engineSources[0] = CreateLoopingSource("Engine_Idle", engineIdle, engineGroup);
            engineSources[1] = CreateLoopingSource("Engine_Low", engineLowRPM, engineGroup);
            engineSources[2] = CreateLoopingSource("Engine_Mid", engineMidRPM, engineGroup);
            engineSources[3] = CreateLoopingSource("Engine_High", engineHighRPM, engineGroup);

            // Tire sources
            tireRollSource = CreateLoopingSource("Tire_Roll", tireRoll, sfxGroup);
            tireSkidSource = CreateLoopingSource("Tire_Skid", tireSkid, sfxGroup);

            // Wind source
            windSource = CreateLoopingSource("Wind", windLoop, ambientGroup);

            // Scrape source
            scrapeSource = CreateLoopingSource("Scrape", metalScrape, sfxGroup);

            // Siren sources dictionary
            sirenSources = new Dictionary<Entity, AudioSource>();

            // Ambient sources
            openRoadSource = CreateLoopingSource("Ambient_Road", openRoadAmbience, ambientGroup);
            distantTrafficSource = CreateLoopingSource("Ambient_Traffic", distantTraffic, ambientGroup);
            tunnelDroneSource = CreateLoopingSource("Ambient_Tunnel", tunnelDrone, ambientGroup);

            // Music sources
            musicBaseSource = CreateLoopingSource("Music_Base", musicBase, musicGroup);
            musicLowSource = CreateLoopingSource("Music_Low", musicLowIntensity, musicGroup);
            musicHighSource = CreateLoopingSource("Music_High", musicHighIntensity, musicGroup);
        }

        private AudioSource CreatePooledSource(string name, AudioMixerGroup group)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = group;
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend3D;
            source.dopplerLevel = dopplerLevel;
            source.minDistance = 2f;
            source.maxDistance = 50f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            return source;
        }

        private AudioSource CreateLoopingSource(string name, AudioClip clip, AudioMixerGroup group)
        {
            var source = CreatePooledSource(name, group);
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0f; // 2D by default for looping sounds
            if (clip != null)
            {
                source.Play();
                source.volume = 0f; // Start silent
            }
            return source;
        }

        private void Update()
        {
            if (!ecsInitialized)
            {
                TryInitializeECS();
                return;
            }

            UpdateEngineAudio();
            UpdateSirenAudio();
            UpdateAmbientAudio();
            UpdateMusicAudio();
            ProcessUIAudioEvents();
            ProcessCollisionAudioEvents();
            ProcessOneShotRequests();
        }

        private void UpdateEngineAudio()
        {
            if (engineQuery.IsEmpty) return;

            var engineAudio = engineQuery.GetSingleton<EngineAudio>();

            if (!engineAudio.IsActive)
            {
                foreach (var source in engineSources)
                {
                    source.volume = Mathf.Lerp(source.volume, 0f, Time.deltaTime * 5f);
                }
                return;
            }

            // Update engine layer volumes
            float masterVolume = 0.8f;
            engineSources[0].volume = Mathf.Lerp(engineSources[0].volume, engineAudio.IdleVolume * masterVolume, Time.deltaTime * 8f);
            engineSources[1].volume = Mathf.Lerp(engineSources[1].volume, engineAudio.LowRPMVolume * masterVolume, Time.deltaTime * 8f);
            engineSources[2].volume = Mathf.Lerp(engineSources[2].volume, engineAudio.MidRPMVolume * masterVolume, Time.deltaTime * 8f);
            engineSources[3].volume = Mathf.Lerp(engineSources[3].volume, engineAudio.HighRPMVolume * masterVolume, Time.deltaTime * 8f);

            // Update pitches
            foreach (var source in engineSources)
            {
                source.pitch = Mathf.Lerp(source.pitch, engineAudio.CurrentPitch, Time.deltaTime * 10f);
            }

            // Update tire audio
            var tireQuery = entityManager.CreateEntityQuery(typeof(TireAudio));
            if (!tireQuery.IsEmpty)
            {
                var tireAudio = tireQuery.GetSingleton<TireAudio>();
                tireRollSource.volume = Mathf.Lerp(tireRollSource.volume, tireAudio.RollVolume, Time.deltaTime * 5f);
                tireSkidSource.volume = Mathf.Lerp(tireSkidSource.volume, tireAudio.SkidVolume, Time.deltaTime * 8f);
            }
            tireQuery.Dispose();

            // Update wind audio
            var windQuery = entityManager.CreateEntityQuery(typeof(WindAudio));
            if (!windQuery.IsEmpty)
            {
                var windAudio = windQuery.GetSingleton<WindAudio>();
                windSource.volume = Mathf.Lerp(windSource.volume, windAudio.Volume, Time.deltaTime * 3f);
                windSource.pitch = Mathf.Lerp(windSource.pitch, windAudio.Pitch, Time.deltaTime * 3f);
            }
            windQuery.Dispose();

            // Update scrape audio
            var scrapeQuery = entityManager.CreateEntityQuery(typeof(ScrapeAudio));
            if (!scrapeQuery.IsEmpty)
            {
                var scrapeAudio = scrapeQuery.GetSingleton<ScrapeAudio>();
                scrapeSource.volume = Mathf.Lerp(scrapeSource.volume, scrapeAudio.Volume, Time.deltaTime * 10f);
                scrapeSource.pitch = scrapeAudio.Pitch;
            }
            scrapeQuery.Dispose();
        }

        private void UpdateSirenAudio()
        {
            // This would iterate through siren entities and update their audio sources
            // For simplicity, handling a single siren
            if (sirenQuery.IsEmpty) return;

            foreach (var sirenAudio in sirenQuery.ToComponentDataArray<SirenAudio>(Unity.Collections.Allocator.Temp))
            {
                if (!sirenAudio.IsActive) continue;

                // In a full implementation, we'd track per-entity sources
                // For now, just demonstrate the concept
            }
        }

        private void UpdateAmbientAudio()
        {
            var ambientQuery = entityManager.CreateEntityQuery(typeof(AmbientAudio));
            if (ambientQuery.IsEmpty)
            {
                ambientQuery.Dispose();
                return;
            }

            foreach (var ambient in ambientQuery.ToComponentDataArray<AmbientAudio>(Unity.Collections.Allocator.Temp))
            {
                AudioSource source = ambient.Type switch
                {
                    AmbientType.OpenRoad => openRoadSource,
                    AmbientType.DistantTraffic => distantTrafficSource,
                    AmbientType.TunnelDrone => tunnelDroneSource,
                    _ => null
                };

                if (source != null)
                {
                    source.volume = Mathf.Lerp(source.volume, ambient.Volume, Time.deltaTime * 2f);
                }
            }
            ambientQuery.Dispose();
        }

        private void UpdateMusicAudio()
        {
            var musicQuery = entityManager.CreateEntityQuery(typeof(MusicState));
            if (musicQuery.IsEmpty)
            {
                musicQuery.Dispose();
                return;
            }

            var musicState = musicQuery.GetSingleton<MusicState>();
            musicQuery.Dispose();

            if (!musicState.IsPlaying)
            {
                musicBaseSource.volume = Mathf.Lerp(musicBaseSource.volume, 0f, Time.deltaTime * 2f);
                musicLowSource.volume = Mathf.Lerp(musicLowSource.volume, 0f, Time.deltaTime * 2f);
                musicHighSource.volume = Mathf.Lerp(musicHighSource.volume, 0f, Time.deltaTime * 2f);
                return;
            }

            float masterMusic = 0.7f;
            musicBaseSource.volume = Mathf.Lerp(musicBaseSource.volume, musicState.BaseLayerVolume * masterMusic, Time.deltaTime * 3f);
            musicLowSource.volume = Mathf.Lerp(musicLowSource.volume, musicState.LowIntensityVolume * masterMusic, Time.deltaTime * 3f);
            musicHighSource.volume = Mathf.Lerp(musicHighSource.volume, musicState.HighIntensityVolume * masterMusic, Time.deltaTime * 3f);
        }

        private void ProcessUIAudioEvents()
        {
            foreach (var entity in uiAudioQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                if (!entityManager.HasBuffer<UIAudioEvent>(entity)) continue;

                var buffer = entityManager.GetBuffer<UIAudioEvent>(entity);

                for (int i = 0; i < buffer.Length; i++)
                {
                    var evt = buffer[i];
                    PlayUISound(evt);
                }

                buffer.Clear();
            }
        }

        private void PlayUISound(UIAudioEvent evt)
        {
            AudioClip clip = evt.Type switch
            {
                UISoundType.ScoreTick => scoreTick,
                UISoundType.MultiplierUp => multiplierUp,
                UISoundType.MultiplierLost => multiplierLost,
                UISoundType.DamageWarning => damageWarning,
                UISoundType.NearMiss => nearMiss,
                UISoundType.LaneChange => laneChange,
                UISoundType.MenuSelect => menuSelect,
                UISoundType.MenuBack => menuBack,
                UISoundType.Pause => pauseSound,
                UISoundType.HighScore => highScore,
                UISoundType.GameOver => gameOver,
                _ => null
            };

            if (clip == null) return;

            var source = GetPooledSource();
            if (source == null) return;

            source.clip = clip;
            source.volume = evt.Volume;
            source.pitch = evt.Pitch;
            source.spatialBlend = 0f; // UI sounds are 2D
            source.Play();
        }

        private void ProcessCollisionAudioEvents()
        {
            foreach (var entity in collisionAudioQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                if (!entityManager.HasBuffer<CollisionAudioEvent>(entity)) continue;

                var buffer = entityManager.GetBuffer<CollisionAudioEvent>(entity);

                for (int i = 0; i < buffer.Length; i++)
                {
                    var evt = buffer[i];
                    PlayCollisionSound(evt);
                }

                buffer.Clear();
            }
        }

        private void PlayCollisionSound(CollisionAudioEvent evt)
        {
            AudioClip clip = evt.Type switch
            {
                CollisionAudioType.LightImpact => lightImpacts.Length > 0 ? lightImpacts[Random.Range(0, lightImpacts.Length)] : null,
                CollisionAudioType.MediumImpact => mediumImpacts.Length > 0 ? mediumImpacts[Random.Range(0, mediumImpacts.Length)] : null,
                CollisionAudioType.HeavyImpact => heavyImpacts.Length > 0 ? heavyImpacts[Random.Range(0, heavyImpacts.Length)] : null,
                CollisionAudioType.MetalScrape => metalScrape,
                CollisionAudioType.GlassShatter => glassShatter,
                _ => null
            };

            if (clip == null) return;

            var source = GetPooledSource();
            if (source == null) return;

            source.clip = clip;
            source.transform.position = new Vector3(evt.Position.x, evt.Position.y, evt.Position.z);

            // Volume based on impulse force
            float volume = Mathf.Clamp01(evt.Impulse / 50f);
            source.volume = 0.5f + volume * 0.5f;

            // Pitch variation based on impact
            source.pitch = 0.9f + Random.Range(0f, 0.2f);

            source.spatialBlend = spatialBlend3D;
            source.minDistance = 2f;
            source.maxDistance = 50f;
            source.Play();
        }

        private void ProcessOneShotRequests()
        {
            foreach (var oneShotBuffer in oneShotQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                var buffer = entityManager.GetBuffer<OneShotAudioRequest>(oneShotBuffer);

                for (int i = 0; i < buffer.Length; i++)
                {
                    var request = buffer[i];
                    PlayOneShot(request);
                }

                buffer.Clear();
            }
        }

        private void PlayOneShot(OneShotAudioRequest request)
        {
            var source = GetPooledSource();
            if (source == null) return;

            AudioClip clip = GetClipByID(request.ClipID);
            if (clip == null) return;

            source.clip = clip;
            source.volume = request.Volume;
            source.pitch = request.Pitch;
            source.spatialBlend = request.Is3D ? spatialBlend3D : 0f;

            if (request.Is3D)
            {
                source.transform.position = new Vector3(request.Position.x, request.Position.y, request.Position.z);
                source.minDistance = request.MinDistance;
                source.maxDistance = request.MaxDistance;
            }

            source.Play();
        }

        private AudioSource GetPooledSource()
        {
            for (int i = 0; i < oneShotPool.Count; i++)
            {
                int index = (nextOneShotIndex + i) % oneShotPool.Count;
                if (!oneShotPool[index].isPlaying)
                {
                    nextOneShotIndex = (index + 1) % oneShotPool.Count;
                    return oneShotPool[index];
                }
            }

            // All busy, steal oldest
            var source = oneShotPool[nextOneShotIndex];
            nextOneShotIndex = (nextOneShotIndex + 1) % oneShotPool.Count;
            return source;
        }

        private AudioClip GetClipByID(int clipID)
        {
            // Collision sounds (0-10)
            if (clipID >= 1 && clipID <= 3 && lightImpacts.Length > 0)
                return lightImpacts[clipID % lightImpacts.Length];
            if (clipID >= 4 && clipID <= 6 && mediumImpacts.Length > 0)
                return mediumImpacts[(clipID - 4) % mediumImpacts.Length];
            if (clipID >= 7 && clipID <= 9 && heavyImpacts.Length > 0)
                return heavyImpacts[(clipID - 7) % heavyImpacts.Length];
            if (clipID == 10) return glassShatter;

            // UI sounds (100+)
            return (clipID - 100) switch
            {
                (int)UISoundType.ScoreTick => scoreTick,
                (int)UISoundType.MultiplierUp => multiplierUp,
                (int)UISoundType.MultiplierLost => multiplierLost,
                (int)UISoundType.DamageWarning => damageWarning,
                (int)UISoundType.NearMiss => nearMiss,
                (int)UISoundType.LaneChange => laneChange,
                (int)UISoundType.MenuSelect => menuSelect,
                (int)UISoundType.MenuBack => menuBack,
                (int)UISoundType.Pause => pauseSound,
                (int)UISoundType.HighScore => highScore,
                (int)UISoundType.GameOver => gameOver,
                _ => null
            };
        }

        private void OnDestroy()
        {
            // Cleanup
            foreach (var kvp in sirenSources)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
        }
    }
}
