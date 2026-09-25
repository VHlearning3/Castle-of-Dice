# Technical Analysis & Survey Report: Editor Tooling, Scene Architecture, Audio Integrity & Runtime Safety

**Agent**: Explorer Survey 3  
**Working Directory**: `D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_survey_3`  
**Date**: 2026-09-24  
**Project**: Castle of the D20 / Castle of Dice (`D:\Unity\3D DnD selainpeli`)

---

## 1. Executive Summary

This report provides an in-depth survey of the Unity project to inform the implementation of a persistent, WebGL-compatible `MusicManager` with dual AudioSource crossfading across 7 audio tracks, automated editor tooling, event wiring, and zero-error runtime safety.

### Core Discoveries & Recommendations:
1. **Asset Integrity**: All 7 MP3 tracks exist in `Assets/Music/` with valid `.meta` files. The audio importer sets `3D: 1` by default; setting `spatialBlend = 0f` (2D stereo) in `MusicManager` avoids distance falloff or panning anomalies without altering `.meta` files.
2. **Scene & Manager Hierarchy**: In `Assets/Scenes/StartVillage.unity`, the `Managers` GameObject (`fileID 556439108`) hosts `AudioManager` directly and parents 9 distinct child manager GameObjects (`GameManager`, `TurnManager`, `AbilityExecutor`, `InventoryManager`, `ShopManager`, `QuestManager`, `DialogueController`, `DungeonRoomController`, `ShopUIController`). `MusicManager` should be attached directly to `Managers` (matching `AudioManager`) or added as a child under `Managers`.
3. **Automated Tooling Integration**: `BuildVillageEditor.cs` already contains `EnsureManagersInScene()`, which is triggered by `AutoSetupGameEditor.RunCompleteGameSetup()`. Adding `EnsureMusicManagerOnManagers(managers)` into this pipeline will automatically instantiate `MusicManager` and assign all 7 clips via `AssetDatabase.LoadAssetAtPath<AudioClip>()`. `CastleOfDiceControlWindow.cs` can display live status and provide a one-click setup/verify button.
4. **GameLocation Enum Extension**: `GameLocation` in `GameManager.cs` currently has 4 values (`Village`, `Courtyard`, `Library`, `CrownHall`). Adding `Forest = 4` with explicit integer assignments prevents breaking YAML serialization (`currentLocation: 0` in `StartVillage.unity`).
5. **Door Transition Gap**: In `DoorTeleporter.cs`, `PerformTeleport()` currently repositions the player but does *not* notify `GameManager.Instance.SetLocation(loc)`. As a result, exploration music would not crossfade when entering `Forest`, `Courtyard`, `Library`, or `CrownHall` until a combat trigger is hit. Updating `DoorTeleporter.PerformTeleport()` to parse `DestinationZone` and call `SetLocation()` fixes this exploration flow seamlessly.
6. **AudioManager Coexistence**: `AudioManager.cs` currently listens to `OnLocationChanged` and `OnPlayModeChanged` to start BGM. To eliminate audio clashing, `AudioManager` should yield BGM handling when `MusicManager.Instance != null`, delegating music to `MusicManager` while retaining SFX and procedural sound generation.

---

## 2. Audio Asset & GUID Integrity Map

All 7 MP3 tracks in `Assets/Music/` were inspected along with their YAML `.meta` files.

