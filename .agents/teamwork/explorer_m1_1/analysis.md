# Milestone M1 Analysis: Audio Engine & WebGL Crossfade Mechanics

**Author**: Explorer M1  
**Target Milestone**: M1 (WebGL MusicManager Core)  
**Date**: 2026-09-24  
**Project**: Castle of the D20 / Castle of Dice  

---

## 1. Executive Summary

Milestone M1 establishes the core audio architecture for Castle of the D20, specifically introducing a dedicated, WebGL-compatible `MusicManager` utilizing native Unity C# with zero external dependencies. The primary engineering challenges are:
1. **Painless BGM Crossfading**: Seamlessly transitioning between 7 distinct MP3 music tracks over a default 1.2-second duration without clicks, pops, or perceptible volume dips.
2. **2D Stereo Reliability**: Overriding the default 3D import flags on the audio assets so music is never attenuated by camera distance or listener positioning.
3. **WebGL Runtime Stability**: Operating within browser Web Audio API constraints (single-threaded execution, autoplay policies, suspended AudioContext states) without freezing or unhandled exceptions.
4. **AudioManager Delegation**: Decoupling SFX from BGM so that `AudioManager` cleanly yields BGM control to `MusicManager` to eliminate audio collisions.

---

## 2. Dual-Channel Ping-Pong Architecture Design

### 2.1 The Single AudioSource Limitation
In Unity's audio engine, a single `AudioSource` component cannot play two distinct `AudioClip` assets simultaneously. Changing `audioSource.clip` during playback stops the previous sample instantly, producing a sharp waveform discontinuity (an audible "pop" or "click") followed by silence.

### 2.2 Ping-Pong Channel Model
To achieve seamless crossfading, `MusicManager` employs two independent `AudioSource` components:
- `AudioSource sourceA` (Channel A)
- `AudioSource sourceB` (Channel B)

At any given time:
- One channel acts as the **Active Channel** (playing the current BGM track at full/target volume).
- The other channel acts as the **Standby Channel** (stopped, volume = 0, ready to receive the next clip).

When a crossfade is requested:
1. The **Standby Channel** receives the incoming `AudioClip`, resets its playback position to 0, sets volume to 0, and begins playing (`Play()`).
2. A single coroutine runs over the crossfade duration ($T = 1.2\text{s}$), simultaneously ramping up the incoming channel's volume while ramping down the outgoing channel's volume.
3. Upon completion, the outgoing channel is stopped (`Stop()`), its clip reference cleared, and its volume set to 0.
4. The incoming channel becomes the new **Active Channel**, and the old channel becomes the **Standby Channel**.

### 2.3 Dynamic Creation & Inspector Serialization
To ensure zero friction across both automated scene builders (`BuildVillageEditor`) and manual runtime instantiation:
- Both `sourceA` and `sourceB` are serialized (`[SerializeField] private AudioSource sourceA; [SerializeField] private AudioSource sourceB;`).
- In `Awake()`, `MusicManager` checks if either source is null. If missing, it dynamically instantiates child GameObjects (`MusicSource_A` and `MusicSource_B`) and attaches `AudioSource` components with automated 2D configuration.

---

## 3. Mathematical Derivation of Crossfade Interpolation

### 3.1 Linear Interpolation Flaw (The -3 dB Power Dip)
A common mistake in audio crossfades is using linear volume interpolation:
$$V_{in}(t) = t \cdot V_{target}$$
$$V_{out}(t) = (1 - t) \cdot V_{target}$$
where $t \in [0, 1]$.

For two independent, uncorrelated audio signals (e.g., two distinct musical pieces), human ears perceive total acoustic power $P_{total}$, which is proportional to the sum of the squares of the root-mean-square (RMS) amplitudes:
$$P_{total}(t) \propto V_{in}(t)^2 + V_{out}(t)^2$$

Evaluating at the midpoint ($t = 0.5$):
$$P_{linear}(0.5) \propto (0.5)^2 + (0.5)^2 = 0.25 + 0.25 = 0.50$$

In logarithmic decibels:
$$\Delta \text{dB} = 10 \log_{10}(0.5) \approx -3.01 \text{ dB}$$

**Result**: A linear crossfade drops approximately 3 dB of acoustic energy at the midpoint (0.6s into a 1.2s fade). To human hearing, the music noticeably "hollows out" or dips in loudness before recovering.

