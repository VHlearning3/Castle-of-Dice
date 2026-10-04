# Castle of Dice: Project Update Dossier

**Audit date:** 2026-10-04
**Audited code:** branch `claude/critical-review-fixes` at commit `1b1466e` (the newest game build; it contains all of local `master`)
**Local `master`:** `c0f9df5` (this dossier is committed on top of it as a docs-only commit)
**Repository:** `VHlearning3/Castle-of-Dice`
**Engine:** Unity 6000.3.14f1, WebGL target

Every number in this file was read from the code, the scene and asset YAML, the project settings, or git, and every test result comes from a run made for this audit. Where an older design note uses a name the project does not have, this file says what the project uses instead.

---

## 1. Executive Summary & Delta Log

### 1.1 Where the game stands

Castle of Dice is a single-hero, turn-based fantasy RPG for the browser. The player picks Sir Roland (warrior), Scholar Elira (mage) or Shadow-Corvo (rogue), leaves the village of Oakhaven, and climbs through seven connected zones to break the curse on the castle. Exploration is free movement in 3D; every fight switches to a tactical grid where all actions resolve as `d20 + bonus >= AC/DC`.

The full main path is playable end to end for all three classes: village and cellar, the Forest Path, the Cursed Commander in the Courtyard, the Castle Hall hub, Shadow Mage Malakor in the Library, the Treasure Tower challenge, and the Gargoyle King in the Throne Room, followed by an ending with an epilogue that reflects the player's story choices, a stats screen and credits. A scripted PlayMode smoke test plays this whole route for each class (results in section 4.2).

Direction in the latest work (the "critical review" packages A–D, all made on 2026-10-04):

- **Rules depth:** reroll runes that actually replace failed hero rolls, Rank 2 upgrades for every ability, cooldowns, opportunity attacks, cover, burning and icy ground, explosive barrels, initiative, five hero levels and three difficulty settings.
- **Story:** fourteen lore letters (two per zone) telling the tale of King Aldred, Malakor and Sir Gareth; Queen Isolde's ring as a boss mechanic; class-specific boss dialogue options; peaceful outcomes for two bosses; an epilogue that remembers them.
- **Content:** a Castle Hall hub with a second merchant and a riddle vault, a Tower puzzle with traps and a mimic, a second Forest Path encounter, a secret gate every class can open, and new enemy types (skeleton archer, curse cultist, mimic, wolf AI).
- **Polish and shipping:** pause menu, story intro and first-fight hints, per-zone music slots, unused-file cleanup, a WebGL texture cap that cut the download from 203.5 MB to 121.0 MB, and a WebGL size-report tool.

### 1.2 Branch state

| Ref | Commit | Notes |
|---|---|---|
| `master` (local) | `c0f9df5` | Review packages A and B plus C2. 12 commits ahead of GitHub `master`; not pushed. |
| `Origin/master` (GitHub) | `5b69738` | Older: ends at the Forest Path gate models. |
| `claude/critical-review-fixes` (local and GitHub) | `1b1466e` | `master` plus 13 commits (review C3–C10, D1–D7 and two deletion commits). Pushed, not merged; waiting for Vili's "merge". |

History on the audited branch: 150 commits (124 without merges) since 2026-09-18; 127 authored as VHlearning3 and 23 as Claude. The busiest days were 2026-09-28 (20 commits), 2026-09-29 (21), 2026-09-30 (27), 2026-10-03 (14) and 2026-10-04 (27).

### 1.3 Delta log

**Legend:** [M] = already on local `master`; [B] = only on `claude/critical-review-fixes`.

#### Architecture
- [M] Assembly definitions for runtime, editor, EditMode and PlayMode code; WebGL rule tests (`2ac1b92`).
- [M] Save format v3 then v4: inventory, quests, bosses, zone, then HP, exact position, story flags and difficulty; the save loads only on Continue (`5f63330`, `65e30df`).
- [M] Duplicate-manager guard (`ManagerDuplicates.Discard`) so a manager that starts twice removes only its duplicate component (`367011a`, `8237a0f`).
- [M] UI theme as a runtime asset (`Resources/UITheme.asset`) so UI never touches `AssetDatabase` in a build (`d1e274b`).
- [M] Combat grids scale from 12x12 up to 32x32 per room (`0e77965`).
- [M] `StoryFlags` (saved) carry dialogue bonuses across scene changes (`65e30df`).
- [B] `ReviewContentBuilder` editor tool rebuilds only a `Review_Content` root in zones 2–7; Oakhaven's notes are placed at runtime by `RuntimeLoreSpawner` so the hand-edited village scene is never opened (`ac93257`, `98127f7`).
- [B] `WebGLTextureCap` (1024 px WebGL-only cap on the low-poly pack) and `WebGLSizeReport` (`2521fbd`).
- [B] Cleanup: duplicate NPC prefabs, `_Recovery`, `SampleScene`, test scenes, old E2E result files, the `scratch/` folder, 48 unused pack files (82.7 MB), the old Barnaby/Mirabel FBX files and the old NPC prefab tool (`4c182e5`, `05da35a`, `1b1466e`).

