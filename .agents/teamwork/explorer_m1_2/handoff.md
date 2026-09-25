# Handoff Report — explorer_m1_2: MusicManager Track Mapping, Enum & Public API

## 1. Observation

Direct observations from inspection of the codebase at `D:\Unity\3D DnD selainpeli`:

1. **Audio Assets in `Assets/Music/`**:
   - `Assets/Music/VillageSong.mp3` (2,177,567 bytes, GUID `8c695f2c8079fff438568e6ed65231a4`)
   - `Assets/Music/Castle_adventure_song.mp3` (2,863,535 bytes, GUID `9fbf8d6dbe585eb41846c4f923b7ef84`)
   - `Assets/Music/Cellar_combat_music.mp3` (2,885,423 bytes, GUID `2d54406a66699564c8d0eafe28646b97`)
   - `Assets/Music/CursedCommander_Combat_music.mp3` (2,582,063 bytes, GUID `64736f9872e4eb245be5d34208a0d48a`)
   - `Assets/Music/Malakor_combat_music.mp3` (2,769,455 bytes, GUID `a276eb9eef11c9748b6f72cfae8bfb51`)
   - `Assets/Music/1_Combat_GargoyleKing_music.mp3` (2,889,263 bytes, GUID `7bf5b50875e533e4b958c21bbddca4ad`)
   - `Assets/Music/2_Combat_GargoyleKing_music.mp3` (2,885,423 bytes, GUID `496ee8670bc23514a8072d733cb535c5`)
   All 7 MP3s and their corresponding `.meta` files are present and uncorrupted.

2. **Absence of Dedicated `MusicManager.cs`**:
   - Codebase search for `MusicManager` returned 0 results in `Assets/Scripts/`.
   - `AudioManager.cs` (`Assets/Scripts/Core/AudioManager.cs`) lines 41–42 contains `bgmSource` and `sfxSource`, but only handles single-track abrupt switching without dual-source crossfading, and has only 3 clips (`villageBgmClip`, `dungeonBgmClip`, `combatBgmClip`).

3. **Boss Identifiers in Room Controllers**:
   - `DungeonRoomController.cs` (`Assets/Scripts/World/DungeonRoomController.cs`) line 37: `public string roomLocation = "Village";`
   - Line 40: `public string bossIdentifier = "";`
   - Line 92: `public static event Action<DungeonRoomController> OnRoomCombatStarted;`
   - In `BuildDungeonWingsEditor.cs`:
     - Line 268: `room.bossIdentifier = "CursedCommander";` (`room.roomLocation = "Courtyard"`)
     - Line 338: `room.bossIdentifier = "ShadowMageMalakor";` (`room.roomLocation = "Library"`)
     - Line 412: `room.bossIdentifier = "GargoyleKing";` (`room.roomLocation = "CrownHall"`)
   - In `BuildCellarEditor.cs`:
     - Lines 602–603: `roomController.roomLocation = "Cellar"; roomController.bossIdentifier = "";`

4. **Phase 2 Boss Trigger**:
   - `GargoyleKingBoss.cs` (`Assets/Scripts/Bosses/GargoyleKingBoss.cs`) line 72: `public static event Action<GargoyleKingBoss> OnStoneFormActivated;`
   - Line 124–127: In `TakeDamage()`:
     ```csharp
     if (IsAlive && !hasEnteredPhase2 && currentHP <= (maxHP / 2))
     {
         EnterStoneForm();
     }
     ```
   - Line 185: `OnStoneFormActivated?.Invoke(this);` inside `EnterStoneForm()`.

5. **Combat Resolution & Fanfares**:
   - `TurnManager.cs` (`Assets/Scripts/Combat/TurnManager.cs`) line 71: `public static event Action<bool> OnCombatEnded;`
   - `AudioManager.cs` lines 169–172:
     ```csharp
     private void HandleCombatEnded(bool isVictory)
     {
         PlaySFX(isVictory ? SoundType.Victory : SoundType.Defeat);
     }
     ```
   - Fanfare duration generated procedurally in `CreateFanfareSynth` is 0.8s.

6. **Defeat Screen Retry Flow**:
   - `DefeatUIController.cs` (`Assets/Scripts/UI/DefeatUIController.cs`) line 176:
     ```csharp
     TurnManager.Instance.StartCombatEncounter(player, room.roomLocation, room.bossIdentifier);
     ```

---

## 2. Logic Chain

1. **Mapping 7 MP3s to Enum**:
   - From Observation 1, exactly 7 MP3 tracks exist in `Assets/Music/`.
   - From Observations 3 & 4, the game features:
     - 1 Village exploration area (`VillageSong.mp3`)
     - 4 Castle exploration areas (`Castle_adventure_song.mp3`)
     - 1 Cellar encounter (`Cellar_combat_music.mp3`)
     - 3 Wing bosses (`CursedCommander_Combat_music.mp3`, `Malakor_combat_music.mp3`, `1_Combat_GargoyleKing_music.mp3`)
     - 1 Phase 2 Stone Form state (`2_Combat_GargoyleKing_music.mp3`)
   - Therefore, a dedicated enum `MusicTrackType` with 8 values (`None = 0`, `Village = 1`, `CastleAdventure = 2`, `CellarCombat = 3`, `CursedCommanderCombat = 4`, `MalakorCombat = 5`, `GargoyleKingPhase1 = 6`, `GargoyleKingPhase2 = 7`) maps 1:1 to every audio asset.

