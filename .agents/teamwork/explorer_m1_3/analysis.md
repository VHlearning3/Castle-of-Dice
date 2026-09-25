# Milestone M1 Analysis: AudioManager Coexistence & Singleton Lifecycle

## 1. Executive Summary

Milestone M1 establishes the core audio architecture for Castle of the D20 / Castle of Dice. The current audio system relies on a monolithic `AudioManager.cs` that handles both background music (BGM) and sound effects (SFX), including WebGL procedural audio synthesis.

To fulfill Requirement R1 and Feature F1.3 (`AudioManager BGM Delegation`), `MusicManager.cs` will become the dedicated, persistent dual-channel audio engine driving all 7 MP3 tracks with 1.2s smooth crossfades. `AudioManager.cs` must cleanly yield BGM playback to `MusicManager.cs` while preserving all SFX capabilities on `sfxSource` (dice rolls, procedural fanfares, UI clicks, combat impacts). Both managers will coexist as Singletons on the shared root `Managers` GameObject across scene loads without race conditions or duplicate destruction conflicts.

---

## 2. In-Depth Inspection of `AudioManager.cs`

`AudioManager.cs` (`Assets/Scripts/Core/AudioManager.cs`, 522 lines) manages audio through two serialized `AudioSource` references and procedural synthesis fallbacks:

### 2.1 Audio Sources & State
- Line 41: `[SerializeField] private AudioSource bgmSource;` — dedicated looping channel for background music.
- Line 42: `[SerializeField] private AudioSource sfxSource;` — dedicated one-shot channel for sound effects.
- Lines 45–47: Master, BGM, and SFX volume sliders (`masterVolume = 0.8f`, `bgmVolume = 0.6f`, `sfxVolume = 0.9f`).
- Lines 51–58: Default music clips (`villageBgmClip`, `dungeonBgmClip`, `combatBgmClip`).
- Lines 60–68: Default SFX clips (`diceRollClip`, `critSuccessClip`, `critFailClip`, `swordSlashClip`, `spellCastClip`, `potionClip`, `buttonClickClip`, `questCompleteClip`).
- Lines 73–74: `proceduralClipCache` dictionary storing generated `AudioClip` instances.

### 2.2 Current Singleton & Hierarchy Lifecycle
- Lines 80–93 (`Awake`):
  ```csharp
  if (Instance != null && Instance != this)
  {
      Destroy(gameObject);
      return;
  }
  Instance = this;
  transform.SetParent(null);
  DontDestroyOnLoad(gameObject);
  InitializeAudioSources();
  ```
- Lines 128–151 (`InitializeAudioSources`):
  Dynamically instantiates two child GameObjects: `BGM_Source` and `SFX_Source` under `AudioManager`'s transform if serialized fields are unassigned. Both are configured with `spatialBlend = 0f` (2D Stereo).

### 2.3 Current BGM Auto-Start Behavior
- Lines 95–113 (`Start`):
  ```csharp
  SubscribeToEvents();

  if (bgmSource != null && !bgmSource.isPlaying)
  {
      if (villageBgmClip != null)
      {
          PlayBGM(villageBgmClip);
      }
      else
      {
          AudioClip proceduralBgm = GenerateHarpBgmClip();
          PlayBGM(proceduralBgm);
      }
  }
  ```
  **Conflict**: `AudioManager.Start()` unconditionally begins looping `villageBgmClip` or procedural harp music on `bgmSource`. When `MusicManager` starts playback of `VillageSong.mp3`, both tracks would play simultaneously, creating harmonic dissonance and audio clipping.

### 2.4 Event Subscriptions & Handlers
- Lines 153–167 (`SubscribeToEvents` / `UnsubscribeFromEvents`):
  Subscribes to:
  1. `DiceSystem.OnDiceRolled += HandleDiceRolled;`
  2. `GameManager.OnLocationChanged += HandleLocationChanged;`
  3. `GameManager.OnPlayModeChanged += HandlePlayModeChanged;`
  4. `CastleOfTheD20.Combat.TurnManager.OnCombatEnded += HandleCombatEnded;`

- Lines 274–287 (`HandleLocationChanged`):
  ```csharp
  switch (newLocation)
  {
      case GameLocation.Village:
          if (villageBgmClip != null) PlayBGM(villageBgmClip);
          break;
      case GameLocation.Courtyard:
      case GameLocation.Library:
      case GameLocation.CrownHall:
          if (dungeonBgmClip != null) PlayBGM(dungeonBgmClip);
          break;
  }
  ```
  **Conflict**: Hardcoded transitions to coarse clips without smooth crossfading; lacks `Forest` location support; conflicts with `MusicManager`'s dynamic track resolution.

- Lines 289–307 (`HandlePlayModeChanged`):
  ```csharp
  if (newMode == GamePlayMode.Combat && combatBgmClip != null)
  {
      PlayBGM(combatBgmClip);
  }
  else if (newMode == GamePlayMode.Exploration)
  {
      ...
  }
  ```
  **Conflict**: Fires generic `combatBgmClip` upon combat mode entry, overriding boss-specific music (Cursed Commander, Malakor, Gargoyle King, Cellar).

