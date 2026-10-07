using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    /// Vili, 2026-10-07: Try Again pressed right after a defeat let the lost fight's waiting enemy turn end a
    /// turn of the retried fight, and a defeat window left open (Esc, Quit to Main Menu) stayed on screen into
    /// the next New Adventure. Both play the Forest Path zombie with Sir Roland.
    /// </summary>
    public class TryAgainDefeatPlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;
        private float? musicBackup;
        private float? sfxBackup;

        // The editor's PlayerPrefs are shared with the main project: New Adventure clears the save slot
        [SetUp]
        public void BackupPrefs()
        {
            saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
            musicBackup = PlayerPrefs.HasKey(PauseMenuUI.MusicVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.MusicVolumeKey) : (float?)null;
            sfxBackup = PlayerPrefs.HasKey(PauseMenuUI.SfxVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.SfxVolumeKey) : (float?)null;
        }

        [TearDown]
        public void RestorePrefs()
        {
            Time.timeScale = 1f;
            TurnManager.OnUnitTurnStarted -= RecordTurn;
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            if (musicBackup.HasValue) PlayerPrefs.SetFloat(PauseMenuUI.MusicVolumeKey, musicBackup.Value);
            else PlayerPrefs.DeleteKey(PauseMenuUI.MusicVolumeKey);
            if (sfxBackup.HasValue) PlayerPrefs.SetFloat(PauseMenuUI.SfxVolumeKey, sfxBackup.Value);
            else PlayerPrefs.DeleteKey(PauseMenuUI.SfxVolumeKey);
            PlayerPrefs.Save();

            // The HUD lives on between tests: leave no window open over it
            if (PauseMenuUI.IsPaused && PauseMenuUI.Instance != null) PauseMenuUI.Instance.Resume();
            if (DefeatUIController.Instance != null) DefeatUIController.Instance.Hide();
            if (LevelUpUIController.Instance != null) LevelUpUIController.Instance.DiscardPendingLevelUps();
        }

        private readonly List<CombatUnit> turns = new List<CombatUnit>();
        private void RecordTurn(CombatUnit unit) => turns.Add(unit);

        [UnityTest, Timeout(300000)]
        public IEnumerator TryAgain_RightAway_DoesNotSkipATurnOfTheRetriedFight()
        {
            yield return StartAdventure();
            yield return Travel("Zone_2_ForestPath");
            DungeonRoomController room = ZombieRoom();
            Assert.IsNotNull(room, "The Forest Path zombie encounter exists.");
            yield return BeginFight(room);

            // Fall the moment the zombie's turn begins, while its delayed turn is still waiting
            TurnManager tm = TurnManager.Instance;
            float until = Time.realtimeSinceStartup + 30f;
            while (tm.IsCombatActive && tm.CurrentState != TurnState.EnemyTurn && Time.realtimeSinceStartup < until)
            {
                if (tm.CurrentState == TurnState.PlayerTurn) tm.EndPlayerTurn();
                yield return null;
            }
            Assert.AreEqual(TurnState.EnemyTurn, tm.CurrentState, "The zombie's turn began.");
            FellHeroNow(Hero());
            yield return null;
            Assert.AreEqual(TurnState.Defeat, tm.CurrentState, "The hero lost the fight.");

            // Try Again at once, before the lost fight's enemy turn has run out
            turns.Clear();
            TurnManager.OnUnitTurnStarted += RecordTurn;
            InvokePrivate(DefeatUIController.Instance, "OnRetryClicked");
            Assert.IsTrue(tm.IsCombatActive, "Try Again restarted the fight.");

            // The retried fight: hero and zombie must take turns in order, and the hero's turn waits for the hero
            PlayerUnit hero = Hero();
            float heroTurnStart = -1f;
            int heroTurnIndex = -1;
            until = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < until && tm.IsCombatActive)
            {
                if (heroTurnStart < 0f && tm.CurrentActiveUnit == hero && tm.CurrentState == TurnState.PlayerTurn)
                {
                    heroTurnStart = Time.realtimeSinceStartup;
                    heroTurnIndex = turns.Count - 1;
                }
                if (heroTurnStart >= 0f && Time.realtimeSinceStartup - heroTurnStart > 3f) break;
                if (RerollableRoll.IsAwaitingDecision) RerollableRoll.Accept();
                yield return null;
            }
            TurnManager.OnUnitTurnStarted -= RecordTurn;

            for (int i = 1; i < turns.Count; i++)
            {
                Assert.AreNotSame(turns[i - 1], turns[i], $"{turns[i].UnitName} had two turns in a row: a turn was skipped (turns: {Describe(turns)}).");
            }
            Assert.IsTrue(heroTurnStart >= 0f, $"The hero got a turn in the retried fight (turns: {Describe(turns)}).");
            Assert.AreEqual(heroTurnIndex + 1, turns.Count,
                $"Nothing ended the hero's turn while the hero did nothing for 3 s (turns: {Describe(turns)}).");
            Assert.IsTrue(tm.IsCombatActive && tm.CurrentActiveUnit == hero && tm.CurrentState == TurnState.PlayerTurn,
                $"The hero's turn is still waiting for the hero 3 s later (turns: {Describe(turns)}).");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator DefeatWindow_QuitToMenu_NewAdventure_LeavesNoWindowOpen()
        {
            yield return StartAdventure();
            yield return Travel("Zone_2_ForestPath");
            DungeonRoomController room = ZombieRoom();
            Assert.IsNotNull(room, "The Forest Path zombie encounter exists.");
            yield return BeginFight(room);

            FellHeroNow(Hero());
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(DefeatPanelOpen(), "The defeat window opened.");

            // A level-up still waiting for its card belongs to the abandoned run too
            PlayerProgressionManager.Instance.GrantBonusLevel("Defeat window test");
            yield return null;

            // Esc on the defeat window, then Quit to Main Menu
            PauseMenuUI pause = PauseMenuUI.Instance;
            Assert.IsNotNull(pause, "The pause menu exists.");
            pause.Open();
            yield return null;
            InvokePrivate(pause, "QuitToMainMenu");
            yield return WaitForLoad();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsFalse(DefeatPanelOpen(), "The defeat window does not cover the main menu.");

            // New Adventure
            MainMenuController menu = MainMenuController.Instance ?? Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "The main menu is there.");
            menu.SelectCharacterClass(LoadClass("Character_Warrior_SirRoland"));
            for (int i = 0; i < 5; i++) yield return null;
            yield return WaitForLoad();
            for (int i = 0; i < 10; i++) yield return null;
            StoryPanelUI.CloseIfOpen();
            yield return null;

            Assert.IsFalse(DefeatPanelOpen(), "No defeat window in the new adventure.");
            Assert.IsTrue(LevelUpUIController.Instance == null || !LevelUpUIController.Instance.IsModalOpen,
                "No level-up window of the old run in the new adventure.");
            Assert.IsTrue(LevelUpUIController.Instance == null || LevelUpUIController.Instance.QueuedLevelUps == 0,
                "No queued level-up of the old run.");
            Assert.IsFalse(PauseMenuUI.IsPaused, "The game runs.");
            Assert.AreEqual(1f, Time.timeScale, "Time runs at normal speed.");
            PlayerUnit hero = Hero();
            Assert.IsTrue(hero != null && hero.IsAlive && hero.Level == 1, "A fresh level 1 hero.");
        }

        #region Helpers

        private static PlayerUnit Hero() => Object.FindAnyObjectByType<PlayerUnit>();

        private static DungeonRoomController ZombieRoom()
        {
            foreach (DungeonRoomController r in Object.FindObjectsByType<DungeonRoomController>(FindObjectsSortMode.None))
            {
                if (r.roomLocation == "Forest" && string.IsNullOrEmpty(r.roomKey)) return r;
            }
            return null;
        }

        private static IEnumerator BeginFight(DungeonRoomController room)
        {
            room.BeginEncounter(Hero());
            for (int i = 0; i < 3; i++) yield return null;
            if (DialogueController.Instance != null && DialogueController.Instance.IsInDialogue) DialogueController.Instance.EndDialogue();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(TurnManager.Instance != null && TurnManager.Instance.IsCombatActive, "The fight started.");
        }

        /// <summary>Drops the hero with one blow: Death's Door already spent this fight.</summary>
        private static void FellHeroNow(PlayerUnit hero)
        {
            Assert.IsNotNull(hero, "The hero is there.");
            FieldInfo spent = typeof(PlayerUnit).GetField("deathsDoorUsedThisFight", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(spent, "PlayerUnit.deathsDoorUsedThisFight exists.");
            spent.SetValue(hero, true);
            hero.TakeDamage(hero.CurrentHP + 50);
        }

        private static bool DefeatPanelOpen()
        {
            DefeatUIController defeat = DefeatUIController.Instance;
            if (defeat == null) return false;
            FieldInfo info = typeof(DefeatUIController).GetField("defeatModalPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            GameObject panel = info != null ? info.GetValue(defeat) as GameObject : null;
            return panel != null && panel.activeInHierarchy;
        }

        private static string Describe(List<CombatUnit> units)
        {
            var names = new List<string>(units.Count);
            foreach (CombatUnit u in units) names.Add(u != null ? u.UnitName : "?");
            return string.Join(", ", names);
        }

        private static void InvokePrivate(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{method} exists.");
            info.Invoke(target, null);
        }

        private static IEnumerator StartAdventure()
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Zone_1_VillageAndCellar");
            for (int i = 0; i < 10; i++) yield return null;
            MainMenuController menu = MainMenuController.Instance ?? Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            CharacterClassSO cls = LoadClass("Character_Warrior_SirRoland");
            Assert.IsTrue(menu != null && cls != null, "Main menu and the warrior class load.");
            menu.SelectCharacterClass(cls);
            for (int i = 0; i < 5; i++) yield return null;
            yield return WaitForLoad();
            for (int i = 0; i < 10; i++) yield return null;
            StoryPanelUI.CloseIfOpen();
            yield return null;
        }

        private static IEnumerator Travel(string sceneName)
        {
            StoryPanelUI.CloseIfOpen();
            SceneLoader.Instance.LoadScene(sceneName);
            for (int i = 0; i < 2; i++) yield return null;
            yield return WaitForLoad();
            for (int i = 0; i < 10; i++) yield return null;
            StoryPanelUI.CloseIfOpen();
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, $"Arrived in {sceneName}.");
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

        #endregion
    }
}