#### Mechanics
- [M] Rune of Reroll replaces failed hero rolls through `RerollableRoll`; Rank 2 for every ability (`b525d73`).
- [M] Corvo gets Shadow Step as his fourth combat ability; Lockpicking is an exploration passive (`f96797f`).
- [M] Cooldowns, opportunity attacks, damage dice for bosses, War Cry hit rolls, Fireball adds INT, Backstab rules (`82304db`, B1–B3).
- [M] Gargoyle King's telegraphed earthquake and petrifying gaze; Malakor as a ranged caster (`82304db`, B4–B5).
- [M] Cover, burning ground, ice, explosive barrels; initiative; levels 4–5 with a fifth ability; Easy/Normal/Hard (`82304db`, B6–B9).
- [M] Quest items and quest scrap cannot be sold; Baldur buys only scrap above the open request (`db365ed`).
- [M] Commander's guard joins at 50% HP; Cellar Pests asks for two rats; the King counts as a boss (`f11a2b8`).
- [M] Defeat retries reset rooms and bosses; Throne Room sealed until both wing bosses fall (`6c53a1c`).
- [M] Village NPCs can be fought from dialogue (knocked out, not killed) (`838ad31`, `6264825`).
- [M] Rogue-only lockpick timing minigame and practice chest (`e948b3d`, `44eda2d`).
- [B] Secret gate for every class: warrior STR DC 15, mage INT DC 14, rogue lockpick (`70fe66f`).
- [B] Skeleton archer, curse cultist and wolf AI; ranged enemies keep distance and need line of sight (`70fe66f`).
- [B] Tower challenge: rune pillars, dart traps (DEX DC 12), mimic guardian; finishing gives a level (`97c9021`).
- [B] Spell scrolls (Warding, Embers) read automatically at the start of the next fight (`830e85f`).

#### UI
- [M] Expedition map redesign (dark/gold, clear current zone) (`69af457`, `7091031`).
- [M] Baldur's shop with Buy/Sell tabs and tiered upgrades (`7754f5a`, `3ff4a13`).
- [M] Quest journal, quest markers, quest tracker; main quest "Break the Castle's Curse" leads the tracker (`fb0a62c`, `a478b4f`).
- [M] HUD potion slots, gold purse, enemy card highlight; combat stats HUD; Move button; tumbling d20 roll animation (`3e820db`, `906f06e`, `ad5182a`, `15ca84a`).
- [M] Ending sequence with stats screen and credits (`6c53a1c`).
- [B] Pause menu on Esc: resume, save (not in fights), music and SFX volume, How to Play, quit to menu (`b0da46a`).
- [B] Story intro after New Adventure; first-fight hints in the cellar; the story panel for letters (`9fa65d0`, `98127f7`).

#### Audio
- [M] 12-source pooled `SFXManager`; `MusicManager` dual-channel crossfade with seven tracks (village, castle exploration, cellar combat, Commander, Malakor, King phase 1 and 2).
- [B] Per-zone exploration music slots: `Resources/Music/Explore_<Location>.mp3` overrides the castle theme when present (`7c9489d`). No per-zone files exist yet, so every castle zone currently plays the castle theme.

#### Narrative
- [M] Main quest "Break the Castle's Curse" with three steps (`a478b4f`).
- [B] Fourteen lore notes, Queen Isolde's ring, class choices and consequences in boss conversations, Malakor's help or betrayal at the throne, epilogue lines (`98127f7`).
- [B] Castle Hall characters: Pip the trapped peddler and the ghost of Sir Aldric (`830e85f`).

#### Art and models (all [M] unless marked)
- Rigged Mixamo models: Sir Roland with sword and shield, Elira (new model with one animation per ability), Corvo, Cursed Commander, Malakor, the King's two golem stages, skeleton, zombie, rat, village NPCs and the village cat (`019f87f`, `140e633`, `061906f`).
- Low-poly quest items and class weapons (`1061350`); zones 2–7 dressed after the floor plans (`b4c9df9`, `44b12d5`); Forest Path gate models (`5b69738`); hand-edited Zone 1 village (`eb04fe7`, `ed780d1`).

---

## 2. Technical & Engine Architecture

### 2.1 Engine and target

| Item | Value | Source |
|---|---|---|
| Unity version | 6000.3.14f1 (revision d68c3f99a318) | `ProjectSettings/ProjectVersion.txt` |
| Render pipeline | URP 17.3.0. The only quality level, "PC", uses `Assets/Settings/PC_RPAsset.asset` (MSAA off, render scale 1). `GraphicsSettings` has no default pipeline asset; URP comes from the quality level. A `Mobile_RPAsset` exists but no quality level uses it. | `Packages/manifest.json`, `QualitySettings.asset` |
| Colour space | Linear | `ProjectSettings.asset` |
| WebGL | Gzip compression with decompression fallback, data caching on, WebAssembly linker target, threads off, explicitly thrown exceptions only, memory 64 MB initial, 2048 MB max, geometric growth, run in background off | `ProjectSettings.asset` |
| WebGL download size | 121.0 MB after the texture cap (was 203.5 MB; data 191.5 → 109.1 MB) [B] | commit `2521fbd` |
| Product name in Player Settings | still Unity's defaults: "My project" / "DefaultCompany", version 0.1.0 | `ProjectSettings.asset` |
| Other packages | Input System 1.19.0, AI Navigation 2.0.14, Timeline 1.8.13, uGUI 2.0.0, Test Framework 1.6.0, unity-cli bridge (Git package, 0.18.1) | `Packages/manifest.json` |

**Build scenes** (all enabled, in this order): `Zone_1_VillageAndCellar`, `Zone_2_ForestPath`, `Zone_3_CastleCourtyard`, `Zone_4_Library`, `Zone_5_CastleHall`, `Zone_6_Tower`, `Zone_7_ThroneRoom`. There is no separate menu scene: the main menu is drawn over Zone 1 (`ThroneRoomEnding.MainMenuSceneName = VillageNPC.StartingVillageSceneName`).

### 2.2 Input

- Player Settings: **Active Input Handling = Both** (`activeInputHandler: 2`).
- Every zone scene has one `EventSystem` with `InputSystemUIInputModule` and no `StandaloneInputModule`; `MainMenuController` also swaps out a legacy module at runtime if it finds one.
- Gameplay input goes through the static `GameInput` class (`Assets/Scripts/Core/GameInput.cs`). It polls the Input System devices directly (`Keyboard.current`, `Mouse.current`, gamepad) and falls back to the legacy `Input` API. It does **not** use an input actions asset or action maps. `Assets/InputSystem_Actions.inputactions` is Unity's default template (Player/UI maps) and no script references it. Mode switching is a flag, `GameInput.SetExplorationInputEnabled(bool)`, that modals (dialogue, shop, level-up, story panel, lockpicking) turn off and on.
- Hotkeys: WASD or arrows to move, mouse to click tiles, units and objects, **Q** quick health potion, **M** expedition map, **J** quest journal, **Esc** pause menu [B].

