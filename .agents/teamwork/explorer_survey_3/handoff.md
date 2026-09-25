# Handoff Report: Editor Tooling, Scene Architecture & Compilation Survey

**Agent**: Explorer Survey 3  
**Working Directory**: `D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_survey_3`  
**Parent**: Orchestrator (`ca3fb1df-84ce-4aaa-ac7d-83cdd973b003`)  
**Type**: Hard Handoff (Investigation Complete)  
**Date**: 2026-09-24  

---

## 1. Observation

1. **Editor Tooling**:
   - In `Assets/Scripts/Editor/BuildVillageEditor.cs`, lines 361-392:
     ```csharp
     private static void EnsureManagersInScene()
     {
         GameObject managers = GameObject.Find("Managers");
         if (managers == null)
         {
             managers = new GameObject("Managers");
             Undo.RegisterCreatedObjectUndo(managers, "Create Managers");
         }

         if (managers.GetComponent<AudioManager>() == null && Object.FindAnyObjectByType<AudioManager>() == null)
         {
             managers.AddComponent<AudioManager>();
         }
         ...
     ```
   - In `Assets/Scripts/Editor/CastleOfDiceControlWindow.cs`, lines 58-88:
     Monitors scene status (`CourtYard`, `Forest`, `Library`, `ThroneRoom`, `Village_Layout`, bosses), but currently has no check or button for `MusicManager`.
   - In `Assets/Scripts/Editor/AutoSetupGameEditor.cs`, line 56:
     Calls `BuildVillageEditor.BuildCompleteVillage()`, which runs `EnsureManagersInScene()`.

2. **Scene Hierarchy (`StartVillage.unity`)**:
   - In `Assets/Scenes/StartVillage.unity`, lines 8720-8770:
     - Root GameObject: `Managers` (`fileID 556439108`, Transform `fileID 556439109`)
     - Attached Component: `AudioManager` (`fileID 556439110`, Script GUID: `e09d634472d870d44ab55ac0738d5ed8`)
     - Child Transforms:
       - `TurnManager` (fileID `1378057752`)
       - `GameManager` (fileID `2064034050`)
       - `AbilityExecutor` (fileID `677255356`)
       - `InventoryManager` (fileID `2061720238`)
       - `ShopManager` (fileID `479882307`)
       - `QuestManager` (fileID `1777815439`)
       - `DialogueController` (fileID `368632553`)
       - `DungeonRoomController` (fileID `717851335`)
       - `ShopUIController` (fileID `1276350411`)
   - `MusicManager` is currently not present in the scene.

3. **Audio Tracks & GUID Integrity**:
   - In `Assets/Music/`, all 7 files and their `.meta` files are present:
     - `VillageSong.mp3` — GUID `8c695f2c8079fff438568e6ed65231a4`
     - `Castle_adventure_song.mp3` — GUID `89bd107ca50cf9f49811cf24b093e6ed`
     - `Cellar_combat_music.mp3` — GUID `10578e684e333d044802dee8f2f0446b`
     - `CursedCommander_Combat_music.mp3` — GUID `af6d1f5f2a827c84d8ab7e04b82fab99`
     - `Malakor_combat_music.mp3` — GUID `64944305ed273854687c0951beb9a69c`
     - `1_Combat_GargoyleKing_music.mp3` — GUID `aed4d214fdf825441891b02b8828e9fa`
     - `2_Combat_GargoyleKing_music.mp3` — GUID `0877d37538dd21c42b49ab1964519b58`
   - In all 7 `.meta` files, line 20 states `3D: 1`.

4. **Event Triggers**:
   - `GameManager.cs`, lines 11-24:
     ```csharp
     public enum GameLocation
     {
         Village,
         Courtyard,
         Library,
         CrownHall
     }
     ```
     `Forest` is absent from `GameLocation`. Line 96 defines `public static event Action<GameLocation> OnLocationChanged;`.
   - `DungeonRoomController.cs`, line 92:
     `public static event Action<DungeonRoomController> OnRoomCombatStarted;`
     Fires with `this` containing `bossIdentifier` ("CursedCommander", "ShadowMageMalakor", "GargoyleKing") and `roomLocation` ("Cellar", "Courtyard", etc.).
   - `GargoyleKingBoss.cs`, line 72:
     `public static event Action<GargoyleKingBoss> OnStoneFormActivated;`
   - `TurnManager.cs`, line 71:
     `public static event Action<bool> OnCombatEnded;`

5. **Door Transitions & Location Setting**:
   - `DoorTeleporter.cs`, lines 124-167:
     Moves player and handles CharacterController, but does NOT call `GameManager.Instance.SetLocation(loc)`.