| Audio Track Filename | GUID | File Size | Importer Settings | Gameplay Mapping |
|---|---|---|---|---|
| `VillageSong.mp3` | `8c695f2c8079fff438568e6ed65231a4` | 3.5 MB | 44.1kHz, MP3, 3D: 1 | Oakhaven / Kivenkolo Village (`GameLocation.Village`) |
| `Castle_adventure_song.mp3` | `89bd107ca50cf9f49811cf24b093e6ed` | 2.8 MB | 44.1kHz, MP3, 3D: 1 | Exploration in Forest, Courtyard, Library, CrownHall |
| `Cellar_combat_music.mp3` | `10578e684e333d044802dee8f2f0446b` | 3.1 MB | 44.1kHz, MP3, 3D: 1 | Tavern Cellar combat (`roomLocation == "Cellar"`) |
| `CursedCommander_Combat_music.mp3` | `af6d1f5f2a827c84d8ab7e04b82fab99` | 4.2 MB | 44.1kHz, MP3, 3D: 1 | Wing 1 Boss combat (`bossIdentifier == "CursedCommander"`) |
| `Malakor_combat_music.mp3` | `64944305ed273854687c0951beb9a69c` | 3.7 MB | 44.1kHz, MP3, 3D: 1 | Wing 2 Boss combat (`bossIdentifier == "ShadowMageMalakor"`) |
| `1_Combat_GargoyleKing_music.mp3` | `aed4d214fdf825441891b02b8828e9fa` | 4.0 MB | 44.1kHz, MP3, 3D: 1 | Wing 3 Final Boss Phase 1 (`bossIdentifier == "GargoyleKing"`) |
| `2_Combat_GargoyleKing_music.mp3` | `0877d37538dd21c42b49ab1964519b58` | 3.9 MB | 44.1kHz, MP3, 3D: 1 | Wing 3 Final Boss Phase 2 Stone Form (`OnStoneFormActivated`) |

### Audio Import & Playback Recommendation:
Because the files have `3D: 1` in their `.meta` files, any `AudioSource` playing them would attenuate volume if the listener moves away unless configured as 2D. Therefore, `MusicManager`'s dual `AudioSource` channels must explicitly set:
```csharp
audioSourceA.spatialBlend = 0f; // 2D Stereo
audioSourceB.spatialBlend = 0f; // 2D Stereo
```
This guarantees crisp, uniform stereo reproduction across all platforms (WebGL, Windows Standalone, Editor) without needing to rewrite any `.meta` files.

---

## 3. Scene & Manager Hierarchy (`StartVillage.unity`)

### 3.1 Hierarchy Structure
In `Assets/Scenes/StartVillage.unity`, the scene contains:
- `Managers` (GameObject `&556439108`, Transform `&556439109`)
  - Component 1: `Transform`
  - Component 2: `AudioManager` (`&556439110`, Script GUID: `e09d634472d870d44ab55ac0738d5ed8`)
  - Child Transforms:
    1. `TurnManager` (`&1378057752`, GameObject `&1378057750`)
    2. `GameManager` (`&2064034050`, GameObject `&2064034049`)
    3. `AbilityExecutor` (`&677255356`, GameObject `&677255355`)
    4. `InventoryManager` (`&2061720238`, GameObject `&2061720237`)
    5. `ShopManager` (`&479882307`, GameObject `&479882306`)
    6. `QuestManager` (`&1777815439`, GameObject `&1777815438`)
    7. `DialogueController` (`&368632553`, GameObject `&368632552`)
    8. `DungeonRoomController` (`&717851335`, GameObject `&717851334`)
    9. `ShopUIController` (`&1276350411`, GameObject `&1276350410`)

### 3.2 Persistence Mechanism
In `AudioManager.cs` lines 89-90:
```csharp
transform.SetParent(null);
DontDestroyOnLoad(gameObject);
```
Since `AudioManager` is attached directly to the root `Managers` GameObject, executing `transform.SetParent(null)` and `DontDestroyOnLoad(gameObject)` marks the entire `Managers` root GameObject and all 9 child managers as persistent across scenes.
Placing `MusicManager` directly on `Managers` inherits this lifecycle naturally.

---

## 4. Editor Tooling Survey (`BuildVillageEditor`, `CastleOfDiceControlWindow`, `AutoSetupGameEditor`)

### 4.1 `BuildVillageEditor.cs`
- Entry Point: `[MenuItem("CastleOfDice/Build Complete Village Layout & NPCs", false, 10)]` -> `BuildCompleteVillage()`
- Method `EnsureManagersInScene()` (Lines 361-392):
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
- **Extension Strategy**:
  Add an automated helper method:
  ```csharp
  public static MusicManager EnsureMusicManagerOnManagers(GameObject managers = null)
  {
      if (managers == null) managers = GameObject.Find("Managers");
      if (managers == null)
      {
          managers = new GameObject("Managers");
          Undo.RegisterCreatedObjectUndo(managers, "Create Managers");
      }

      MusicManager mm = managers.GetComponent<MusicManager>();
      if (mm == null)
      {
          mm = Object.FindAnyObjectByType<MusicManager>();
          if (mm == null)
          {
              mm = Undo.AddComponent<MusicManager>(managers);
          }
      }

      // Auto-assign all 7 AudioClips from Assets/Music/
      mm.AssignClips(
          AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/VillageSong.mp3"),
          AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/Castle_adventure_song.mp3"),
          AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/Cellar_combat_music.mp3"),
          AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/CursedCommander_Combat_music.mp3"),
          AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/Malakor_combat_music.mp3"),
          AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/1_Combat_GargoyleKing_music.mp3"),
          AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/2_Combat_GargoyleKing_music.mp3")
      );

      EditorUtility.SetDirty(mm);
      return mm;
  }
  ```
  Call `EnsureMusicManagerOnManagers(managers)` from `EnsureManagersInScene()`.