### 2.3 Managers and persistence

**Singletons kept across scenes (`DontDestroyOnLoad`):** `GameManager`, `SceneLoader`, `MusicManager`, `PlayerProgressionManager`, `SFXManager`, `InventoryManager`, `PlayerHUD`, `QuestJournalUI`, and `PauseMenuUI` [B]. Each zone scene has its own `Managers` object; when a second copy starts after a scene load, `ManagerDuplicates.Discard` removes the duplicate component. `GameManager.Instance` finds or creates a manager on demand but never during application quit. `DiceSystem` is a lazy plain-C# singleton (no GameObject).

| Manager | Role |
|---|---|
| `GameManager` | Location, play mode (Exploration, Combat, Dialogue, Shop), defeated bosses, cleared wings, claimed one-time rewards. `IsThroneRoomOpen` = Commander and Malakor both defeated. Events for location, mode, wing cleared, boss defeated, game won and lost. |
| `SceneLoader` | Async zone loading with a fade overlay (1920x1080 canvas), loading tips, a minimum 0.5 s transition, placement at a named `StartSpawnPoint`, and syncing the hero into `PlayerDataSO` before leaving a scene. |
| `PlayerProgressionManager` | Milestone levels 1–5 (section 3.1). |
| `InventoryManager` | Gold, scrap, items; falls back to `Resources/Items` for items not in a scene catalogue. |
| `QuestManager` | The four village side quests plus the runtime main quest. |
| `MusicManager` / `SFXManager` | Audio (section 1.3 Audio; SFX pool of 12 `AudioSource`s, `SFXManager.DefaultPoolSize = 12`). |

**Persistence model:**

- **`PlayerDataSO`** (`Assets/Scripts/Data/PlayerDataSO.cs`) is the in-session progression store. `PlayerDataSO.Session` is the shared instance, because every zone spawns its own hero prefab. It holds the selected class, level, max-HP bonus, attribute bonus, weapon and armour bonuses, Rank 2 ability slots, gold, scrap and current HP; `ApplyToPlayer` and `SyncFromPlayer` move it to and from the hero. No `.asset` of this type is saved in the project; it lives only at runtime.
- **`SaveSystem`** (`Assets/Scripts/Core/SaveSystem.cs`) serialises `PlayerSaveData` to JSON in `PlayerPrefs` under the key `CastleOfDice_SaveData`. Save format version 4 holds: level, max-HP and attribute bonuses, weapon and armour bonuses, Rank 2 slots, gold, scrap, reroll scrolls, class, inventory (ids and counts), quests (ids, states, progress), defeated bosses, cleared wings, scene name, claimed rewards, quest bonuses, adventure stats (turns, natural 20s, deaths), current HP, exact position and rotation, story flags and difficulty. Older formats load with defaults.
- **Save points:** the Rune Shrine in the Castle Hall (`SavePoint.cs`) heals and saves; the pause menu can save outside fights [B]. Continue on the main menu loads the save and places the hero back on the saved spot; New Adventure deletes the old save and reloads the village.
- **Story state:** `StoryFlags` (a saved set of strings) holds dialogue bonuses, boss choices, read letters, tutorial pages shown, the vault and Tower progress.

### 2.4 Codebase metrics

Counts are for the audited commit `1b1466e`, from `find` and `wc -l` over the `.cs` files (blank lines included in "Lines", excluded in "Non-blank").

| Folder | Scripts | Lines | Non-blank |
|---|---:|---:|---:|
| `Assets/Scripts/UI` | 27 | 15,854 | 13,588 |
| `Assets/Scripts/Editor` | 28 | 13,668 | 12,084 |
| `Assets/Scripts/World` | 29 | 6,986 | 5,797 |
| `Assets/Scripts/Combat` | 18 | 6,843 | 5,837 |
| `Assets/Scripts/Core` | 16 | 4,211 | 3,581 |
| `Assets/Scripts/Economy` | 5 | 1,973 | 1,632 |
| `Assets/Scripts/Bosses` | 5 | 1,263 | 1,041 |
| `Assets/Scripts/Dialogue` | 6 | 1,185 | 986 |
| `Assets/Scripts/Data` | 7 | 1,031 | 847 |
| `Assets/Scripts/Audio` | 1 | 456 | 394 |
| **Game and editor code total** | **142** | **53,470** | **45,787** |
| `Assets/Tests/EditMode` | 27 | 6,852 | 5,764 |
| `Assets/Tests/PlayMode` | 1 | 395 | 347 |
| **Tests total** | **28** | **7,247** | **6,111** |

- Runtime code (everything but `Editor`): 114 scripts, 39,802 lines.
- Type counts: 55 runtime `MonoBehaviour` subclasses, 10 `ScriptableObject` types, 12 `Interactable` subclasses, 7 `EnemyUnit` subclasses (three bosses, mimic, cultist, wolf, village brawler body), 175 public top-level types.
- Assemblies: `CastleOfTheD20.Runtime`, `CastleOfTheD20.Editor`, `CastleOfTheD20.Tests.EditMode`, `CastleOfTheD20.Tests.PlayMode`.
- Largest files: `CombatUIController.cs` 2,151 lines, `ShopUIController.cs` 2,137, `ZoneDressingBuilder.cs` 2,127, `AbilityVfx.cs` 1,422, `PlayerHUD.cs` 1,404.
- Growth from `master` to the branch: 122 → 142 scripts and 50,726 → 53,470 lines of game code; 25 → 28 test files and 6,223 → 7,247 test lines.

