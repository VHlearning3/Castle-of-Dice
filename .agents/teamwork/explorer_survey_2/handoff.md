# Handoff Report — explorer_survey_2: Audio Architecture & Game State Survey

## 1. Observation

Direct observations from inspection of the codebase at `D:\Unity\3D DnD selainpeli`:

1. **Audio Clips Inventory**:
   - `Assets/Music/VillageSong.mp3` (2,177,567 bytes, GUID `8c695f2c8079fff438568e6ed65231a4`)
   - `Assets/Music/Castle_adventure_song.mp3` (2,863,535 bytes, GUID `9fbf8d6dbe585eb41846c4f923b7ef84`)
   - `Assets/Music/Cellar_combat_music.mp3` (2,885,423 bytes, GUID `2d54406a66699564c8d0eafe28646b97`)
   - `Assets/Music/CursedCommander_Combat_music.mp3` (2,582,063 bytes, GUID `64736f9872e4eb245be5d34208a0d48a`)
   - `Assets/Music/Malakor_combat_music.mp3` (2,769,455 bytes, GUID `a276eb9eef11c9748b6f72cfae8bfb51`)
   - `Assets/Music/1_Combat_GargoyleKing_music.mp3` (2,889,263 bytes, GUID `7bf5b50875e533e4b958c21bbddca4ad`)
   - `Assets/Music/2_Combat_GargoyleKing_music.mp3` (2,885,423 bytes, GUID `496ee8670bc23514a8072d733cb535c5`)
   All 7 MP3s and their matching `.meta` files are present.

2. **Absence of `MusicManager.cs`**:
   A codebase grep for `MusicManager` returned 0 results. No dedicated `MusicManager` script currently exists.

3. **Current Audio Manager (`Assets/Scripts/Core/AudioManager.cs`)**:
   - Lines 41–42: `[SerializeField] private AudioSource bgmSource; [SerializeField] private AudioSource sfxSource;`
   - Lines 100–112: In `Start()`, plays `villageBgmClip` or falls back to procedural harp synth `GenerateHarpBgmClip()`.
   - Lines 153–167: Subscribes to `DiceSystem.OnDiceRolled`, `GameManager.OnLocationChanged`, `GameManager.OnPlayModeChanged`, `TurnManager.OnCombatEnded`.
   - Lines 274–287: `HandleLocationChanged` switches on `GameLocation.Village`, `Courtyard`, `Library`, `CrownHall`.

4. **`GameManager.cs` & `GameLocation` Enum (`Assets/Scripts/Core/GameManager.cs`)**:
   - Lines 11–24:
     ```csharp
     public enum GameLocation
     {
         Village,
         Courtyard,
         Library,
         CrownHall
     }
     ```
     `Forest` is absent from `GameLocation`.
   - Line 96: `public static event Action<GameLocation> OnLocationChanged;`
   - Lines 162–169: `SetLocation(GameLocation newLocation)` triggers `OnLocationChanged?.Invoke(currentLocation)`.

5. **`DungeonRoomController.cs` (`Assets/Scripts/World/DungeonRoomController.cs`)**:
   - Line 37: `public string roomLocation = "Village";`
   - Line 40: `public string bossIdentifier = "";`
   - Line 92: `public static event Action<DungeonRoomController> OnRoomCombatStarted;`
   - Line 312: `OnRoomCombatStarted?.Invoke(this);` inside `TriggerEncounter()`.
   - In `Assets/Scripts/Editor/BuildDungeonWingsEditor.cs`:
     - Line 268: `room.bossIdentifier = "CursedCommander";` (`room.roomLocation = "Courtyard"`)
     - Line 338: `room.bossIdentifier = "ShadowMageMalakor";` (`room.roomLocation = "Library"`)
     - Line 412: `room.bossIdentifier = "GargoyleKing";` (`room.roomLocation = "CrownHall"`)
   - In `Assets/Scripts/Editor/BuildCellarEditor.cs`:
     - Line 602–603: `roomController.roomLocation = "Cellar"; roomController.bossIdentifier = "";`

6. **`GargoyleKingBoss.cs` (`Assets/Scripts/Bosses/GargoyleKingBoss.cs`)**:
   - Line 72: `public static event Action<GargoyleKingBoss> OnStoneFormActivated;`
   - Lines 124–127: In `TakeDamage()`:
     ```csharp
     if (IsAlive && !hasEnteredPhase2 && currentHP <= (maxHP / 2))
     {
         EnterStoneForm();
     }
     ```
   - Line 185: `OnStoneFormActivated?.Invoke(this);` inside `EnterStoneForm()`.