### 4.2 `CastleOfDiceControlWindow.cs`
- `CastleOfDiceControlWindow` provides status displays and build buttons.
- Under `Planes & Room Status:` and `Individual Steps:`, add:
  1. A check:
     ```csharp
     MusicManager mm = Object.FindAnyObjectByType<MusicManager>();
     bool hasMusicMgr = mm != null && mm.HasAllClipsAssigned;
     EditorGUILayout.LabelField("MusicManager (7 Tracks):", hasMusicMgr ? "✓ Ready" : "✗ Incomplete/Missing");
     ```
  2. An action button:
     ```csharp
     if (GUILayout.Button("Setup & Assign MusicManager (7 Tracks)", GUILayout.Height(28)))
     {
         BuildVillageEditor.EnsureMusicManagerOnManagers();
     }
     ```

### 4.3 `AutoSetupGameEditor.cs`
- `AutoSetupGameEditor.RunCompleteGameSetup()` invokes `BuildVillageEditor.BuildCompleteVillage()`.
- Updating `EnsureManagersInScene()` automatically integrates into `AutoSetupGameEditor` without requiring duplicate setup logic.

---

## 5. Event Wiring & Dynamic Audio Transitions

### 5.1 Location Transitions (`GameManager.OnLocationChanged`)
- Current Enum:
  ```csharp
  public enum GameLocation
  {
      Village = 0,
      Courtyard = 1,
      Library = 2,
      CrownHall = 3,
      Forest = 4
  }
  ```
- Mapping to Exploration Tracks:
  - `GameLocation.Village` -> `VillageSong.mp3`
  - `GameLocation.Forest`, `Courtyard`, `Library`, `CrownHall` -> `Castle_adventure_song.mp3`
- Crossfade rule: If the target track is already playing on the active AudioSource, do not retrigger or restart it.

### 5.2 Boss Combat Transitions (`DungeonRoomController.OnRoomCombatStarted`)
In `DungeonRoomController.cs` lines 260-415 and `BuildDungeonWingsEditor.cs`:
- Wing 1: `room.roomLocation = "Courtyard"`, `room.bossIdentifier = "CursedCommander"`
- Wing 2: `room.roomLocation = "Library"`, `room.bossIdentifier = "ShadowMageMalakor"`
- Wing 3: `room.roomLocation = "CrownHall"`, `room.bossIdentifier = "GargoyleKing"`
- Cellar: `room.roomLocation = "Cellar"`, `room.bossIdentifier = ""`
Mapping:
```csharp
private void HandleRoomCombatStarted(DungeonRoomController room)
{
    if (room == null) return;
    
    if (string.Equals(room.bossIdentifier, "CursedCommander", StringComparison.OrdinalIgnoreCase))
        CrossfadeTo(cursedCommanderCombatClip);
    else if (string.Equals(room.bossIdentifier, "ShadowMageMalakor", StringComparison.OrdinalIgnoreCase))
        CrossfadeTo(malakorCombatClip);
    else if (string.Equals(room.bossIdentifier, "GargoyleKing", StringComparison.OrdinalIgnoreCase))
        CrossfadeTo(gargoyleKingPhase1Clip);
    else if (string.Equals(room.roomLocation, "Cellar", StringComparison.OrdinalIgnoreCase))
        CrossfadeTo(cellarCombatClip);
    else
        CrossfadeTo(cellarCombatClip); // default encounter combat fallback
}
```