Subsystem responsibilities: **Core** rules (dice, enums, attributes, difficulty, reroll flow), managers, save, scene loading, input, music and story data. **Combat** grid, tiles, turn order, units, ability execution, VFX, status effects, cooldowns, terrain, barrels, scrolls and new enemy types. **Bosses** the three boss AIs and the King's stage swap and lava reveal. **Dialogue** node assets, the controller, action tags and boss choices. **Economy** inventory, shop, quests. **World** exploration movement, interaction, doors, rooms, lockpicking, chests, pickups, NPCs, lore notes, Tower and vault pieces, ending. **UI** HUD, combat bar, dice modal, dialogue, shop, map, journal, level-up, pause, story panel. **Editor** scene and content builders, data generators, WebGL build and size tools.

---

## 3. Game Design & Narrative Specification (GDD Status)

### 3.1 Hero classes and abilities

Base stats come from `Assets/Data/Character_*.asset`. All three classes have a primary attribute bonus of +3 (Roland STR, Elira INT, Corvo DEX, from `HeroAttributes.GetPrimaryAttribute`).

| Hero | HP | AC | Move | Abilities 1–4 | Level 4 ability |
|---|---:|---:|---:|---|---|
| Sir Roland (Warrior) | 30 | 14 | 4 | Sword Slash, Shield Block, War Cry, Iron Will | Retaliation |
| Scholar Elira (Mage) | 20 | 12 | 3 | Fireball, Frostbite, Mana Shield, Blink | Arcane Chains |
| Shadow-Corvo (Rogue) | 25 | 13 | 5 | Backstab, Smoke Bomb, Poison Dagger, Shadow Step | Poison Cloud |

Corvo's Lockpicking (`Ability_Rogue_Lockpicking.asset`) is an exploration passive, not a combat card.

**Ability details** (from `Assets/Data/Ability_*.asset`, `AbilityExecutor.cs`, `AbilityCooldowns.cs`, `StatusEffectController.cs`):

| Ability | Target | Range | Effect | Cooldown | Rank 2 |
|---|---|---:|---|---:|---|
| Sword Slash | single | 1 | 1d8 + STR; half the damage cleaves an adjacent enemy | 0 | +3 potency |
| Shield Block (Shield Wall) | self | 0 | +4 AC until next turn; 1d6 counter when an adjacent enemy misses | 0 | +6 AC |
| War Cry | 3x3 | 1 | d20 + STR against each adjacent enemy; a hit pushes 1–2 tiles and deals 1d4 + STR | 2 | +3 potency |
| Iron Will | self | 0 | Heal 30% max HP; clears poison, frostbite, blindness | 3 | +3 potency |
| Retaliation (Lv 4) | self | 0 | 2 turns: every melee attacker takes 1d8 + STR, hit or miss | 3 | +3 potency |
| Fireball | 3x3 | 4 | 2d6 + INT; ground burns 2 rounds | 0 | +3 potency |
| Frostbite | single | 4 | 1d6 + INT; halves movement 2 turns; ices the ground | 0 | +3 potency |
| Mana Shield | self | 0 | 3 turns; absorbs the next hit completely | 3 | absorbs two hits |
| Blink | tile | 7 | Teleport without provoking | 2 | range 9 and Advantage on the next attack |
| Arcane Chains (Lv 4) | single | 4 | 1d6 + INT; target cannot move 2 turns | 3 | +3 potency |
| Backstab | single | 1 | 2d6 + DEX; doubled against a blinded target or straight out of Shadow Step (which also gives Advantage) | 0 | +3 potency |
| Smoke Bomb | 3x3 | 3 | Blinds everything in the area 1 turn (Disadvantage) | 2 | 5x5 and one turn longer |
| Poison Dagger | single | 1 | 1d4 + DEX, then 1d6 poison per turn for 2 turns | 0 | +3 potency |
| Shadow Step | tile | 3 | Teleport without provoking; Advantage on the next attack | 0 | range 5 |
| Poison Cloud (Lv 4) | 3x3 | 3 | 1d4 to each enemy; poisoned 3 turns | 3 | +3 potency |

**Animation and VFX triggers.** On use, `AbilityExecutor` first tries an Animator trigger named after the ability id. Sir Roland's controller (`HeroKnightAnimator`) has `warrior_sword_slash`, `warrior_shield_block`, `warrior_war_cry` and `warrior_iron_will`; Elira's (`Hero_Mage_Elira_Animator`) has `mage_fireball`, `mage_frostbite`, `mage_mana_shield`, `mage_blink` and `DrinkPotion`. Corvo's controller has only the generic triggers. Without a matching trigger the hero plays `CastSpell` (self-targeted and spell abilities) or `Attack`. Generic triggers on all three hero controllers: `IsMoving`, `Attack`, `CastSpell`, `TakeHit`, `Die`, `Victory` (Roland and Elira also have `IsWalking`). Retaliation, Arcane Chains and Poison Cloud use the generic fallback. Procedural VFX in `AbilityVfx.cs`: Blink, Shadow Step, Malakor's teleport, War Cry push and slide, Fireball, explosions, ranged bolts, Frostbite, potion heal, Mana Shield absorb, status auras and camera shake.

**Levels** (`PlayerProgressionManager`, `PlayerUnit.MaxLevel = 5`, no XP):

| Level | Reached by |
|---|---|
| 2 | Defeating the Cursed Commander, or opening the Forest Path secret route as the rogue |
| 3 | Defeating Shadow Mage Malakor |
| 4 | Finishing the Tower challenge (the fifth ability unlocks on a fifth action-bar card) |
| 5 | Defeating the Gargoyle King (recorded without the picker, since the ending takes over) |

Each level-up offers three cards: Hero's Resilience (+5 max HP and a full heal), Attribute Bonus (+1 primary attribute, which raises d20 rolls and damage), or upgrading one ability to Rank 2.

