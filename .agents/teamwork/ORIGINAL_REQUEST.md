# Original User Request

## Initial Request — 2026-09-24T21:52:30+03:00

Implement a complete, WebGL-compatible MusicManager with smooth cross-fading for all 7 game music tracks, integrate dynamic audio transitions across exploration and boss battles, verify notebook-specified features, and test the game code for stability and zero errors.

Working directory: D:\Unity\3D DnD selainpeli
Integrity mode: development

## Requirements

### R1. WebGL-Compatible MusicManager Architecture
Implement a persistent `MusicManager` using pure native Unity C# (no external dependencies) with dual-channel AudioSource crossfading (1.2s smooth volume interpolation). Support and map all 7 MP3 tracks in `Assets/Music/`:
- `VillageSong.mp3` (Oakhaven / Kivenkolo Village)
- `Castle_adventure_song.mp3` (Forest, Courtyard, Library, CrownHall exploration)
- `Cellar_combat_music.mp3` (Tavern Cellar encounter)
- `CursedCommander_Combat_music.mp3` (Wing 1 Boss: Cursed Commander)
- `Malakor_combat_music.mp3` (Wing 2 Boss: Shadow Mage Malakor)
- `1_Combat_GargoyleKing_music.mp3` (Wing 3 Final Boss: Gargoyle King Phase 1)
- `2_Combat_GargoyleKing_music.mp3` (Wing 3 Final Boss: Gargoyle King Phase 2 Stone Form)

### R2. Dynamic State & Location Event Integration
Wire the audio system to listen to:
- `GameLocation` updates via `GameManager.OnLocationChanged` (including newly added `Forest` location in `GameLocation` enum).
- Boss encounter starts via `DungeonRoomController.OnRoomCombatStarted` (mapping `bossIdentifier`).
- Phase 2 shift via `GargoyleKingBoss.OnStoneFormActivated` to trigger immediate crossfade to Phase 2 combat music.
- Encounter victory or defeat via `TurnManager.OnCombatEnded` to restore exploration music after result fanfares.

### R3. Music Attribution & Notebook Requirements
- Embed the Scottish Harp song credit (`https://pixabay.com/music/scotland-harp-587446/`) into the Main Menu / UI as recorded in `Castle of dice.txt`.
- Validate that ability tooltips (`AbilityTooltipUI`), defeat screen modal (`DefeatUIController`), and character class selection (`MainMenuController`) function seamlessly.

### R4. Automated Setup & Editor Tooling
Provide automated setup in `BuildVillageEditor` and `CastleOfDiceControlWindow` to instantiate `MusicManager` on `Managers` in `StartVillage.unity` and auto-assign all 7 AudioClips from `Assets/Music/` without manual inspector dragging. Preserve all `.meta` files and GUID integrity.

### R5. Comprehensive Code Verification & Bug Fixing
Test all C# scripts to ensure zero compilation errors (`Assembly-CSharp.dll` and `Assembly-CSharp-Editor.dll`). Verify that raycasts, door transitions, combat grids, and UI clicks execute cleanly with zero null reference exceptions.

## Acceptance Criteria

### Audio & Music Functionality
- [ ] `MusicManager.cs` compiles cleanly and executes dual AudioSource crossfading without clicks or pops.
- [ ] All 7 MP3 tracks in `Assets/Music` are loaded and assigned to `MusicManager`.
- [ ] Entering `Village` plays `VillageSong.mp3`.
- [ ] Moving into `Forest`, `CourtYard`, `Library`, or `ThroneRoom` in exploration plays `Castle_adventure_song.mp3`.
- [ ] Engaging Cursed Commander, Malakor, or Gargoyle King triggers their respective combat tracks.
- [ ] Gargoyle King dropping below 50% HP triggers `OnStoneFormActivated` and crossfades to `2_Combat_GargoyleKing_music.mp3`.
- [ ] Pixabay Scottish Harp credit link/text is visible in the UI.

### Code Quality & Unity Stability
- [ ] All code uses standard native Unity C# with 0 external libraries, fully compatible with WebGL.
- [ ] Existing `.meta` files and asset GUIDs remain intact and uncorrupted.
- [ ] `Assembly-CSharp` and `Assembly-CSharp-Editor` compile with 0 errors and 0 warnings of missing references.
- [ ] `StartVillage.unity` has `MusicManager` active under the `Managers` GameObject.
- [ ] End-to-end player flow (Village -> Quest -> Forest -> Doors -> Boss Battles -> Defeat/Victory) operates without unhandled exceptions.