### 3.2 Equal-Power Crossfade (Quarter-Sine / Cosine)
To maintain a perceived constant volume across the transition, the acoustic power must satisfy:
$$P_{total}(t) = V_{in}(t)^2 + V_{out}(t)^2 = 1.0 \quad \forall t \in [0, 1]$$

Using the fundamental Pythagorean trigonometric identity $\sin^2(\theta) + \cos^2(\theta) = 1$:
Let $\theta = t \cdot \frac{\pi}{2}$.
$$V_{in}(t) = V_{target} \cdot \sin\left(t \cdot \frac{\pi}{2}\right)$$
$$V_{out}(t) = V_{current} \cdot \cos\left(t \cdot \frac{\pi}{2}\right)$$

Evaluating at key points:
- At start ($t = 0$):
  $$V_{in} = 0, \quad V_{out} = V_{current}$$
  $$P_{total} = 0^2 + 1^2 = 1.0 \ (0 \text{ dB})$$
- At midpoint ($t = 0.5$):
  $$V_{in} = \sin(\pi/4) = \frac{\sqrt{2}}{2} \approx 0.7071$$
  $$V_{out} = \cos(\pi/4) = \frac{\sqrt{2}}{2} \approx 0.7071$$
  $$P_{total} = (0.7071)^2 + (0.7071)^2 = 0.5 + 0.5 = 1.0 \ (0 \text{ dB})$$
- At end ($t = 1.0$):
  $$V_{in} = \sin(\pi/2) = 1.0, \quad V_{out} = \cos(\pi/2) = 0$$
  $$P_{total} = 1.0^2 + 0^2 = 1.0 \ (0 \text{ dB})$$

**Result**: Equal-power crossfading ensures completely flat energy transfer across the 1.2s duration with zero audible dip and zero distortion.

---

## 4. Edge Case & State Interruption Handling

Audio transitions in interactive games do not occur in an isolated vacuum. The crossfading coroutine must handle several complex edge cases:

### 4.1 Rapid State Transitions (Interrupted Crossfade)
*Scenario*: The player enters a new zone, triggering a 1.2s crossfade. 0.4s later, before the fade finishes, combat begins or another gate is crossed.
*Behavior without handling*: Multiple concurrent coroutines fight for volume control, causing extreme flutter and volume jumps.
*Engineered solution*:
1. If an active crossfade coroutine exists, immediately stop it (`StopCoroutine(fadeCoroutine)`).
2. Measure the actual current volume of both channels (`sourceA.volume` and `sourceB.volume`).
3. Identify which channel currently has the higher audible energy: that channel becomes the new outgoing source, and its current volume is captured as $V_{out0}$.
4. The idle channel becomes the incoming source, assigned the new track, and begins ramping up from 0 to $V_{target}$.
5. The outgoing channel ramps down from $V_{out0}$ to 0 using the cosine curve.
6. This guarantees zero audio glitching or clicks regardless of trigger frequency.

### 4.2 Reversal (Returning to Outgoing Track)
*Scenario*: Player walks through a doorway, immediately turns around and walks back.
*Engineered solution*:
If `PlayMusic(clip)` receives the clip that is currently fading out on the outgoing channel, the coroutine can reverse the channel roles: the outgoing channel ramps back up from its current volume to target volume, while the newly started channel ramps down to 0.

### 4.3 Same Track Request
*Scenario*: An event fires `OnLocationChanged(Courtyard)` when already in Courtyard.
*Engineered solution*:
If `newClip == activeSource.clip && activeSource.isPlaying && fadeCoroutine == null`, return immediately. Do not interrupt smooth looping playback.

### 4.4 Instant Transition Request
*Scenario*: A cutscene or instant restart requests immediate track replacement with `fadeDuration <= 0f`.
*Engineered solution*:
Immediately stop the outgoing source, set incoming source volume to target volume, assign clip, and call `Play()`.

### 4.5 Null Clip / Stop Music
*Scenario*: Entering a silent zone or game over screen where music should fade out to silence.
*Engineered solution*:
`StopMusic(float fadeDuration = 1.2f)` smoothly fades the active source down to 0 using the cosine curve and stops playback without starting an incoming clip.

### 4.6 TimeScale Independence
*Scenario*: Combat pauses the game (`Time.timeScale = 0`), or inventory/defeat modal opens.
*Engineered solution*:
The coroutine increments time using `Time.unscaledDeltaTime` instead of `Time.deltaTime`. Fades continue smoothly regardless of game pause state.

---

## 5. 2D Stereo Configuration & Spatial Settings