**Difficulty** (`DifficultySettings`, chosen on hero selection and saved): Easy gives the hero +2 AC and one free reroll per fight; Normal is the rules as written; Hard gives every enemy +2 to hit.

**Rune of Reroll.** Hero attack rolls and dialogue checks run through `RerollableRoll`. Only a failed hero roll pauses, offering [Continue] or [Use Reroll Scroll]; successes and enemy rolls never pause. Saves start with one reroll scroll.

### 3.2 Campaign map and world structure

Seven scenes connected by `DoorTeleporter`s, each arriving at a named `StartSpawnPoint`:

| Zone | Scene | Exits (target spawn) | Content |
|---|---|---|---|
| 1 Oakhaven | `Zone_1_VillageAndCellar` | Forest Path (`Spawn_From_Village`); two in-scene doors for Barnaby's cellar hatch | Baldur's forge, Barnaby's tavern and wine cellar fight, Mirabel, Othelia, the village cat, lockpick practice chest, two lore notes placed at runtime |
| 2 Forest Path | `Zone_2_ForestPath` | Village; Courtyard; **secret route to the Library** (`Spawn_From_SecretPath`) | Zombie encounter, Forest Ambush (second encounter), swamp blossoms, the secret gate |
| 3 Castle Courtyard | `Zone_3_CastleCourtyard` | Forest Path; Castle Hall | Cursed Commander (32x32 grid), his skeleton guard |
| 4 Library | `Zone_4_Library` | Forest Path (secret route back); Castle Hall | Shadow Mage Malakor (20x20 grid) and his mirror image |
| 5 Castle Hall | `Zone_5_CastleHall` | Courtyard, Library, Tower, Throne Room | Safe hub: Rune Shrine save point, Pip the peddler, Sir Aldric's riddle vault [B]; no combat |
| 6 Treasure Tower | `Zone_6_Tower` | Castle Hall | Rune pillars, pressure plates, mimic guardian, Othelia's signet ring and the Giant's Elixir (+30 max HP) |
| 7 Throne Room | `Zone_7_ThroneRoom` | Castle Hall | The Gargoyle King (20x20 grid), lava cracks in phase 2, the ending |

- **Gate rule:** `GameManager.CanEnterLocation` seals only the Throne Room, which opens when both the Commander and Malakor are defeated (or resolved peacefully through dialogue). The expedition map follows the same rule.
- **Secret route:** the Forest Path gate (`LockpickInteraction`, DC 13 lock) opens a shortcut to the Library. The rogue picks it in a timing minigame; the warrior can force it with d20 + STR against DC 15 and the mage can read its rune with d20 + INT against DC 14, retrying after a failure (the warrior takes 1 damage per failed try) [B]. Vili's own gate models are used: locked medieval gate and open stone archway.
- **Expedition map** (`DungeonMapUIController`, M key) shows all seven zones and the current one.

### 3.3 Combat system and grid

- **Grid:** `GridManager` builds a grid of `GridTile`s at combat start, centred on the fight (`fitGridAroundPlayer`), tile size 1.6 world units, any size from 1x1 to 32x32 (`DungeonRoomController.MaxGridSize = 32`). Sizes used by the scenes: cellar 12x12, both Forest Path encounters 12x12, Courtyard 32x32, Library 20x20, Tower 12x12, Throne Room 20x20; village brawls use 12x12. Tile cover comes from obstacle height: low props give half cover (+2 AC against ranged attacks), tall ones give full cover (block ranged attacks).
- **Layers:** 6 GridTile, 7 Obstacle, 8 Interactable, 9 Unit.
- **Turn state machine** (`TurnState` in `GameEnums.cs`, driven by `TurnManager`): `PlayerTurn` → `EnemyTurn` → `ResolveAbilities` → back, ending in `Victory` or `Defeat`. At combat start every unit rolls initiative, d20 + DEX for the hero or d20 + the enemy's initiative bonus, and the order is logged. End Turn waits while a reroll choice is open.
- **Core rules:** `d20 + bonus >= AC/DC`; natural 20 doubles damage; natural 1 misses automatically. Moving away from an adjacent enemy provokes a free attack, except with Blink and Shadow Step.
- **Terrain** (`CombatTerrain`): Fireball ground burns 2 rounds for 1d4 per turn; Frostbite ice makes a unit starting there pass DEX DC 10 or not move that turn. Every real fight places one explosive barrel (`ExplosiveBarrel`): half cover, blows up when hit, caught in a Fireball or reached by flames, 2d6 fire to everyone in its 3x3 area and burning ground for 2 rounds; barrels chain.
- **Encounter density:** `DungeonRoomController` and `TurnManager` limit regular encounters to one elite (max HP 25 or more, or attack damage 5 or more) or at most two weaker enemies; boss rooms are exempt. Boss reinforcements are capped at one living add.
- **Victory loot:** `TurnManager.AwardCombatVictoryScrap()` pays a random 2–10 scrap (inclusive) for every real victory; village brawls pay none. `Resources/LootDropTable.asset` adds world drops: 3–8 gold and a 25% potion chance per enemy, 25 gold and a guaranteed potion per boss, a 50% potion chance per chest.
- **Defeat:** "Try Again" restarts the fight; "Return to Village" resets the room and travels back to Oakhaven. Bosses reset their phases and summons on a retry.

**Regular enemies** (scene and prefab values after `InitializeUnit`):

