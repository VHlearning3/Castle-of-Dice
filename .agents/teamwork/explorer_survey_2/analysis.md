# Codebase Survey & Audio Architecture Analysis Report

**Investigator**: explorer_survey_2  
**Date**: 2026-09-24  
**Target Project**: Castle of the D20 / Castle of Dice (`D:\Unity\3D DnD selainpeli`)  
**Scope**: Audio architecture, Game State & Event wiring (`GameManager`, `GameLocation`, `DungeonRoomController`, `GargoyleKingBoss`, `TurnManager`), UI requirements, and Editor tooling.

---

## 1. Executive Summary

A comprehensive read-only survey of the project codebase and asset database was conducted. The investigation confirmed:
1. **Audio Assets**: All 7 required MP3 music tracks are present in `Assets/Music/` with valid `.meta` files.
2. **`MusicManager` Status**: No dedicated `MusicManager.cs` currently exists. Background music (BGM) is minimally handled by `AudioManager.cs` using a single `AudioSource`, which lacks dual-channel crossfading and maps only 3 procedural/placeholder clips.
3. **`GameLocation` Enum**: Currently contains `Village`, `Courtyard`, `Library`, and `CrownHall`. The location `Forest` is **missing** and must be added.
4. **Event Wiring**:
   - `GameManager.OnLocationChanged` (`Action<GameLocation>`) is in place.
   - `DungeonRoomController.OnRoomCombatStarted` (`Action<DungeonRoomController>`) provides the controller instance containing `bossIdentifier` and `roomLocation`.
   - `GargoyleKingBoss.OnStoneFormActivated` (`Action<GargoyleKingBoss>`) is defined and fires when HP drops to $\le 50\%$.
   - `TurnManager.OnCombatEnded` (`Action<bool>`) fires on victory/defeat; fanfare sound effects play before exploration music is restored.
5. **Door Transitions**: `DoorTeleporter.cs` has a `DestinationZone` string property, but does not currently invoke `GameManager.Instance.SetLocation()`. Wiring this call ensures automatic music transitions when moving through physical gates.
6. **UI & Attribution**: The Scottish Harp Pixabay credit (`https://pixabay.com/music/scotland-harp-587446/`) must be embedded into `MainMenuController.cs`.

---

## 2. Track Mapping & Music Asset Inventory

All 7 music files were verified directly on disk in `Assets/Music/`:

| Track Filename | Size (Bytes) | GUID (from .meta) | Gameplay Context / Target Mapping |
| :--- | :--- | :--- | :--- |
| `VillageSong.mp3` | 2,177,567 | `8c695f2c8079fff438568e6ed65231a4` | Safe Haven: Oakhaven / Kivenkolo Village (`GameLocation.Village`) |
| `Castle_adventure_song.mp3` | 2,863,535 | `9fbf8d6dbe585eb41846c4f923b7ef84` | Exploration: `Forest`, `Courtyard`, `Library`, `CrownHall` |
| `Cellar_combat_music.mp3` | 2,885,423 | `2d54406a66699564c8d0eafe28646b97` | Tavern Cellar Encounter (`roomLocation == "Cellar"`) |
| `CursedCommander_Combat_music.mp3` | 2,582,063 | `64736f9872e4eb245be5d34208a0d48a` | Wing 1 Boss: Cursed Commander (`bossIdentifier == "CursedCommander"`) |
| `Malakor_combat_music.mp3` | 2,769,455 | `a276eb9eef11c9748b6f72cfae8bfb51` | Wing 2 Boss: Shadow Mage Malakor (`bossIdentifier == "ShadowMageMalakor"`) |
| `1_Combat_GargoyleKing_music.mp3` | 2,889,263 | `7bf5b50875e533e4b958c21bbddca4ad` | Wing 3 Final Boss: Gargoyle King Phase 1 (`bossIdentifier == "GargoyleKing"`) |
| `2_Combat_GargoyleKing_music.mp3` | 2,885,423 | `496ee8670bc23514a8072d733cb535c5` | Wing 3 Final Boss: Gargoyle King Phase 2 Stone Form (`OnStoneFormActivated`) |

---

## 3. Game State & Event Wiring Survey

### 3.1 `GameManager.cs` & `GameLocation` Enum
- **File**: `Assets/Scripts/Core/GameManager.cs`
- **Current Definition** (lines 11–24):
  ```csharp
  public enum GameLocation
  {
      Village,
      Courtyard,
      Library,
      CrownHall
  }
  ```
- **Finding**: `Forest` is not in the enum.
- **Required Update**:
  ```csharp
  public enum GameLocation
  {
      Village,
      Forest,
      Courtyard,
      Library,
      CrownHall
  }
  ```