### 5.3 Gargoyle King Phase 2 Shift (`GargoyleKingBoss.OnStoneFormActivated`)
- In `GargoyleKingBoss.cs` line 72:
  `public static event Action<GargoyleKingBoss> OnStoneFormActivated;`
- Triggered when HP drops below 50%:
  ```csharp
  private void HandleStoneFormActivated(GargoyleKingBoss boss)
  {
      CrossfadeTo(gargoyleKingPhase2Clip, crossfadeDuration: 1.0f);
  }
  ```

### 5.4 Combat End & Exploration Music Restoration (`TurnManager.OnCombatEnded`)
- In `TurnManager.cs` line 71:
  `public static event Action<bool> OnCombatEnded;`
- In `AudioManager.cs`, victory or defeat triggers an 0.8s SFX fanfare:
  `PlaySFX(isVictory ? SoundType.Victory : SoundType.Defeat);`
- `MusicManager` handles `OnCombatEnded`:
  Wait 1.2s to allow the fanfare to conclude, then crossfade back to the exploration track corresponding to `GameManager.Instance.CurrentLocation`:
  - If `CurrentLocation == GameLocation.Village`: `VillageSong.mp3`
  - Otherwise (`Forest`, `Courtyard`, `Library`, `CrownHall`): `Castle_adventure_song.mp3`

---

## 6. AudioManager vs. MusicManager Coexistence

`AudioManager.cs` currently has:
```csharp
private void HandleLocationChanged(GameLocation newLocation)
{
    switch (newLocation)
    {
        case GameLocation.Village:
            if (villageBgmClip != null) PlayBGM(villageBgmClip);
            break;
        ...
    }
}
```
If left as-is, `AudioManager` would play BGM on `bgmSource` at the same time `MusicManager` plays tracks on its dual sources.
**Solution**:
In `AudioManager.cs`:
In `Start()`, `HandleLocationChanged()`, and `HandlePlayModeChanged()`, add:
```csharp
if (MusicManager.Instance != null)
{
    // MusicManager manages all background music and crossfades
    return;
}
```
This cleanly decouples BGM (managed entirely by `MusicManager`) while preserving `AudioManager`'s SFX, volume controls, and procedural WebGL audio fallbacks.

---

## 7. Door Transition Investigation & Gap Resolution

### 7.1 Identified Gap in `DoorTeleporter.cs`
In `BuildDungeonWingsEditor.cs`, doors between zones are assigned `DestinationZone`:
- `Castle_Gate_Portal`: `DestinationZone = "Courtyard"`
- `Door_Village_Forest`: `DestinationZone = "Forest"`
- `Door_Forest_Courtyard`: `DestinationZone = "Courtyard"`
- `Door_Courtyard_Library`: `DestinationZone = "Library"`
- `Door_Courtyard_ThroneRoom`: `DestinationZone = "CrownHall"`

However, in `DoorTeleporter.PerformTeleport()` (lines 124-174):
```csharp
playerObj.transform.position = targetPosition;
playerObj.transform.rotation = targetRotation;
Physics.SyncTransforms();
```
`DestinationZone` is never checked or passed to `GameManager`. As a result:
- When the player steps through the castle gate or into the forest during exploration, `GameManager.CurrentLocation` remains unchanged until combat starts!
- Exploration music would not crossfade to `Castle_adventure_song.mp3` when leaving the village.

### 7.2 The Solution
In `DoorTeleporter.PerformTeleport()`, add:
```csharp
if (!string.IsNullOrEmpty(DestinationZone) && GameManager.Instance != null)
{
    string zone = DestinationZone.Trim();
    if (string.Equals(zone, "ThroneRoom", StringComparison.OrdinalIgnoreCase))
    {
        zone = "CrownHall";
    }

    if (Enum.TryParse<GameLocation>(zone, true, out GameLocation loc))
    {
        GameManager.Instance.SetLocation(loc);
    }
}
```
This ensures seamless, real-time audio crossfades whenever the player walks or clicks through any door portal.

---

## 8. UI, Raycast, & Interaction Safety Analysis