- Lines 169–172 (`HandleCombatEnded`):
  ```csharp
  private void HandleCombatEnded(bool isVictory)
  {
      PlaySFX(isVictory ? SoundType.Victory : SoundType.Defeat);
  }
  ```
  **Preservation Note**: This handler fires SFX (`SoundType.Victory` or `SoundType.Defeat`), NOT BGM! It generates or plays an 0.8s fanfare on `sfxSource`. This must be preserved.

---

## 3. BGM Delegation Design: `AudioManager` → `MusicManager`

### 3.1 Principles of Delegation
1. **Exclusive BGM Authority**: When `MusicManager.Instance != null`, `AudioManager` must completely cease initiating, switching, or looping audio on `bgmSource`.
2. **Backward-Compatible API**: Any legacy calls to `AudioManager.Instance.PlayBGM(clip)` or `AudioManager.Instance.StopBGM()` must delegate directly to `MusicManager.Instance`.
3. **Graceful Fallback**: If `MusicManager` is not present in a lightweight test scene, `AudioManager` can still provide basic fallback music and procedural harp playback.
4. **Two-Way Silencing Guarantee**:
   - `AudioManager.Start()` silences `bgmSource` if `MusicManager.Instance != null`.
   - `MusicManager.Awake()` and `MusicManager.PlayMusic()` explicitly call `AudioManager.Instance?.StopBGM()` to guarantee no race condition can leave `bgmSource` playing.

### 3.2 Specific Modifications in `AudioManager.cs`

#### A. `AudioManager.Start()`
```csharp
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

    // Fallback: Start default village background music only if MusicManager is absent
    if (bgmSource != null && !bgmSource.isPlaying)
    {
        if (villageBgmClip != null)
        {
            PlayBGM(villageBgmClip);
        }
        else
        {
            AudioClip proceduralBgm = GenerateHarpBgmClip();
            PlayBGM(proceduralBgm);
        }
    }
}
```

#### B. `AudioManager.HandleLocationChanged()`
```csharp
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
            if (dungeonBgmClip != null) PlayBGM(dungeonBgmClip);
            break;
    }
}
```

#### C. `AudioManager.HandlePlayModeChanged()`
```csharp
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
```

#### D. `AudioManager.PlayBGM()` & `AudioManager.StopBGM()`
```csharp
/// <summary>
/// Plays background music. Delegates to MusicManager if active, otherwise plays on legacy bgmSource.
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
```

#### E. Volume Synchronization (`AudioManager.SetVolumes`)
```csharp
public void SetVolumes(float master, float bgm, float sfx)
{
    masterVolume = Mathf.Clamp01(master);
    bgmVolume = Mathf.Clamp01(bgm);
    sfxVolume = Mathf.Clamp01(sfx);
    UpdateVolumes();

    if (MusicManager.Instance != null)
    {
        MusicManager.Instance.SetVolume(masterVolume * bgmVolume);
    }
}
```

---

## 4. SFX Preservation Analysis

### 4.1 Channel Isolation
`AudioManager` plays all sound effects through `sfxSource`:
```csharp
sfxSource.PlayOneShot(clipToPlay, masterVolume * sfxVolume * Mathf.Clamp01(volumeScale));
```
- `sfxSource` is dedicated and independent from `bgmSource` and `MusicManager`'s dual sources (`audioSourceA` and `audioSourceB`).
- Calling `PlayOneShot` allows multiple SFX clips to overlap cleanly without clipping or premature truncation.
- 2D spatial blend (`spatialBlend = 0f`) ensures uniform stereo delivery in WebGL.

### 4.2 Combat Fanfare Handshake
`TurnManager.OnCombatEnded(bool isVictory)` triggers two coordinated actions across both managers:
1. **SFX Layer (`AudioManager.cs`)**:
   - `HandleCombatEnded(bool isVictory)` invokes `PlaySFX(isVictory ? SoundType.Victory : SoundType.Defeat)`.
   - Procedural synthesis generates an 0.8s ascending major fanfare (Victory) or descending minor fanfare (Defeat).
   - Played immediately on `sfxSource`.
2. **Music Layer (`MusicManager.cs`)**:
   - Subscribes to `TurnManager.OnCombatEnded`.
   - Starts a coroutine that waits **1.5 seconds** (delay buffer).
   - During the 1.5s window, the fanfare is heard crisply without competing exploration music.
   - After 1.5s, `MusicManager` begins the 1.2s smooth crossfade back to the active `GameLocation` exploration track.

### 4.3 Dice & UI Sound Effects
All existing game systems trigger SFX through `AudioManager`:
- `DiceSystem.OnDiceRolled`: `SoundType.CriticalSuccess`, `SoundType.CriticalFailure`, `SoundType.DiceRoll`.
- `MainMenuController.cs`: `SoundType.ButtonClick`, `SoundType.CriticalSuccess`.
- `DefeatUIController.cs`: `SoundType.Defeat`, `SoundType.ButtonClick`.
None of these interfere with BGM or `MusicManager`.

---