- **Event Signature** (line 96):
  ```csharp
  public static event Action<GameLocation> OnLocationChanged;
  ```
- **Invocation** (lines 162–169):
  `SetLocation(GameLocation newLocation)` triggers `OnLocationChanged?.Invoke(currentLocation)`.
- **Existing Callers**:
  - `MainMenuController.cs`: line 99 (`SetLocation(GameLocation.Village)`)
  - `DefeatUIController.cs`: line 207 (`SetLocation(GameLocation.Village)`)
  - `DungeonRoomController.cs`: line 291 (`SetLocation(parsedLocation)`)
- **Integration with `DoorTeleporter.cs`**:
  `DoorTeleporter.cs` (line 58) has `public string DestinationZone { get; set; }`. In `BuildDungeonWingsEditor.cs`, doors are assigned destination zones:
  - `Door_Village_Forest`: `"Forest"` / `"Village"`
  - `Door_Forest_Courtyard`: `"Courtyard"` / `"Forest"`
  - `Door_Courtyard_Library`: `"Library"` / `"Courtyard"`
  - `Door_Courtyard_ThroneRoom`: `"CrownHall"` / `"Courtyard"`
  
  In `DoorTeleporter.PerformTeleport`, adding:
  ```csharp
  if (!string.IsNullOrEmpty(DestinationZone) && GameManager.Instance != null)
  {
      if (Enum.TryParse<GameLocation>(DestinationZone, true, out GameLocation targetLoc))
      {
          GameManager.Instance.SetLocation(targetLoc);
      }
  }
  ```
  ensures that whenever the player steps through any door or portal, `GameManager.SetLocation` is called, which broadcasts `OnLocationChanged` to `MusicManager`.

---

### 3.2 `DungeonRoomController.cs` & `OnRoomCombatStarted`
- **File**: `Assets/Scripts/World/DungeonRoomController.cs`
- **Event Definition** (line 92):
  ```csharp
  public static event Action<DungeonRoomController> OnRoomCombatStarted;
  ```
- **Invocation** (line 312):
  Fired at the end of `public void TriggerEncounter()`:
  ```csharp
  OnRoomCombatStarted?.Invoke(this);
  ```
- **Inspector Fields on `DungeonRoomController`**:
  - `public string roomLocation = "Village";`
  - `public string bossIdentifier = "";`
- **Configured Values in Code & Scenes**:
  - Tavern Cellar: `roomLocation = "Cellar"`, `bossIdentifier = ""` (`BuildCellarEditor.cs:602-603`)
  - Wing 1: `roomLocation = "Courtyard"`, `bossIdentifier = "CursedCommander"` (`BuildDungeonWingsEditor.cs:267-268`)
  - Wing 2: `roomLocation = "Library"`, `bossIdentifier = "ShadowMageMalakor"` (`BuildDungeonWingsEditor.cs:337-338`)
  - Wing 3: `roomLocation = "CrownHall"`, `bossIdentifier = "GargoyleKing"` (`BuildDungeonWingsEditor.cs:411-412`)
- **`MusicManager` Hooking**:
  `MusicManager` subscribes to `DungeonRoomController.OnRoomCombatStarted += HandleRoomCombatStarted;`.
  The handler inspects `room.bossIdentifier` and `room.roomLocation` to crossfade to the appropriate battle theme:
  ```csharp
  if (string.Equals(room.bossIdentifier, "CursedCommander", StringComparison.OrdinalIgnoreCase))
      CrossfadeTo(cursedCommanderCombatMusic, 1.2f);
  else if (string.Equals(room.bossIdentifier, "ShadowMageMalakor", StringComparison.OrdinalIgnoreCase))
      CrossfadeTo(malakorCombatMusic, 1.2f);
  else if (string.Equals(room.bossIdentifier, "GargoyleKing", StringComparison.OrdinalIgnoreCase))
      CrossfadeTo(gargoyleKingPhase1Music, 1.2f);
  else if (string.Equals(room.roomLocation, "Cellar", StringComparison.OrdinalIgnoreCase))
      CrossfadeTo(cellarCombatMusic, 1.2f);
  ```

---

### 3.3 `GargoyleKingBoss.cs` & `OnStoneFormActivated`
- **File**: `Assets/Scripts/Bosses/GargoyleKingBoss.cs`
- **Event Definition** (line 72):
  ```csharp
  public static event Action<GargoyleKingBoss> OnStoneFormActivated;
  ```
