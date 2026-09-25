# Handoff Report: Milestone M1 AudioManager Coexistence & Singleton Lifecycle

## 1. Observation

Direct observations from inspection of the codebase at `D:\Unity\3D DnD selainpeli`:

1. **`AudioManager.cs` BGM Start Conflict (`Assets/Scripts/Core/AudioManager.cs`)**:
   - Lines 41–42: `[SerializeField] private AudioSource bgmSource;` and `[SerializeField] private AudioSource sfxSource;`.
   - Lines 100–112: Inside `Start()`:
     ```csharp
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
     `AudioManager` auto-starts BGM looping unconditionally upon scene start, directly clashing with any active `MusicManager`.

2. **`AudioManager.cs` Event Interception**:
   - Lines 153–167: Subscribes to `DiceSystem.OnDiceRolled`, `GameManager.OnLocationChanged`, `GameManager.OnPlayModeChanged`, `TurnManager.OnCombatEnded`.
   - Lines 274–287: `HandleLocationChanged` switches on `GameLocation.Village`, `Courtyard`, `Library`, `CrownHall`, calling `PlayBGM()`.
   - Lines 289–307: `HandlePlayModeChanged` plays generic `combatBgmClip` on combat mode, overriding boss-specific tracks.
   - Lines 169–172:
     ```csharp
     private void HandleCombatEnded(bool isVictory)
     {
         PlaySFX(isVictory ? SoundType.Victory : SoundType.Defeat);
     }
     ```
     Plays victory/defeat fanfare on `sfxSource` (0.8s duration via `CreateFanfareSynth`, lines 407–425).

3. **External Audio References Across Codebase**:
   Grep for `AudioManager` across `Assets/` returned references in:
   - `Assets/Scenes/StartVillage.unity` (line 8772): Attached to root GameObject `Managers` (ID 556439108).
   - `Assets/Scripts/Editor/BuildVillageEditor.cs` (lines 370–373): `managers.AddComponent<AudioManager>()`.
   - `Assets/Scripts/UI/MainMenuController.cs` (lines 106, 112, 118, 124, 134): Calls `AudioManager.Instance.PlaySFX(SoundType.ButtonClick)` and `CriticalSuccess`.
   - `Assets/Scripts/UI/DefeatUIController.cs` (lines 114, 142, 188): Calls `AudioManager.Instance.PlaySFX(SoundType.Defeat)` and `ButtonClick`.
   - Zero external scripts call `AudioManager.Instance.PlayBGM` or `StopBGM`.

4. **Scene Hierarchy and Singleton Setup**:
   - In `Assets/Scenes/StartVillage.unity` (lines 8731–8759), root GameObject `Managers` holds `AudioManager` and children `TurnManager`, etc.
   - In `BuildVillageEditor.cs` (lines 361–379), `EnsureManagersInScene` adds `AudioManager` and `DialogueActionTrigger` to `Managers`.
   - Milestone M1 and M4 specifications require `MusicManager` to be instantiated on `Managers` in `StartVillage.unity`.
   - In `AudioManager.cs` (lines 80–93), `Awake()` calls `Instance = this; transform.SetParent(null); DontDestroyOnLoad(gameObject);`.
   - If duplicate: `Destroy(gameObject); return;`.

---

## 2. Logic Chain

1. **BGM Dissonance Elimination (Observations 1 & 3)**:
   - Observation 1 shows `AudioManager.Start()` unconditionally starts `bgmSource` with either `villageBgmClip` or `GenerateHarpBgmClip()`.
   - Because `MusicManager` will be responsible for WebGL crossfading of `VillageSong.mp3`, having `bgmSource` simultaneously active produces dual-track clash.
   - Observation 3 shows zero external scripts call `PlayBGM`; only internal event triggers fire it.
   - Therefore, gating `AudioManager.Start()`, `HandleLocationChanged()`, and `HandlePlayModeChanged()` behind `if (MusicManager.Instance != null) return;` completely silences `bgmSource` during normal gameplay while retaining fallback procedural audio if `MusicManager` is absent.

2. **SFX Integrity Preservation (Observations 2 & 3)**:
   - Observation 2 reveals that `HandleCombatEnded` triggers `PlaySFX(isVictory ? SoundType.Victory : SoundType.Defeat)`.
   - This generates an 0.8s fanfare sound effect through `sfxSource`.
   - Observation 3 shows all external UI controllers (`MainMenuController`, `DefeatUIController`) and `DiceSystem` interact exclusively with `AudioManager.Instance.PlaySFX`.
   - Because `sfxSource` is a separate audio channel from `MusicManager`'s dual music sources, keeping all SFX and procedural synthesis intact in `AudioManager` guarantees 100% SFX continuity with zero channel collision.

3. **Fanfare & Music Transition Coordination (Observation 2 & PROJECT.md F2.6)**:
   - Observation 2 shows the fanfare SFX plays for ~0.8s on combat end.
   - PROJECT.md F2.6 requires waiting ~1.5s after `TurnManager.OnCombatEnded` before restoring exploration music.
   - Therefore, allowing `AudioManager.HandleCombatEnded` to play the fanfare SFX immediately, while `MusicManager` delays its exploration crossfade by 1.5s, provides a seamless, non-overlapping acoustic transition.

4. **Singleton Coexistence on Shared `Managers` Root (Observations 3 & 4)**:
   - In `StartVillage.unity`, `Managers` is a root GameObject hosting `AudioManager`, and will soon host `MusicManager`.
   - In Unity, calling `DontDestroyOnLoad(gameObject)` on `Managers` preserves the root GameObject and all attached components and children.
   - If a duplicate scene loads, calling `Destroy(gameObject)` if `Instance != null && Instance != this && Instance.gameObject != gameObject` cleanly destroys the redundant `Managers` tree.
   - If `Instance.gameObject == gameObject` (accidental double component), destroying `this` prevents destroying the original `Managers` GameObject.
   - Therefore, this dual-check singleton pattern in both `AudioManager` and `MusicManager` guarantees safe lifecycle management across all scene transitions.

---

## 3. Caveats

1. **Assembly Compilation Order**:
   `AudioManager.cs` references `MusicManager.Instance`. Both scripts reside in `Assets/Scripts/Core/` and compile within `Assembly-CSharp.dll`. `MusicManager.cs` must be created or compiled together with `AudioManager.cs` edits to avoid temporary symbol errors.
2. **Procedural Harp Retention**:
   The procedural harp generator (`GenerateHarpBgmClip`) is preserved in `AudioManager.cs` as a graceful fallback for offline or headless environments where `MusicManager` is not present.
3. **Volume Slider Sync**:
   If the game later exposes volume options via `AudioManager.SetVolumes()`, forwarding the calculated music volume (`master * bgm`) to `MusicManager.Instance.SetVolume()` ensures synchronized audio levels.

---

## 4. Conclusion

`AudioManager.cs` and `MusicManager.cs` can coexist cleanly with minimal, high-impact edits:
1. `AudioManager` yields all BGM playback (`Start()`, `HandleLocationChanged()`, `HandlePlayModeChanged()`) when `MusicManager.Instance != null`.
2. Any legacy `PlayBGM` / `StopBGM` calls on `AudioManager` delegate to `MusicManager.Instance.PlayMusic()` / `StopMusic()`.
3. `AudioManager` retains 100% ownership of SFX (`sfxSource`, `PlaySFX`, `DiceSystem` rolls, victory/defeat fanfares, procedural synthesizers).
4. Both components share the `Managers` GameObject marked `DontDestroyOnLoad`, utilizing safe duplicate destruction checks (`if (Instance.gameObject != gameObject) Destroy(gameObject); else Destroy(this);`).
5. A two-way handshake (`MusicManager.Awake()` calling `AudioManager.Instance?.StopBGM()`) guarantees zero race conditions or audio overlap.

---

## 5. Verification Method

1. **Code & Symbol Inspection**:
   - Inspect `Assets/Scripts/Core/AudioManager.cs` around lines 80–115, 180–205, and 270–310 to confirm delegation guards.
   - Verify `Assets/Scripts/Core/MusicManager.cs` includes `Instance`, `PlayMusic(AudioClip, float)`, `StopMusic(float)`, and `SetVolume(float)`.
2. **Compilation Verification**:
   - Verify `Assembly-CSharp.dll` compiles with 0 errors via Unity or CLI build.
3. **Runtime & Play Mode Validation**:
   - In `StartVillage.unity`, press Play.
   - Observe Audio hierarchy: `Managers/MusicSource_A`, `Managers/MusicSource_B`, `Managers/SFX_Source`, `Managers/BGM_Source`.
   - Verify `Managers/BGM_Source` remains `isPlaying == false`.
   - Verify `MusicManager` plays `VillageSong.mp3`.
   - Click UI buttons or roll dice: verify SFX plays on `Managers/SFX_Source`.
   - Trigger combat end: verify fanfare SFX plays on `sfxSource`, followed 1.5s later by exploration music crossfade on `MusicManager`.
