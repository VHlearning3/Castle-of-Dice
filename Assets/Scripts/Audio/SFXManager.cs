using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Audio
{
    /// <summary>
    /// Supported sound effect clips for procedural audio or assigned audio clips.
    /// </summary>
    public enum SFXClipType
    {
        LevelUp,
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
    /// Audio manager dedicated to Sound Effects (SFX) with 12-channel polyphonic pooling.
    /// Supports 3D spatial playback, organic pitch micro-variations, and zero-GC cached procedural synthesis for WebGL.
    /// </summary>
    public class SFXManager : MonoBehaviour
    {
        #region Singleton

        public static SFXManager Instance { get; private set; }

        public static void SetInstanceForTesting(SFXManager instance)
        {
            Instance = instance;
        }

        #endregion

        #region Serialized Fields

        [Header("Volume Configuration")]
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.9f;

        [Header("Audio Clips (Optional - procedural synthesis fallback)")]
        [SerializeField] private AudioClip levelUpClip;
        [SerializeField] private AudioClip diceRollClip;
        [SerializeField] private AudioClip critSuccessClip;
        [SerializeField] private AudioClip critFailClip;
        [SerializeField] private AudioClip swordHitClip;
        [SerializeField] private AudioClip spellCastClip;
        [SerializeField] private AudioClip damageTakenClip;
        [SerializeField] private AudioClip potionDrinkClip;
        [SerializeField] private AudioClip buttonClickClip;
        [SerializeField] private AudioClip questCompleteClip;
        [SerializeField] private AudioClip doorOpenClip;
        [SerializeField] private AudioClip victoryClip;
        [SerializeField] private AudioClip defeatClip;

        #endregion

        #region Audio Pool

        public const int DefaultPoolSize = 12;
        private AudioSource[] audioSources;
        private int currentSourceIndex = 0;

        /// <summary>Read-only access to pooled audio sources for tests and diagnostics.</summary>
        public AudioSource[] AudioSources => audioSources;

        /// <summary>Active pool size.</summary>
        public int PoolCount => audioSources != null ? audioSources.Length : DefaultPoolSize;

        /// <summary>Current round-robin index in the pool.</summary>
        public int CurrentSourceIndex => currentSourceIndex;

        /// <summary>Overall SFX volume multiplier.</summary>
        public float SFXVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }

        #endregion

        #region Procedural Cache (Zero-GC)

        private static readonly Dictionary<SFXClipType, AudioClip> s_proceduralCache =
            new Dictionary<SFXClipType, AudioClip>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                ManagerDuplicates.Discard(this);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            PrewarmProceduralClips();
        }

        public void InitializeAudioSources(int poolSize = DefaultPoolSize)
        {
            if (audioSources != null && audioSources.Length > 0)
            {
                return;
            }

            audioSources = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                GameObject child = new GameObject($"SFX_Source_{i}");
                child.transform.SetParent(transform);
                AudioSource src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f; // 2D by default, dynamically set in PlaySFX
                audioSources[i] = src;
            }
        }

        private void PrewarmProceduralClips()
        {
            foreach (SFXClipType type in (SFXClipType[])Enum.GetValues(typeof(SFXClipType)))
            {
                if (!s_proceduralCache.ContainsKey(type))
                {
                    s_proceduralCache[type] = SynthesizeProceduralClip(type);
                }
            }
        }

        #endregion

        #region Public Playback API

        /// <summary>
        /// Plays a sound effect enum from an assigned clip or cached procedural synthesis through the 12-channel pool.
        /// </summary>
        public void PlaySFX(SFXClipType clipType, Vector3 position = default, float volumeScale = 1.0f, bool randomizePitch = true)
        {
            if (audioSources == null || audioSources.Length == 0)
            {
                InitializeAudioSources();
            }

            AudioClip clip = GetAssignedClip(clipType);
            if (clip == null)
            {
                clip = GetOrCreateProceduralClip(clipType);
            }

            if (clip != null)
            {
                bool pitchShift = randomizePitch && clipType != SFXClipType.ButtonClick && clipType != SFXClipType.LevelUp;
                PlayInternal(clip, position, volumeScale, pitchShift);
            }
        }

        /// <summary>
        /// Plays an explicit AudioClip directly through the 12-channel polyphonic pool.
        /// </summary>
        public void PlaySFX(AudioClip clip, Vector3 position = default, float volumeScale = 1.0f, bool randomizePitch = false)
        {
            if (clip == null) return;

            if (audioSources == null || audioSources.Length == 0)
            {
                InitializeAudioSources();
            }

            PlayInternal(clip, position, volumeScale, randomizePitch);
        }

        private void PlayInternal(AudioClip clip, Vector3 position, float volumeScale, bool randomizePitch)
        {
            if (audioSources == null || audioSources.Length == 0) return;

            AudioSource source = audioSources[currentSourceIndex];
            currentSourceIndex = (currentSourceIndex + 1) % audioSources.Length;

            if (source == null) return;

            // Position & spatial blend
            if (position != default)
            {
                source.transform.position = position;
                source.spatialBlend = 0.35f;
            }
            else
            {
                source.spatialBlend = 0f;
            }

            // Pitch variation (subtle tabletop micro-variation)
            source.pitch = randomizePitch ? UnityEngine.Random.Range(0.96f, 1.04f) : 1.0f;
            source.volume = Mathf.Clamp01(sfxVolume * volumeScale);
            source.PlayOneShot(clip);
        }

        #endregion

        #region Clip Lookups & Synthesis

        private AudioClip GetAssignedClip(SFXClipType clipType)
        {
            switch (clipType)
            {
                case SFXClipType.LevelUp: return levelUpClip;
                case SFXClipType.DiceRoll: return diceRollClip;
                case SFXClipType.CriticalSuccess: return critSuccessClip;
                case SFXClipType.CriticalFailure: return critFailClip;
                case SFXClipType.SwordHit: return swordHitClip;
                case SFXClipType.SpellCast: return spellCastClip;
                case SFXClipType.DamageTaken: return damageTakenClip;
                case SFXClipType.PotionDrink: return potionDrinkClip;
                case SFXClipType.ButtonClick: return buttonClickClip;
                case SFXClipType.QuestComplete: return questCompleteClip;
                case SFXClipType.DoorOpen: return doorOpenClip;
                case SFXClipType.Victory: return victoryClip;
                case SFXClipType.Defeat: return defeatClip;
                default: return null;
            }
        }

        private AudioClip GetOrCreateProceduralClip(SFXClipType clipType)
        {
            if (s_proceduralCache.TryGetValue(clipType, out AudioClip cached) && cached != null)
            {
                return cached;
            }

            AudioClip synth = SynthesizeProceduralClip(clipType);
            s_proceduralCache[clipType] = synth;
            return synth;
        }

        private AudioClip SynthesizeProceduralClip(SFXClipType clipType)
        {
            int sampleRate = 44100;
            float duration = 0.25f;

            switch (clipType)
            {
                case SFXClipType.LevelUp:
                    duration = 1.0f;
                    break;
                case SFXClipType.Victory:
                case SFXClipType.Defeat:
                case SFXClipType.QuestComplete:
                    duration = 0.75f;
                    break;
                case SFXClipType.DiceRoll:
                    duration = 0.45f;
                    break;
                case SFXClipType.DoorOpen:
                    duration = 0.5f;
                    break;
                default:
                    duration = 0.22f;
                    break;
            }

            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            switch (clipType)
            {
                case SFXClipType.LevelUp:
                {
                    // Ascending triumphant arpeggio (C major: C4, E4, G4, C5)
                    float[] notes = { 261.63f, 329.63f, 392.00f, 523.25f };
                    float noteDuration = duration / notes.Length;
                    int samplesPerNote = Mathf.FloorToInt(sampleRate * noteDuration);

                    for (int i = 0; i < totalSamples; i++)
                    {
                        int noteIdx = Mathf.Clamp(i / samplesPerNote, 0, notes.Length - 1);
                        float freq = notes[noteIdx];
                        float t = (float)i / sampleRate;
                        float noteT = (float)(i % samplesPerNote) / samplesPerNote;
                        float env = Mathf.Sin(noteT * Mathf.PI);

                        float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f +
                                     Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.3f;
                        samples[i] = wave * env * 0.75f;
                    }
                    break;
                }

                case SFXClipType.DiceRoll:
                {
                    // Multi-tap rattling clatter simulating tumbling polyhedral dice
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float t = (float)i / sampleRate;
                        float env = Mathf.Exp(-t * 8f);
                        // Rattle impacts at 0.05s, 0.12s, 0.22s, 0.33s
                        float click1 = Mathf.Exp(-Mathf.Abs(t - 0.05f) * 80f) * Mathf.Sin(2f * Mathf.PI * 800f * t);
                        float click2 = Mathf.Exp(-Mathf.Abs(t - 0.12f) * 70f) * Mathf.Sin(2f * Mathf.PI * 920f * t);
                        float click3 = Mathf.Exp(-Mathf.Abs(t - 0.22f) * 60f) * Mathf.Sin(2f * Mathf.PI * 750f * t);
                        float click4 = Mathf.Exp(-Mathf.Abs(t - 0.33f) * 50f) * Mathf.Sin(2f * Mathf.PI * 680f * t);
                        samples[i] = (click1 + click2 + click3 + click4) * env * 0.65f;
                    }
                    break;
                }

                case SFXClipType.SwordHit:
                {
                    // Sharp metal clatter & high shimmer
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float t = (float)i / sampleRate;
                        float env = Mathf.Exp(-t * 22f);
                        float clang = Mathf.Sin(2f * Mathf.PI * 1250f * t) * 0.6f +
                                      Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.4f;
                        samples[i] = clang * env * 0.75f;
                    }
                    break;
                }

                case SFXClipType.SpellCast:
                {
                    // Resonant arcane frequency sweep
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float t = (float)i / sampleRate;
                        float env = Mathf.Sin(t / duration * Mathf.PI);
                        float freq = 450f + (t / duration) * 550f;
                        float wave = Mathf.Sin(2f * Mathf.PI * freq * t) +
                                     0.5f * Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t);
                        samples[i] = wave * env * 0.5f;
                    }
                    break;
                }

                case SFXClipType.Victory:
                case SFXClipType.QuestComplete:
                {
                    // Triumphant chord fanfare (G major: G4, B4, D5)
                    float[] chordNotes = { 392f, 493.88f, 587.33f };
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float t = (float)i / sampleRate;
                        float env = Mathf.Exp(-t * 3.5f);
                        float wave = 0f;
                        for (int n = 0; n < chordNotes.Length; n++)
                        {
                            wave += Mathf.Sin(2f * Mathf.PI * chordNotes[n] * t) * 0.33f;
                        }
                        samples[i] = wave * env * 0.75f;
                    }
                    break;
                }

                case SFXClipType.Defeat:
                case SFXClipType.CriticalFailure:
                {
                    // Descending somber tone
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float t = (float)i / sampleRate;
                        float env = Mathf.Exp(-t * 4.5f);
                        float freq = Mathf.Lerp(220f, 90f, t / duration);
                        samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.7f;
                    }
                    break;
                }

                case SFXClipType.DamageTaken:
                {
                    // Deep blunt impact thud
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float t = (float)i / sampleRate;
                        float env = Mathf.Exp(-t * 30f);
                        samples[i] = Mathf.Sin(2f * Mathf.PI * 130f * t) * env * 0.85f;
                    }
                    break;
                }

                case SFXClipType.PotionDrink:
                {
                    // Liquid bubble / gulp
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float t = (float)i / sampleRate;
                        float env = Mathf.Exp(-t * 18f);
                        float freq = 350f + Mathf.Sin(2f * Mathf.PI * 25f * t) * 120f;
                        samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.6f;
                    }
                    break;
                }

                case SFXClipType.DoorOpen:
                {
                    // Deep resonant wooden latch & creak
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float t = (float)i / sampleRate;
                        float env = Mathf.Sin(t / duration * Mathf.PI);
                        float creak = Mathf.Sin(2f * Mathf.PI * 180f * t) +
                                      0.3f * Mathf.Sin(2f * Mathf.PI * 340f * t);
                        samples[i] = creak * env * 0.6f;
                    }
                    break;
                }

                case SFXClipType.CriticalSuccess:
                {
                    // High ringing crystal bell
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float t = (float)i / sampleRate;
                        float env = Mathf.Exp(-t * 6f);
                        float chime = Mathf.Sin(2f * Mathf.PI * 1046.5f * t) * 0.7f +
                                      Mathf.Sin(2f * Mathf.PI * 2093f * t) * 0.3f;
                        samples[i] = chime * env * 0.75f;
                    }
                    break;
                }

                default: // ButtonClick and fallback
                {
                    for (int i = 0; i < totalSamples; i++)
                    {
                        float t = (float)i / sampleRate;
                        float env = Mathf.Exp(-t * 40f);
                        samples[i] = Mathf.Sin(2f * Mathf.PI * 750f * t) * env * 0.5f;
                    }
                    break;
                }
            }

            AudioClip generated = AudioClip.Create($"SFX_{clipType}_Synth", totalSamples, 1, sampleRate, false);
            generated.SetData(samples, 0);
            return generated;
        }

        #endregion
    }
}