- **Threshold Logic & Invocation** (lines 124–128, 176–186):
  ```csharp
  // TakeDamage override:
  if (IsAlive && !hasEnteredPhase2 && currentHP <= (maxHP / 2))
  {
      EnterStoneForm();
  }

  // EnterStoneForm method:
  public void EnterStoneForm()
  {
      hasEnteredPhase2 = true;
      isStoneFormActive = true;
      armorClass += 3;
      StatusEffects?.ApplyEffect(StatusEffectType.ManaShield, durationTurns: 2);
      OnStoneFormActivated?.Invoke(this);
  }
  ```
- **`MusicManager` Hooking**:
  `MusicManager` subscribes to `GargoyleKingBoss.OnStoneFormActivated += HandleStoneFormActivated;`.
  When received, it immediately crossfades to `2_Combat_GargoyleKing_music.mp3` over 1.2s:
  ```csharp
  private void HandleStoneFormActivated(GargoyleKingBoss boss)
  {
      CrossfadeTo(gargoyleKingPhase2Music, 1.2f);
  }
  ```

---

### 3.4 `TurnManager.cs` & `OnCombatEnded`
- **File**: `Assets/Scripts/Combat/TurnManager.cs`
- **Event Definition** (line 71):
  ```csharp
  public static event Action<bool> OnCombatEnded;
  ```
- **Invocation Points**:
  - `EndCombat(bool isVictory)` (line 169)
  - `CheckCombatEndConditions()`:
    - Player party wiped: line 393 (`OnCombatEnded?.Invoke(false)`)
    - Enemies wiped: line 404 (`OnCombatEnded?.Invoke(true)`)
- **Fanfare Timing & Exploration Music Restoration**:
  - In `AudioManager.cs`, victory or defeat SFX is played on `OnCombatEnded`. The procedural fanfare length is ~0.8s, and recorded fanfares run ~1.0–1.5s.
  - `MusicManager` subscribes to `TurnManager.OnCombatEnded += HandleCombatEnded;`.
  - When combat concludes, `MusicManager` starts a coroutine:
    1. Wait 1.5 seconds for the result fanfare SFX to finish.
    2. Check `GameManager.Instance.CurrentLocation`.
    3. Crossfade to exploration music:
       - `GameLocation.Village` -> `VillageSong.mp3`
       - `GameLocation.Forest`, `Courtyard`, `Library`, `CrownHall` -> `Castle_adventure_song.mp3`

---

## 4. Audio Architecture: `AudioManager.cs` vs `MusicManager.cs`

### 4.1 Existing `AudioManager.cs` Analysis
`AudioManager.cs` is responsible for:
- Singleton lifecycle (`Instance`, `DontDestroyOnLoad`).
- SFX audio synthesis (zero-dependency WebGL fallback for clicks, dice rolls, swords, spells, hit, potion, fanfare).
- Playing SFX via `sfxSource.PlayOneShot(...)`.
- A single `bgmSource` with hardcoded clips (`villageBgmClip`, `dungeonBgmClip`, `combatBgmClip`).
- Listening to `OnLocationChanged`, `OnPlayModeChanged`, `OnCombatEnded`.

### 4.2 Potential Conflicts & Separation of Concerns
If `MusicManager` is added as an independent component with dual AudioSources while `AudioManager` continues to play BGM on `bgmSource`:
1. Two different AudioSources would play music simultaneously (e.g. `AudioManager` playing procedural harp or unassigned BGM, while `MusicManager` plays `VillageSong.mp3`).
2. **Architectural Clean Separation**:
   - **`AudioManager`**: Sole owner of Sound Effects (SFX) and master audio settings.
   - **`MusicManager`**: Sole owner of Background Music (BGM), playlist, and dual-channel crossfading.
   - In `AudioManager.cs`: Disable/remove the automatic `bgmSource.Play()` calls in `Start()`, `HandleLocationChanged()`, and `HandlePlayModeChanged()`, OR route `AudioManager.PlayBGM` to `MusicManager.Instance.PlayMusic()`.

---

## 5. `MusicManager.cs` Design Specification

### 5.1 Architecture & Requirements
- **Namespace**: `CastleOfTheD20.Core`
- **Location**: `Assets/Scripts/Core/MusicManager.cs`
- **Persistence**: `DontDestroyOnLoad`, singleton pattern with `Instance`.
- **Pure Native Unity**: Zero third-party packages, fully compatible with WebGL audio constraints.
- **Dual-Channel AudioSource Ping-Pong**:
  - `AudioSource musicSourceA`
  - `AudioSource musicSourceB`
  - At any time, one is `activeSource` (playing at target volume) and the other is `standbySource` (volume 0, ready to receive incoming track).
  - During a 1.2s crossfade:
    - Active source interpolates from current volume down to 0.
    - Standby source starts at 0, starts `Play()`, and interpolates up to `targetMusicVolume`.
    - At the end of 1.2s, the outgoing source is stopped, and references swap.
  - Smooth interpolation avoids clicks and pops using linear or smoothstep interpolation:
    `float t = Mathf.Clamp01(elapsed / duration);`
    `sourceOut.volume = Mathf.Lerp(startOut, 0f, t);`
    `sourceIn.volume = Mathf.Lerp(startIn, targetVol, t);`
  - Uses `Time.unscaledDeltaTime` so crossfading continues uninterrupted during pause menus or timescale shifts.