| Enemy | Where | HP | AC | Hit | Damage | Move | Notes |
|---|---|---:|---:|---:|---|---:|---|
| Giant Cellar Rat | Cellar (3 placed, 2 fight) | 12 | 12 | +2 | 4 | 3 | |
| Rotting Zombie | Forest Path | 20 | 11 | +2 | 4 | 3 | |
| Skeleton Archer | Forest Ambush [B] | 16 | 12 | +4 | 1d8 + 1 | 3 | Range 5, needs line of sight, keeps distance |
| Curse Cultist | Forest Ambush [B] | 18 | 11 | +3 | 4 | 3 | Heals a hurt ally 1d8 + 2, or curses the hero from 4 tiles: 1d4 and Blind 1 turn. Never activates today (section 4.3) |
| Armored Skeleton Guard | Courtyard | 20 | 12 | +2 | 4 | 3 | The Commander's guard |
| Mimic | Tower [B] | 32 | 13 | +4 | 2d6 + 1 | 3 | A hit also roots the hero for their next turn |
| Grey Wolf | none yet | 14 | prefab | +3 | prefab | 6 | Pack AI (Advantage when another wolf flanks); waits for a wolf model |
| Village NPC (brawl) | Oakhaven | 16 | 11 | +2 | 3 | 3 | Defaults; knocked out, not killed; no scrap |

### 3.4 Boss mechanics and pre-combat dialogue hooks

A boss conversation starts when the hero walks into the boss room's trigger. A passed dialogue check stores a tag that the boss reads when combat starts; class options and peaceful outcomes are `StoryFlags` and are saved.

| Boss | Stats | Mechanics | Dialogue checks and tags |
|---|---|---|---|
| **Cursed Commander** (Courtyard, 32x32) | 54 HP, AC 16, +4 to hit, 1d8 + 2, move 2, initiative +1 | Calls his skeleton guard in at 50% HP (one add maximum) | `[DC 13 Soldier's Honor]` → tag `SoldiersHonor`: AC −2 for the first 2 rounds. Baldur's `[Lore]` tip → tag `CommanderArmorWeakened`, same effect. Warrior: honour duel, DC 13 → flag `CommanderHonorDuel` (the guard stays out). Any class: "I release you from your oath, Sir Gareth", DC 15 → flag `CommanderReleased` (no fight, half the chest gold). |
| **Shadow Mage Malakor** (Library, 20x20) | 44 HP, AC 13, +5 to hit, shadow bolt 2d4 + 3 at range 4, move 3, initiative +2 | Keeps his distance; teleports when hit, at most every other turn; casts Mirror Image (one decoy, 18 HP, that also fires weaker bolts) | `[DC 14 Arcane Rebuke]` → tag `ArcaneHeresy`: the true Malakor is revealed. Mage: "your binding holds only the loyal", DC 14 → flag `MalakorShaken` (no mirror image, true form shown). Any class: "[Mercy]", DC 15 → flag `MalakorSpared` (no fight; at the throne he helps or betrays on a coin flip, remembered as `MalakorHelped` or `MalakorBetrayed`). |
| **The Gargoyle King** (Throne Room, 20x20) | 66 HP, AC 15, +5 to hit, 2d6 + 2, move 2 | Phase 1: marks a 3x3 area under the hero in red that quakes on his next turn (2d6, never hurts him). Phase 2 at 50% HP, Stone Form: +3 AC, golem model swaps to stage 2, lava cracks open and the lights turn red, and his petrifying gaze every other turn roots the hero unless they pass CON DC 13 (rerollable); the gaze comes on quake turns so a marked area can always be left. "Show the Queen's Ring" (Stone Form, once) breaks his stone armour for 2 turns. | `[DC 16 Intimidation]` → tag `GargoyleKingIntimidated`: his damage −3 for the first 3 rounds. Rogue: lift his crown, DC 15 → flag `CrownStolen` (AC −2). |

The ending (`ThroneRoomEnding`) breaks the curse, shows Othelia at the hall door, a short closing dialogue with `BossChoices.BuildEpilogue()` lines, a stats screen (turns, natural 20s, gold, falls), credits, and returns to the main menu. The main quest names the final boss "the Petrified King"; in code and dialogue he is "The Gargoyle King" (`GargoyleKingBoss`).

### 3.5 Quests, economy and dialogue trees

**Quests** (`Assets/Data/Quests/*.asset` and `MainQuest.cs`):

| Quest | Giver | Objective | Reward | Bonus |
|---|---|---|---|---|
| Break the Castle's Curse (main) | none, active from the start | Commander → Malakor → King | Ending | |
| Cellar Pests | Innkeeper Barnaby | Slay 2 giant cellar rats | 30 gold, 2 Small Health Potions | +15 gold after a passed Persuasion DC 13 (45 total) |
| Herbs for the Healer | Mirabel | Gather 3 Swamp Blossoms on the Forest Path | 30 gold, Poison Vial | Greater Health Potion after a passed Nature Lore DC 10 |
| Scrap for the Forge | Baldur | Collect 5 scrap metal | 50 gold, Sharpened Blade | |
| The Lost Signet Ring | Elder Othelia | Bring back her signet ring from the Tower | 50 gold, Rune of Fate | Carrying the ring, or having turned the quest in, enables "Show the Queen's Ring" against the King |

Quest items and the scrap an open Baldur request still needs cannot be sold.

**Baldur's forge** (`ShopManager`, `[ACTION_OPEN_SHOP]`): scrap sells at 1 scrap = 10 gold. Buy tab: Small Health Potion 15g (heals 15), Greater Health Potion 25g (heals 35), Poison Vial 30g (+5 damage on the first hit of the next fight), Rune of Fate 75g (one reroll), Sharpened Blade and Runic Armor at tiered prices of 50g, 75g, then 100g for +1 permanent damage or +1 permanent AC each, up to +3. Sell prices come from each item asset (for example Small Potion 7g, Greater Potion 12g).

**Pip the trapped peddler** (Castle Hall) [B]: Scroll of Warding 60g (a two-hit mana shield when the next fight starts), Scroll of Embers 50g (2d4 to every enemy when the next fight starts), Rune of Fate 60g, Greater Health Potion 30g. **Sir Aldric's vault** [B]: answering his riddle with "Footsteps" opens the Queen's treasury (80 gold and a Rune of Fate).

