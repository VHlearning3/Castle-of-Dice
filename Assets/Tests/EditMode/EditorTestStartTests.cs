using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.DevTools;
using CastleOfTheD20.Economy;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Editor Test Start (Vili, 2026-10-08): the window's setup turns into a normal Continue save, and while a test
    /// runs every save goes to its own slot, never the real one.
    /// </summary>
    public class EditorTestStartTests
    {
        private const string RealKey = "CastleOfDice_SaveData";

        [TearDown]
        public void ClearOverride()
        {
            SaveSystem.EditorSaveKeyOverride = null;
            PlayerPrefs.DeleteKey(EditorTestStart.TestSaveKey);
            PlayerPrefs.Save();
            StoryFlags.Clear();
        }

        [Test]
        public void Setup_BuildsSave_WithLevelPicksGearAndStory()
        {
            var setup = new EditorTestStartSetup
            {
                scene = "Zone_4_Library",
                spawn = "Spawn_From_Hall",
                hero = CharacterClassType.Mage,
                level = 5,
                levelPicks = new[] { EditorTestStartSetup.PickResilience, 1, EditorTestStartSetup.PickAttribute, EditorTestStartSetup.PickResilience },
                difficulty = DifficultyLevel.Hard,
                gold = 321,
                scrap = 4,
                runesOfFate = 2,
                bladeTier = 2,
                armorTier = 3,
                heroQuestDone = true,
                firstBattleWon = true,
                commanderDefeated = true,
                commanderHint = true,
                extraFlags = "MyFlag, Other;Third",
            };
            setup.itemIds.Add("potion_health_small");
            setup.itemCounts.Add(3);
            setup.itemIds.Add("ignored_zero");
            setup.itemCounts.Add(0);

            PlayerSaveData save = setup.BuildSave();

            Assert.AreEqual((int)CharacterClassType.Mage, save.characterClass);
            Assert.AreEqual("Zone_4_Library", save.sceneName);
            Assert.IsFalse(save.hasPosition, "the hero arrives at a spawn point, not a saved spot");
            Assert.AreEqual(5, save.currentLevel);
            Assert.AreEqual(10, save.maxHPBonus, "two Hero's Resilience picks");
            Assert.AreEqual(1, save.attributeBonusModifier);
            CollectionAssert.AreEqual(new[] { 1 }, save.upgradedAbilityIndices);
            Assert.AreEqual((int)DifficultyLevel.Hard, save.difficulty);
            Assert.AreEqual(321, save.gold);
            Assert.AreEqual(4, save.scrapMetal);
            Assert.AreEqual(2, save.rerollScrolls);
            Assert.AreEqual(3, save.permanentWeaponDamageBonus, "Baldur +2 and the hero's quest +1");
            Assert.AreEqual(3, save.permanentArmorClassBonus);
            CollectionAssert.AreEqual(new[] { "potion_health_small" }, save.itemIds);
            CollectionAssert.AreEqual(new[] { 3 }, save.itemCounts);

            CollectionAssert.Contains(save.storyFlags, ShopManager.UpgradeFlagPrefix + "Weapon:2");
            CollectionAssert.DoesNotContain(save.storyFlags, ShopManager.UpgradeFlagPrefix + "Weapon:3");
            CollectionAssert.Contains(save.storyFlags, ShopManager.UpgradeFlagPrefix + "Armor:3");
            CollectionAssert.Contains(save.storyFlags, ClassQuests.Mage.RewardFlag);
            CollectionAssert.Contains(save.storyFlags, PlayerProgressionManager.FirstVictoryFlag);
            CollectionAssert.DoesNotContain(save.storyFlags, PlayerProgressionManager.SeasonedFlag);
            CollectionAssert.Contains(save.storyFlags, "CommanderArmorWeakened");
            CollectionAssert.Contains(save.storyFlags, "MyFlag");
            CollectionAssert.Contains(save.storyFlags, "Other");
            CollectionAssert.Contains(save.storyFlags, "Third");
            CollectionAssert.AreEqual(new[] { "CursedCommander" }, save.defeatedBosses);
        }

        [Test]
        public void Setup_LevelOne_HasNoPicks_AndMirrorRoomOpensTheTower()
        {
            var setup = new EditorTestStartSetup { level = 1, scene = "Zone_6_Tower", spawn = EditorTestStartSetup.MirrorRoomSpawn };
            PlayerSaveData save = setup.BuildSave();
            Assert.AreEqual(1, save.currentLevel);
            Assert.AreEqual(0, save.maxHPBonus);
            Assert.AreEqual(0, save.attributeBonusModifier);
            Assert.IsEmpty(save.upgradedAbilityIndices);
            Assert.AreEqual(InventoryManager.StartingGold, save.gold);
            CollectionAssert.Contains(save.storyFlags, TowerChallenge.CompletedFlag, "the mirror room is only open after the Tower challenge");
        }

        [Test]
        public void SaveKeyOverride_KeepsTheRealSaveUntouched()
        {
            string realBefore = PlayerPrefs.HasKey(RealKey) ? PlayerPrefs.GetString(RealKey) : null;

            SaveSystem.EditorSaveKeyOverride = EditorTestStart.TestSaveKey;
            PlayerPrefs.SetString(EditorTestStart.TestSaveKey, JsonUtility.ToJson(new EditorTestStartSetup { gold = 777 }.BuildSave()));
            Assert.IsTrue(SaveSystem.HasSavedGame());
            Assert.AreEqual(777, SaveSystem.PeekSave().gold, "loads read the test slot");

            SaveSystem.SaveGame();
            SaveSystem.ClearSave();
            Assert.IsFalse(PlayerPrefs.HasKey(EditorTestStart.TestSaveKey), "clearing clears the test slot");

            SaveSystem.EditorSaveKeyOverride = null;
            string realAfter = PlayerPrefs.HasKey(RealKey) ? PlayerPrefs.GetString(RealKey) : null;
            Assert.AreEqual(realBefore, realAfter, "the real save is never written during a test start");
        }

        [Test]
        public void End_RestoresTheRealSlot_AndDropsTheTestSave()
        {
            EditorTestStart.Begin(JsonUtility.ToJson(new EditorTestStartSetup().BuildSave()), string.Empty);
            Assert.IsTrue(EditorTestStart.IsActive);
            Assert.AreEqual(EditorTestStart.TestSaveKey, SaveSystem.EditorSaveKeyOverride);

            EditorTestStart.End();
            Assert.IsFalse(EditorTestStart.IsActive);
            Assert.IsNull(SaveSystem.EditorSaveKeyOverride);
            Assert.IsFalse(PlayerPrefs.HasKey(EditorTestStart.TestSaveKey));
        }
    }
}