2. **Boss & Encounter Resolution**:
   - Observation 3 shows `bossIdentifier` is `"CursedCommander"`, `"ShadowMageMalakor"`, or `"GargoyleKing"`, while Cellar uses `roomLocation = "Cellar"`.
   - Therefore, `PlayCombatMusicForBoss(string bossIdentifier, string roomLocation = "")` must resolve boss names case-insensitively, check both English and Finnish names (`Commander`/`Komentaja`, `Malakor`/`Varjomaagi`, `Gargoyle`/`Kivettymiskuningas`), and fall back to `roomLocation` matching.

3. **Exploration Music Interruption & Restoration**:
   - Observation 5 shows `OnCombatEnded(bool isVictory)` fires when combat ends and plays victory/defeat fanfares for ~0.8s.
   - Therefore, `RestoreExplorationMusic(float delay = 1.5f)` must use a coroutine to wait 1.5s so fanfares are heard clearly before crossfading back.
   - Ground truth for exploration restoration is determined by checking `GameManager.Instance.CurrentLocation` (`Village` -> `VillageSong`, other locations -> `Castle_adventure_song`), with fallback to `currentExplorationTrack` memory if `GameManager.Instance` is null.
   - From Observation 6, clicking "Retry" in `DefeatUIController` immediately restarts combat. Therefore, any call to `PlayCombatMusicForBoss` or `PlayTrack` MUST cancel any pending restore coroutine, preventing delayed exploration restoration from trampling newly engaged combat music.

4. **Coexistence with `AudioManager`**:
   - Observation 2 shows `AudioManager` plays BGM on start.
   - Therefore, `AudioManager` must check `if (MusicManager.Instance != null) return;` in `Start()`, `HandleLocationChanged()`, and `HandlePlayModeChanged()`, yielding all BGM duties to `MusicManager` while continuing to supervise SFX.

---

## 3. Caveats

1. **`GameLocation.Forest` Enum Member**:
   `Forest` is to be added to `GameLocation` in Milestone M2. In Milestone M1, `MusicManager.HandleLocationChanged` can safely cast or compare `(int)newLocation == 4` or use string comparison so that it compiles cleanly in both M1 and M2.
2. **Editor Assignment & GUID Stability**:
   When `BuildVillageEditor` populates `MusicManager` fields in Milestone M4, using `AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/...")` preserves GUIDs without touching `.meta` files.
3. **No External Dependencies**:
   All crossfading and volume interpolation must run entirely within standard Unity `MonoBehaviour` coroutines without third-party tweening packages (e.g. DOTween or LeanTween) to guarantee WebGL runtime compatibility.

---

## 4. Conclusion

The specification for `MusicManager.cs` track mapping and public API is complete and verified:
- **Enum**: `MusicTrackType` (None, Village, CastleAdventure, CellarCombat, CursedCommanderCombat, MalakorCombat, GargoyleKingPhase1, GargoyleKingPhase2).
- **Serialized Fields**: 7 `AudioClip` fields for the tracks, 2 `AudioSource` fields (`audioSourceA`, `audioSourceB`), volume controls, and fade duration.
- **Public API**:
  - `PlayMusic(AudioClip clip, float fadeDuration = 1.2f)`
  - `PlayTrack(MusicTrackType track, float fadeDuration = 1.2f)`
  - `PlayCombatMusicForBoss(string bossIdentifier, string roomLocation = "")`
  - `RestoreExplorationMusic(float delay = 1.5f)`
  - `StopMusic(float fadeDuration = 1.2f)`
  - `AssignClips(...)` for automated setup and E2E testing
  - Properties: `CurrentTrack`, `CurrentExplorationTrack`, `CurrentClip`, `IsPlaying`, `IsCrossFading`, `MasterVolume`, `MusicVolume`.
- Full detailed implementation design is documented in `analysis.md`.

---

## 5. Verification Method

1. **Verify Asset Presence & Integrity**:
   Inspect `Assets/Music/` to verify all 7 `.mp3` and `.meta` files exist with the expected GUIDs.
2. **Code Inspection**:
   Inspect `D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_m1_2\analysis.md` for complete API signatures and resolution matrix.
3. **Compilation & E2E Validation**:
   When `MusicManager.cs` is implemented by the implementer agent:
   - Compile `Assembly-CSharp.dll` with 0 errors.
   - Run the E2E test runner once implemented under `Assets/Tests/E2E/` to verify all 7 tracks, zero-fade, boss mapping, and exploration restore cases pass.