**Dialogue structure.** `DialogueNodeSO` assets (26 in `Assets/Data/Dialogues`) hold a speaker, text and options. Each `DialogueOption` can require a check (`targetDC`, `skillCheckDescription`), branch to success and failure nodes, and carry a tag in its `combatDebuffTag` field. `HeroAttributes` reads which attribute a check names ("Intimidation / Strength", "Persuasion (Charisma/Agility)") and uses the hero's best match. Castle Hall conversations (`ScriptedNpc`) and boss class options (`BossChoices`) are built in code. Conversations exist for Baldur (5 nodes), Barnaby (5), Mirabel (4), Othelia (3), the Commander (3), Malakor (3), the King (3), Pip and Sir Aldric.

**Action tags** handled by `DialogueActionTrigger` and its callers:

| Tag | Effect |
|---|---|
| `[ACTION_OPEN_SHOP]`, `[ACTION_OPEN_BLACKSMITH]` | Open Baldur's shop |
| `[ACTION_ACCEPT_QUEST:<id>]`, `[ACTION_ACCEPT_QUEST:<id>:bonus]` | Accept a quest, optionally with the bonus reward |
| `[ACTION_COMPLETE_QUEST:<id>]` | Turn a quest in |
| `[ACTION_CLOSE_DIALOGUE]` | End the conversation |
| `[ACTION_BUY:<item_id>:<price>]` | Buy from Pip (or branch to "not enough gold") [B] |
| `[ACTION_OPEN_VAULT]` | Open Sir Aldric's vault [B] |
| `[ACTION_FLAG:<Name>]` | Set a story flag [B] |
| `[ACTION_START_FIGHT]` | Start a brawl with a village NPC |
| Plain tags (`SoldiersHonor`, `CommanderArmorWeakened`, `ArcaneHeresy`, `GargoyleKingIntimidated`) | Combat modifiers stored as story flags and read by the boss |

**Lore** (`LoreTexts.cs`) [B]: fourteen notes, two per zone, from "Notice Nailed to the Well" to "The King's Last Entry". They tell how King Aldred asked his court mage Malakor for immortality, how Sir Gareth swore to hold the gates forever, and how Queen Isolde kept her ring and fled to Oakhaven. Read notes stop glowing (story flag).

### 3.6 HUD and screens

Canvases use a 1920x1080 reference resolution with match 0.5. The village scene contains `Hero_Status_Card` (crest, HP bar, gold, scrap, quick potion [Q]), `Zone_Indicator_Banner`, `Quest_Tracker_Card`, `CombatActionBar` (four ability cards, a fifth from level 4, End Turn and Move buttons, cooldown numbers) and `DiceModalPanel` (tumbling d20, capped at 2 seconds). Other screens: combat stats HUD with enemy cards, dialogue, Baldur's shop, expedition map, quest journal, level-up modal, lockpicking minigame, defeat screen, ending, story panel, and pause menu [B].

---

## 4. Assets & Test Verification Manifest

### 4.1 ScriptableObject inventory

| Type | Assets | Location |
|---|---|---|
| `CharacterClassSO` (heroes) | 3: Sir Roland, Scholar Elira, Shadow-Corvo | `Assets/Data` |
| `AbilitySO` | 16: five per class plus Lockpicking | `Assets/Data` |
| `ItemSO` | 13: Small Health Potion, Greater Health Potion, Poison Vial, Rune of Fate, Sharpened Blade, Runic Armor, Scrap Metal, Othelia's Signet Ring, Swamp Blossom, Legendary Giant's Elixir; Scroll of Warding and Scroll of Embers [B] in `Resources/Items` | `Assets/Data`, `Assets/Resources/Items` |
| `QuestSO` | 4 side quests (the main quest is built at runtime) | `Assets/Data/Quests` |
| `DialogueNodeSO` | 26 | `Assets/Data/Dialogues` |
| `ZoneMusicSO` | 1: `ZoneMusicConfig` (village → Village; Forest, Courtyard, Library, Castle Hall, Tower, Crown Hall → CastleAdventure; cellar → CellarCombat; Commander, Malakor, King phase 1 and 2 → their own tracks) | `Assets/Data` |
| `LootDropTableSO` | 1: `LootDropTable` | `Assets/Resources` |
| `HeroWeaponMountsSO` | 1: `HeroWeaponMounts` (mage staff in the right hand, rogue daggers in both) | `Assets/Resources` |
| `UITheme` | 1: fantasy panel, button, bar, crest and icon sprites | `Assets/Resources` |
| `PlayerDataSO` | none saved; created at runtime as `PlayerDataSO.Session` | |

**Enemy data** has no ScriptableObject type: enemies are `EnemyUnit` components on prefabs (`Assets/PREFABS/Enemies`) and scene objects, and bosses set their stats in code (section 3.3).

**Music assets** (`Assets/Music`, 7 MP3s): `VillageSong`, `Castle_adventure_song`, `Cellar_combat_music`, `CursedCommander_Combat_music`, `Malakor_combat_music`, `1_Combat_GargoyleKing_music`, `2_Combat_GargoyleKing_music`. `Assets/Resources/Music` holds only a README describing the per-zone `Explore_<Location>.mp3` slots.

**Other assets** at `1b1466e`: 140 prefabs, 138 FBX models, 159 materials, 133 PNG/JPG textures, 14 animator controllers, 7 scenes, 14 shaders; the `Assets` folder is 506 MB on disk. Unused low-poly pack files (82.7 MB) and the old Barnaby/Mirabel FBX files were deleted on the branch at Vili's request (`05da35a`, `1b1466e`); the rest of the pack is still referenced by prefabs and materials.

### 4.2 Verification status

Both suites ran for this audit in a separate worktree at `1b1466e` (`D:\Unity\castle-dossier`), in batch mode, so Vili's open editor was not touched.

**EditMode** (`-runTests -testPlatform EditMode`, 2026-10-04 16:03 UTC):

