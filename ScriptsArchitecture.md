# CODE ARCHITECTURE & SCRIPT BLUEPRINT: CASTLE OF THE D20

**Project:** Castle of the D20 (Castle of Dice)  
**Engine:** Unity (Universal Render Pipeline / WebGL)  
**Architecture:** Event-driven State Machine with ScriptableObjects  
**Language Standard:** C# (Code and Game UI in English)  

---

## 1. CORE & DICE SYSTEM

| Script | Type | Responsibility & Implementation Details |
| :--- | :--- | :--- |
| **`DiceSystem.cs`** | `Static` / `Singleton` | Core rule engine. Handles D20 rolls ($1..20$), applies character bonuses, evaluates vs. DC/AC, handles Nat 20 / Nat 1 logic, and triggers `OnDiceRolled(DiceResult result)`. |
| **`DiceResult.cs`** | `Struct` / `Class` | Data container for roll results: `rawRoll`, `bonus`, `finalTotal`, `targetDC`, `isCriticalSuccess`, `isCriticalFail`, `isSuccess`. |
| **`GameEnums.cs`** | `Enums` | Central definitions: `TurnState`, `AdvantageType`, `CharacterClass`, `AbilityTargetType`, `StatusEffectType`, `QuestState`. |
| **`GameManager.cs`** | `MonoBehaviour` (Persistent) | Manages high-level game state (Village vs. Dungeon wings), scene transitions, and game flow between exploration and combat. |

---

## 2. COMBAT & GRID MANAGEMENT

| Script | Type | Responsibility & Implementation Details |
| :--- | :--- | :--- |
| **`TurnManager.cs`** | `MonoBehaviour` | Combat State Machine (`PlayerTurn`, `EnemyTurn`, `ResolveAbilities`, `Victory`, `Defeat`). Coordinates turn order, actions, and end-of-turn events. |
| **`GridManager.cs`** | `MonoBehaviour` | Generates and manages the tactical grid, coordinate mapping, pathfinding distance checks, and tile occupation. |
| **`GridTile.cs`** | `MonoBehaviour` | Individual grid cell. Handles mouse hover, selection events, and visual state changes (walkable, blocked, attack range). |
| **`GridTargetHighlighter.cs`** | `MonoBehaviour` | Highlights ability target areas (e.g., Mage's 3x3 Fireball area, Warrior's single target / knockback vector). |

---

## 3. UNITS & STATUS EFFECTS

| Script | Type | Responsibility & Implementation Details |
| :--- | :--- | :--- |
| **`CombatUnit.cs`** | `MonoBehaviour` (Base) | Base class for all combatants: current/max HP, Armor Class (AC), initiative, `TakeDamage()`, `Heal()`, `ApplyDebuff()`, and death triggers. |
| **`PlayerUnit.cs`** | `MonoBehaviour` : `CombatUnit` | Player hero instance. Holds active `CharacterClassSO`, equipment modifiers (+1 DMG / +1 AC), and 4 class ability slots. |
| **`EnemyUnit.cs`** | `MonoBehaviour` : `CombatUnit` | Base enemy AI (skeletons, zombies, wraiths). Finds closest player target, checks attack range, and executes turn action. |
| **`StatusEffectController.cs`** | `MonoBehaviour` | Manages per-turn debuffs/buffs: Poison (d6 damage / 3 turns), Frostbite (movement halved), Blindness (Smoke Bomb), and Mana Shield absorb charges. |

---

## 4. ABILITIES & SCRIPTABLEOBJECTS

| Script | Type | Responsibility & Implementation Details |
| :--- | :--- | :--- |
| **`AbilitySO.cs`** | `ScriptableObject` | Ability template: ability name, icon, range, area size, target filter, animation trigger, base value, and DC check requirements. |
| **`CharacterClassSO.cs`** | `ScriptableObject` | Class definitions: Warrior (Sir Roland), Mage (Elira), Rogue (Corvo). Holds base stats, attributes, and references to the 4 specific `AbilitySO` assets. |
| **`AbilityExecutor.cs`** | `MonoBehaviour` | Execution logic for individual abilities:<br>• **Warrior:** *Sword Slash, Shield Block (+3 AC), War Cry (knockback), Iron Will (30% heal)*<br>• **Mage:** *Fireball (3x3 AOE), Frostbite (slow), Mana Shield, Blink (teleport)*<br>• **Rogue:** *Backstab (2x bonus), Smoke Bomb, Poison Dagger, Lockpicking* |

---

## 5. BOSS MECHANICS & AI

| Script | Type | Responsibility & Implementation Details |
| :--- | :--- | :--- |
| **`CursedCommanderBoss.cs`** | `MonoBehaviour` : `EnemyUnit` | Courtyard Boss: Heavy shield (high AC). Spawns 2 skeleton adds when hitting 50% HP. Reacts to pre-combat dialogue debuffs (-2 AC for 2 turns). |
| **`ShadowMageMalakorBoss.cs`** | `MonoBehaviour` : `EnemyUnit` | Library Boss: Teleports across grid corners and spawns decoy illusion copies. Handles arcane dispel mechanics from dialogue checks. |
| **`GargoyleKingBoss.cs`** | `MonoBehaviour` : `EnemyUnit` | Crown Hall Final Boss: Two-phase fight. Phase 1: Rockfalls & earthquake AOE. Phase 2: Stone Form (spell reflection). Weakened by pre-combat Intimidation. |