## 5. Singleton Lifecycle & Scene Hierarchy Coexistence

### 5.1 The `Managers` Hierarchy Structure
In `Assets/Scenes/StartVillage.unity` (and constructed by `BuildVillageEditor.cs`), the root `Managers` GameObject hosts:
- `Transform` (root, `parent == null`)
- `AudioManager` (MonoBehaviour)
- `DialogueActionTrigger` (MonoBehaviour)
- `MusicManager` (MonoBehaviour, added in M1/M4)
- Children GameObjects: `TurnManager`, `GridController`, `CombatInteractionHandler`, etc.

### 5.2 Duplicate Destruction Hazard & Resolution
If a duplicate scene is loaded or re-entered:
- **Hazard**: If `MusicManager.Awake()` calls `Destroy(gameObject)` when duplicate, but `this.gameObject` is the shared `Managers` GameObject:
  - If executed on the duplicate scene's `Managers`, destroying `gameObject` is intended.
  - BUT if multiple components on the same GameObject check their instance, calling `Destroy(gameObject)` on the original object would destroy all managers!
- **Safe Singleton Pattern**:
  Both `AudioManager` and `MusicManager` must verify whether the duplicate is on another GameObject or the same GameObject:
  ```csharp
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
  ```
  - If a new scene loads a duplicate `Managers` GameObject (`Instance.gameObject != gameObject`), destroying `gameObject` cleanly eliminates the duplicate `Managers` GameObject with all its duplicate children and components in a single pass.
  - If an errant duplicate component was added to the same GameObject (`Instance.gameObject == gameObject`), destroying `this` cleans up the redundant component without harming the parent `Managers` GameObject or other managers.

### 5.3 `DontDestroyOnLoad` Idempotency
- When `AudioManager.Awake()` executes:
  `transform.SetParent(null); DontDestroyOnLoad(gameObject);`
- When `MusicManager.Awake()` executes:
  `if (transform.parent != null) transform.SetParent(null); DontDestroyOnLoad(gameObject);`
Calling `DontDestroyOnLoad` on an already-marked root GameObject in Unity is completely idempotent and safe.

### 5.4 Destruction / Reset Protocol (`OnDestroy`)
When exiting play mode or during scene transitions:
```csharp
private void OnDestroy()
{
    UnsubscribeFromEvents();
    if (Instance == this)
    {
        Instance = null;
    }
}
```
Ensures static references are cleared and do not leak between Play Mode test cycles.

---

## 6. Implementation Patch Proposal for `AudioManager.cs`

```csharp
// Changes to Assets/Scripts/Core/AudioManager.cs

// 1. In Awake(): Robust duplicate check
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

// 2. In Start(): Yield to MusicManager
private void Start()
{
    SubscribeToEvents();

    if (MusicManager.Instance != null)
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }
        return;
    }

    if (bgmSource != null && !bgmSource.isPlaying)
    {
        if (villageBgmClip != null)
        {
            PlayBGM(villageBgmClip);
        }
        else
        {
            AudioClip proceduralBgm = GenerateHarpBgmClip();
            PlayBGM(proceduralBgm);
        }
    }
}

// 3. In PlayBGM(): Delegate to MusicManager
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

// 4. In StopBGM(): Delegate to MusicManager
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

// 5. In HandleLocationChanged(): Yield to MusicManager
private void HandleLocationChanged(GameLocation newLocation)
{
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
            if (dungeonBgmClip != null) PlayBGM(dungeonBgmClip);
            break;
    }
}

// 6. In HandlePlayModeChanged(): Yield to MusicManager
private void HandlePlayModeChanged(GamePlayMode newMode)
{
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

// 7. In SetVolumes(): Forward volume to MusicManager
public void SetVolumes(float master, float bgm, float sfx)
{
    masterVolume = Mathf.Clamp01(master);
    bgmVolume = Mathf.Clamp01(bgm);
    sfxVolume = Mathf.Clamp01(sfx);
    UpdateVolumes();

    if (MusicManager.Instance != null)
    {
        MusicManager.Instance.SetVolume(masterVolume * bgmVolume);
    }
}
```

---

## 7. `MusicManager.cs` Requirements from `AudioManager` Coexistence

For seamless coexistence, `MusicManager.cs` should expose the following public methods and lifecycle behaviors:

1. **Singleton Accessor**:
   `public static MusicManager Instance { get; private set; }`
2. **Music Playback & Stop API**:
   - `public void PlayMusic(AudioClip clip, float fadeDuration = 1.2f)`
   - `public void StopMusic(float fadeDuration = 1.2f)`
   - `public void SetVolume(float volume)`
3. **Awake Handshake**:
   Inside `MusicManager.Awake()`, immediately check and silence `AudioManager`:
   ```csharp
   if (AudioManager.Instance != null)
   {
       AudioManager.Instance.StopBGM();
   }
   ```
4. **Combat End Coordination**:
   `MusicManager` subscribes to `TurnManager.OnCombatEnded += HandleCombatEnded;` and waits 1.5 seconds before crossfading exploration music, allowing `AudioManager`'s fanfare SFX to complete.