- **Serialized Track Slots**:
  1. `[SerializeField] private AudioClip villageMusic;`
  2. `[SerializeField] private AudioClip castleExplorationMusic;`
  3. `[SerializeField] private AudioClip cellarCombatMusic;`
  4. `[SerializeField] private AudioClip cursedCommanderCombatMusic;`
  5. `[SerializeField] private AudioClip malakorCombatMusic;`
  6. `[SerializeField] private AudioClip gargoyleKingPhase1Music;`
  7. `[SerializeField] private AudioClip gargoyleKingPhase2Music;`

---

## 6. Notebook & UI Requirements (R3)

1. **Scottish Harp Song Credit**:
   - Source: `https://pixabay.com/music/scotland-harp-587446/`
   - File: `Assets/Scripts/UI/MainMenuController.cs`
   - Implementation: In `EnsureUIHierarchy()`, create a footer attribution text at the bottom of the main menu card:
     `Musiikki: Pixabay - Scottish Harp (pixabay.com/music/scotland-harp-587446/)`
2. **Component Integrity Checks**:
   - `AbilityTooltipUI.cs`: Implements dynamic hover/pointer cards with full damage formula breakdown. No compilation or reference errors.
   - `DefeatUIController.cs`: Correctly listens to `TurnManager.OnTurnStateChanged`, provides retry and return-to-village flows.
   - `MainMenuController.cs`: Correctly instantiates class selection modal and rules guide.

---

## 7. Automated Setup & Editor Tooling (R4)

To satisfy R4 (automated instantiation on `Managers` in `StartVillage.unity` and auto-assigning all 7 clips without manual inspector dragging):

1. **`BuildVillageEditor.cs`** (`EnsureManagersInScene`):
   ```csharp
   MusicManager musicMgr = managers.GetComponent<MusicManager>();
   if (musicMgr == null && Object.FindAnyObjectByType<MusicManager>() == null)
   {
       musicMgr = managers.AddComponent<MusicManager>();
   }
   if (musicMgr != null)
   {
       musicMgr.AssignTracks(
           AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/VillageSong.mp3"),
           AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/Castle_adventure_song.mp3"),
           AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/Cellar_combat_music.mp3"),
           AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/CursedCommander_Combat_music.mp3"),
           AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/Malakor_combat_music.mp3"),
           AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/1_Combat_GargoyleKing_music.mp3"),
           AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/2_Combat_GargoyleKing_music.mp3")
       );
       EditorUtility.SetDirty(musicMgr);
   }
   ```

2. **`CastleOfDiceControlWindow.cs`**:
   Add detection status and a direct button to setup/re-link `MusicManager` and all 7 tracks:
   ```csharp
   bool hasMusicMgr = GameObject.Find("Managers")?.GetComponent<MusicManager>() != null;
   EditorGUILayout.LabelField("MusicManager (Dual-Channel):", hasMusicMgr ? "✓ Configured" : "✗ Missing");
   if (GUILayout.Button("Setup MusicManager & Assign 7 Music Tracks", GUILayout.Height(26)))
   {
       // Execute assignment
   }
   ```

---

## 8. Summary of Proposed Changes by File

| File Path | Nature of Change | Purpose |
| :--- | :--- | :--- |
| `Assets/Scripts/Core/GameManager.cs` | Modify `GameLocation` enum | Add `Forest` location |
| `Assets/Scripts/Core/MusicManager.cs` | **New File** | Complete persistent dual-channel crossfading audio manager |
| `Assets/Scripts/Core/AudioManager.cs` | Modify | Remove/delegate BGM logic so `MusicManager` has sole BGM ownership |
| `Assets/Scripts/World/DoorTeleporter.cs` | Modify `PerformTeleport` | Call `GameManager.Instance.SetLocation` based on `DestinationZone` |
| `Assets/Scripts/UI/MainMenuController.cs` | Modify `EnsureUIHierarchy` | Add Scottish Harp credit text and URL |
| `Assets/Scripts/Editor/BuildVillageEditor.cs` | Modify `EnsureManagersInScene` | Auto-instantiate `MusicManager` & auto-assign 7 MP3s |
| `Assets/Scripts/Editor/CastleOfDiceControlWindow.cs` | Modify `OnGUI` | Add MusicManager status and one-click setup button |
