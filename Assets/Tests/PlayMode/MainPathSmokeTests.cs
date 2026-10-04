using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// Critical review D7: a play-mode smoke run along the main path for each class. The hero starts a New
    /// Adventure in Oakhaven, clears the cellar, both Forest Path fights, the Cursed Commander, the Castle Hall
    /// stops, Malakor, the Tower challenge and the Gargoyle King. Fights are auto-played with the first
    /// ability (the hero is healed every turn: this checks the flow, not the balance). Every error the game
    /// logs is collected; the run writes a report (SMOKE_REPORT_DIR env var or Logs/) and fails if a step
    /// cannot be reached.
    /// </summary>
    public class MainPathSmokeTests
    {
        private readonly List<string> errors = new List<string>();
        private readonly StringBuilder report = new StringBuilder();
        private bool failed;
        private string saveBackup;

        private const string SaveKey = "CastleOfDice_SaveData";

        // The editor's PlayerPrefs are shared with the main project: keep Vili's own save safe
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

        [UnityTest, Timeout(1800000)]
        public IEnumerator Warrior_MainPath() => RunMainPath("Character_Warrior_SirRoland");

        [UnityTest, Timeout(1800000)]
        public IEnumerator Mage_MainPath() => RunMainPath("Character_Mage_Elira");

        [UnityTest, Timeout(1800000)]
        public IEnumerator Rogue_MainPath() => RunMainPath("Character_Rogue_Corvo");

        private IEnumerator RunMainPath(string classAsset)
        {
            errors.Clear();
            report.Clear();
            failed = false;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CollectErrors;
            report.AppendLine($"Smoke run: {classAsset}  ({DateTime.Now:yyyy-MM-dd HH:mm})");

            // 1. New Adventure in Oakhaven
            SceneManager.LoadScene("Zone_1_VillageAndCellar");
            yield return Frames(10);
            CharacterClassSO cls = LoadClass(classAsset);
            MainMenuController menu = MainMenuController.Instance ?? UnityEngine.Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Step("Main menu found", menu != null && cls != null);
            if (menu != null && cls != null) menu.SelectCharacterClass(cls);
            yield return Frames(5);

            // After an earlier run New Adventure reloads the village first
            float loadUntil = Time.realtimeSinceStartup + 30f;
            while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading && Time.realtimeSinceStartup < loadUntil) yield return null;
            yield return Frames(10);
            Step("Main menu closed after New Adventure", MainMenuController.Instance == null || !MainMenuController.Instance.IsMenuOpen);
            Step("Story intro shown after New Adventure", StoryPanelUI.IsOpen);
            StoryPanelUI.CloseIfOpen();
            PlayerUnit hero = Hero();
            Step($"Hero is {cls?.CharacterName}", hero != null && hero.CharacterClass == cls);
            Time.timeScale = 3f;

            // 2. Barnaby's cellar
            yield return Fight("Cellar rats", r => r.roomLocation == "Cellar");
            StoryPanelUI.CloseIfOpen();

            // 3. Forest Path: the zombie and the ambush
            yield return Travel("Zone_2_ForestPath");
            yield return Fight("Forest zombie", r => r.roomLocation == "Forest" && string.IsNullOrEmpty(r.roomKey));
            yield return Fight("Forest ambush (archer + cultist)", r => r.roomKey == "ForestAmbush");

            // 4. Courtyard: the Cursed Commander
            yield return Travel("Zone_3_CastleCourtyard");
            yield return Fight("Cursed Commander", r => r.bossIdentifier == "CursedCommander");
            Step("Commander counted as defeated", GameManager.Instance != null && GameManager.Instance.IsCommanderDefeated);

            // 5. Castle Hall: the shrine and Pip
            yield return Travel("Zone_5_CastleHall");
            SavePoint shrine = UnityEngine.Object.FindAnyObjectByType<SavePoint>();
            if (shrine != null && Hero() != null) shrine.CommuneAndSave(Hero());
            Step("Saved at the rune shrine", SaveSystem.HasSavedGame());
            ScriptedNpc pip = FindNpc(ScriptedNpc.Kind.TrappedMerchant);
            if (pip != null && Hero() != null) pip.Interact(Hero());
            yield return Frames(3);
            DialogueController dialogue = DialogueController.Instance;
            Step("Pip the Peddler talks", dialogue != null && dialogue.IsInDialogue && dialogue.CurrentNode != null
                && dialogue.CurrentNode.SpeakerName == HubDialogues.MerchantName);
            dialogue?.EndDialogue();

            // 6. Library: Shadow Mage Malakor
            yield return Travel("Zone_4_Library");
            yield return Fight("Shadow Mage Malakor", r => r.bossIdentifier == "ShadowMageMalakor");
            Step("Malakor counted as defeated", GameManager.Instance != null && GameManager.Instance.IsMalakorDefeated);

            // 7. Tower: rune pillars and the mimic
            yield return Travel("Zone_6_Tower");
            TowerChallenge challenge = UnityEngine.Object.FindAnyObjectByType<TowerChallenge>();
            Step("Tower challenge present", challenge != null);
            if (challenge != null)
            {
                for (int i = 0; i < challenge.Pillars.Count; i++)
                {
                    for (int turns = 0; turns <= i; turns++) challenge.Pillars[i].Interact(Hero());
                }
                Step("Rune pillars solved (SUN, MOON, STAR)", challenge.RunesSolved);
                MimicChest mimic = UnityEngine.Object.FindAnyObjectByType<MimicChest>();
                if (mimic != null && Hero() != null) mimic.Interact(Hero());
                yield return AutoBattle("Tower mimic");
                yield return Frames(10);
                CloseLevelUp();
                Step("Tower challenge complete, rewards unsealed", challenge.IsComplete);
            }

            // 8. Throne Room: the Gargoyle King
            yield return Travel("Zone_7_ThroneRoom");
            yield return Fight("Gargoyle King", r => r.bossIdentifier == "GargoyleKing");
            Step("King counted as defeated", GameManager.Instance != null && GameManager.Instance.IsGargoyleKingDefeated);
            float endingUntil = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < endingUntil && (DialogueController.Instance == null || !DialogueController.Instance.IsInDialogue)) yield return null;
            Step("Ending started (Othelia's dialogue)", DialogueController.Instance != null && DialogueController.Instance.IsInDialogue);
            Step("Main quest complete", Economy.QuestManager.Instance == null
                || Economy.QuestManager.Instance.IsQuestCompleted(Economy.MainQuest.QuestId));

            Time.timeScale = 1f;
            Application.logMessageReceived -= CollectErrors;
            WriteReport(classAsset);
            Assert.IsFalse(failed, report.ToString());
        }

        #region Steps

        private IEnumerator Travel(string sceneName)
        {
            CloseLevelUp();
            StoryPanelUI.CloseIfOpen();
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(sceneName);
                yield return Frames(2);
                float until = Time.realtimeSinceStartup + 30f;
                while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading && Time.realtimeSinceStartup < until) yield return null;
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
            yield return Frames(10);
            Step($"Arrived in {sceneName}", SceneManager.GetActiveScene().name == sceneName && Hero() != null);
        }

        private IEnumerator Fight(string label, Predicate<DungeonRoomController> which)
        {
            DungeonRoomController room = null;
            foreach (DungeonRoomController r in UnityEngine.Object.FindObjectsByType<DungeonRoomController>(FindObjectsSortMode.None))
            {
                if (which(r)) room = r;
            }
            if (room == null)
            {
                Step($"{label}: encounter found", false);
                yield break;
            }

            room.BeginEncounter(Hero());
            yield return Frames(3);

            // A boss talks first: take the plain [Fight] path
            if (DialogueController.Instance != null && DialogueController.Instance.IsInDialogue)
            {
                DialogueController.Instance.EndDialogue();
                yield return Frames(3);
            }

            yield return AutoBattle(label);
            yield return Frames(10);
            Step($"{label}: room cleared", room.IsCleared);
            CloseLevelUp();
        }

        private IEnumerator AutoBattle(string label)
        {
            float until = Time.realtimeSinceStartup + 600f;
            int heroTurns = 0;
            yield return Frames(3);
            if (TurnManager.Instance == null || !TurnManager.Instance.IsCombatActive)
            {
                Step($"{label}: combat started", false);
                yield break;
            }

            while (TurnManager.Instance != null && TurnManager.Instance.IsCombatActive && Time.realtimeSinceStartup < until)
            {
                if (RerollableRoll.IsAwaitingDecision) RerollableRoll.Accept();
                StoryPanelUI.CloseIfOpen();

                PlayerUnit hero = Hero();
                TurnManager tm = TurnManager.Instance;
                if (hero != null && tm.CurrentState == TurnState.PlayerTurn && tm.CurrentActiveUnit == hero && !hero.IsWalking)
                {
                    heroTurns++;
                    hero.Heal(999);
                    yield return HeroTurn(hero);
                    if (TurnManager.Instance != null && TurnManager.Instance.IsCombatActive) TurnManager.Instance.EndPlayerTurn();
                }
                yield return null;
            }

            bool won = TurnManager.Instance != null && TurnManager.Instance.CurrentState == TurnState.Victory;
            string end = TurnManager.Instance != null ? TurnManager.Instance.CurrentState.ToString() : "no TurnManager";
            Step(won ? $"{label}: won in {heroTurns} hero turns" : $"{label}: ended in {end} after {heroTurns} hero turns", won);
        }

        private IEnumerator HeroTurn(PlayerUnit hero)
        {
            EnemyUnit target = NearestEnemy(hero);
            GridManager grid = GridManager.Instance;
            AbilitySO ability = hero.GetAbility(0);
            if (target == null || grid == null || ability == null) yield break;

            int range = Mathf.Max(1, ability.Range);

            // Step out of ground the King has marked for his earthquake, like a player would
            GridTile standing = grid.GetTileAt(hero.GridPosition);
            if (standing != null && standing.HazardWarning)
            {
                GridTile safe = null;
                int bestDist = int.MaxValue;
                foreach (GridTile t in grid.GetReachableTiles(hero.GridPosition, hero.MovementRange))
                {
                    if (t.HazardWarning) continue;
                    int d = grid.GetDistance(t.GridPosition, target.GridPosition);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        safe = t;
                    }
                }
                if (safe != null)
                {
                    hero.WalkToTile(safe);
                    hero.HasMovedThisTurn = true;
                    float walkUntil = Time.realtimeSinceStartup + 10f;
                    while (hero != null && hero.IsWalking && Time.realtimeSinceStartup < walkUntil) yield return null;
                }
            }

            if (!hero.HasMovedThisTurn && (grid.GetDistance(hero.GridPosition, target.GridPosition) > range || !grid.HasLineOfSight(hero.GridPosition, target.GridPosition)))
            {
                GridTile tile = grid.FindBestReachableTileToTarget(hero.GridPosition, hero.MovementRange, target.GridPosition, range)
                    ?? grid.FindReachableTileClosestToTarget(hero.GridPosition, hero.MovementRange, target.GridPosition);
                if (tile != null && tile != hero.CurrentTile)
                {
                    hero.WalkToTile(tile);
                    hero.HasMovedThisTurn = true;
                    float until = Time.realtimeSinceStartup + 10f;
                    while (hero != null && hero.IsWalking && Time.realtimeSinceStartup < until) yield return null;
                }
            }

            if (hero == null || !hero.IsAlive) yield break;
            if (hero.CanUseAbility(0) && grid.GetDistance(hero.GridPosition, target.GridPosition) <= range)
            {
                hero.UseAbility(0, target.GridPosition, AbilityExecutor.Instance);
            }

            // Let the dice settle (and answer the reroll choice with Continue)
            float settle = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < settle)
            {
                if (RerollableRoll.IsAwaitingDecision) RerollableRoll.Accept();
                yield return null;
            }
        }

        #endregion

        #region Helpers

        private static PlayerUnit Hero() => UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

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

        private static ScriptedNpc FindNpc(ScriptedNpc.Kind kind)
        {
            foreach (ScriptedNpc npc in UnityEngine.Object.FindObjectsByType<ScriptedNpc>(FindObjectsSortMode.None))
            {
                if (npc.NpcKind == kind) return npc;
            }
            return null;
        }

        private static void CloseLevelUp()
        {
            LevelUpUIController levelUp = LevelUpUIController.Instance;
            if (levelUp == null || !levelUp.IsModalOpen) return;
            MethodInfo choose = typeof(LevelUpUIController).GetMethod("OnResilienceChosen", BindingFlags.Instance | BindingFlags.NonPublic);
            if (choose != null) choose.Invoke(levelUp, null);
            else levelUp.CloseLevelUpModal();
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
            string firstLine = message.Split('\n')[0];
            if (errors.Count < 200) errors.Add($"{type}: {firstLine}");
        }

        private void WriteReport(string classAsset)
        {
            report.AppendLine();
            report.AppendLine($"Errors logged during the run: {errors.Count}");
            Dictionary<string, int> distinct = new Dictionary<string, int>();
            foreach (string e in errors) distinct[e] = distinct.TryGetValue(e, out int n) ? n + 1 : 1;
            foreach (var kv in distinct) report.AppendLine($"  x{kv.Value}  {kv.Key}");

            string dir = Environment.GetEnvironmentVariable("SMOKE_REPORT_DIR");
            if (string.IsNullOrEmpty(dir)) dir = "Logs";
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"smoke_{classAsset}.txt"), report.ToString());
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        #endregion
    }
}