7. **`TurnManager.cs` (`Assets/Scripts/Combat/TurnManager.cs`)**:
   - Line 71: `public static event Action<bool> OnCombatEnded;`
   - Invocation points: Line 169 in `EndCombat()`, Line 393 on defeat, Line 404 on victory.

8. **`DoorTeleporter.cs` (`Assets/Scripts/World/DoorTeleporter.cs`)**:
   - Line 58: `public string DestinationZone { get; set; } = string.Empty;`
   - Lines 123–173: `PerformTeleport()` moves the player and syncs physics, but does not call `GameManager.Instance.SetLocation()`.

9. **Scene & Editor Setup (`Assets/Scenes/StartVillage.unity`, `BuildVillageEditor.cs`, `CastleOfDiceControlWindow.cs`)**:
   - In `StartVillage.unity` (line 8731): GameObject `Managers` has `Transform` and `AudioManager`.
   - In `BuildVillageEditor.cs` (lines 361–373): `EnsureManagersInScene()` checks for and adds `AudioManager` and `DialogueActionTrigger` on `Managers`.
   - In `CastleOfDiceControlWindow.cs`: Monitors scene planes (`CourtYard`, `Forest`, `Library`, `ThroneRoom`) and triggers setup builders.

10. **Attribution Requirement**:
    - `MainMenuController.cs` has title and class selection, but does not currently contain the Scottish Harp Pixabay credit (`https://pixabay.com/music/scotland-harp-587446/`).

---

## 2. Logic Chain

1. **Need for `MusicManager.cs`**:
   - Observation 2 shows `MusicManager.cs` does not exist. Observation 1 confirms all 7 target MP3 tracks exist. Observation 3 shows `AudioManager` has only a single BGM source without interpolation.
   - Therefore, a dedicated `MusicManager.cs` must be created in `Assets/Scripts/Core/` with dual `AudioSource` ping-pong interpolation (1.2s duration) to fulfill R1 without audio pops or clicks.

2. **Adding `Forest` to `GameLocation`**:
   - Observation 4 shows `GameLocation` only has `Village`, `Courtyard`, `Library`, `CrownHall`.
   - In `BuildDungeonWingsEditor.cs`, doors between Village and Courtyard explicitly route through `"Forest"`, and R2 mandates listening to `Forest` via `OnLocationChanged`.
   - Therefore, `Forest` must be added to `GameLocation` enum in `GameManager.cs`.

3. **Door Transition Integration**:
   - Observation 8 shows `DoorTeleporter.PerformTeleport` has `DestinationZone` strings (`"Forest"`, `"Courtyard"`, etc.) but never updates `GameManager.Instance.SetLocation()`.
   - Without calling `SetLocation()`, walking through gates would not trigger `OnLocationChanged`, leaving music unchanged during world exploration.
   - Therefore, adding `Enum.TryParse<GameLocation>(DestinationZone, ...)` in `DoorTeleporter.PerformTeleport` ensures state updates fire automatically.

4. **Combat Music Dispatching**:
   - Observation 5 confirms `OnRoomCombatStarted` passes `DungeonRoomController`, which exposes `bossIdentifier` and `roomLocation`.
   - Therefore, `MusicManager` can map:
     - `bossIdentifier == "CursedCommander"` -> `CursedCommander_Combat_music.mp3`
     - `bossIdentifier == "ShadowMageMalakor"` -> `Malakor_combat_music.mp3`
     - `bossIdentifier == "GargoyleKing"` -> `1_Combat_GargoyleKing_music.mp3`
     - `roomLocation == "Cellar"` -> `Cellar_combat_music.mp3`

5. **Phase 2 Music Transition**:
   - Observation 6 confirms `GargoyleKingBoss.OnStoneFormActivated` is triggered immediately when the boss's HP drops to $\le 50\%$.
   - Therefore, `MusicManager` subscribing to `GargoyleKingBoss.OnStoneFormActivated` will immediately trigger the 1.2s crossfade to `2_Combat_GargoyleKing_music.mp3`.

6. **Combat End Restoration**:
   - Observation 7 confirms `TurnManager.OnCombatEnded(bool isVictory)` fires when combat resolves. Observation 3 confirms fanfare SFX plays upon combat end (~0.8–1.5s).
   - Therefore, waiting 1.5s via a coroutine in `MusicManager` allows the victory/defeat fanfare to be heard clearly before smoothly crossfading back to the exploration music of `GameManager.Instance.CurrentLocation`.

7. **Preventing BGM Conflicts**:
   - Observation 3 shows `AudioManager` plays BGM on `bgmSource` in `Start()`. If `MusicManager` is active simultaneously, two music tracks would play at once.
   - Therefore, `AudioManager` must either delegate BGM playback to `MusicManager` or cease auto-starting BGM, reserving `AudioManager` exclusively for SFX.

