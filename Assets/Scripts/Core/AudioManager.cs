using System;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Sound effect types that can be triggered dynamically or synthesized procedurally.
    /// </summary>
    public enum SoundType
    {
        DiceRoll,
        CriticalSuccess,
        CriticalFailure,
        SwordHit,
        SpellCast,
        DamageTaken,
        PotionDrink,
        ButtonClick,
        QuestComplete,
        DoorOpen,
        Victory,
        Defeat
    }

    /// <summary>
    /// Persistent Audio Manager supervising background music (BGM) and sound effects (SFX).
    /// Supports both assigned AudioClips and procedural audio synthesis for zero-dependency WebGL sound.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        #region Singleton

        public static AudioManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Volume Controls (0.0 - 1.0)")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.9f;

        [Header("Default Music Clips")]
        [Tooltip("Background harp music for Oakhaven village (e.g. Scottish harp from Pixabay).")]
        [SerializeField] private AudioClip villageBgmClip;

        [Tooltip("Tension/ambient music for Castle wings and dungeons.")]
        [SerializeField] private AudioClip dungeonBgmClip;

        [Tooltip("Action music for tactical combat encounters.")]
        [SerializeField] private AudioClip combatBgmClip;

        [Header("Default Sound Effect Clips (Optional - procedural synthesis used if unassigned)")]
        [SerializeField] private AudioClip diceRollClip;
        [SerializeField] private AudioClip critSuccessClip;
        [SerializeField] private AudioClip critFailClip;
        [SerializeField] private AudioClip swordSlashClip;
        [SerializeField] private AudioClip spellCastClip;
        [SerializeField] private AudioClip potionClip;
        [SerializeField] private AudioClip buttonClickClip;
        [SerializeField] private AudioClip questCompleteClip;

        #endregion

        #region Private State

        private readonly System.Collections.Generic.Dictionary<SoundType, AudioClip> proceduralClipCache =
            new System.Collections.Generic.Dictionary<SoundType, AudioClip>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Instance.gameObject != gameObject)
                {
                    Destroy(gameObject);
                }
                else
                {
                    Destroy(this);
                }
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
        }

        private void Start()
        {
            SubscribeToEvents();

            // Yield BGM authority to MusicManager if active
            if (MusicManager.Instance != null)
            {
                if (bgmSource != null && bgmSource.isPlaying)
                {
                    bgmSource.Stop();
                }
                return;
            }

            // Start default village background music if available
            if (bgmSource != null && !bgmSource.isPlaying)
            {
                if (villageBgmClip != null)
                {
                    PlayBGM(villageBgmClip);
                }
                else
                {
                    // Play peaceful procedural harp-like chord loop if no clip is set
                    AudioClip proceduralBgm = GenerateHarpBgmClip();
                    PlayBGM(proceduralBgm);
                }
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

        #region Initialization & Setup

        private void InitializeAudioSources()
        {
            if (bgmSource == null)
            {
                GameObject bgmObj = new GameObject("BGM_Source");
                bgmObj.transform.SetParent(transform);
                bgmSource = bgmObj.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
                bgmSource.spatialBlend = 0f; // 2D Stereo
            }

            if (sfxSource == null)
            {
                GameObject sfxObj = new GameObject("SFX_Source");
                sfxObj.transform.SetParent(transform);
                sfxSource = sfxObj.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f; // 2D Stereo
            }

            UpdateVolumes();
        }

        private void SubscribeToEvents()
        {
            DiceSystem.OnDiceRolled += HandleDiceRolled;
            GameManager.OnLocationChanged += HandleLocationChanged;
            GameManager.OnPlayModeChanged += HandlePlayModeChanged;
            CastleOfTheD20.Combat.TurnManager.OnCombatEnded += HandleCombatEnded;
        }

        private void UnsubscribeFromEvents()
        {
            DiceSystem.OnDiceRolled -= HandleDiceRolled;
            GameManager.OnLocationChanged -= HandleLocationChanged;
            GameManager.OnPlayModeChanged -= HandlePlayModeChanged;
            CastleOfTheD20.Combat.TurnManager.OnCombatEnded -= HandleCombatEnded;
        }

        private void HandleCombatEnded(bool isVictory)
        {
            PlaySFX(isVictory ? SoundType.Victory : SoundType.Defeat);
        }

        #endregion

        #region Public BGM & SFX API

        /// <summary>
        /// Plays background music with looping enabled. Delegates to MusicManager if active.
        /// </summary>
        public void PlayBGM(AudioClip clip, bool loop = true)
        {
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.PlayMusic(clip);
                return;
            }

            if (clip == null || bgmSource == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.volume = masterVolume * bgmVolume;
            bgmSource.Play();
        }

        /// <summary>
        /// Stops background music on both MusicManager and legacy bgmSource.
        /// </summary>
        public void StopBGM()
        {
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.StopMusic();
            }

            if (bgmSource != null)
            {
                bgmSource.Stop();
            }
        }

        /// <summary>
        /// Returns true if background music is currently playing on either MusicManager or bgmSource.
        /// </summary>
        public bool IsBGMPlaying => (MusicManager.Instance != null && MusicManager.Instance.IsPlaying) ||
                                    (bgmSource != null && bgmSource.isPlaying);

        /// <summary>
        /// Plays a sound effect from an assigned clip or falls back to procedural synthesis.
        /// </summary>
        public void PlaySFX(SoundType sound, float volumeScale = 1.0f)
        {
            AudioClip clipToPlay = GetClipForSoundType(sound);
            if (clipToPlay == null)
            {
                clipToPlay = GetOrCreateProceduralClip(sound);
            }

            if (clipToPlay != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clipToPlay, masterVolume * sfxVolume * Mathf.Clamp01(volumeScale));
            }
        }

        /// <summary>
        /// Plays an explicit AudioClip through the SFX channel.
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
        {
            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip, masterVolume * sfxVolume * Mathf.Clamp01(volumeScale));
            }
        }

        /// <summary>
        /// Sets volume levels (0.0 to 1.0) and propagates BGM volume to MusicManager if active.
        /// </summary>
        public void SetVolumes(float master, float bgm, float sfx)
        {
            masterVolume = Mathf.Clamp01(master);
            bgmVolume = Mathf.Clamp01(bgm);
            sfxVolume = Mathf.Clamp01(sfx);
            UpdateVolumes();

            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.SetVolume(masterVolume, bgmVolume);
            }
        }

        private void UpdateVolumes()
        {
            if (bgmSource != null)
            {
                bgmSource.volume = masterVolume * bgmVolume;
            }
            if (sfxSource != null)
            {
                sfxSource.volume = masterVolume * sfxVolume;
            }
        }

        #endregion

        #region Event Handlers

        private void HandleDiceRolled(DiceResult result)
        {
            if (result.isCriticalSuccess)
            {
                PlaySFX(SoundType.CriticalSuccess);
            }
            else if (result.isCriticalFail)
            {
                PlaySFX(SoundType.CriticalFailure);
            }
            else
            {
                PlaySFX(SoundType.DiceRoll);
            }
        }

        private void HandleLocationChanged(GameLocation newLocation)
        {
            // MusicManager handles dynamic location-based music crossfading
            if (MusicManager.Instance != null)
            {
                return;
            }

            switch (newLocation)
            {
                case GameLocation.Village:
                    if (villageBgmClip != null) PlayBGM(villageBgmClip);
                    break;
                case GameLocation.Courtyard:
                case GameLocation.Library:
                case GameLocation.CrownHall:
                case GameLocation.Forest:
                    if (dungeonBgmClip != null) PlayBGM(dungeonBgmClip);
                    break;
            }
        }

        private void HandlePlayModeChanged(GamePlayMode newMode)
        {
            // MusicManager handles combat and exploration tracks via event subscriptions
            if (MusicManager.Instance != null)
            {
                return;
            }

            if (newMode == GamePlayMode.Combat && combatBgmClip != null)
            {
                PlayBGM(combatBgmClip);
            }
            else if (newMode == GamePlayMode.Exploration)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentLocation == GameLocation.Village)
                {
                    if (villageBgmClip != null) PlayBGM(villageBgmClip);
                }
                else if (dungeonBgmClip != null)
                {
                    PlayBGM(dungeonBgmClip);
                }
            }
        }

        #endregion

        #region Procedural Audio Synthesis (WebGL Fallback)

        private AudioClip GetClipForSoundType(SoundType sound)
        {
            return sound switch
            {
                SoundType.DiceRoll => diceRollClip,
                SoundType.CriticalSuccess => critSuccessClip,
                SoundType.CriticalFailure => critFailClip,
                SoundType.SwordHit => swordSlashClip,
                SoundType.SpellCast => spellCastClip,
                SoundType.PotionDrink => potionClip,
                SoundType.ButtonClick => buttonClickClip,
                SoundType.QuestComplete => questCompleteClip,
                _ => null
            };
        }

        private AudioClip GetOrCreateProceduralClip(SoundType sound)
        {
            if (proceduralClipCache.TryGetValue(sound, out AudioClip cached) && cached != null)
            {
                return cached;
            }

            AudioClip generated = GenerateProceduralSound(sound);
            if (generated != null)
            {
                proceduralClipCache[sound] = generated;
            }
            return generated;
        }

        private AudioClip GenerateProceduralSound(SoundType sound)
        {
            int sampleRate = 44100;
            return sound switch
            {
                SoundType.DiceRoll => CreateDiceRollSynth(sampleRate),
                SoundType.CriticalSuccess => CreateFanfareSynth(sampleRate, true),
                SoundType.CriticalFailure => CreateFanfareSynth(sampleRate, false),
                SoundType.SwordHit => CreateSwordClangSynth(sampleRate),
                SoundType.SpellCast => CreateSpellWhooshSynth(sampleRate),
                SoundType.DamageTaken => CreateHitSynth(sampleRate),
                SoundType.PotionDrink => CreateBubbleGulpSynth(sampleRate),
                SoundType.ButtonClick => CreateClickSynth(sampleRate),
                SoundType.QuestComplete => CreateFanfareSynth(sampleRate, true),
                SoundType.Victory => CreateFanfareSynth(sampleRate, true),
                SoundType.Defeat => CreateFanfareSynth(sampleRate, false),
                _ => CreateClickSynth(sampleRate)
            };
        }

        private AudioClip CreateClickSynth(int sampleRate)
        {
            int length = (int)(sampleRate * 0.05f);
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 80f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * 1200f * t) * decay * 0.4f;
            }
            AudioClip clip = AudioClip.Create("SFX_Click_Synth", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateDiceRollSynth(int sampleRate)
        {
            // Rapid series of rhythmic wooden clicks simulating dice bouncing on a tray
            int length = (int)(sampleRate * 0.45f);
            float[] samples = new float[length];
            float[] tapTimes = new float[] { 0.02f, 0.08f, 0.16f, 0.25f, 0.33f, 0.40f };

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float s = 0f;
                foreach (float tap in tapTimes)
                {
                    if (t >= tap && t < tap + 0.04f)
                    {
                        float dt = t - tap;
                        float decay = Mathf.Exp(-dt * 120f);
                        s += Mathf.Sin(2f * Mathf.PI * 450f * dt) * decay * 0.6f;
                    }
                }
                samples[i] = Mathf.Clamp(s, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("SFX_DiceRoll_Synth", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateFanfareSynth(int sampleRate, bool isVictory)
        {
            int length = (int)(sampleRate * 0.8f);
            float[] samples = new float[length];
            float[] freqs = isVictory
                ? new float[] { 523.25f, 659.25f, 783.99f, 1046.50f } // C major triad
                : new float[] { 369.99f, 349.23f, 311.13f, 261.63f }; // Minor downward

            float noteDuration = 0.8f / freqs.Length;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                int noteIndex = Mathf.Clamp((int)(t / noteDuration), 0, freqs.Length - 1);
                float noteT = t - (noteIndex * noteDuration);
                float decay = Mathf.Exp(-noteT * 6f);
                float freq = freqs[noteIndex];
                samples[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t)) * decay * 0.5f;
            }

            AudioClip clip = AudioClip.Create(isVictory ? "SFX_Fanfare_Win" : "SFX_Fanfare_Loss", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateSwordClangSynth(int sampleRate)
        {
            int length = (int)(sampleRate * 0.35f);
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 22f);
                float clang = Mathf.Sin(2f * Mathf.PI * 880f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 1320f * t);
                samples[i] = clang * decay * 0.6f;
            }
            AudioClip clip = AudioClip.Create("SFX_SwordClang_Synth", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateSpellWhooshSynth(int sampleRate)
        {
            int length = (int)(sampleRate * 0.5f);
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(250f, 900f, t / 0.5f);
                float decay = Mathf.Sin((t / 0.5f) * Mathf.PI);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * decay * 0.5f;
            }
            AudioClip clip = AudioClip.Create("SFX_SpellCast_Synth", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateHitSynth(int sampleRate)
        {
            int length = (int)(sampleRate * 0.2f);
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 35f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * 160f * t) * decay * 0.7f;
            }
            AudioClip clip = AudioClip.Create("SFX_Hit_Synth", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateBubbleGulpSynth(int sampleRate)
        {
            int length = (int)(sampleRate * 0.3f);
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(400f, 650f, Mathf.Repeat(t * 12f, 1f));
                float decay = Mathf.Exp(-t * 8f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * decay * 0.5f;
            }
            AudioClip clip = AudioClip.Create("SFX_Potion_Synth", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateHarpBgmClip()
        {
            // Generates a 6-second calming procedural harp arpeggio loop
            int sampleRate = 22050;
            float duration = 6.0f;
            int length = (int)(sampleRate * duration);
            float[] samples = new float[length];
            float[] arpNotes = new float[] { 261.63f, 329.63f, 392.00f, 523.25f, 392.00f, 329.63f }; // C - E - G - C - G - E

            float noteDuration = duration / arpNotes.Length;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                int noteIndex = Mathf.Clamp((int)(t / noteDuration), 0, arpNotes.Length - 1);
                float noteT = t - (noteIndex * noteDuration);
                float decay = Mathf.Exp(-noteT * 3.5f);
                float freq = arpNotes[noteIndex];
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * freq * t);
                samples[i] = tone * decay * 0.35f;
            }

            AudioClip clip = AudioClip.Create("BGM_Harp_Procedural", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
