using System;
using UnityEngine;

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
    /// Supports 3D spatial playback and zero-dependency procedural synthesis for WebGL.
    /// </summary>
    public class SFXManager : MonoBehaviour
    {
        #region Singleton

        public static SFXManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Volume Configuration")]
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.9f;

        [Header("Audio Clips (Optional - procedural synthesis fallback)")]
        [SerializeField] private AudioClip levelUpClip;
        [SerializeField] private AudioClip diceRollClip;
        [SerializeField] private AudioClip victoryClip;
        [SerializeField] private AudioClip buttonClickClip;

        #endregion

        #region Audio Pool

        private const int PoolSize = 12;
        private AudioSource[] audioSources;
        private int currentSourceIndex = 0;

        #endregion

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
            InitializeAudioSources();
        }

        private void InitializeAudioSources()
        {
            audioSources = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject child = new GameObject($"SFX_Source_{i}");
                child.transform.SetParent(transform);
                AudioSource src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f; // 2D by default, configured in PlaySFX
                audioSources[i] = src;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Plays a sound effect at an optional world position with volume scaling.
        /// </summary>
        public void PlaySFX(SFXClipType clipType, Vector3 position = default, float volumeScale = 1.0f)
        {
            if (audioSources == null || audioSources.Length == 0) return;

            AudioSource source = audioSources[currentSourceIndex];
            currentSourceIndex = (currentSourceIndex + 1) % PoolSize;

            AudioClip clip = GetClip(clipType);
            if (clip == null)
            {
                clip = SynthesizeProceduralClip(clipType);
            }

            if (clip != null)
            {
                if (position != default)
                {
                    source.transform.position = position;
                    source.spatialBlend = 0.35f;
                }
                else
                {
                    source.spatialBlend = 0f;
                }

                source.volume = Mathf.Clamp01(sfxVolume * volumeScale);
                source.PlayOneShot(clip);
            }
        }

        private AudioClip GetClip(SFXClipType clipType)
        {
            switch (clipType)
            {
                case SFXClipType.LevelUp: return levelUpClip;
                case SFXClipType.DiceRoll: return diceRollClip;
                case SFXClipType.Victory: return victoryClip;
                case SFXClipType.ButtonClick: return buttonClickClip;
                default: return null;
            }
        }

        private AudioClip SynthesizeProceduralClip(SFXClipType clipType)
        {
            int sampleRate = 44100;
            float duration = clipType == SFXClipType.LevelUp ? 1.0f : 0.25f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            if (clipType == SFXClipType.LevelUp)
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

                    // Blend fundamental + octave shimmer
                    float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f +
                                 Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.3f;
                    samples[i] = wave * env * 0.75f;
                }
            }
            else
            {
                // Crisp click / blip
                for (int i = 0; i < totalSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    float env = Mathf.Exp(-t * 25f);
                    samples[i] = Mathf.Sin(2f * Mathf.PI * 600f * t) * env * 0.5f;
                }
            }

            AudioClip generated = AudioClip.Create($"SFX_{clipType}", totalSamples, 1, sampleRate, false);
            generated.SetData(samples, 0);
            return generated;
        }

        #endregion
    }
}