6. **AudioManager BGM Logic**:
   - `AudioManager.cs`, lines 99-113 and 274-307:
     `Start()`, `HandleLocationChanged()`, and `HandlePlayModeChanged()` call `PlayBGM()` directly.

7. **Main Menu UI**:
   - `MainMenuController.cs`, lines 208-250:
     Constructs `MainMenuPanel` dynamically; currently lacks the Scottish Harp credit text.

---

## 2. Logic Chain

1. **AudioSource 2D Configuration**:
   - *From Observation 3*: Audio `.meta` files specify `3D: 1`.
   - *Reasoning*: If played on a 3D AudioSource, music volume drops off with camera/listener distance. Setting `audioSource.spatialBlend = 0f` makes the AudioSource treat clips as 2D stereo regardless of the importer setting, avoiding the need to edit or reimport `.meta` files.

2. **Manager Hierarchy & Automated Setup**:
   - *From Observation 1 & 2*: `BuildVillageEditor.EnsureManagersInScene()` manages components on `Managers`.
   - *Reasoning*: Adding `EnsureMusicManagerOnManagers()` directly into `BuildVillageEditor.EnsureManagersInScene()` automatically ensures `MusicManager` is attached and assigned all 7 audio clips via `AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/...")` whenever the village or game setup is run.

3. **Enum Compatibility**:
   - *From Observation 4*: `StartVillage.unity` serializes `currentLocation: 0`.
   - *Reasoning*: Assigning explicit integers (`Village = 0, Courtyard = 1, Library = 2, CrownHall = 3, Forest = 4`) preserves backward compatibility with serialized YAML values.

4. **Door Transition Exploration Music**:
   - *From Observation 5*: `DoorTeleporter` does not update `GameManager.Instance.CurrentLocation`.
   - *Reasoning*: Calling `GameManager.Instance.SetLocation(loc)` from `DoorTeleporter.PerformTeleport()` ensures that crossing portals into `Forest`, `Courtyard`, `Library`, or `CrownHall` triggers `GameManager.OnLocationChanged`, causing `MusicManager` to crossfade smoothly from `VillageSong.mp3` to `Castle_adventure_song.mp3`.

5. **Audio Clashing Prevention**:
   - *From Observation 6*: `AudioManager` plays BGM upon location or play mode change.
   - *Reasoning*: Having `AudioManager` check `if (MusicManager.Instance != null) return;` ensures that when `MusicManager` is running, `AudioManager` delegates all BGM control to `MusicManager`, eliminating overlapping audio while preserving SFX and procedural sounds.

---

## 3. Caveats

- In headless CLI mode where Unity is not running interactively, code changes will not trigger Unity's internal domain reload until opened in the Unity Editor. Scene YAML can be updated directly if required, or left to be updated by editor tools upon opening.
- No other caveats.

---

## 4. Conclusion

The codebase is well-structured and ready for `MusicManager` implementation:
1. `MusicManager.cs` should be implemented in `Assets/Scripts/Core/` with dual-channel AudioSources (`spatialBlend = 0f`), 1.2s volume lerp, and event handlers for `GameManager.OnLocationChanged`, `DungeonRoomController.OnRoomCombatStarted`, `GargoyleKingBoss.OnStoneFormActivated`, and `TurnManager.OnCombatEnded`.
2. `GameLocation` in `GameManager.cs` must be expanded with `Forest = 4`.
3. `AudioManager.cs` must yield BGM control when `MusicManager.Instance != null`.
4. `DoorTeleporter.cs` must call `GameManager.Instance.SetLocation(...)` in `PerformTeleport()`.
5. `MainMenuController.cs` must display the Scottish Harp Pixabay credit text in `EnsureUIHierarchy()`.
6. `BuildVillageEditor.cs` and `CastleOfDiceControlWindow.cs` must provide automated setup and verification for `MusicManager` and its 7 audio clips.

---

## 5. Verification Method

To independently verify findings:
1. **Inspect Audio Assets**:
   Confirm all 7 GUIDs match the `.meta` files in `Assets/Music/`.
2. **Inspect Scene Hierarchy**:
   Open `Assets/Scenes/StartVillage.unity` and verify `Managers` at line 8721 and its children.
3. **Inspect Tooling**:
   Open `Assets/Scripts/Editor/BuildVillageEditor.cs` at line 361 to verify `EnsureManagersInScene()`.
4. **Inspect Door Teleporter**:
   Open `Assets/Scripts/World/DoorTeleporter.cs` at line 124 to verify `PerformTeleport()` currently omits `GameManager.Instance.SetLocation()`.
5. **Compilation Verification**:
   Verify that all touched files maintain 0 errors across `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj`.