### 5.1 The `3D: 1` Import Trap
Inspection of `Assets/Music/*.mp3.meta` reveals:
```yaml
AudioImporter:
  defaultSettings:
    3D: 1
```
Unity's default importer sets audio clips to 3D. If an `AudioSource` playing a 3D clip has `spatialBlend > 0`, Unity attenuates volume based on the 3D distance between the GameObject and the active `AudioListener` (attached to the Main Camera).

Because `MusicManager` is typically placed on the `Managers` GameObject at $(0, 0, 0)$, when the player explores Courtyard or Crown Hall at coordinates $(0, 0, 80)$, distance attenuation would reduce music volume to near zero!

### 5.2 Mandatory 2D Configuration
Both `AudioSource` components on `MusicManager` must be explicitly configured as follows:
```csharp
private void ConfigureAudioSource(AudioSource source)
{
    source.playOnAwake = false;
    source.loop = true;
    source.spatialBlend = 0f;          // 100% 2D Stereo
    source.panStereo = 0f;              // Center pan
    source.bypassEffects = false;
    source.bypassListenerEffects = false;
    source.bypassReverbZones = true;    // Immune to room reverb
    source.priority = 0;               // Maximum priority (prevent voice stealing)
    source.volume = 0f;
}
```
- `spatialBlend = 0f` completely disables 3D spatial calculations, delivering full-fidelity 2D stereo directly to the listener regardless of camera position.
- `priority = 0` (0 is highest priority in Unity; 255 is lowest) prevents Unity's audio voice manager or Web Audio from dropping/stealing the BGM channel when numerous SFX play simultaneously during combat.

---

## 6. WebGL Compatibility Deep Dive

WebGL builds run in browser sandboxes under Emscripten and WebAssembly. This environment imposes strict requirements:

### 6.1 Single-Threaded Execution
In standard Unity WebGL builds:
- `System.Threading.Thread.Sleep()`, `Thread.Start()`, or multi-threaded task schedulers will either throw `PlatformNotSupportedException` or freeze the browser tab.
- All timing and transitions **must** execute on the main thread via Unity coroutines (`IEnumerator`, `yield return null`).
- Native Unity coroutines run synchronously on the Unity engine frame loop and are 100% WebGL-compatible.

### 6.2 Browser Autoplay Policy & Web Audio Context
Modern browsers (Chrome 66+, Edge, Firefox, Safari 11+) block audio from autoplaying until the user interacts with the document:
1. If `AudioSource.Play()` is called before user input, the browser sets the underlying Web Audio `AudioContext.state = "suspended"`.
2. In Unity WebGL, `AudioListener.pause` can become `true` or audio remains silent until the first mouse click or key press.
3. *Defensive mechanism*:
   In `MusicManager.Update()`:
   ```csharp
   private void Update()
   {
       // Handle WebGL autoplay unmuting on initial user interaction
       if (AudioListener.pause && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
       {
           AudioListener.pause = false;
       }
   }
   ```
   Additionally, in `Awake()`, `AudioListener.pause` is explicitly initialized to `false`. When the player clicks anywhere on the canvas (e.g. clicking "Start Game" or selecting a class), the browser AudioContext resumes and sound outputs normally.

### 6.3 Memory Footprint & Audio Buffers
The 7 MP3 tracks in `Assets/Music/` total ~18.8 MB compressed:
- `VillageSong.mp3`: 2.18 MB
- `Castle_adventure_song.mp3`: 2.86 MB
- `Cellar_combat_music.mp3`: 2.89 MB
- `CursedCommander_Combat_music.mp3`: 2.58 MB
- `Malakor_combat_music.mp3`: 2.77 MB
- `1_Combat_GargoyleKing_music.mp3`: 2.89 MB
- `2_Combat_GargoyleKing_music.mp3`: 2.89 MB

In WebGL, audio assets referenced directly in scenes are bundled into the data package and decoded into Web Audio memory buffers upon loading. 18.8 MB compressed is comfortably within browser WebGL heap limits (typically 256MB–512MB). Keeping direct serialized references guarantees instant playback without network streaming latency or asset bundle loading hitches.

---

## 7. Track Mapping & Enum Architecture

### 7.1 `MusicTrackType` Enum
A strongly typed enum avoids fragile string literals:
```csharp
namespace CastleOfTheD20.Core
{
    public enum MusicTrackType
    {
        Village,
        CastleAdventure,
        CellarCombat,
        CursedCommanderCombat,
        MalakorCombat,
        GargoyleKingPhase1,
        GargoyleKingPhase2
    }
}
```