8. **Automated Setup**:
   - Observation 9 shows `BuildVillageEditor.EnsureManagersInScene()` manages components on `Managers`.
   - Adding `MusicManager` instantiation and `AssetDatabase.LoadAssetAtPath<AudioClip>()` assignment of all 7 tracks into `EnsureManagersInScene()` and `CastleOfDiceControlWindow.cs` satisfies R4 cleanly.

---

## 3. Caveats

1. **Unity GUI / Inspector Serialized Data**:
   Because `GameLocation` is an enum, adding `Forest` between `Village` and `Courtyard` shifts integer values of subsequent enum members (`Courtyard` becomes 2 instead of 1, etc.). No serialized assets were found storing integer values of `GameLocation` (code uses string parsing via `Enum.TryParse`), but adding `Forest` at the end or cleanly handling serialization should be kept in mind during implementation.
2. **Defeat Screen Retry Flow**:
   When the player clicks "Retry" on the Defeat screen, `DefeatUIController` calls `TurnManager.Instance.StartCombatEncounter(player, room.roomLocation, room.bossIdentifier)`. `MusicManager` should provide a public `PlayCombatMusicForBoss(string bossId, string location)` or remember the active combat track so retrying directly from defeat screen re-engages combat music properly.
3. **Command Execution**:
   Terminal execution of `dotnet build` timed out on interactive permissions during survey; all analysis was performed via direct source file inspection and static code analysis.

---

## 4. Conclusion

The architecture of the game is cleanly modular and well-suited for the addition of `MusicManager.cs`. All 7 MP3 audio assets exist and are valid. The exact events required by R2 (`GameManager.OnLocationChanged`, `DungeonRoomController.OnRoomCombatStarted`, `GargoyleKingBoss.OnStoneFormActivated`, and `TurnManager.OnCombatEnded`) already exist with compatible signatures and trigger points.

The required implementation steps are:
1. Add `Forest` to `GameLocation` enum in `Assets/Scripts/Core/GameManager.cs`.
2. Add `GameManager.Instance.SetLocation()` call inside `DoorTeleporter.PerformTeleport()`.
3. Create `Assets/Scripts/Core/MusicManager.cs` featuring dual-channel `AudioSource` crossfading (1.2s), track mapping for all 7 MP3s, and event subscriptions.
4. Silence/delegate BGM playback in `Assets/Scripts/Core/AudioManager.cs` so it focuses strictly on SFX.
5. Embed the Scottish Harp credit text and URL in `Assets/Scripts/UI/MainMenuController.cs`.
6. Add automated `MusicManager` instantiation and clip loading in `BuildVillageEditor.cs` and `CastleOfDiceControlWindow.cs`.

---

## 5. Verification Method

To independently verify these findings:
1. **Inspect Target Event Definitions**:
   - `GameLocation` enum & `OnLocationChanged`: `Assets/Scripts/Core/GameManager.cs` (lines 11–24, 96, 162–169).
   - `OnRoomCombatStarted`: `Assets/Scripts/World/DungeonRoomController.cs` (lines 92, 312).
   - `OnStoneFormActivated`: `Assets/Scripts/Bosses/GargoyleKingBoss.cs` (lines 72, 124–128, 185).
   - `OnCombatEnded`: `Assets/Scripts/Combat/TurnManager.cs` (lines 71, 169, 393, 404).
   - `DestinationZone`: `Assets/Scripts/World/DoorTeleporter.cs` (line 58).
2. **Inspect Audio Assets**:
   - Verify presence of 7 `.mp3` and `.meta` files in `Assets/Music/`.
3. **Post-Implementation Verification**:
   - Open Unity Editor, open `StartVillage.unity`, run `CastleOfDice/Open Control Panel` or `CastleOfDice/Run Complete Game Setup & Build All`.
   - Verify `Managers` GameObject has `MusicManager` with all 7 clips populated.
   - Enter Play Mode:
     - Village BGM (`VillageSong.mp3`) plays.
     - Teleport to Forest / Courtyard -> smooth 1.2s crossfade to `Castle_adventure_song.mp3`.
     - Enter Cellar encounter -> crossfade to `Cellar_combat_music.mp3`.
     - Enter Cursed Commander / Malakor / Gargoyle King -> respective boss combat music plays.
     - Damage Gargoyle King below 50% HP -> crossfade to `2_Combat_GargoyleKing_music.mp3`.
     - End combat -> fanfare plays, followed 1.5s later by exploration music restoration.
     - Check Main Menu for Scottish Harp attribution text.