| Assembly | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| `CastleOfTheD20.Tests.EditMode` (game) | 263 | **263** | 0 | 0 |
| `UnityCliBridge.Editor` (package) | 54 | 54 | 0 | 0 |
| `UnityCliBridge.Tests` (package) | 536 | 520 | 11 | 5 |

The game's 263 tests in 27 fixtures all pass in 6.6 seconds. The 11 failures are all in the unity-cli bridge package (runtime-change persistence, eval assembly loading, input notifications, bridge integration, Addressables), which needs a live editor and auth token; they fail in batch mode every time and are not game code.

Game test fixtures: VillageQuestTests (27), CriticalReviewPackage2Tests (20), CriticalReviewPackage3Tests (19), BaldurShopTests (18), SpecAlignmentTests (18), CriticalReviewContentTests (17), ReviewFixesTests (17), CoreRulesTests (13), LockpickMinigameTests (13), NpcFightOptionTests (12), CombatRefactorTests (11), PotionAndEnemyFocusTests (10), DungeonMapUITests (9), CombatStatsHUDTests (8), DiceRollAnimationTests (6), ZoneTransitionTests (6), EncounterGridTests (5), MilestoneLevelUpTests (5), UIThemeTests (5), CombatMoveButtonTests (4), CriticalReviewUsabilityTests (4), QuestHUDTrackerTests (4), BossDialogueTriggerTests (3), GridWalkTests (3), SFXPoolTests (3), ZoneSceneManagerTests (2), CombatScrapDropTests (1).

**PlayMode** (`MainPathSmokeTests`, `-testPlatform PlayMode -testFilter CastleOfTheD20.Tests.PlayMode`, 2026-10-04 16:03–16:18 UTC): **2 of 3 passed; 0 errors logged in any run.**

| Test | Result | Duration | Fights won (hero turns) |
|---|---|---:|---|
| `Mage_MainPath` | **Passed**, 33/33 steps | 188 s | cellar 3, zombie 1, ambush 3, Commander 10, Malakor 11, mimic 5, King 13 |
| `Warrior_MainPath` | **Passed**, 33/33 steps | 325 s | cellar 4, zombie 5, ambush 6, Commander 16, Malakor 18, mimic 12, King 25 |
| `Rogue_MainPath` | **Failed**, 28/33 steps | 360 s | cellar 10, zombie 8, ambush 3, Commander 18, Malakor 13, mimic 11; **lost to the Gargoyle King after 30 turns** |

Each run starts a New Adventure, plays the cellar, both Forest Path fights, the Commander, saves at the Castle Hall shrine, talks to Pip, beats Malakor, solves the Tower pillars and the mimic, and fights the King to the ending. The rogue run failed on dice, not on a code error: the scripted bot uses only plain Backstab, which needs a natural 15 or better against the King's Stone Form AC 18 (15 + 3) with Corvo's +3, and a natural-20 critical for 26 damage dropped Corvo from 18 of 25 HP. The four steps after the King (room cleared, King counted, ending, main quest) failed only because that fight was lost. The review thread's earlier smoke run on the same code (`afe7775`) took all three classes to the ending, so the outcome depends on the dice; it does point at the rogue's late-game survivability (still 25 max HP in this run) and at Shadow Step's Advantage being needed against the King.

There is no separate E2E suite; the PlayMode smoke run is the project's end-to-end check. The old `TestResults_E2E.*` files were removed in the cleanup (`4c182e5`).

### 4.3 Known issues found during the audit

- **The Forest Ambush fights only the Skeleton Archer; the Curse Cultist never joins.** The archer is built with attack damage 5 (`Assets/Scripts/Editor/ReviewContentBuilder.cs:359`), and the encounter cap treats any enemy with attack damage 5 or more as an elite and then allows one enemy (`Assets/Scripts/World/DungeonRoomController.cs:499-508`). The smoke run confirms it: the cultist's name never appears in the PlayMode log, although the report line is labelled "archer + cultist". Fix options: give the archer flat damage below 5 (its real damage is 1d8 + 1), exempt keyed second encounters from the cap, or raise the elite threshold.
- **Per-zone music slots are empty.** `Assets/Resources/Music` has only its README, so every castle zone plays `Castle_adventure_song` until `Explore_<Location>.mp3` files are added.
- **Grey Wolf is unused.** `WolfUnit` and its pack AI exist, but no scene places a wolf (no wolf model yet).
- **Player Settings still say "My project" / "DefaultCompany" (version 0.1.0).** The browser tab title and the `PlayerPrefs` storage location of a WebGL build come from these.
- **Git LFS mismatch:** `.gitattributes` sends `*.fbx` and `*.png` through LFS, but 162 files under `Assets/LowPolyVillageAll` are stored as ordinary git blobs, so every fresh checkout reports "files that should have been pointers". Harmless for the game; `git lfs migrate import` on those paths would tidy it.
- **Project notes that describe things the code does not have:** `CLAUDE.md` describes `Exploration`/`Combat` input action maps and a `_CoreManagers` object; the game uses `GameInput` polling and per-scene `Managers` objects (section 2.2–2.3). It calls the final boss "Kivettymiskuningas"; the code calls him "The Gargoyle King". `Assets/InputSystem_Actions.inputactions` and `Assets/Settings/Mobile_RPAsset.asset` are unused defaults.

### 4.4 Git reference

| | Value |
|---|---|
| Audited branch | `claude/critical-review-fixes` |
| Audited commit | `1b1466e6483801e63768ab9273d24411d3918914` (2026-10-04 19:00 +0300) |
| Local `master` before this dossier | `c0f9df5` (2026-10-04 17:07 +0300) |
| GitHub `master` | `5b697387213fcaaf98a63d07c4a352d44f21d81f` |
| Diff `master` → branch | 324 files changed, 19,599 insertions, 122,538 deletions (most deletions are the removed pack files and scenes); code and tests alone: 74 files, 4,481 insertions, 626 deletions |