---

## 6. DIALOGUE & SKILL CHECKS

| Script | Type | Responsibility & Implementation Details |
| :--- | :--- | :--- |
| **`DialogueNodeSO.cs`** | `ScriptableObject` | Dialogue branch node: speaker name, portrait, text lines, and a collection of `DialogueOption` choices. |
| **`DialogueOption.cs`** | `Class` / `Serializable` | Branch selection data: option text, required D20 check (`requiresCheck`, `dc`, `statType`), success/failure target nodes, and combat debuff hooks. |
| **`DialogueController.cs`** | `MonoBehaviour` | Runs conversation flows, passes checks to `DiceSystem`, updates UI, and triggers quest state changes or combat modifiers. |

---

## 7. VILLAGE, SHOP & QUESTS

| Script | Type | Responsibility & Implementation Details |
| :--- | :--- | :--- |
| **`InventoryManager.cs`** | `MonoBehaviour` (Singleton) | Tracks player gold, scrap metal/ore, health potions, and quest items (Signet Ring, Poison Vial, Reroll Rune Stone). |
| **`ShopManager.cs`** | `MonoBehaviour` | Blacksmith Baldur's economy: converts scrap metal to gold (1 scrap = 10 gold), sells health potions (25 gold), sharpened weapons (+1 DMG, 60 gold), and runic armor (+1 AC, 100 gold). |
| **`ItemSO.cs`** | `ScriptableObject` | Definition for items: consumables, equipment upgrades, quest materials, and usable items. |
| **`QuestManager.cs`** | `MonoBehaviour` | Tracks progress for the 3 village side quests (Cellar Rats, Lost Signet Ring, Swamp Herbs) and applies rewards. |
| **`QuestSO.cs`** | `ScriptableObject` | Quest definitions: target counters, dialogue triggers, base rewards, and bonus rewards from D20 negotiation. |

---

## 8. EXPLORATION & WORLD INTERACTION

| Script | Type | Responsibility & Implementation Details |
| :--- | :--- | :--- |
| **`Interactable.cs`** | `MonoBehaviour` (Abstract) | Base interaction script for NPCs, chests, levers, loot pickups, and doorways. Handles player proximity and mouse click triggers. |
| **`LockpickInteraction.cs`** | `MonoBehaviour` : `Interactable` | Locked chests and secret doors. Triggers Rogue skill check (`d20 + bonus >= DC`) to unlock without keys. |
| **`DungeonRoomController.cs`** | `MonoBehaviour` | Controls room state: door locks during combat encounters, enemy group activation, and secret path reveals. |

---

## 9. USER INTERFACE (UI CONTROLLERS)

| Script | Type | Responsibility & Implementation Details |
| :--- | :--- | :--- |
| **`DiceUIController.cs`** | `MonoBehaviour` | Visual 3D/2D D20 roll animation, roll sound effects, numerical result display, and critical glow indicators (Nat 20 gold/green, Nat 1 red). |
| **`CombatUIController.cs`** | `MonoBehaviour` | 4 ability action buttons, current turn indicator, floating health bars, combat log, and "End Turn" button. |
| **`DialogueUIController.cs`** | `MonoBehaviour` | Dialogue window, speaker portrait, text typing effect, interactive choice buttons, and integrated D20 check popups. |
| **`ShopUIController.cs`** | `MonoBehaviour` | Blacksmith shop interface: buy list, scrap metal selling interface, gold tracker, and exit button. |
| **`PlayerHUD.cs`** | `MonoBehaviour` | Persistent top bar HUD: current HP bar, gold count, potion hotbar, and active quest markers. |

---

## 10. IMPLEMENTATION ROADMAP (ANTIGRAVITY)

[Phase 1: Core Engine]

GameEnums.cs

DiceResult.cs

DiceSystem.cs (Unit tests / verify DC logic)

[Phase 2: Character & Ability Data]
4. AbilitySO.cs
5. CharacterClassSO.cs
6. ItemSO.cs

[Phase 3: Turn-Based Combat Loop]
7. GridTile.cs & GridManager.cs
8. CombatUnit.cs -> PlayerUnit.cs & EnemyUnit.cs
9. TurnManager.cs (PlayerTurn -> EnemyTurn state flow)
10. AbilityExecutor.cs & StatusEffectController.cs

[Phase 4: Exploration, Dialogue & Village]
11. DialogueNodeSO.cs & DialogueController.cs
12. Interactable.cs & LockpickInteraction.cs
13. InventoryManager.cs & ShopManager.cs
14. QuestSO.cs & QuestManager.cs

[Phase 5: Boss AI & Polish]
15. CursedCommanderBoss.cs, ShadowMageMalakorBoss.cs, GargoyleKingBoss.cs
16. UI Controllers (DiceUIController, CombatUIController, DialogueUIController)