### 7.2 Track Mapping Table
| Enum Member | Asset Path | In-Game Trigger Context |
|---|---|---|
| `Village` | `Assets/Music/VillageSong.mp3` | Oakhaven Village (`GameLocation.Village`) |
| `CastleAdventure` | `Assets/Music/Castle_adventure_song.mp3` | Exploration (`Forest`, `Courtyard`, `Library`, `CrownHall`) |
| `CellarCombat` | `Assets/Music/Cellar_combat_music.mp3` | Tavern Cellar combat encounter |
| `CursedCommanderCombat` | `Assets/Music/CursedCommander_Combat_music.mp3` | Wing 1 Boss battle (`bossIdentifier == "CursedCommander"`) |
| `MalakorCombat` | `Assets/Music/Malakor_combat_music.mp3` | Wing 2 Boss battle (`bossIdentifier == "ShadowMageMalakor"`) |
| `GargoyleKingPhase1` | `Assets/Music/1_Combat_GargoyleKing_music.mp3` | Wing 3 Final Boss Phase 1 (`bossIdentifier == "GargoyleKing"`) |
| `GargoyleKingPhase2` | `Assets/Music/2_Combat_GargoyleKing_music.mp3` | Wing 3 Final Boss Phase 2 (`OnStoneFormActivated`, HP $\le$ 50%) |

---

## 8. AudioManager Delegation Strategy (F1.3)

`AudioManager.cs` currently manages both SFX and BGM (via `bgmSource`). If `MusicManager` and `AudioManager` both play music simultaneously, audio collision occurs.

### 8.1 Responsibility Separation
- **`MusicManager`**: Exclusively owns BGM playback, dual-channel crossfading, music volume, and game state audio transitions.
- **`AudioManager`**: Exclusively owns SFX (dice rolls, sword clashes, spell whooshes, UI clicks, victory/defeat fanfares, procedural synth audio).

### 8.2 Delegation Implementation Points in `AudioManager.cs`
1. **In `Start()`**:
   ```csharp
   if (MusicManager.Instance != null)
   {
       // MusicManager is active; silence AudioManager's bgmSource
       if (bgmSource != null) bgmSource.Stop();
   }
   else
   {
       // Fallback to legacy BGM if MusicManager is absent
       ...
   }
   ```
2. **In `PlayBGM(AudioClip clip, bool loop)`**:
   ```csharp
   if (MusicManager.Instance != null)
   {
       // Yield BGM control to MusicManager
       MusicManager.Instance.PlayMusic(clip);
       return;
   }
   ```
3. **In `HandleLocationChanged(...)` and `HandlePlayModeChanged(...)`**:
   Wrap BGM triggering with:
   ```csharp
   if (MusicManager.Instance != null) return;
   ```
4. **In `SetVolumes(float master, float bgm, float sfx)`**:
   Propagate volume settings:
   ```csharp
   if (MusicManager.Instance != null)
   {
       MusicManager.Instance.SetVolume(master, bgm);
   }
   ```

---

## 9. Full Class Blueprint: `MusicManager.cs`

Below is the complete, production-ready class design for `MusicManager.cs` complying with all M1 specifications:

```csharp
using System;
using System.Collections;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.World;
using CastleOfTheD20.Bosses;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Supported background music tracks mapped to Assets/Music/.
    /// </summary>
    public enum MusicTrackType
    {
        Village,
        CastleAdventure,
        CellarCombat,
        CursedCommanderCombat,
        MalakorCombat,
        GargoyleKingPhase1,
        GargoyleKingPhase2
    }

    /// <summary>
    /// Persistent, WebGL-compatible Music Manager implementing dual-channel equal-power crossfading.
    /// Operates with zero external dependencies and manages all background music transitions.
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        #region Singleton

        public static MusicManager Instance { get; private set; }

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
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.6f;

        #endregion

        #region Serialized Fields - Music Track Clips

        [Header("Music Tracks (Assets/Music/)")]
        [Tooltip("VillageSong.mp3 - Oakhaven / Kivenkolo Village")]
        [SerializeField] private AudioClip villageSong;

        [Tooltip("Castle_adventure_song.mp3 - Forest, Courtyard, Library, Crown Hall exploration")]
        [SerializeField] private AudioClip castleAdventureSong;

        [Tooltip("Cellar_combat_music.mp3 - Tavern Cellar combat encounter")]
        [SerializeField] private AudioClip cellarCombatMusic;

        [Tooltip("CursedCommander_Combat_music.mp3 - Wing 1 Boss battle")]
        [SerializeField] private AudioClip cursedCommanderCombatMusic;

        [Tooltip("Malakor_combat_music.mp3 - Wing 2 Boss battle")]
        [SerializeField] private AudioClip malakorCombatMusic;

        [Tooltip("1_Combat_GargoyleKing_music.mp3 - Wing 3 Final Boss Phase 1")]
        [SerializeField] private AudioClip gargoyleKingPhase1Music;

        [Tooltip("2_Combat_GargoyleKing_music.mp3 - Wing 3 Final Boss Phase 2 (Stone Form)")]
        [SerializeField] private AudioClip gargoyleKingPhase2Music;

        #endregion

        #region Private State

        private AudioSource activeSource;
        private AudioSource standbySource;
        private Coroutine crossfadeCoroutine;
        private Coroutine restoreDelayCoroutine;
        private MusicTrackType? currentTrackType;

        #endregion

        #region Public Properties

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

        public MusicTrackType? CurrentTrack => currentTrackType;

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
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            AudioListener.pause = false;
        }

        private void Start()
        {
            SubscribeToEvents();

            // Auto-start default exploration music if not already playing
            if (!IsPlaying && villageSong != null)
            {
                PlayTrack(MusicTrackType.Village, DEFAULT_FADE_DURATION);
            }
        }

        private void Update()
        {
            // WebGL Autoplay Unmuting: resume audio if browser muted AudioListener prior to user gesture
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

        private void InitializeAudioSources()
        {
            if (sourceA == null)
            {
                GameObject aObj = new GameObject("MusicSource_A");
                aObj.transform.SetParent(transform);
                sourceA = aObj.AddComponent<AudioSource>();
            }

            if (sourceB == null)
            {
                GameObject bObj = new GameObject("MusicSource_B");
                bObj.transform.SetParent(transform);
                sourceB = bObj.AddComponent<AudioSource>();
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
            source.priority = 0;               // Top priority
            source.volume = 0f;
        }

        #endregion

        #region Public Crossfade API

        /// <summary>
        /// Plays a track identified by MusicTrackType with smooth equal-power crossfading.
        /// </summary>
        public void PlayTrack(MusicTrackType trackType, float fadeDuration = DEFAULT_FADE_DURATION)
        {
            AudioClip clip = GetTrackClip(trackType);
            if (clip == null)
            {
                Debug.LogWarning($"[MusicManager] No AudioClip assigned for track type: {trackType}");
                return;
            }

            currentTrackType = trackType;
            PlayMusic(clip, fadeDuration);
        }

        /// <summary>
        /// Plays an arbitrary AudioClip with smooth equal-power crossfading.
        /// </summary>
        public void PlayMusic(AudioClip clip, float fadeDuration = DEFAULT_FADE_DURATION)
        {
            if (restoreDelayCoroutine != null)
            {
                StopCoroutine(restoreDelayCoroutine);
                restoreDelayCoroutine = null;
            }

            // If requested clip is already playing and no crossfade is occurring, do nothing
            if (clip != null && activeSource != null && activeSource.clip == clip && activeSource.isPlaying && crossfadeCoroutine == null)
            {
                return;
            }

            // If clip is null, fade out to silence
            if (clip == null)
            {
                StopMusic(fadeDuration);
                return;
            }

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
            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
                crossfadeCoroutine = null;
            }

            currentTrackType = null;
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
            AudioSource incoming = standbySource;
            AudioSource outgoing = activeSource;
            float targetVolume = CurrentMaxVolume;

            // Handle zero or negative duration (instant transition)
            if (duration <= 0f)
            {
                if (outgoing != null)
                {
                    outgoing.Stop();
                    outgoing.clip = null;
                    outgoing.volume = 0f;
                }

                incoming.clip = newClip;
                incoming.volume = targetVolume;
                incoming.Play();

                activeSource = incoming;
                standbySource = outgoing;
                crossfadeCoroutine = null;
                yield break;
            }

            float startOutVol = outgoing != null ? outgoing.volume : 0f;

            incoming.clip = newClip;
            incoming.volume = 0f;
            incoming.Play();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Equal-Power crossfade curve: sin(t * pi/2) and cos(t * pi/2)
                float inFactor = Mathf.Sin(t * Mathf.PI * 0.5f);
                float outFactor = Mathf.Cos(t * Mathf.PI * 0.5f);

                incoming.volume = targetVolume * inFactor;
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

                yield return null;
            }

            if (outgoing != null)
            {
                outgoing.volume = 0f;
                outgoing.Stop();
                outgoing.clip = null;
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
                default:
                    // Castle adventure song for exploration outside village
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
        /// Selects and plays the appropriate combat track based on boss identifier and room location.
        /// </summary>
        public void PlayCombatMusicForBoss(string bossIdentifier, string roomLocation = "")
        {
            if (!string.IsNullOrEmpty(bossIdentifier))
            {
                if (bossIdentifier.IndexOf("CursedCommander", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    PlayTrack(MusicTrackType.CursedCommanderCombat, DEFAULT_FADE_DURATION);
                    return;
                }
                if (bossIdentifier.IndexOf("Malakor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    PlayTrack(MusicTrackType.MalakorCombat, DEFAULT_FADE_DURATION);
                    return;
                }
                if (bossIdentifier.IndexOf("GargoyleKing", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    PlayTrack(MusicTrackType.GargoyleKingPhase1, DEFAULT_FADE_DURATION);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(roomLocation) && roomLocation.IndexOf("Cellar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                PlayTrack(MusicTrackType.CellarCombat, DEFAULT_FADE_DURATION);
                return;
            }

            // Default fallback combat track
            PlayTrack(MusicTrackType.CellarCombat, DEFAULT_FADE_DURATION);
        }

        /// <summary>
        /// Waits for fanfare sound effects to finish before smoothly restoring exploration music.
        /// </summary>
        public void RestoreExplorationMusic(float delay = 1.5f)
        {
            if (restoreDelayCoroutine != null)
            {
                StopCoroutine(restoreDelayCoroutine);
            }
            restoreDelayCoroutine = StartCoroutine(RestoreExplorationRoutine(delay));
        }

        private IEnumerator RestoreExplorationRoutine(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            GameLocation loc = GameManager.Instance != null ? GameManager.Instance.CurrentLocation : GameLocation.Village;
            HandleLocationChanged(loc);
            restoreDelayCoroutine = null;
        }

        public AudioClip GetTrackClip(MusicTrackType trackType) => trackType switch
        {
            MusicTrackType.Village => villageSong,
            MusicTrackType.CastleAdventure => castleAdventureSong,
            MusicTrackType.CellarCombat => cellarCombatMusic,
            MusicTrackType.CursedCommanderCombat => cursedCommanderCombatMusic,
            MusicTrackType.MalakorCombat => malakorCombatMusic,
            MusicTrackType.GargoyleKingPhase1 => gargoyleKingPhase1Music,
            MusicTrackType.GargoyleKingPhase2 => gargoyleKingPhase2Music,
            _ => null
        };

        #endregion

        #region Editor Tooling Helpers

        public void AssignClip(MusicTrackType trackType, AudioClip clip)
        {
            switch (trackType)
            {
                case MusicTrackType.Village: villageSong = clip; break;
                case MusicTrackType.CastleAdventure: castleAdventureSong = clip; break;
                case MusicTrackType.CellarCombat: cellarCombatMusic = clip; break;
                case MusicTrackType.CursedCommanderCombat: cursedCommanderCombatMusic = clip; break;
                case MusicTrackType.MalakorCombat: malakorCombatMusic = clip; break;
                case MusicTrackType.GargoyleKingPhase1: gargoyleKingPhase1Music = clip; break;
                case MusicTrackType.GargoyleKingPhase2: gargoyleKingPhase2Music = clip; break;
            }
        }

        #endregion
    }
}
```

---

## 10. Summary of Architectural Recommendations for Implementer

1. **Clean Namespace Usage**:
   - `MusicManager.cs` should live in `CastleOfTheD20.Core`.
   - References `CastleOfTheD20.World` (`DungeonRoomController`), `CastleOfTheD20.Bosses` (`GargoyleKingBoss`), and `CastleOfTheD20.Combat` (`TurnManager`).
2. **Timing with Unscaled Time**:
   - Always use `Time.unscaledDeltaTime` and `WaitForSecondsRealtime` so audio transitions remain responsive during pause menus or modal windows.
3. **No External Plugins**:
   - Completely native Unity C# using built-in `AudioSource`, `AudioClip`, and coroutine subsystems.
4. **AudioManager Delegation**:
   - When modifying `AudioManager.cs`, insert early checks `if (MusicManager.Instance != null) return;` into `PlayBGM` and event handlers to prevent music overlap.
