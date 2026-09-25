using System;
using System.Collections;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.World;
using CastleOfTheD20.Bosses;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Discrete music track identifiers for Castle of the D20.
    /// Maps 1:1 to the 7 MP3 assets in Assets/Music/.
    /// </summary>
    public enum MusicTrackType
    {
        None = 0,
        Village = 1,                 // Assets/Music/VillageSong.mp3
        CastleAdventure = 2,         // Assets/Music/Castle_adventure_song.mp3
        CellarCombat = 3,            // Assets/Music/Cellar_combat_music.mp3
        CursedCommanderCombat = 4,   // Assets/Music/CursedCommander_Combat_music.mp3
        MalakorCombat = 5,           // Assets/Music/Malakor_combat_music.mp3
        GargoyleKingPhase1 = 6,      // Assets/Music/1_Combat_GargoyleKing_music.mp3
        GargoyleKingPhase2 = 7       // Assets/Music/2_Combat_GargoyleKing_music.mp3
    }

    /// <summary>
    /// Persistent, WebGL-compatible Music Manager implementing dual-channel equal-power crossfading.
    /// Operates with zero external dependencies and manages all background music transitions.
    /// </summary>
    [ExecuteAlways]
    public class MusicManager : MonoBehaviour
    {
        #region Singleton

        public static MusicManager Instance { get; set; }

        #endregion

        #region Constants

        public const float DEFAULT_FADE_DURATION = 1.2f;

        #endregion

        #region Serialized Fields - Audio Channels

        [Header("Audio Channels (2D Stereo Crossfade)")]
        [SerializeField] private AudioSource sourceA;
        [SerializeField] private AudioSource sourceB;

        #endregion

        #region Serialized Fields - Volume Settings

        [Header("Volume Settings (0.0 - 1.0)")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.7f;

        #endregion

        #region Serialized Fields - Music Track Clips

        [Header("Music Tracks (Assets/Music/)")]
        [Tooltip("VillageSong.mp3 - Oakhaven / Kivenkolo Village theme")]
        [SerializeField] private AudioClip villageSongClip;

        [Tooltip("Castle_adventure_song.mp3 - Forest, Courtyard, Library, Crown Hall exploration theme")]
        [SerializeField] private AudioClip castleAdventureSongClip;

        [Tooltip("Cellar_combat_music.mp3 - Tavern Cellar combat encounter")]
        [SerializeField] private AudioClip cellarCombatClip;

        [Tooltip("CursedCommander_Combat_music.mp3 - Wing 1 Boss battle")]
        [SerializeField] private AudioClip cursedCommanderCombatClip;

        [Tooltip("Malakor_combat_music.mp3 - Wing 2 Boss battle")]
        [SerializeField] private AudioClip malakorCombatClip;

        [Tooltip("1_Combat_GargoyleKing_music.mp3 - Wing 3 Final Boss Phase 1")]
        [SerializeField] private AudioClip gargoyleKingPhase1Clip;

        [Tooltip("2_Combat_GargoyleKing_music.mp3 - Wing 3 Final Boss Phase 2 (Stone Form)")]
        [SerializeField] private AudioClip gargoyleKingPhase2Clip;

        #endregion

        #region Private State

        private AudioSource activeSource;
        private AudioSource standbySource;
        private Coroutine crossfadeCoroutine;
        private Coroutine restoreDelayCoroutine;
        private MusicTrackType currentTrackType = MusicTrackType.None;
        private MusicTrackType currentExplorationTrack = MusicTrackType.Village;

        #endregion

        #region Public Properties

        public AudioSource SourceA
        {
            get
            {
                if (sourceA == null) InitializeAudioSources();
                return sourceA;
            }
        }

        public AudioSource SourceB
        {
            get
            {
                if (sourceB == null) InitializeAudioSources();
                return sourceB;
            }
        }

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                ApplyVolume();
            }
        }

        public float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                ApplyVolume();
            }
        }

        public float CurrentMaxVolume => masterVolume * musicVolume;

        public bool IsPlaying => (activeSource != null && activeSource.isPlaying) ||
                                 (standbySource != null && standbySource.isPlaying);

        public AudioClip CurrentClip => activeSource != null ? activeSource.clip : null;

        public MusicTrackType CurrentTrack => currentTrackType;

        public MusicTrackType CurrentExplorationTrack => currentExplorationTrack;

        public bool IsCrossFading => crossfadeCoroutine != null;

        public AudioClip VillageSongClip => villageSongClip;
        public AudioClip CastleAdventureSongClip => castleAdventureSongClip;
        public AudioClip CellarCombatClip => cellarCombatClip;
        public AudioClip CursedCommanderCombatClip => cursedCommanderCombatClip;
        public AudioClip MalakorCombatClip => malakorCombatClip;
        public AudioClip GargoyleKingPhase1Clip => gargoyleKingPhase1Clip;
        public AudioClip GargoyleKingPhase2Clip => gargoyleKingPhase2Clip;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Instance.gameObject != gameObject)
                {
                    if (Application.isPlaying)
                        Destroy(gameObject);
                    else
                        DestroyImmediate(gameObject);
                }
                else
                {
                    if (Application.isPlaying)
                        Destroy(this);
                    else
                        DestroyImmediate(this);
                }
                return;
            }

            Instance = this;
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            InitializeAudioSources();
            AudioListener.pause = false;

            // Two-way handshake: silence legacy AudioManager BGM if already active
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopBGM();
            }
        }

        private void Start()
        {
            SubscribeToEvents();

            // Auto-start default exploration music if not already playing
            if (!IsPlaying && villageSongClip != null)
            {
                PlayTrack(MusicTrackType.Village, DEFAULT_FADE_DURATION);
            }
        }

        private void Update()
        {
            // WebGL Autoplay Unmuting: resume audio if browser muted AudioListener prior to user interaction
            if (AudioListener.pause && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            {
                AudioListener.pause = false;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Audio Source Initialization

        public void InitializeAudioSources()
        {
            if (sourceA == null)
            {
                Transform existingA = transform.Find("MusicSource_A");
                if (existingA != null)
                {
                    sourceA = existingA.GetComponent<AudioSource>();
                }
                if (sourceA == null)
                {
                    GameObject aObj = new GameObject("MusicSource_A");
                    aObj.transform.SetParent(transform);
                    sourceA = aObj.AddComponent<AudioSource>();
                }
            }

            if (sourceB == null)
            {
                Transform existingB = transform.Find("MusicSource_B");
                if (existingB != null)
                {
                    sourceB = existingB.GetComponent<AudioSource>();
                }
                if (sourceB == null)
                {
                    GameObject bObj = new GameObject("MusicSource_B");
                    bObj.transform.SetParent(transform);
                    sourceB = bObj.AddComponent<AudioSource>();
                }
            }

            ConfigureAudioSource(sourceA);
            ConfigureAudioSource(sourceB);

            activeSource = sourceA;
            standbySource = sourceB;
        }

        private void ConfigureAudioSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;          // Pure 2D Stereo
            source.panStereo = 0f;
            source.bypassEffects = false;
            source.bypassListenerEffects = false;
            source.bypassReverbZones = true;
            source.priority = 0;               // Maximum priority (prevents voice stealing)
            source.volume = 0f;
        }

        #endregion

        #region Public Playback & Crossfade API

        /// <summary>
        /// Plays a track identified by MusicTrackType with smooth equal-power crossfading.
        /// </summary>
        public void PlayTrack(MusicTrackType trackType, float fadeDuration = DEFAULT_FADE_DURATION)
        {
            if (trackType == MusicTrackType.None)
            {
                StopMusic(fadeDuration);
                return;
            }

            AudioClip clip = GetTrackClip(trackType);
            if (clip == null)
            {
                Debug.LogWarning($"[MusicManager] Audio clip for track '{trackType}' is not assigned.");
                return;
            }

            if (IsExplorationTrack(trackType))
            {
                currentExplorationTrack = trackType;
            }

            currentTrackType = trackType;
            PlayMusic(clip, fadeDuration);
        }

        /// <summary>
        /// Plays an arbitrary AudioClip with smooth equal-power crossfading.
        /// </summary>
        public void PlayMusic(AudioClip clip, float fadeDuration = DEFAULT_FADE_DURATION)
        {
            // Cancel any pending exploration restoration
            if (restoreDelayCoroutine != null)
            {
                StopCoroutine(restoreDelayCoroutine);
                restoreDelayCoroutine = null;
            }

            // If clip is null, smoothly fade out to silence
            if (clip == null)
            {
                StopMusic(fadeDuration);
                return;
            }

            // Redundant request check: already playing this exact clip and not crossfading
            if (activeSource != null && activeSource.clip == clip && activeSource.isPlaying && crossfadeCoroutine == null)
            {
                return;
            }

            // Update tracked track type if clip matches known track
            MusicTrackType resolved = GetTrackTypeForClip(clip);
            if (resolved != MusicTrackType.None)
            {
                currentTrackType = resolved;
                if (IsExplorationTrack(resolved))
                {
                    currentExplorationTrack = resolved;
                }
            }

            // Interrupt any running crossfade
            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
                crossfadeCoroutine = null;
            }

            crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(clip, fadeDuration));
        }

        /// <summary>
        /// Fades out currently playing music to complete silence over fadeDuration.
        /// </summary>
        public void StopMusic(float fadeDuration = DEFAULT_FADE_DURATION)
        {
            if (restoreDelayCoroutine != null)
            {
                StopCoroutine(restoreDelayCoroutine);
                restoreDelayCoroutine = null;
            }

            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
                crossfadeCoroutine = null;
            }

            currentTrackType = MusicTrackType.None;
            crossfadeCoroutine = StartCoroutine(FadeOutRoutine(fadeDuration));
        }

        /// <summary>
        /// Updates master and music volume levels and immediately reflects on the active channel.
        /// </summary>
        public void SetVolume(float master, float music)
        {
            masterVolume = Mathf.Clamp01(master);
            musicVolume = Mathf.Clamp01(music);
            ApplyVolume();
        }

        /// <summary>
        /// Updates music volume level directly.
        /// </summary>
        public void SetVolume(float music)
        {
            musicVolume = Mathf.Clamp01(music);
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (crossfadeCoroutine == null && activeSource != null && activeSource.isPlaying)
            {
                activeSource.volume = CurrentMaxVolume;
            }
        }

        #endregion

        #region Crossfade Coroutines

        private IEnumerator CrossfadeRoutine(AudioClip newClip, float duration)
        {
            duration = Mathf.Max(0f, duration);
            float targetVolume = CurrentMaxVolume;

            // Handle zero or negative duration (instant transition)
            if (duration <= 0f)
            {
                if (activeSource != null)
                {
                    activeSource.Stop();
                    activeSource.clip = null;
                    activeSource.volume = 0f;
                }

                if (standbySource != null)
                {
                    standbySource.clip = newClip;
                    standbySource.volume = targetVolume;
                    standbySource.Play();

                    // Swap channel pointers
                    AudioSource temp = activeSource;
                    activeSource = standbySource;
                    standbySource = temp;
                }
                else if (activeSource != null)
                {
                    activeSource.clip = newClip;
                    activeSource.volume = targetVolume;
                    activeSource.Play();
                }

                crossfadeCoroutine = null;
                yield break;
            }

            // Channel selection & reversal recovery
            AudioSource incoming;
            AudioSource outgoing;

            if (standbySource != null && standbySource.clip == newClip)
            {
                incoming = standbySource;
                outgoing = activeSource;
            }
            else if (activeSource != null && activeSource.clip == newClip)
            {
                incoming = activeSource;
                outgoing = standbySource;
            }
            else
            {
                incoming = standbySource;
                outgoing = activeSource;
                incoming.clip = newClip;
                incoming.volume = 0f;
                incoming.Play();
            }

            if (!incoming.isPlaying)
            {
                incoming.Play();
            }

            float startInVol = incoming.volume;
            float startOutVol = outgoing != null ? outgoing.volume : 0f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Equal-Power crossfade curve: sin(t * pi/2) and cos(t * pi/2)
                float inFactor = Mathf.Sin(t * Mathf.PI * 0.5f);
                float outFactor = Mathf.Cos(t * Mathf.PI * 0.5f);

                incoming.volume = Mathf.Lerp(startInVol, targetVolume, inFactor);
                if (outgoing != null)
                {
                    outgoing.volume = startOutVol * outFactor;
                }

                yield return null;
            }

            incoming.volume = targetVolume;
            if (outgoing != null)
            {
                outgoing.volume = 0f;
                outgoing.Stop();
                outgoing.clip = null;
            }

            // Swap channel pointers
            activeSource = incoming;
            standbySource = outgoing;
            crossfadeCoroutine = null;
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            duration = Mathf.Max(0f, duration);
            AudioSource outgoing = activeSource;
            float startVol = outgoing != null ? outgoing.volume : 0f;

            if (duration <= 0f)
            {
                if (outgoing != null)
                {
                    outgoing.Stop();
                    outgoing.clip = null;
                    outgoing.volume = 0f;
                }
                if (standbySource != null)
                {
                    standbySource.Stop();
                    standbySource.clip = null;
                    standbySource.volume = 0f;
                }
                crossfadeCoroutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float outFactor = Mathf.Cos(t * Mathf.PI * 0.5f);

                if (outgoing != null)
                {
                    outgoing.volume = startVol * outFactor;
                }
                if (standbySource != null && standbySource.isPlaying)
                {
                    standbySource.volume = standbySource.volume * outFactor;
                }

                yield return null;
            }

            if (outgoing != null)
            {
                outgoing.volume = 0f;
                outgoing.Stop();
                outgoing.clip = null;
            }
            if (standbySource != null)
            {
                standbySource.volume = 0f;
                standbySource.Stop();
                standbySource.clip = null;
            }

            crossfadeCoroutine = null;
        }

        #endregion

        #region Game State Event Subscriptions & Handlers

        private void SubscribeToEvents()
        {
            GameManager.OnLocationChanged += HandleLocationChanged;
            DungeonRoomController.OnRoomCombatStarted += HandleCombatStarted;
            GargoyleKingBoss.OnStoneFormActivated += HandleStoneFormActivated;
            TurnManager.OnCombatEnded += HandleCombatEnded;
        }

        private void UnsubscribeFromEvents()
        {
            GameManager.OnLocationChanged -= HandleLocationChanged;
            DungeonRoomController.OnRoomCombatStarted -= HandleCombatStarted;
            GargoyleKingBoss.OnStoneFormActivated -= HandleStoneFormActivated;
            TurnManager.OnCombatEnded -= HandleCombatEnded;
        }

        public void HandleLocationChanged(GameLocation newLocation)
        {
            switch (newLocation)
            {
                case GameLocation.Village:
                    PlayTrack(MusicTrackType.Village, DEFAULT_FADE_DURATION);
                    break;
                case GameLocation.Courtyard:
                case GameLocation.Library:
                case GameLocation.CrownHall:
                case GameLocation.Forest:
                default:
                    // Castle adventure exploration theme for zones outside Village
                    PlayTrack(MusicTrackType.CastleAdventure, DEFAULT_FADE_DURATION);
                    break;
            }
        }

        public void HandleCombatStarted(DungeonRoomController room)
        {
            if (room == null) return;
            PlayCombatMusicForBoss(room.bossIdentifier, room.roomLocation);
        }

        public void HandleStoneFormActivated(GargoyleKingBoss boss)
        {
            PlayTrack(MusicTrackType.GargoyleKingPhase2, DEFAULT_FADE_DURATION);
        }

        public void HandleCombatEnded(bool isVictory)
        {
            RestoreExplorationMusic(1.5f);
        }

        #endregion

        #region State Dispatchers & Helpers

        /// <summary>
        /// Resolves the target combat track from bossIdentifier and roomLocation strings.
        /// </summary>
        public MusicTrackType ResolveCombatTrack(string bossIdentifier, string roomLocation = "")
        {
            string boss = (bossIdentifier ?? string.Empty).Trim();
            string room = (roomLocation ?? string.Empty).Trim();

            // 1. Direct bossIdentifier matching
            if (boss.IndexOf("Commander", StringComparison.OrdinalIgnoreCase) >= 0 ||
                boss.IndexOf("Komentaja", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MusicTrackType.CursedCommanderCombat;
            }
            if (boss.IndexOf("Malakor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                boss.IndexOf("Varjomaagi", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MusicTrackType.MalakorCombat;
            }
            if (boss.IndexOf("Gargoyle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                boss.IndexOf("Kivettymiskuningas", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MusicTrackType.GargoyleKingPhase1;
            }

            // 2. Room location matching
            if (room.IndexOf("Cellar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                room.IndexOf("Kellari", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MusicTrackType.CellarCombat;
            }
            if (room.IndexOf("Courtyard", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MusicTrackType.CursedCommanderCombat;
            }
            if (room.IndexOf("Library", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MusicTrackType.MalakorCombat;
            }
            if (room.IndexOf("CrownHall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                room.IndexOf("ThroneRoom", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MusicTrackType.GargoyleKingPhase1;
            }

            // 3. Fallback
            return MusicTrackType.CellarCombat;
        }

        /// <summary>
        /// Selects and plays the appropriate combat track based on boss identifier and room location.
        /// </summary>
        public void PlayCombatMusicForBoss(string bossIdentifier, string roomLocation = "")
        {
            MusicTrackType combatTrack = ResolveCombatTrack(bossIdentifier, roomLocation);
            PlayTrack(combatTrack, DEFAULT_FADE_DURATION);
        }

        /// <summary>
        /// Waits for fanfare sound effects to finish before smoothly restoring exploration music.
        /// </summary>
        public void RestoreExplorationMusic(float delay = 1.5f)
        {
            if (restoreDelayCoroutine != null)
            {
                StopCoroutine(restoreDelayCoroutine);
                restoreDelayCoroutine = null;
            }

            restoreDelayCoroutine = StartCoroutine(RestoreExplorationRoutine(delay));
        }

        private IEnumerator RestoreExplorationRoutine(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            MusicTrackType targetTrack = MusicTrackType.Village;
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.CurrentLocation == GameLocation.Village)
                {
                    targetTrack = MusicTrackType.Village;
                }
                else
                {
                    targetTrack = MusicTrackType.CastleAdventure;
                }
            }
            else if (currentExplorationTrack != MusicTrackType.None)
            {
                targetTrack = currentExplorationTrack;
            }

            restoreDelayCoroutine = null;
            PlayTrack(targetTrack, DEFAULT_FADE_DURATION);
        }

        public bool IsExplorationTrack(MusicTrackType track)
        {
            return track == MusicTrackType.Village || track == MusicTrackType.CastleAdventure;
        }

        public bool IsCombatTrack(MusicTrackType track)
        {
            return track >= MusicTrackType.CellarCombat && track <= MusicTrackType.GargoyleKingPhase2;
        }

        public AudioClip GetTrackClip(MusicTrackType trackType) => trackType switch
        {
            MusicTrackType.Village => villageSongClip,
            MusicTrackType.CastleAdventure => castleAdventureSongClip,
            MusicTrackType.CellarCombat => cellarCombatClip,
            MusicTrackType.CursedCommanderCombat => cursedCommanderCombatClip,
            MusicTrackType.MalakorCombat => malakorCombatClip,
            MusicTrackType.GargoyleKingPhase1 => gargoyleKingPhase1Clip,
            MusicTrackType.GargoyleKingPhase2 => gargoyleKingPhase2Clip,
            _ => null
        };

        public AudioClip GetClip(MusicTrackType trackType) => GetTrackClip(trackType);

        public MusicTrackType GetTrackTypeForClip(AudioClip clip)
        {
            if (clip == null) return MusicTrackType.None;
            if (clip == villageSongClip) return MusicTrackType.Village;
            if (clip == castleAdventureSongClip) return MusicTrackType.CastleAdventure;
            if (clip == cellarCombatClip) return MusicTrackType.CellarCombat;
            if (clip == cursedCommanderCombatClip) return MusicTrackType.CursedCommanderCombat;
            if (clip == malakorCombatClip) return MusicTrackType.MalakorCombat;
            if (clip == gargoyleKingPhase1Clip) return MusicTrackType.GargoyleKingPhase1;
            if (clip == gargoyleKingPhase2Clip) return MusicTrackType.GargoyleKingPhase2;
            return MusicTrackType.None;
        }

        #endregion

        #region Editor Tooling & Programmatic Setup Helpers

        public void AssignClips(
            AudioClip village,
            AudioClip castleAdventure,
            AudioClip cellarCombat,
            AudioClip cursedCommander,
            AudioClip malakor,
            AudioClip gargoylePhase1,
            AudioClip gargoylePhase2)
        {
            villageSongClip = village;
            castleAdventureSongClip = castleAdventure;
            cellarCombatClip = cellarCombat;
            cursedCommanderCombatClip = cursedCommander;
            malakorCombatClip = malakor;
            gargoyleKingPhase1Clip = gargoylePhase1;
            gargoyleKingPhase2Clip = gargoylePhase2;
        }

        public void AssignClip(MusicTrackType trackType, AudioClip clip)
        {
            switch (trackType)
            {
                case MusicTrackType.Village: villageSongClip = clip; break;
                case MusicTrackType.CastleAdventure: castleAdventureSongClip = clip; break;
                case MusicTrackType.CellarCombat: cellarCombatClip = clip; break;
                case MusicTrackType.CursedCommanderCombat: cursedCommanderCombatClip = clip; break;
                case MusicTrackType.MalakorCombat: malakorCombatClip = clip; break;
                case MusicTrackType.GargoyleKingPhase1: gargoyleKingPhase1Clip = clip; break;
                case MusicTrackType.GargoyleKingPhase2: gargoyleKingPhase2Clip = clip; break;
            }
        }

        #endregion
    }
}
