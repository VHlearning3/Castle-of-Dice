using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// The HUD in real fights, per class: a New Adventure in Oakhaven, then both Forest Path fights. After
    /// every change the test reads what the screen shows (hero card, stats block, gold, scrap, potions, zone
    /// banner, ability bar, defeat panel) and compares it with the hero. The ambush is lost once on purpose
    /// to check the defeat panel and Try Again. Writes a report (SMOKE_REPORT_DIR env var or Logs/).
    /// </summary>
    public class HudSyncPlayTests
    {
        private readonly StringBuilder report = new StringBuilder();
        private readonly List<string> errors = new List<string>();
        private bool failed;
        private string saveBackup;

        private const string SaveKey = "CastleOfDice_SaveData";

        [SetUp]
        public void BackupSave()
        {
            saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
        }

        [TearDown]
        public void RestoreSave()
        {
            Time.timeScale = 1f;
            Application.logMessageReceived -= CollectErrors;
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Warrior_HudFollowsTheHero() => Run("Character_Warrior_SirRoland");

        [UnityTest, Timeout(900000)]
        public IEnumerator Mage_HudFollowsTheHero() => Run("Character_Mage_Elira");

        [UnityTest, Timeout(900000)]
        public IEnumerator Rogue_HudFollowsTheHero() => Run("Character_Rogue_Corvo");

        private IEnumerator Run(string classAsset)
        {
            report.Clear();
            errors.Clear();
            failed = false;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CollectErrors;
            report.AppendLine($"HUD run: {classAsset}  ({DateTime.Now:yyyy-MM-dd HH:mm})");

            // 1. New Adventure in Oakhaven
            SceneManager.LoadScene("Zone_1_VillageAndCellar");
            yield return Frames(10);
            CharacterClassSO cls = LoadClass(classAsset);
            MainMenuController menu = MainMenuController.Instance ?? UnityEngine.Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Step("Main menu found", menu != null && cls != null);
            if (menu != null && cls != null) menu.SelectCharacterClass(cls);
            yield return Frames(5);
            yield return WaitForLoad();
            yield return Frames(10);
            StoryPanelUI.CloseIfOpen();
            yield return Frames(2);

            Step($"Hero is {cls?.CharacterName}", Hero() != null && Hero().CharacterClass == cls);
            CheckHud("Village");

            // 2. Forest Path: the HUD must let go of the village hero and follow the forest one
            yield return Travel("Zone_2_ForestPath");
            PlayerUnit hero = Hero();
            Step("HUD tracks the Forest Path hero", PlayerHUD.Instance != null && hero != null && PlayerHUD.Instance.TrackedPlayer == hero);
            CheckHud("Forest Path, arrived");

            // 3. Damage outside of combat (a trap) shows at once
            if (hero != null)
            {
                hero.TakeDamage(3);
                yield return Frames(2);
                CheckHud("After 3 trap damage");
                hero.Heal(999);
                yield return Frames(2);
            }

            // 4. The zombie: real enemy turns, the hero is not healed while the HUD is compared
            Time.timeScale = 3f;
            yield return Fight("Forest zombie", r => r.roomLocation == "Forest" && string.IsNullOrEmpty(r.roomKey), false);
            CheckHud("After the zombie fight");

            // 5. The ambush: lost once on purpose, then Try Again and win
            DungeonRoomController ambush = FindRoom(r => r.roomKey == "ForestAmbush");
            Step("Ambush room found", ambush != null);
            if (ambush != null && Hero() != null)
            {
                ambush.BeginEncounter(Hero());
                yield return Frames(3);
                if (DialogueController.Instance != null && DialogueController.Instance.IsInDialogue) DialogueController.Instance.EndDialogue();
                yield return Frames(3);
                Step("Ambush combat started", TurnManager.Instance != null && TurnManager.Instance.IsCombatActive);
                CheckAbilityBar("Ambush");

                hero = Hero();
                hero.TakeDamage(hero.CurrentHP + 50);
                yield return Frames(5);
                CheckHud("Hero fallen in the ambush");
                Step("Defeat panel open", DefeatPanelOpen());
                Step("HUD shows 0 HP at defeat", Label(PlayerHUD.Instance, "healthText").StartsWith("HP: 0 /"));

                InvokePrivate(DefeatUIController.Instance, "OnRetryClicked");
                yield return Frames(10);
                Step("Defeat panel closed after Try Again", !DefeatPanelOpen());
                PlayerUnit revived = PlayerHUD.FindHero();
                Step("Hero back at full HP after Try Again", revived != null && revived.IsAlive && revived.CurrentHP == revived.MaxHP);
                CheckHud("After Try Again");
                Step("Try Again restarted the ambush", ambush.CurrentState == RoomState.CombatActive
                    && TurnManager.Instance != null && TurnManager.Instance.IsCombatActive);
                Step("Only the ambush's foes are in the retried fight", OnlyRoomEnemies(ambush));

                yield return AutoBattle("Forest ambush (retry)", false);
                yield return Frames(10);
                Step("Ambush cleared", ambush.IsCleared);
                CheckHud("After the ambush");
            }

            // 6. Gold and potions change outside of combat too
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory != null)
            {
                inventory.AddGold(7);
                yield return Frames(2);
                CheckHud("After +7 gold");
                ItemSO potion = inventory.FindItemByID(ShopManager.SMALL_POTION_ID);
                if (potion != null)
                {
                    inventory.AddItem(potion, 1);
                    yield return Frames(2);
                    CheckHud("After a potion was added");
                }
            }

            // 7. The pause menu opens and closes over the HUD
            PauseMenuUI pause = PauseMenuUI.Instance;
            if (pause != null)
            {
                pause.Open();
                yield return Frames(2);
                Step("Pause menu opens", PauseMenuUI.IsPaused);
                pause.Resume();
                yield return Frames(2);
                Step("Pause menu closes", !PauseMenuUI.IsPaused);
            }

            Time.timeScale = 1f;
            Application.logMessageReceived -= CollectErrors;
            WriteReport(classAsset);
            Assert.IsFalse(failed, report.ToString());
        }

        #region HUD checks

        /// <summary>Compares every readout of the hero card with the hero and the purse.</summary>
        private void CheckHud(string when)
        {
            PlayerHUD hud = PlayerHUD.Instance;
            PlayerUnit hero = PlayerHUD.FindHero();
            if (hud == null || hero == null)
            {
                Step($"{when}: HUD and hero present", false);
                return;
            }

            var problems = new List<string>();
            Expect(problems, "HP", Label(hud, "healthText"), $"HP: {hero.CurrentHP} / {hero.MaxHP}");
            Expect(problems, "name", Label(hud, "heroNameText"), hero.UnitName);
            Expect(problems, "AC badge", Label(hud, "heroACText"), $"AC {hero.ArmorClass}");
            string classLine = Label(hud, "heroClassText");
            if (hero.CharacterClass != null && !classLine.StartsWith(hero.CharacterClass.ClassType.ToString()))
            {
                problems.Add($"class '{classLine}' (hero is {hero.CharacterClass.ClassType})");
            }

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory != null)
            {
                Expect(problems, "gold", Label(hud, "goldCounterText"), $"{inventory.CurrentGold} Gold");
                Expect(problems, "scrap", Label(hud, "scrapCounterText"), PlayerHUD.FormatScrap(inventory.ScrapMetalCount));

                // The potion flasks (bottom-left) refresh in their own Update, a frame after a change
                PotionQuickBar flasks = PotionQuickBar.Instance;
                if (flasks != null)
                {
                    ExpectFlask(problems, flasks, "smallSlot", inventory, ShopManager.SMALL_POTION_ID);
                    ExpectFlask(problems, flasks, "largeSlot", inventory, ShopManager.GREATER_POTION_ID);
                }
                else
                {
                    problems.Add("no potion flasks");
                }
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentLocation == GameLocation.Forest)
            {
                Expect(problems, "zone", Label(hud, "zoneTitleText"), "Whispering Woods");
            }

            // The stats block refreshes in its own Update: read it after forcing a rebuild
            CombatStatsHUD stats = hud.GetComponentInChildren<CombatStatsHUD>(true) ?? UnityEngine.Object.FindAnyObjectByType<CombatStatsHUD>();
            if (stats != null)
            {
                stats.SetPlayer(hero);
                stats.RefreshHero(true);
                Expect(problems, "to hit", Label(stats, "heroHitValue"), Signed(hero.PrimaryAttributeBonus));
                Expect(problems, "weapon", Label(stats, "heroWeaponValue"), Signed(hero.WeaponDamageBonus));
                Expect(problems, "armor", Label(stats, "heroArmorValue"), hero.ArmorClass.ToString());
                Expect(problems, "move", Label(stats, "heroMoveValue"), hero.MovementRange.ToString());
                string detail = Label(stats, "heroDetailText");
                if (!detail.Contains($"</color> {hero.MaxHP}")) problems.Add($"max HP line '{detail}' (hero has {hero.MaxHP})");
            }
            else
            {
                problems.Add("no stats block");
            }

            if (Label(hud, "scrapCounterText").Contains("kpl")) problems.Add("scrap counter still in Finnish");

            Step(problems.Count == 0 ? $"{when}: HUD matches the hero (HP {hero.CurrentHP}/{hero.MaxHP})"
                                     : $"{when}: HUD out of date: {string.Join("; ", problems)}", problems.Count == 0);
        }

        private void CheckAbilityBar(string when)
        {
            CombatUIController ui = CombatUIController.Instance;
            PlayerUnit hero = Hero();
            if (ui == null || hero == null)
            {
                Step($"{when}: ability bar present", false);
                return;
            }

            ui.RefreshAbilityBar();
            var problems = new List<string>();
            IReadOnlyList<AbilitySO> abilities = hero.ActiveAbilities;
            for (int i = 0; i < abilities.Count && i < ui.AbilityNames.Count; i++)
            {
                if (abilities[i] == null || abilities[i].AbilityID.IndexOf("lockpick", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string shown = ui.AbilityNames[i] != null ? ui.AbilityNames[i].text : "";
                if (shown != abilities[i].AbilityName) problems.Add($"slot {i + 1} '{shown}' (hero has '{abilities[i].AbilityName}')");
            }
            Step(problems.Count == 0 ? $"{when}: ability bar shows the hero's abilities"
                                     : $"{when}: ability bar wrong: {string.Join("; ", problems)}", problems.Count == 0);
        }

        private static void Expect(List<string> problems, string what, string shown, string expected)
        {
            if (shown != expected) problems.Add($"{what} '{shown}' (expected '{expected}')");
        }

        private static void ExpectFlask(List<string> problems, PotionQuickBar flasks, string slotField, InventoryManager inventory, string itemID)
        {
            FieldInfo slotInfo = typeof(PotionQuickBar).GetField(slotField, BindingFlags.Instance | BindingFlags.NonPublic);
            object slot = slotInfo != null ? slotInfo.GetValue(flasks) : null;
            ItemSO item = inventory.FindItemByID(itemID);
            int count = item != null ? inventory.GetItemCount(item) : 0;
            Expect(problems, slotField, Label(slot, "Amount"), "x" + count);
        }

        private static string Signed(int value) => value >= 0 ? "+" + value : value.ToString();

        private static string Label(object owner, string field)
        {
            if (owner == null) return "";
            FieldInfo info = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            TMP_Text text = info != null ? info.GetValue(owner) as TMP_Text : null;
            return text != null ? text.text : "";
        }

        private static bool DefeatPanelOpen()
        {
            DefeatUIController defeat = DefeatUIController.Instance;
            if (defeat == null) return false;
            FieldInfo info = typeof(DefeatUIController).GetField("defeatModalPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            GameObject panel = info != null ? info.GetValue(defeat) as GameObject : null;
            return panel != null && panel.activeInHierarchy;
        }

        private static bool OnlyRoomEnemies(DungeonRoomController room)
        {
            TurnManager tm = TurnManager.Instance;
            if (tm == null || room.roomEnemies == null) return false;
            int enemies = 0;
            foreach (CombatUnit unit in tm.ActiveUnits)
            {
                if (!(unit is EnemyUnit)) continue;
                enemies++;
                if (!room.roomEnemies.Contains(unit.gameObject)) return false;
            }
            return enemies > 0;
        }

        #endregion

        #region Fights

        private IEnumerator Fight(string label, Predicate<DungeonRoomController> which, bool healHero)
        {
            DungeonRoomController room = FindRoom(which);
            if (room == null)
            {
                Step($"{label}: encounter found", false);
                yield break;
            }

            room.BeginEncounter(Hero());
            yield return Frames(3);
            if (DialogueController.Instance != null && DialogueController.Instance.IsInDialogue)
            {
                DialogueController.Instance.EndDialogue();
                yield return Frames(3);
            }

            CheckAbilityBar(label);
            yield return AutoBattle(label, healHero);
            yield return Frames(10);
            Step($"{label}: room cleared", room.IsCleared);
        }

        /// <summary>
        /// Plays the fight with the first ability. The HUD is compared with the hero at the start of every
        /// hero turn, so the damage the enemies dealt must already show. The hero is topped up only when low,
        /// after the comparison, so the run checks the HUD and not the balance.
        /// </summary>
        private IEnumerator AutoBattle(string label, bool healHero)
        {
            float until = Time.realtimeSinceStartup + 400f;
            int heroTurns = 0;
            bool sawDamage = false;
            yield return Frames(3);

            while (TurnManager.Instance != null && TurnManager.Instance.IsCombatActive && Time.realtimeSinceStartup < until)
            {
                if (RerollableRoll.IsAwaitingDecision) RerollableRoll.Accept();
                StoryPanelUI.CloseIfOpen();

                PlayerUnit hero = Hero();
                TurnManager tm = TurnManager.Instance;
                if (hero != null && tm.CurrentState == TurnState.PlayerTurn && tm.CurrentActiveUnit == hero && !hero.IsWalking)
                {
                    heroTurns++;
                    if (hero.CurrentHP < hero.MaxHP) sawDamage = true;
                    CheckHud($"{label}, hero turn {heroTurns}");
                    if (healHero || hero.CurrentHP <= hero.MaxHP / 2) hero.Heal(999);
                    yield return HeroTurn(hero);
                    if (TurnManager.Instance != null && TurnManager.Instance.IsCombatActive) TurnManager.Instance.EndPlayerTurn();
                }
                yield return null;
            }

            bool won = TurnManager.Instance != null && TurnManager.Instance.CurrentState == TurnState.Victory;
            Step(won ? $"{label}: won in {heroTurns} hero turns{(sawDamage ? ", the hero took hits on the way" : "")}"
                     : $"{label}: not won after {heroTurns} hero turns", won);
        }

        private IEnumerator HeroTurn(PlayerUnit hero)
        {
            EnemyUnit target = NearestEnemy(hero);
            GridManager grid = GridManager.Instance;
            AbilitySO ability = hero.GetAbility(0);
            if (target == null || grid == null || ability == null) yield break;

            int range = Mathf.Max(1, ability.Range);
            if (!hero.HasMovedThisTurn && (grid.GetDistance(hero.GridPosition, target.GridPosition) > range || !grid.HasLineOfSight(hero.GridPosition, target.GridPosition)))
            {
                GridTile tile = grid.FindBestReachableTileToTarget(hero.GridPosition, hero.MovementRange, target.GridPosition, range)
                    ?? grid.FindReachableTileClosestToTarget(hero.GridPosition, hero.MovementRange, target.GridPosition);
                if (tile != null && tile != hero.CurrentTile)
                {
                    hero.WalkToTile(tile);
                    hero.HasMovedThisTurn = true;
                    float walkUntil = Time.realtimeSinceStartup + 10f;
                    while (hero != null && hero.IsWalking && Time.realtimeSinceStartup < walkUntil) yield return null;
                }
            }

            if (hero == null || !hero.IsAlive) yield break;
            if (hero.CanUseAbility(0) && grid.GetDistance(hero.GridPosition, target.GridPosition) <= range)
            {
                hero.UseAbility(0, target.GridPosition, AbilityExecutor.Instance);
            }

            float settle = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < settle)
            {
                if (RerollableRoll.IsAwaitingDecision) RerollableRoll.Accept();
                yield return null;
            }
        }

        private IEnumerator Travel(string sceneName)
        {
            StoryPanelUI.CloseIfOpen();
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(sceneName);
                yield return Frames(2);
                yield return WaitForLoad();
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
            yield return Frames(10);
            StoryPanelUI.CloseIfOpen();
            Step($"Arrived in {sceneName}", SceneManager.GetActiveScene().name == sceneName && Hero() != null);
        }

        #endregion

        #region Helpers

        private static PlayerUnit Hero() => UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

        private static DungeonRoomController FindRoom(Predicate<DungeonRoomController> which)
        {
            foreach (DungeonRoomController r in UnityEngine.Object.FindObjectsByType<DungeonRoomController>(FindObjectsSortMode.None))
            {
                if (which(r)) return r;
            }
            return null;
        }

        private static EnemyUnit NearestEnemy(PlayerUnit hero)
        {
            if (TurnManager.Instance == null || GridManager.Instance == null) return null;
            EnemyUnit best = null;
            int bestDist = int.MaxValue;
            foreach (CombatUnit unit in TurnManager.Instance.ActiveUnits)
            {
                if (!(unit is EnemyUnit enemy) || !enemy.IsAlive) continue;
                int d = GridManager.Instance.GetDistance(hero.GridPosition, enemy.GridPosition);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = enemy;
                }
            }
            return best;
        }

        private static void InvokePrivate(object target, string method)
        {
            if (target == null) return;
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            info?.Invoke(target, null);
        }

        private static IEnumerator WaitForLoad()
        {
            float until = Time.realtimeSinceStartup + 30f;
            while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading && Time.realtimeSinceStartup < until) yield return null;
        }

        private static CharacterClassSO LoadClass(string assetName)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterClassSO>($"Assets/Data/{assetName}.asset");
#else
            return null;
#endif
        }

        private void Step(string what, bool ok)
        {
            report.AppendLine($"[{(ok ? "OK" : "FAIL")}] {what}");
            if (!ok) failed = true;
        }

        private void CollectErrors(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (errors.Count < 200) errors.Add($"{type}: {message.Split('\n')[0]}");
        }

        private void WriteReport(string classAsset)
        {
            report.AppendLine();
            report.AppendLine($"Errors logged during the run: {errors.Count}");
            var distinct = new Dictionary<string, int>();
            foreach (string e in errors) distinct[e] = distinct.TryGetValue(e, out int n) ? n + 1 : 1;
            foreach (var kv in distinct) report.AppendLine($"  x{kv.Value}  {kv.Key}");

            string dir = Environment.GetEnvironmentVariable("SMOKE_REPORT_DIR");
            if (string.IsNullOrEmpty(dir)) dir = "Logs";
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"hud_{classAsset}.txt"), report.ToString());
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        #endregion
    }
}
