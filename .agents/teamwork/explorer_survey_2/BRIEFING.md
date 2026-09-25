# BRIEFING — 2026-09-24T19:00:00Z

## Mission
Survey the codebase for audio architecture and game state/event wiring (GameManager, GameLocation, DungeonRoomController, GargoyleKingBoss, TurnManager, MusicManager).

## 🔒 My Identity
- Archetype: teamwork_preview_explorer
- Roles: survey, analysis, synthesis
- Working directory: D:\Unity\3D DnD selainpeli\.agents\teamwork\explorer_survey_2
- Original parent: ca3fb1df-84ce-4aaa-ac7d-83cdd973b003
- Milestone: survey

## 🔒 Key Constraints
- Read-only investigation — do NOT implement
- Produce structured analysis.md and handoff.md in working directory
- Provide exact file paths, line numbers, event signatures, and implementation guidance

## Current Parent
- Conversation ID: ca3fb1df-84ce-4aaa-ac7d-83cdd973b003
- Updated: 2026-09-24T19:00:00Z

## Investigation State
- **Explored paths**:
  - `Assets/Music/` (all 7 MP3s present with .meta)
  - `Assets/Scripts/Core/GameManager.cs` (GameLocation enum, OnLocationChanged, SetLocation)
  - `Assets/Scripts/World/DungeonRoomController.cs` (OnRoomCombatStarted, bossIdentifier, roomLocation)
  - `Assets/Scripts/Bosses/GargoyleKingBoss.cs` (OnStoneFormActivated, 50% HP threshold)
  - `Assets/Scripts/Combat/TurnManager.cs` (OnCombatEnded, StartCombatEncounter)
  - `Assets/Scripts/Core/AudioManager.cs` (existing SFX and basic BGM)
  - `Assets/Scripts/World/DoorTeleporter.cs` (DestinationZone, PerformTeleport)
  - `Assets/Scripts/UI/MainMenuController.cs`, `DefeatUIController.cs`, `AbilityTooltipUI.cs`
  - `Assets/Scripts/Editor/BuildVillageEditor.cs`, `BuildDungeonWingsEditor.cs`, `BuildCellarEditor.cs`, `CastleOfDiceControlWindow.cs`
- **Key findings**:
  1. Forest is MISSING from `GameLocation` enum in `GameManager.cs`. Needs to be added.
  2. `MusicManager.cs` does not exist yet. Needs to be placed in `Assets/Scripts/Core/MusicManager.cs`.
  3. `AudioManager.cs` currently has its own single-channel `bgmSource` and subscribes to `OnLocationChanged`. Needs separation so `MusicManager` owns BGM and `AudioManager` owns SFX.
  4. All 7 music tracks are present in `Assets/Music/`.
  5. `DungeonRoomController.OnRoomCombatStarted` passes `this`, from which `bossIdentifier` and `roomLocation` are read.
  6. `GargoyleKingBoss.OnStoneFormActivated` is already defined and fires when HP <= maxHP / 2.
  7. `TurnManager.OnCombatEnded(bool isVictory)` fires on win/loss; fanfares take ~1.2-1.5s before restoring exploration music.
  8. `DoorTeleporter.cs` has `DestinationZone` but does not currently call `GameManager.SetLocation`; adding that call ensures exploration music updates when walking through doors.
  9. Scottish Harp credit link needs to be embedded in `MainMenuController.cs` UI.
- **Unexplored areas**: None. Complete survey achieved.

## Key Decisions Made
- Recommend non-breaking addition of `Forest` to `GameLocation`.
- Recommend clean separation: `MusicManager` handles dual-channel BGM crossfade; `AudioManager` handles SFX.
- Provide full design sketches and code snippets in analysis.md and handoff.md.

## Artifact Index
- DISPATCH.md — Task assignment and incoming prompts
- BRIEFING.md — Persistent working memory and state
- progress.md — Liveness heartbeat
- analysis.md — Detailed survey analysis
- handoff.md — 5-component handoff report
