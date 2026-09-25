# Specification Mining & Architecture Survey Report

## Executive Summary

This report delivers the authoritative specification mining and system architecture survey for **Castle of the D20 (Castle of Dice)**. The investigation covers the core requirements outlined in `ORIGINAL_REQUEST.md`, user notebook notes in `Castle of dice.txt` (located at `C:\Users\Omistaja\Desktop\Castle of dice.txt`), audio assets in `Assets/Music/`, UI controllers (`AbilityTooltipUI`, `DefeatUIController`, `MainMenuController`), dynamic event hooks across the codebase (`GameManager`, `DungeonRoomController`, `GargoyleKingBoss`, `TurnManager`), and editor automation scripts (`BuildVillageEditor`, `CastleOfDiceControlWindow`).

---

## Authoritative Specification Sources

| Source | Location | Scope & Key Findings |
| :--- | :--- | :--- |
| **ORIGINAL_REQUEST.md** | `.agents/teamwork/ORIGINAL_REQUEST.md` | Primary contract specifying Requirements R1–R5: WebGL MusicManager, dual AudioSource crossfade (1.2s), 7 MP3 tracks, dynamic event listening, Scottish Harp attribution, editor tooling, and zero-error stability. |
| **Castle of dice.txt** | `C:\Users\Omistaja\Desktop\Castle of dice.txt` | Creator's master task list: records Scottish Harp attribution URL (`https://pixabay.com/music/scotland-harp-587446/`), lists missing components ("Puuttuvia: Ability textit, defeat screen, main menu, äänet ja musiikit"), and details village layout/NPCs. |
| **Assets/Music/** | `Assets/Music/` | Directory containing all 7 target MP3 audio tracks and corresponding Unity `.meta` files with persistent GUIDs. |
| **UI Scripts** | `Assets/Scripts/UI/` | `MainMenuController.cs`, `AbilityTooltipUI.cs`, `DefeatUIController.cs`, `CombatUIController.cs`. |
| **Core & Combat Scripts** | `Assets/Scripts/Core/`, `Assets/Scripts/Combat/`, `Assets/Scripts/Bosses/`, `Assets/Scripts/World/` | `GameManager.cs` (`GameLocation` enum, `OnLocationChanged`), `TurnManager.cs` (`OnCombatEnded`), `DungeonRoomController.cs` (`OnRoomCombatStarted`), `GargoyleKingBoss.cs` (`OnStoneFormActivated`), `DoorTeleporter.cs`. |
| **Editor Tooling** | `Assets/Scripts/Editor/` | `BuildVillageEditor.cs`, `CastleOfDiceControlWindow.cs`, `BuildDungeonWingsEditor.cs`, `BuildCellarEditor.cs`, `AutoSetupGameEditor.cs`. |

---

## Features Discovered

| # | Category | Feature | Description | Inputs | Outputs | Error Behavior | Discovered Via |
|---|----------|---------|-------------|--------|---------|----------------|----------------|
| 1 | Audio Architecture | Dual-Channel AudioSource Engine | Persistent `MusicManager` utilizing two `AudioSource` channels (Channel A & B) for clickless, pop-free audio crossfading in native Unity C#. | Target `AudioClip`, fade duration (1.2s default), volume scale | Smooth volume crossfade between sources, active source swap | Re-uses active source if same clip requested; prevents duplicate playback | `ORIGINAL_REQUEST.md` R1 |
| 2 | Audio Architecture | 1.2s Smooth Volume Interpolation | Coroutine-driven smoothstep/equal-power interpolation over 1.2 seconds using `Time.unscaledDeltaTime`. | Normalized interpolation time $t \in [0, 1]$ | Volume curve applied to outgoing ($1 \to 0$) and incoming ($0 \to 1$) sources | Gracefully handles mid-fade interruptions by beginning new crossfade from current volume levels | `ORIGINAL_REQUEST.md` R1 |
| 3 | Audio Architecture | WebGL Zero-Dependency Audio | Pure native Unity C# implementation without external DLLs (FMOD, NAudio, etc.) ensuring full WebGL browser compatibility. | Unity WebGL AudioContext | Browser audio stream playback | Handles browser autoplay policy restrictions gracefully without unhandled exceptions | `ORIGINAL_REQUEST.md` R1, R5 |
| 4 | Audio Asset Mapping | Village Theme (`VillageSong.mp3`) | Exploration background music for safe zone (Oakhaven / Kivenkolo Village). | `GameLocation.Village` event or game start | Looping playback of `VillageSong.mp3` | Falls back to silence or procedural synth if clip is missing | `ORIGINAL_REQUEST.md` R1, `Castle of dice.txt` |
| 5 | Audio Asset Mapping | Castle Exploration Theme (`Castle_adventure_song.mp3`) | Ambient adventure exploration music for Forest, Courtyard, Library, and CrownHall. | `GameLocation` in {Forest, Courtyard, Library, CrownHall} with `GamePlayMode.Exploration` | Looping playback of `Castle_adventure_song.mp3` | Seamless continuation if transitioning between connected dungeon zones | `ORIGINAL_REQUEST.md` R1, R2 |
| 6 | Audio Asset Mapping | Cellar Combat Theme (`Cellar_combat_music.mp3`) | Tactical battle music for Barnaby's Tavern Cellar encounter. | `DungeonRoomController.OnRoomCombatStarted` with `roomLocation == "Cellar"` | Looping playback of `Cellar_combat_music.mp3` | Falls back to default combat track if specific track missing | `ORIGINAL_REQUEST.md` R1, `BuildCellarEditor.cs` |
| 7 | Audio Asset Mapping | Cursed Commander Boss Theme (`CursedCommander_Combat_music.mp3`) | Wing 1 Boss encounter battle music. | `DungeonRoomController.OnRoomCombatStarted` with `bossIdentifier == "CursedCommander"` | Looping playback of `CursedCommander_Combat_music.mp3` | Reverts to exploration theme on victory/defeat | `ORIGINAL_REQUEST.md` R1, R2, `BuildDungeonWingsEditor.cs` |
| 8 | Audio Asset Mapping | Shadow Mage Malakor Boss Theme (`Malakor_combat_music.mp3`) | Wing 2 Boss encounter battle music in Arcane Library. | `DungeonRoomController.OnRoomCombatStarted` with `bossIdentifier == "ShadowMageMalakor"` | Looping playback of `Malakor_combat_music.mp3` | Restores library exploration music upon victory | `ORIGINAL_REQUEST.md` R1, R2, `BuildDungeonWingsEditor.cs` |
| 9 | Audio Asset Mapping | Gargoyle King Phase 1 Theme (`1_Combat_GargoyleKing_music.mp3`) | Wing 3 Final Boss initial combat theme. | `DungeonRoomController.OnRoomCombatStarted` with `bossIdentifier == "GargoyleKing"` | Looping playback of `1_Combat_GargoyleKing_music.mp3` | Switches to Phase 2 theme when boss drops $\le 50\%$ HP | `ORIGINAL_REQUEST.md` R1, R2, `GargoyleKingBoss.cs` |
| 10 | Audio Asset Mapping | Gargoyle King Phase 2 Theme (`2_Combat_GargoyleKing_music.mp3`) | Wing 3 Final Boss Stone Form battle theme. | `GargoyleKingBoss.OnStoneFormActivated` event trigger | Immediate 1.2s crossfade to `2_Combat_GargoyleKing_music.mp3` | Logs warning if boss transitions while music manager inactive | `ORIGINAL_REQUEST.md` R1, R2, `GargoyleKingBoss.cs` |
| 11 | Dynamic Integration | Location Change Listener | Listens to `GameManager.OnLocationChanged` to update background exploration music. | `GameLocation` enum value | Triggers crossfade to appropriate area BGM | Ignores event if current combat encounter is active | `ORIGINAL_REQUEST.md` R2, `GameManager.cs` |
| 12 | Dynamic Integration | `Forest` Location Enum Addition | Extension of `GameLocation` enum in `GameManager.cs` to explicitly support the newly built Forest zone plane. | `GameLocation.Forest` | Valid enum parsing for `DoorTeleporter` and `GameManager.SetLocation` | Eliminates enum parse failures when entering Forest zone | `ORIGINAL_REQUEST.md` R2, `GameManager.cs` |
| 13 | Dynamic Integration | Door Teleporter Location Synchronization | Synchronizes player's active `GameLocation` upon walking or interacting through doors/gates. | `DoorTeleporter.PerformTeleport` destination zone string | Calls `GameManager.Instance.SetLocation(parsedZone)` | Gracefully skips if `DestinationZone` is unassigned or invalid | `ORIGINAL_REQUEST.md` R2, `DoorTeleporter.cs` |
| 14 | Dynamic Integration | Boss Room Combat Trigger | Subscribes to `DungeonRoomController.OnRoomCombatStarted` to automatically route boss music based on `bossIdentifier`. | `DungeonRoomController` instance | Selects and crossfades to specific boss combat music | Defaults to generic/cellar combat music if `bossIdentifier` is unrecognized | `ORIGINAL_REQUEST.md` R2, `DungeonRoomController.cs` |
| 15 | Dynamic Integration | Combat Resolution & Exploration Recovery | Subscribes to `TurnManager.OnCombatEnded` to restore peaceful exploration music after victory/defeat fanfares. | `bool isVictory` | Waits for result fanfare then crossfades back to current zone exploration BGM | Does not crash if `GameManager.CurrentLocation` is unassigned | `ORIGINAL_REQUEST.md` R2, `TurnManager.cs` |
| 16 | Music Attribution | Pixabay Scottish Harp UI Credit | Visible attribution for the Scottish Harp song (`https://pixabay.com/music/scotland-harp-587446/`) embedded in the Main Menu and UI. | UI initialization / MainMenuPanel | Rendered attribution text and interactive URL link | Safe URL execution via `Application.OpenURL` | `ORIGINAL_REQUEST.md` R3, `Castle of dice.txt` |
| 17 | UI Controller | Ability Tooltip System | Hover inspection card displaying ability name, range, targeting type, damage/healing formulas, and blacksmith weapon bonuses. | `IPointerEnterHandler` / `IPointerExitHandler` on action buttons | Dynamic canvas overlay positioned above action button | Clamps safely, hides on pointer exit, handles null player/ability slots | `ORIGINAL_REQUEST.md` R3, `AbilityTooltipUI.cs` |
| 18 | UI Controller | Defeat Screen Modal | Modal game over dialog triggered upon party wipeout offering "Yritä uudelleen" (Retry) or "Palaa Kivenkoloon" (Retreat to Village). | `TurnManager.OnTurnStateChanged` with `TurnState.Defeat` | Displays defeat modal with ominous backdrop and action buttons | Safely revives player and teleports to spawn point upon retreat | `ORIGINAL_REQUEST.md` R3, `DefeatUIController.cs` |
| 19 | UI Controller | Hero Class Selection | Interactive character selection (Warrior Sir Roland, Mage Elira, Rogue Corvo) configuring stats and refreshing ability action bars. | Class selection button click | Assigns `CharacterClassSO` to `PlayerUnit`, re-initializes unit, refreshes `CombatUIController` | Falls back to asset database discovery if resources missing | `ORIGINAL_REQUEST.md` R3, `MainMenuController.cs` |
| 20 | Editor Automation | Automated MusicManager Setup | Editor tooling method to instantiate `MusicManager` on `Managers` GameObject in `StartVillage.unity` and auto-wire all 7 MP3 clips. | Menu item / button click in Editor | `MusicManager` component added and populated with loaded `AudioClip` assets | Checks scene state; prompts user if `StartVillage.unity` is not active | `ORIGINAL_REQUEST.md` R4, `BuildVillageEditor.cs` |
| 21 | Editor Automation | Control Window Integration | Visual dashboard panel in `CastleOfDiceControlWindow.cs` displaying detection status of `MusicManager` and providing one-click setup. | Editor window GUI repaint / button click | Displays live status ("✓ Active & Assigned" vs "✗ Missing") and triggers auto-setup | Repaints without GUI layout errors | `ORIGINAL_REQUEST.md` R4, `CastleOfDiceControlWindow.cs` |
| 22 | System Stability | SFX & BGM Separation | Clear responsibility split between `AudioManager` (sound effects, dice rolls, fanfares) and `MusicManager` (dual-channel BGM crossfade). | Event invocations | `AudioManager.PlaySFX` handles one-shots; `MusicManager` manages continuous BGM | Prevents conflicting duplicate BGM streams from playing simultaneously | `ORIGINAL_REQUEST.md` R1, R5, `AudioManager.cs` |

---

## Edge Cases

| # | Feature | Input | Observed / Required Behavior |
|---|---------|-------|------------------------------|
| 1 | Dual Crossfading | Transition requested while previous crossfade is still interpolating | Existing coroutine is safely cancelled or re-anchored; interpolation begins immediately from current volume levels of both sources to avoid audio volume jumps. |
| 2 | Dual Crossfading | Transition requested for clip that is already playing on active source | No-op. The active track continues playing without restarting, stuttering, or volume dip. |
| 3 | Gargoyle King Phase 2 | Boss drops $\le 50\%$ HP while Phase 1 combat music is actively playing | `GargoyleKingBoss.OnStoneFormActivated` triggers immediate 1.2s crossfade to `2_Combat_GargoyleKing_music.mp3` without waiting for combat turn resolution. |
| 4 | Rapid Zone Transitions | Player repeatedly steps across door triggers (ping-ponging between zones) | `DoorTeleporter` enforces a 1.0s cooldown (`teleportCooldown`). `MusicManager` ensures consecutive crossfade requests smoothly crossfade without audio pops. |
| 5 | WebGL AudioContext | Browser autoplay policy blocks audio until first user interaction | AudioSources initialize without throwing unhandled exceptions. As soon as the user clicks any UI button (e.g. "Uusi seikkailu"), the WebGL AudioContext unpauses and music begins. |
| 6 | Combat Conclusion (Victory) | Boss or enemy group defeated; `TurnManager.OnCombatEnded(true)` fires | `AudioManager` plays Victory fanfare SFX. `MusicManager` delays briefly or smoothly crossfades back to the exploration theme for the current `GameLocation`. |
| 7 | Combat Conclusion (Defeat) | Player health drops to 0; `TurnManager.OnCombatEnded(false)` fires | Defeat SFX plays; `DefeatUIController` modal appears. If player chooses "Palaa Kivenkoloon", location is set to `GameLocation.Village` and `VillageSong.mp3` fades in. If "Yritä uudelleen", room encounter restarts and combat music resumes. |
| 8 | Doorway Location Update | Door without `DestinationZone` or with unrecognized string | `Enum.TryParse<GameLocation>` fails safely (`out _`); player is teleported physically, but `GameLocation` remains unchanged without throwing an exception. |
| 9 | Dungeon Room Combat | Room triggered with empty or unmapped `bossIdentifier` (e.g. generic cellar combat) | Routes to `Cellar_combat_music.mp3` (if in Cellar) or standard combat theme rather than throwing NullReferenceException. |
| 10 | Ability Tooltip | Hovering over empty or unassigned ability slot (index > active abilities count) | `AbilityTooltipUI.ShowTooltipForSlot` checks index bounds and returns immediately; tooltip overlay remains hidden. |
| 11 | Class Selection | Changing class multiple times in Main Menu before starting adventure | Each selection cleanly overwrites `PlayerUnit` stats and invokes `CombatUIController.RefreshAbilityBar()` without leaking listeners or creating duplicate UI cards. |
| 12 | Editor Automation | Running `BuildVillageEditor` or `CastleOfDiceControlWindow` when `StartVillage.unity` is not active | Editor displays standard dialog prompting user to open `Assets/Scenes/StartVillage.unity`. |
| 13 | Asset Integrity | Re-running setup scripts multiple times | AudioClips are assigned via `AssetDatabase.LoadAssetAtPath`; `.meta` files and GUIDs remain completely untouched. |

---

## Technical Specifications & Asset Inventory

### 1. Audio Track Inventory & Metadata

All 7 tracks are located in `Assets/Music/`. Importer settings specify Vorbis compression and 2D playback (`AudioSource.spatialBlend = 0f`).

| Asset File | Target Zone / Encounter | File Size | Unity Asset GUID |
| :--- | :--- | :---: | :--- |
| `VillageSong.mp3` | Safe Zone: Oakhaven / Kivenkolo Village | 2,177,567 B | `8c695f2c8079fff438568e6ed65231a4` |
| `Castle_adventure_song.mp3` | Castle Exploration: Forest, Courtyard, Library, CrownHall | 2,863,535 B | `89bd107ca50cf9f49811cf24b093e6ed` |
| `Cellar_combat_music.mp3` | Quest Encounter: Innkeeper Barnaby's Tavern Cellar | 2,885,423 B | `10578e684e333d044802dee8f2f0446b` |
| `CursedCommander_Combat_music.mp3` | Wing 1 Boss: Cursed Commander (Courtyard) | 2,582,063 B | `af6d1f5f2a827c84d8ab7e04b82fab99` |
| `Malakor_combat_music.mp3` | Wing 2 Boss: Shadow Mage Malakor (Library) | 2,769,455 B | `64944305ed273854687c0951beb9a69c` |
| `1_Combat_GargoyleKing_music.mp3` | Wing 3 Final Boss: The Gargoyle King (Phase 1) | 2,889,263 B | `aed4d214fdf825441891b02b8828e9fa` |
| `2_Combat_GargoyleKing_music.mp3` | Wing 3 Final Boss: The Gargoyle King (Phase 2 Stone Form) | 2,885,423 B | `0877d37538dd21c42b49ab1964519b58` |

### 2. Event Hook Architecture

```
[GameManager.OnLocationChanged]
    ├── GameLocation.Village ──────────────► Crossfade to VillageSong.mp3
    └── GameLocation.Forest/Courtyard/.. ──► Crossfade to Castle_adventure_song.mp3 (Exploration)

[DungeonRoomController.OnRoomCombatStarted]
    ├── bossIdentifier == "CursedCommander" ──► Crossfade to CursedCommander_Combat_music.mp3
    ├── bossIdentifier == "ShadowMageMalakor" ─► Crossfade to Malakor_combat_music.mp3
    ├── bossIdentifier == "GargoyleKing" ─────► Crossfade to 1_Combat_GargoyleKing_music.mp3
    └── roomLocation == "Cellar" ─────────────► Crossfade to Cellar_combat_music.mp3

[GargoyleKingBoss.OnStoneFormActivated]
    └── (currentHP <= maxHP / 2) ─────────────► Immediate Crossfade to 2_Combat_GargoyleKing_music.mp3

[TurnManager.OnCombatEnded]
    ├── isVictory == true ─────────────────────► Play Victory SFX -> Restore Current Zone Exploration BGM
    └── isVictory == false ────────────────────► Play Defeat SFX -> Show Defeat Screen Modal
```

### 3. Missing Links & Integration Requirements

1. **`GameLocation` Enum Expansion:**
   In `Assets/Scripts/Core/GameManager.cs`, the enum must be updated to:
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
2. **`DoorTeleporter` Location Reporting:**
   In `Assets/Scripts/World/DoorTeleporter.PerformTeleport`:
   When teleporting the player, parse `DestinationZone` into `GameLocation` and invoke `GameManager.Instance.SetLocation(parsedLocation)`. This will trigger `OnLocationChanged` and seamlessly switch exploration music across doorways.
3. **Attribution Embedding in `MainMenuController`:**
   In `Assets/Scripts/UI/MainMenuController.cs`:
   Render credit text on `MainMenuPanel`:
   `Musiikki: "Scottish Harp" (Pixabay) — https://pixabay.com/music/scotland-harp-587446/`
   Include interactive button or click handler to open the URL via `Application.OpenURL(...)`.
4. **Editor Setup Hook in `BuildVillageEditor` & `CastleOfDiceControlWindow`:**
   In `BuildVillageEditor.EnsureManagersInScene()`:
   Ensure `MusicManager` component is attached to the `Managers` GameObject and call its clip auto-population routine.
   In `CastleOfDiceControlWindow.cs`:
   Add status detection line and an automated button to run the `MusicManager` configuration.