### 8.1 Music Attribution (`MainMenuController.cs`)
Requirement R3: "Embed the Scottish Harp song credit (`https://pixabay.com/music/scotland-harp-587446/`) into the Main Menu / UI as recorded in `Castle of dice.txt`."
- In `MainMenuController.cs`, `EnsureUIHierarchy()` constructs the menu UI procedurally.
- We add an attribution label at the bottom of `mainMenuPanel`:
  ```csharp
  GameObject creditObj = new GameObject("MusicCredit_Text");
  creditObj.transform.SetParent(panel.transform, false);
  RectTransform creditRect = creditObj.AddComponent<RectTransform>();
  creditRect.anchoredPosition = new Vector2(0f, -195f);
  creditRect.sizeDelta = new Vector2(650f, 30f);
  TextMeshProUGUI creditTMP = creditObj.AddComponent<TextMeshProUGUI>();
  creditTMP.text = "<size=11><color=#8899AA>Musiikki: Scottish Harp by Pixabay (https://pixabay.com/music/scotland-harp-587446/)</color></size>";
  creditTMP.alignment = TextAlignmentOptions.Center;
  creditTMP.raycastTarget = false;
  ```

### 8.2 Tooltips, Defeat Screen, & Raycast Systems
- `AbilityTooltipUI.cs`: Uses `IPointerEnterHandler` / `IPointerExitHandler` and dynamic overlay instantiation (`EnsureTooltipOverlayExists`). Fully guarded with null checks.
- `DefeatUIController.cs`: Subscribes cleanly in `OnEnable`/`OnDisable` to `TurnManager.OnTurnStateChanged`. Builds UI procedurally if missing.
- `PlayerInteractionRaycaster.cs`:
  - `IsPointerOverInteractiveUI()` uses `EventSystem.current.RaycastAll` to ensure clicking on UI buttons or modal windows never penetrates into 3D world interactables.
  - Automatically resets layer mask to `~0` if misconfigured to 0.
- `FixAllUITextEditor.cs`: Sets `raycastTarget = false` on all TMP text elements, guaranteeing UI button clicks are not intercepted by text geometry.

---

## 9. Compilation & WebGL Compatibility

1. **Unity Version**: Unity 6000.3.14f1.
2. **Language Specification**: C# 9.0, .NET Standard 2.1.
3. **Assemblies**: Standard `Assembly-CSharp` and `Assembly-CSharp-Editor`.
4. **Dependencies**: 0 external packages or native C++ plugins. Pure native Unity engine APIs (`UnityEngine`, `UnityEngine.UI`, `TMPro`, `UnityEditor`).
5. **WebGL Compatibility**:
   - `MusicManager` will use standard `AudioSource` playback, `Mathf.Lerp`, and standard coroutines (`IEnumerator`).
   - No threading (`System.Threading.Thread`), synchronous file I/O, or unsupported WebGL APIs.
   - Dual-channel AudioSources eliminate any audio clicks or pops when switching clips.

---

## 10. Summary Checklist for Implementation Phase

| Task | Target File | Description |
|---|---|---|
| 1. Update GameLocation Enum | `Assets/Scripts/Core/GameManager.cs` | Add `Forest = 4` with explicit values for `Village=0, Courtyard=1, Library=2, CrownHall=3, Forest=4`. |
| 2. Create MusicManager | `Assets/Scripts/Core/MusicManager.cs` | Dual AudioSource 1.2s crossfading, event listeners, persistent singleton, clip references. |
| 3. AudioManager Coexistence | `Assets/Scripts/Core/AudioManager.cs` | Skip BGM playback if `MusicManager.Instance != null`. |
| 4. Door Transition Update | `Assets/Scripts/World/DoorTeleporter.cs` | Call `GameManager.Instance.SetLocation(loc)` in `PerformTeleport()`. |
| 5. MainMenu Attribution | `Assets/Scripts/UI/MainMenuController.cs` | Add Pixabay Scottish Harp credit text in `EnsureUIHierarchy()`. |
| 6. Editor Tooling Update | `Assets/Scripts/Editor/BuildVillageEditor.cs` | Add `EnsureMusicManagerOnManagers()`, assign all 7 clips via `AssetDatabase.LoadAssetAtPath`. |
| 7. Control Window Update | `Assets/Scripts/Editor/CastleOfDiceControlWindow.cs` | Add status display and button for `MusicManager`. |
| 8. Scene Verification | `Assets/Scenes/StartVillage.unity` | Verify `MusicManager` active on `Managers` with all 7 clips configured. |
