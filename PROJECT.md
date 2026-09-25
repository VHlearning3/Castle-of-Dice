# Project: Castle of the D20 / Castle of Dice — WebGL Music System & Dynamic State Audio Integration

## Architecture
- **Core Engine & Platform**: Unity (native C#, WebGL compatible, no external runtime dependencies).
- **Audio Architecture**:
  - `MusicManager`: Persistent Singleton (`MonoBehaviour`) attached to `Managers` in `StartVillage.unity`. Dual `AudioSource` channels (`audioSourceA`, `audioSourceB`) with `spatialBlend = 0f` (2D stereo) and smooth 1.2s crossfade interpolation coroutine.
  - `AudioManager`: Retains responsibility for SFX and procedural fallbacks; checks `MusicManager.Instance` to yield BGM control to `MusicManager`.
- **Event-Driven Integration**:
  - `GameManager.OnLocationChanged(GameLocation)`: Dynamic exploration music switching.
  - `DoorTeleporter.PerformTeleport`: Updates `GameManager.Instance.SetLocation()` using `DestinationZone`.
  - `DungeonRoomController.OnRoomCombatStarted(DungeonRoomController)`: Triggers boss/cellar combat tracks using `bossIdentifier` and `roomLocation`.
  - `GargoyleKingBoss.OnStoneFormActivated(GargoyleKingBoss)`: Triggers phase 2 boss music crossfade.
  - `TurnManager.OnCombatEnded(bool isVictory)`: Waits for fanfares (~1.5s) then restores exploration music.
- **UI & Attribution Layer**:
  - `MainMenuController`: Hosts Scottish Harp Pixabay credit link (`https://pixabay.com/music/scotland-harp-587446/`) in UI.
  - `AbilityTooltipUI`: Procedural tooltip layer on combat buttons.
  - `DefeatUIController`: Defeat modal handling retry and retreat.
- **Editor Tooling**:
  - `BuildVillageEditor`: Extends `EnsureManagersInScene` to instantiate and wire `MusicManager` with all 7 audio clips from `Assets/Music/`.
  - `CastleOfDiceControlWindow`: Visual status and one-click audio setup action.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| F1.1 | Dual-Channel Crossfade MusicManager | WebGL-compatible persistent manager with 2 AudioSources and 1.2s interpolation | M1 | ORIGINAL_REQUEST §R1 |
| F1.2 | 7 MP3 Tracks Audio Mapping | Direct mapping & playback for Village, Castle Adventure, Cellar, 3 Bosses, & Phase 2 | M1 | ORIGINAL_REQUEST §R1 |
| F1.3 | AudioManager BGM Delegation | Prevent overlapping audio; AudioManager yields BGM playback to MusicManager | M1 | Survey (explorer 2 & 3) |
| F2.1 | GameLocation.Forest Extension | Add Forest = 4 enum value to GameLocation in GameManager.cs preserving YAML serialization | M2 | ORIGINAL_REQUEST §R2 |
| F2.2 | DoorTeleporter Location Sync | Update GameManager.SetLocation on door teleportation to trigger location events | M2 | Survey (explorer 2 & 3) |
| F2.3 | Dynamic Exploration Music | Crossfade to VillageSong on Village, Castle_adventure_song on Forest/Courtyard/Library/CrownHall | M2 | ORIGINAL_REQUEST §R2 |
| F2.4 | Boss Encounter Combat Tracks | Map CursedCommander, ShadowMageMalakor, GargoyleKing, and Cellar combat tracks | M2 | ORIGINAL_REQUEST §R2 |
| F2.5 | Gargoyle King Phase 2 Shift | Immediate crossfade to 2_Combat_GargoyleKing_music on OnStoneFormActivated (HP <= 50%) | M2 | ORIGINAL_REQUEST §R2 |
| F2.6 | Combat End Restoration | Wait ~1.5s after OnCombatEnded for fanfares, then restore exploration music | M2 | ORIGINAL_REQUEST §R2 |
| F3.1 | Scottish Harp Attribution UI | Display Pixabay credit and URL in Main Menu UI per Castle of dice.txt | M3 | ORIGINAL_REQUEST §R3 |
| F3.2 | UI Systems Validation | Verify AbilityTooltipUI, DefeatUIController, MainMenuController operate without errors | M3 | ORIGINAL_REQUEST §R3 |
| F4.1 | BuildVillageEditor Music Setup | Automatically instantiate MusicManager under Managers in StartVillage.unity with 7 clips | M4 | ORIGINAL_REQUEST §R4 |
| F4.2 | Control Window Integration | Add MusicManager status and setup button to CastleOfDiceControlWindow | M4 | ORIGINAL_REQUEST §R4 |
| F4.3 | GUID & .meta Preservation | Ensure 0 changes/corruptions to existing .meta files and asset GUIDs | M4 | ORIGINAL_REQUEST §R4 |
| F5.1 | Zero Compilation Errors | Compile Assembly-CSharp and Assembly-CSharp-Editor with 0 errors and warnings | M5 | ORIGINAL_REQUEST §R5 |
| F5.2 | Zero Null Reference Exceptions | Validate raycasts, door transitions, combat grids, UI clicks cleanly | M5 | ORIGINAL_REQUEST §R5 |
| F5.3 | 100% E2E Test Suite Passing | Pass all E2E test cases across Tiers 1-4 and execute adversarial hardening (Tier 5) | M5 | Project Pattern Final Milestone |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | WebGL MusicManager Core | F1.1, F1.2, F1.3: `MusicManager.cs` implementation, dual-channel crossfade, clip mapping, `AudioManager.cs` BGM delegation | none | PLANNED |
| M2 | State & Event Audio Integration | F2.1, F2.2, F2.3, F2.4, F2.5, F2.6: `GameLocation.Forest`, `DoorTeleporter` sync, event subscriptions for location, boss encounters, phase 2, and combat end | M1 | PLANNED |
| M3 | Music Attribution & UI Validation | F3.1, F3.2: Scottish Harp credit in `MainMenuController`, validation of `AbilityTooltipUI`, `DefeatUIController`, click/tooltip safety | none | PLANNED |
| M4 | Editor Tooling & Scene Setup | F4.1, F4.2, F4.3: `BuildVillageEditor.cs`, `CastleOfDiceControlWindow.cs`, `StartVillage.unity` update, GUID verification | M1, M2 | PLANNED |
| M5 | Final Milestone: E2E Verification & Hardening | F5.1, F5.2, F5.3: Pass 100% E2E test suite (Tiers 1-4), adversarial hardening (Tier 5), 0 compilation errors, 0 runtime exceptions | M1, M2, M3, M4, TEST_READY.md | PLANNED |

## Interface Contracts

### MusicManager ↔ Game State Events
- `public static MusicManager Instance { get; }`
- `public void PlayMusic(AudioClip clip, float fadeDuration = 1.2f)`
- `public void PlayTrack(MusicTrackType trackType, float fadeDuration = 1.2f)`
- `public void PlayCombatMusicForBoss(string bossIdentifier, string roomLocation = "")`
- `public void RestoreExplorationMusic(float delay = 1.5f)`
- Subscribes to:
  - `GameManager.OnLocationChanged += HandleLocationChanged;`
  - `DungeonRoomController.OnRoomCombatStarted += HandleCombatStarted;`
  - `GargoyleKingBoss.OnStoneFormActivated += HandleStoneFormActivated;`
  - `TurnManager.OnCombatEnded += HandleCombatEnded;`

### DoorTeleporter ↔ GameManager
- In `PerformTeleport()`:
  - Parses `DestinationZone` string into `GameLocation` enum.
  - Calls `GameManager.Instance.SetLocation(parsedLocation)`.

### Editor Tooling ↔ Scene Setup
- `BuildVillageEditor.EnsureMusicManagerOnManagers(GameObject managersGO)`:
  - Loads 7 clips from `Assets/Music/` via `AssetDatabase.LoadAssetAtPath<AudioClip>()`.
  - Adds or retrieves `MusicManager` component on `managersGO`.
  - Assigns clip references directly into `MusicManager` fields.
  - Marks scene dirty with `EditorUtility.SetDirty`.

## Code Layout
- `Assets/Scripts/Core/MusicManager.cs` — Core WebGL dual-channel crossfade music manager.
- `Assets/Scripts/Core/GameManager.cs` — `GameLocation` enum extension (`Forest = 4`).
- `Assets/Scripts/Core/AudioManager.cs` — Yield BGM control to `MusicManager`.
- `Assets/Scripts/World/DoorTeleporter.cs` — Call `GameManager.Instance.SetLocation()` on teleport.
- `Assets/Scripts/UI/MainMenuController.cs` — Scottish Harp Pixabay credit link and UI display.
- `Assets/Scripts/Editor/BuildVillageEditor.cs` — Scene setup automation for `MusicManager`.
- `Assets/Scripts/Editor/CastleOfDiceControlWindow.cs` — Control panel UI and audio setup button.
- `Assets/Scenes/StartVillage.unity` — Updated scene with `MusicManager` component on `Managers`.
- `Assets/Tests/E2E/` — E2E test suite and runner scripts.
